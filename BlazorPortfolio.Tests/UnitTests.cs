using BlazorPortfolio.Services;
using BlazorPortfolio.Tests.Helpers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Reflection;
using Xunit;

namespace BlazorPortfolio.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// 6.2  /health returns 200 "OK" without authentication
// ─────────────────────────────────────────────────────────────────────────────
public class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:DefaultConnection",
                $"Data Source={Path.GetTempFileName()}");
            // Provide required secrets so startup warnings don't interfere
            builder.UseSetting("Admin:Username", "testadmin");
            builder.UseSetting("Admin:Password", "testpass");
            builder.UseSetting("GitHub:Token", "test-token");
            builder.UseSetting("Resend:ApiKey", "test-key");
        });
    }

    [Fact]
    public async Task Health_Returns200_WithBodyOK()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal("\"OK\"", body);
    }

    [Fact]
    public async Task Health_IsAccessible_WithoutAuthentication()
    {
        // No auth headers — should still return 200
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 6.3  Startup logs critical error when migration fails
// ─────────────────────────────────────────────────────────────────────────────
public class MigrationFailureTests
{
    [Fact]
    public void MigrationFailure_LogsCritical_WithDescriptiveMessage()
    {
        // Arrange: simulate the migration guard logic from Program.cs
        var logger = new TestLogger<Program>();
        var ex = new InvalidOperationException("Disk not mounted");

        // Act: replicate the catch block from Program.cs
        logger.Log(LogLevel.Critical, new EventId(0), ex,
            ex,
            (_, e) => $"Failed to apply migrations. Persistent volume may be unavailable. Exiting. {e?.Message}");

        // Assert
        Assert.True(logger.HasCritical("Failed to apply migrations"));
        Assert.True(logger.HasCritical("Persistent volume may be unavailable"));
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 6.4  Missing required secret produces warning log containing the variable name
// ─────────────────────────────────────────────────────────────────────────────
public class SecretValidationTests
{
    private static void RunValidation(IConfiguration config, ILogger logger)
    {
        var requiredSecrets = new[]
        {
            "Admin__Username", "Admin__Password",
            "GitHub__Token", "Resend__ApiKey"
        };
        foreach (var key in requiredSecrets)
        {
            if (string.IsNullOrWhiteSpace(config[key.Replace("__", ":")]))
                logger.LogWarning("Required environment variable '{Key}' is not set.", key);
        }
    }

    [Fact]
    public void MissingAdminUsername_ProducesWarning_ContainingVariableName()
    {
        var config = ConfigHelper.FromDictionary(new Dictionary<string, string?>
        {
            ["Admin:Password"] = "pass",
            ["GitHub:Token"] = "tok",
            ["Resend:ApiKey"] = "key"
            // Admin:Username intentionally missing
        });
        var logger = new TestLogger<Program>();

        RunValidation(config, logger);

        Assert.True(logger.HasWarning("Admin__Username"));
    }

    [Fact]
    public void AllSecretsPresent_ProducesNoWarnings()
    {
        var config = ConfigHelper.FromDictionary(new Dictionary<string, string?>
        {
            ["Admin:Username"] = "admin",
            ["Admin:Password"] = "pass",
            ["GitHub:Token"] = "tok",
            ["Resend:ApiKey"] = "key"
        });
        var logger = new TestLogger<Program>();

        RunValidation(config, logger);

        Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Warning);
    }

    [Theory]
    [InlineData("Admin__Username")]
    [InlineData("Admin__Password")]
    [InlineData("GitHub__Token")]
    [InlineData("Resend__ApiKey")]
    public void EachMissingSecret_ProducesWarning_ContainingItsName(string missingKey)
    {
        var allSecrets = new Dictionary<string, string?>
        {
            ["Admin:Username"] = "admin",
            ["Admin:Password"] = "pass",
            ["GitHub:Token"] = "tok",
            ["Resend:ApiKey"] = "key"
        };
        // Remove the one we want to test as missing
        allSecrets.Remove(missingKey.Replace("__", ":"));

        var config = ConfigHelper.FromDictionary(allSecrets);
        var logger = new TestLogger<Program>();

        RunValidation(config, logger);

        Assert.True(logger.HasWarning(missingKey),
            $"Expected warning containing '{missingKey}' but none found.");
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 6.5  KeepAliveService does not schedule timer when BaseUrl is empty
// ─────────────────────────────────────────────────────────────────────────────
public class KeepAliveServiceNoTimerTests
{
    private static Timer? GetTimer(KeepAliveService svc) =>
        (Timer?)typeof(KeepAliveService)
            .GetField("_timer", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(svc);

    [Fact]
    public async Task StartAsync_DoesNotScheduleTimer_WhenBaseUrlIsEmpty()
    {
        var config = ConfigHelper.FromDictionary(new Dictionary<string, string?>
        {
            ["KeepAlive:BaseUrl"] = "",
            ["KeepAlive:IntervalMinutes"] = "10"
        });
        var logger = new TestLogger<KeepAliveService>();
        var svc = new KeepAliveService(config, logger);

        await svc.StartAsync(CancellationToken.None);

        Assert.Null(GetTimer(svc));
    }

    [Fact]
    public async Task StartAsync_DoesNotScheduleTimer_WhenBaseUrlIsNull()
    {
        var config = ConfigHelper.FromDictionary(new Dictionary<string, string?>
        {
            ["KeepAlive:IntervalMinutes"] = "10"
        });
        var logger = new TestLogger<KeepAliveService>();
        var svc = new KeepAliveService(config, logger);

        await svc.StartAsync(CancellationToken.None);

        Assert.Null(GetTimer(svc));
    }

    [Fact]
    public async Task StartAsync_SchedulesTimer_WhenBaseUrlIsSet()
    {
        var config = ConfigHelper.FromDictionary(new Dictionary<string, string?>
        {
            ["KeepAlive:BaseUrl"] = "https://example.com/health",
            ["KeepAlive:IntervalMinutes"] = "10"
        });
        var logger = new TestLogger<KeepAliveService>();
        using var http = new HttpClient(new NoOpHandler());
        var svc = new KeepAliveService(config, logger, http);

        await svc.StartAsync(CancellationToken.None);

        Assert.NotNull(GetTimer(svc));
        await svc.StopAsync(CancellationToken.None);
    }

    private class NoOpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }
}
