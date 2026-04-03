using BlazorPortfolio.Components;
using BlazorPortfolio.Data;
using BlazorPortfolio.Models;
using BlazorPortfolio.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

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
    options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
    options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
});

builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddDataProtection()
    .PersistKeysToDbContext<AppDbContext>()
    .SetApplicationName("BlazorPortfolio");

builder.Services.AddScoped<ContentService>();
builder.Services.AddScoped<AdminAuthService>();
builder.Services.AddScoped<CacheService>();
builder.Services.AddScoped<GitHubService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddHostedService<KeepAliveService>();
builder.Services.AddHostedService<WarmUpService>();

var app = builder.Build();

// Trust the reverse proxy (Render) so antiforgery and HTTPS work correctly
var forwardedOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
// Clear default loopback-only restrictions so Render's proxy is trusted
forwardedOptions.KnownNetworks.Clear();
forwardedOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedOptions);

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

// Auto-migrate on startup and seed default admin if none exists
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        db.Database.Migrate();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogCritical(ex, "Failed to apply migrations. Persistent volume may be unavailable. Exiting.");
        Environment.Exit(1);
    }

    if (!db.AdminUsers.Any())
    {
        db.AdminUsers.Add(new AdminUser
        {
            Username = "admin",
            Email = "admin@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123")
        });
        db.SaveChanges();
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseWebSockets();
app.UseResponseCompression();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // Cache static assets for 7 days — fingerprinted files can go longer
        var headers = ctx.Context.Response.Headers;
        var path = ctx.File.Name;
        if (path.EndsWith(".css") || path.EndsWith(".js") || path.EndsWith(".wasm"))
            headers["Cache-Control"] = "public, max-age=604800, immutable"; // 7 days
        else if (path.EndsWith(".png") || path.EndsWith(".jpg") || path.EndsWith(".jpeg")
              || path.EndsWith(".webp") || path.EndsWith(".svg") || path.EndsWith(".ico"))
            headers["Cache-Control"] = "public, max-age=2592000"; // 30 days
        else
            headers["Cache-Control"] = "public, max-age=3600"; // 1 hour default
    }
});
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapGet("/health", () => Results.Ok("OK"));

app.Run();

// Make Program class accessible for WebApplicationFactory in tests
public partial class Program { }
