using BlazorPortfolio.Components;
using BlazorPortfolio.Data;
using BlazorPortfolio.Models;
using BlazorPortfolio.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(options =>
    {
        options.EnableDetailedErrors = !builder.Environment.IsProduction();
        options.HandshakeTimeout = TimeSpan.FromSeconds(15);
        options.KeepAliveInterval = TimeSpan.FromSeconds(10);
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

builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
    options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
    options.IdleTimeout = TimeSpan.FromHours(8);
});

builder.Services.AddScoped<ContentService>();
builder.Services.AddScoped<AdminAuthService>();
builder.Services.AddScoped<CacheService>();
builder.Services.AddScoped<GitHubService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddHostedService<KeepAliveService>();

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
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseWebSockets();
app.UseSession();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapGet("/health", () => Results.Ok("OK"));

app.Run();

// Make Program class accessible for WebApplicationFactory in tests
public partial class Program { }
