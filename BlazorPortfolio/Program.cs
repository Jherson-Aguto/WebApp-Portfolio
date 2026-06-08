using BlazorPortfolio.Components;
using BlazorPortfolio.Data;
using BlazorPortfolio.Models;
using BlazorPortfolio.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components.Server.Circuits;

// Load local environment variables from .env file for development (if not running tests)
var isTesting = AppDomain.CurrentDomain.GetAssemblies().Any(a => 
    a.FullName!.Contains("xunit", StringComparison.OrdinalIgnoreCase) || 
    a.FullName!.Contains("Microsoft.TestPlatform", StringComparison.OrdinalIgnoreCase) || 
    a.FullName!.Contains("testhost", StringComparison.OrdinalIgnoreCase));

if (!isTesting)
{
    var envPath = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "../.env");
    if (!System.IO.File.Exists(envPath))
    {
        envPath = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), ".env");
    }
    if (System.IO.File.Exists(envPath))
    {
        foreach (var line in System.IO.File.ReadAllLines(envPath))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                continue;

            var parts = line.Split('=', 2);
            if (parts.Length == 2)
            {
                var key = parts[0].Trim();
                var val = parts[1].Trim().Trim('"');
                Environment.SetEnvironmentVariable(key, val);
            }
        }
    }
}

var builder = WebApplication.CreateBuilder(args);

// Fix for Render/Linux "inotify instances has been reached" error
// Disables file watching for appsettings.json
foreach (var source in builder.Configuration.Sources.OfType<Microsoft.Extensions.Configuration.Json.JsonConfigurationSource>())
{
    source.ReloadOnChange = false;
}
var allowedFrameAncestors = builder.Configuration
    .GetSection("Embedding:AllowedFrameAncestors")
    .Get<string[]>()?
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    .Select(origin => origin.Trim())
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray() ?? [];

// Brotli + Gzip compression for static assets and API responses
builder.Services.AddResponseCompression(opts =>
{
    opts.EnableForHttps = true;
    opts.Providers.Add<BrotliCompressionProvider>();
    opts.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(opts =>
    opts.Level = System.IO.Compression.CompressionLevel.Fastest);

// Server-side memory cache (replaces JS sessionStorage cache)
builder.Services.AddMemoryCache();

// Register secure CORS policy for our public static resume subdomain and labs portal
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowResumeSubdomain", policy =>
    {
        policy.WithOrigins("https://resume.jhersonaguto.dev", "https://labs.jhersonaguto.dev", "http://localhost:5173", "http://localhost:3000", "http://localhost:8080", "https://jhersonaguto.dev")
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Rate limiting — protect admin login from brute force (handled in AdminAuthService)
builder.Services.AddRateLimiter(opts =>
{
    opts.RejectionStatusCode = 429;
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(options =>
    {
        options.EnableDetailedErrors = false; // disable in prod — saves bandwidth
        options.HandshakeTimeout = TimeSpan.FromSeconds(30); // more forgiving on 3G/4G
        options.KeepAliveInterval = TimeSpan.FromSeconds(15); // reduce keep-alive pings
        options.ClientTimeoutInterval = TimeSpan.FromSeconds(60); // tolerate mobile network gaps
        options.MaximumReceiveMessageSize = 512 * 1024; // 512 KB — GitHub data can be large
    });

builder.Services.AddAntiforgery(options =>
{
    // Only enforce Secure cookie in production (HTTPS). Local dev runs on HTTP.
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest
        : Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
    options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
    // We control framing with CSP frame-ancestors below for allow-list support.
    options.SuppressXFrameOptionsHeader = true;
});

builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddDataProtection()
    .PersistKeysToDbContext<AppDbContext>()
    .SetApplicationName("BlazorPortfolio");

builder.Services.AddHttpClient();
builder.Services.AddScoped<ContentService>();
builder.Services.AddScoped<AdminAuthService>();
builder.Services.AddScoped<CacheService>();
builder.Services.AddScoped<GitHubService>();
builder.Services.AddScoped<GitHubStorageService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<GeminiService>();
builder.Services.AddHostedService<KeepAliveService>();
builder.Services.AddHostedService<WarmUpService>();

// Smart Database Keep-Alive (Neon Free Tier Optimization)
builder.Services.Configure<DatabaseKeepAliveOptions>(builder.Configuration.GetSection("DatabaseKeepAlive"));
builder.Services.AddSingleton<DatabaseKeepAliveService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<DatabaseKeepAliveService>());
builder.Services.AddScoped<CircuitHandler, ActivityCircuitHandler>();

var app = builder.Build();

var frameAncestorsValue = BuildFrameAncestorsDirective(allowedFrameAncestors);

app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        context.Response.Headers.Remove("X-Frame-Options");

        var existingCsp = context.Response.Headers.ContentSecurityPolicy.ToString();
        context.Response.Headers.ContentSecurityPolicy = UpsertFrameAncestorsDirective(existingCsp, frameAncestorsValue);
        return Task.CompletedTask;
    });

    await next();
});

// Trust the reverse proxy (Render) so antiforgery and HTTPS work correctly
var forwardedOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
// Clear default loopback-only restrictions so Render's proxy is trusted
forwardedOptions.KnownIPNetworks.Clear();
forwardedOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedOptions);

// Track user activity to keep the database warm under free tier limits
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "";

    // Ignore static asset requests
    var isStaticFile = path.Contains('.') && 
                       (path.EndsWith(".js", StringComparison.OrdinalIgnoreCase) || 
                        path.EndsWith(".css", StringComparison.OrdinalIgnoreCase) || 
                        path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                        path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || 
                        path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) || 
                        path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ||
                        path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) || 
                        path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase) || 
                        path.EndsWith(".wasm", StringComparison.OrdinalIgnoreCase) ||
                        path.EndsWith(".woff", StringComparison.OrdinalIgnoreCase) || 
                        path.EndsWith(".woff2", StringComparison.OrdinalIgnoreCase));

    // Ignore automated health/keep-alive endpoint pings
    var isHealthCheck = path.Equals("/health", StringComparison.OrdinalIgnoreCase) ||
                        path.StartsWith("/api/resume/active", StringComparison.OrdinalIgnoreCase);

    if (!isStaticFile && !isHealthCheck)
    {
        var keepAliveService = context.RequestServices.GetService<DatabaseKeepAliveService>();
        keepAliveService?.RecordActivity();
    }

    await next();
});

// Warn on missing required secrets
var requiredSecrets = new[]
{
    "Admin__Username", "Admin__Password",
    "GitHub__Token", "Resend__ApiKey"
};
foreach (var key in requiredSecrets)
{
    if (string.IsNullOrWhiteSpace(app.Configuration[key.Replace("__", ":")]))
        app.Logger.LogWarning("Required environment variable '{Key}' is not set.", key);
}

// Auto-migrate on startup and seed default admin if none exists (skipped during tests to prevent crashes)
if (!isTesting)
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        try
        {
            // Only run migrations if there are pending ones — avoids a round-trip on every cold start
            var pending = db.Database.GetPendingMigrations();
            if (pending.Any())
                db.Database.Migrate();
        }
        catch (Exception ex)
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogCritical(ex, "Failed to apply migrations. Persistent volume may be unavailable. Exiting.");
            Environment.Exit(1);
        }

        if (!await db.AdminUsers.AnyAsync())
        {
            db.AdminUsers.Add(new AdminUser
            {
                Username = "admin",
                Email = "admin@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123")
            });
            await db.SaveChangesAsync();
        }

        // Correct any legacy project data shifted during column renaming
        try
        {
            var projectsToFix = await db.Projects.ToListAsync();
            bool dbChanged = false;
            foreach (var proj in projectsToFix)
            {
                bool modified = false;

                // 1. If status is a description (i.e. not a standard short status string)
                if (proj.Status != "Live" && proj.Status != "Active" && proj.Status != "Prototype" && proj.Status != "Archived")
                {
                    proj.DetailedDescription = proj.Status;
                    proj.ShortDescription = proj.Status.Length > 150 ? proj.Status.Substring(0, 147) + "..." : proj.Status;
                    proj.Status = "Prototype";
                    modified = true;
                }

                // 2. Ensure Category is not empty
                if (string.IsNullOrWhiteSpace(proj.Category) || proj.Category == "-- Select Category --")
                {
                    proj.Category = "Other";
                    modified = true;
                }

                // 3. Ensure Slug is a valid generated slug
                if (string.IsNullOrWhiteSpace(proj.Slug) || proj.Slug.StartsWith("project-"))
                {
                    var baseSlug = proj.Title.ToLowerInvariant();
                    baseSlug = Regex.Replace(baseSlug, @"[^a-z0-9\s-]", "");
                    baseSlug = Regex.Replace(baseSlug, @"[\s-]+", "-").Trim('-');
                    proj.Slug = baseSlug;
                    modified = true;
                }

                // 4. Correct RepositoryUrl if it ended up in SolutionOverview
                if (string.IsNullOrWhiteSpace(proj.RepositoryUrl) && !string.IsNullOrWhiteSpace(proj.SolutionOverview) && 
                    (proj.SolutionOverview.Contains("github.com") || proj.SolutionOverview.StartsWith("http")))
                {
                    proj.RepositoryUrl = proj.SolutionOverview;
                    proj.SolutionOverview = null;
                    modified = true;
                }

                if (modified)
                {
                    db.Projects.Update(proj);
                    dbChanged = true;
                }
            }
            if (dbChanged)
            {
                await db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "Error occurred during projects data migration correction.");
        }
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseWebSockets();
app.UseCors("AllowResumeSubdomain");
app.UseResponseCompression();
app.UseRateLimiter();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var headers = ctx.Context.Response.Headers;
        var path = ctx.File.Name;
        // WASM files from _framework are fingerprinted by Blazor — safe to use immutable
        if (path.EndsWith(".wasm"))
            headers["Cache-Control"] = "public, max-age=604800, immutable";
        // Self-hosted fonts never change between deploys — cache 1 year
        else if (path.EndsWith(".woff2") || path.EndsWith(".woff"))
            headers["Cache-Control"] = "public, max-age=31536000, immutable";
        // Non-fingerprinted JS/CSS — use must-revalidate so browsers check on deploy
        else if (path.EndsWith(".css") || path.EndsWith(".js"))
            headers["Cache-Control"] = "public, max-age=3600, must-revalidate"; // 1 hour
        else if (path.EndsWith(".png") || path.EndsWith(".jpg") || path.EndsWith(".jpeg")
              || path.EndsWith(".webp") || path.EndsWith(".svg") || path.EndsWith(".ico"))
            headers["Cache-Control"] = "public, max-age=86400"; // 1 day
        else
            headers["Cache-Control"] = "public, max-age=3600";
    }
});
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Dynamic API endpoint for the resume subdomain to fetch the active resume
app.MapGet("/api/resume/active", async (BlazorPortfolio.Services.ContentService svc) =>
{
    var active = await svc.GetActiveResumeAsync();
    if (active == null) return Results.NotFound();

    // Convert on-the-fly to jsDelivr CDN link format for optimal loading
    var fileUrl = active.FileUrl;
    if (fileUrl.Contains("raw.githubusercontent.com"))
    {
        fileUrl = fileUrl.Replace("raw.githubusercontent.com/", "cdn.jsdelivr.net/gh/")
                         .Replace("/main/", "@main/")
                         .Replace("/master/", "@master/");
    }

    return Results.Ok(new { fileUrl });
}).RequireCors("AllowResumeSubdomain");

// Public API endpoints for the Labs Workspace registry catalog
app.MapGet("/api/labs/projects", async (BlazorPortfolio.Services.ContentService svc) =>
{
    var projects = await svc.GetProjectsAsync();
    var response = projects
        .Where(p => p.IsPublished)
        .OrderBy(p => p.SortOrder)
        .Select(p => new LabsProjectDto
        {
            Id = p.Id,
            Title = p.Title,
            Slug = p.Slug,
            ShortDescription = p.ShortDescription,
            DetailedDescription = p.DetailedDescription,
            ProblemStatement = p.ProblemStatement,
            SolutionOverview = p.SolutionOverview,
            KeyFeatures = string.IsNullOrWhiteSpace(p.KeyFeatures) 
                ? new List<string>() 
                : p.KeyFeatures.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            TechStack = string.IsNullOrWhiteSpace(p.TechStack) 
                ? new List<string>() 
                : p.TechStack.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            Category = p.Category,
            Status = p.Status,
            ImageUrl = p.ImageUrl,
            LiveUrl = p.LiveUrl,
            RepositoryUrl = p.RepositoryUrl,
            DemoUrl = p.DemoUrl,
            Featured = p.Featured,
            PublishedAt = p.PublishedAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            UpdatedAt = p.UpdatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ")
        }).ToList();
    return Results.Ok(response);
}).RequireCors("AllowResumeSubdomain");

app.MapGet("/api/labs/projects/{slug}", async (string slug, BlazorPortfolio.Services.ContentService svc) =>
{
    var p = await svc.GetProjectBySlugAsync(slug);
    if (p == null || !p.IsPublished) return Results.NotFound();

    var response = new LabsProjectDto
    {
        Id = p.Id,
        Title = p.Title,
        Slug = p.Slug,
        ShortDescription = p.ShortDescription,
        DetailedDescription = p.DetailedDescription,
        ProblemStatement = p.ProblemStatement,
        SolutionOverview = p.SolutionOverview,
        KeyFeatures = string.IsNullOrWhiteSpace(p.KeyFeatures) 
            ? new List<string>() 
            : p.KeyFeatures.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
        TechStack = string.IsNullOrWhiteSpace(p.TechStack) 
            ? new List<string>() 
            : p.TechStack.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
        Category = p.Category,
        Status = p.Status,
        ImageUrl = p.ImageUrl,
        LiveUrl = p.LiveUrl,
        RepositoryUrl = p.RepositoryUrl,
        DemoUrl = p.DemoUrl,
        Featured = p.Featured,
        PublishedAt = p.PublishedAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
        UpdatedAt = p.UpdatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ")
    };
    return Results.Ok(response);
}).RequireCors("AllowResumeSubdomain");

app.MapGet("/health", () => Results.Ok("OK"));

app.Run();

static string BuildFrameAncestorsDirective(IEnumerable<string> allowedAncestors)
{
    var values = new List<string> { "'self'" };
    values.AddRange(allowedAncestors);
    return $"frame-ancestors {string.Join(' ', values)}";
}

static string UpsertFrameAncestorsDirective(string existingCsp, string frameAncestorsDirective)
{
    if (string.IsNullOrWhiteSpace(existingCsp))
        return frameAncestorsDirective;

    if (Regex.IsMatch(existingCsp, @"(^|;)\s*frame-ancestors\s+[^;]*", RegexOptions.IgnoreCase))
    {
        return Regex.Replace(
            existingCsp,
            @"(^|;)\s*frame-ancestors\s+[^;]*",
            match => match.Value.StartsWith(";", StringComparison.Ordinal) ? $"; {frameAncestorsDirective}" : frameAncestorsDirective,
            RegexOptions.IgnoreCase);
    }

    return $"{existingCsp.Trim().TrimEnd(';')}; {frameAncestorsDirective}";
}

// Make Program class accessible for WebApplicationFactory in tests
public partial class Program { }
