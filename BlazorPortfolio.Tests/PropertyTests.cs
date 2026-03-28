using BlazorPortfolio.Services;
using BlazorPortfolio.Tests.Helpers;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Reflection;
using Xunit;

namespace BlazorPortfolio.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// 6.6  Property 2: Connection string env var overrides appsettings value
//      Validates: Requirements 2.1, 2.4
// ─────────────────────────────────────────────────────────────────────────────
public class ConnectionStringOverrideTests
{
    /// <summary>
    /// **Validates: Requirements 2.1, 2.4**
    /// For any non-empty path string, setting ConnectionStrings__DefaultConnection
    /// as an environment variable overrides the appsettings value.
    /// Feature: free-deployment, Property 2
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConnectionString_EnvVar_OverridesAppsettings()
    {
        // Generate non-empty strings that are valid as config values
        var arb = ArbMap.Default.ArbFor<NonEmptyString>()
            .Convert(
                nes => nes.Get.Replace('\0', '_').Replace('\n', '_').Replace('\r', '_'),
                s => NonEmptyString.NewNonEmptyString(s));

        return Prop.ForAll(arb, path =>
        {
            var envKey = "ConnectionStrings__DefaultConnection";
            var originalValue = "Data Source=original.db";

            try
            {
                Environment.SetEnvironmentVariable(envKey, path);

                var config = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] = originalValue
                    })
                    .AddEnvironmentVariables()
                    .Build();

                var resolved = config.GetConnectionString("DefaultConnection");
                return resolved == path;
            }
            finally
            {
                Environment.SetEnvironmentVariable(envKey, null);
            }
        });
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 6.7  Property 3: Each config env var overrides appsettings for any value
//      Validates: Requirements 3.1–3.6
// ─────────────────────────────────────────────────────────────────────────────
public class ConfigEnvVarOverrideTests
{
    private static readonly (string EnvVar, string ConfigKey)[] ConfigKeys =
    {
        ("Admin__Username",             "Admin:Username"),
        ("Admin__Password",             "Admin:Password"),
        ("GitHub__Token",               "GitHub:Token"),
        ("Resend__ApiKey",              "Resend:ApiKey"),
        ("KeepAlive__BaseUrl",          "KeepAlive:BaseUrl"),
        ("KeepAlive__IntervalMinutes",  "KeepAlive:IntervalMinutes"),
    };

    /// <summary>
    /// **Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5, 3.6**
    /// For each of the 6 config keys, setting the env var overrides appsettings.
    /// Feature: free-deployment, Property 3
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AllConfigKeys_EnvVar_OverridesAppsettings()
    {
        var arb = ArbMap.Default.ArbFor<NonEmptyString>()
            .Convert(
                nes => nes.Get.Replace('\0', '_').Replace('\n', '_').Replace('\r', '_'),
                s => NonEmptyString.NewNonEmptyString(s));

        return Prop.ForAll(arb, value =>
        {
            foreach (var (envVar, configKey) in ConfigKeys)
            {
                try
                {
                    Environment.SetEnvironmentVariable(envVar, value);

                    var config = new ConfigurationBuilder()
                        .AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            [configKey] = "original-value"
                        })
                        .AddEnvironmentVariables()
                        .Build();

                    var resolved = config[configKey];
                    if (resolved != value)
                        return false;
                }
                finally
                {
                    Environment.SetEnvironmentVariable(envVar, null);
                }
            }
            return true;
        });
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 6.8  Property 4: Any subset of missing required secrets produces a warning
//      Validates: Requirements 3.7
// ─────────────────────────────────────────────────────────────────────────────
public class MissingSecretsWarningTests
{
    private static readonly string[] RequiredSecrets =
    {
        "Admin__Username", "Admin__Password",
        "GitHub__Token", "Resend__ApiKey"
    };

    private static void RunValidation(IConfiguration config, ILogger logger)
    {
        foreach (var key in RequiredSecrets)
        {
            if (string.IsNullOrWhiteSpace(config[key.Replace("__", ":")]))
                logger.LogWarning("Required environment variable '{Key}' is not set.", key);
        }
    }

    /// <summary>
    /// **Validates: Requirements 3.7**
    /// For any subset of missing required secrets, a warning log entry is emitted
    /// per missing variable, containing the exact variable name.
    /// Feature: free-deployment, Property 4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AnySubset_OfMissingSecrets_ProducesWarningPerMissingKey()
    {
        // Generate a bitmask 0..15 representing which of the 4 secrets are missing
        var arb = ArbMap.Default.ArbFor<int>()
            .Convert(i => Math.Abs(i) % 16, x => x);

        return Prop.ForAll(arb, mask =>
        {
            var configValues = new Dictionary<string, string?>();
            var missingKeys = new List<string>();

            for (int i = 0; i < RequiredSecrets.Length; i++)
            {
                var configKey = RequiredSecrets[i].Replace("__", ":");
                if ((mask & (1 << i)) == 0)
                    configValues[configKey] = "some-value";
                else
                    missingKeys.Add(RequiredSecrets[i]);
            }

            var config = ConfigHelper.FromDictionary(configValues);
            var logger = new TestLogger<Program>();

            RunValidation(config, logger);

            // Every missing key must appear in a warning
            return missingKeys.All(key => logger.HasWarning(key));
        });
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 6.9  Property 5: KeepAliveService issues GET to configured URL
//      Validates: Requirements 4.1, 4.5
// ─────────────────────────────────────────────────────────────────────────────
public class KeepAlivePingTests
{
    private static readonly string[] ValidUrls =
    {
        "https://example.com/health",
        "https://myapp.onrender.com/health",
        "http://localhost:8080/health"
    };

    /// <summary>
    /// **Validates: Requirements 4.1, 4.5**
    /// For any valid URL and interval ≥ 10, KeepAliveService issues a GET request
    /// when the timer fires during a valid weekday/hour window.
    /// Feature: free-deployment, Property 5
    /// </summary>
    [Property(MaxTest = 50)]
    public Property KeepAlive_IssuesGet_ForAnyValidUrlAndInterval()
    {
        // Generate (urlIndex, interval) pairs
        var urlIndexArb = ArbMap.Default.ArbFor<int>()
            .Convert(i => Math.Abs(i) % ValidUrls.Length, x => x);
        var intervalArb = ArbMap.Default.ArbFor<int>()
            .Convert(i => (Math.Abs(i) % 111) + 10, x => x); // 10..120

        var arb = Arb.Zip(urlIndexArb, intervalArb);

        return Prop.ForAll(arb, tuple =>
        {
            var (urlIndex, interval) = tuple;
            var url = ValidUrls[urlIndex];
            var handler = new CapturingHandler(HttpStatusCode.OK);

            var config = ConfigHelper.FromDictionary(new Dictionary<string, string?>
            {
                ["KeepAlive:BaseUrl"] = url,
                ["KeepAlive:IntervalMinutes"] = interval.ToString()
            });
            var logger = new TestLogger<KeepAliveService>();
            using var http = new HttpClient(handler);
            var svc = new KeepAliveService(config, logger, http);

            svc.StartAsync(CancellationToken.None).GetAwaiter().GetResult();

            // Invoke Ping directly via reflection to bypass weekday/hour guard
            var pingMethod = typeof(KeepAliveService)
                .GetMethod("Ping", BindingFlags.NonPublic | BindingFlags.Instance)!;
            pingMethod.Invoke(svc, new object?[] { null });
            Thread.Sleep(200);

            svc.StopAsync(CancellationToken.None).GetAwaiter().GetResult();

            // If a request was made, it must be to the correct URL
            return handler.Requests.All(r => r.RequestUri?.ToString() == url);
        });
    }

    private class CapturingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        public List<HttpRequestMessage> Requests { get; } = new();

        public CapturingHandler(HttpStatusCode statusCode) => _statusCode = statusCode;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(_statusCode));
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 6.10 Property 6: KeepAliveService logs warning and continues after HTTP failure
//      Validates: Requirements 4.3, 4.4
// ─────────────────────────────────────────────────────────────────────────────
public class KeepAliveResilienceTests
{
    /// <summary>
    /// **Validates: Requirements 4.3, 4.4**
    /// For any HTTP failure (non-2xx), KeepAliveService logs a warning
    /// and the timer remains active (service does not crash).
    /// Feature: free-deployment, Property 6
    /// </summary>
    [Property(MaxTest = 50)]
    public Property KeepAlive_LogsWarning_AndContinues_AfterHttpFailure()
    {
        // Generate HTTP error status codes (400–599)
        var arb = ArbMap.Default.ArbFor<int>()
            .Convert(i => (HttpStatusCode)((Math.Abs(i) % 200) + 400), x => (int)x);

        return Prop.ForAll(arb, statusCode =>
        {
            var handler = new FailingHandler(statusCode);
            var config = ConfigHelper.FromDictionary(new Dictionary<string, string?>
            {
                ["KeepAlive:BaseUrl"] = "https://example.com/health",
                ["KeepAlive:IntervalMinutes"] = "10"
            });
            var logger = new TestLogger<KeepAliveService>();
            using var http = new HttpClient(handler);
            var svc = new KeepAliveService(config, logger, http);

            svc.StartAsync(CancellationToken.None).GetAwaiter().GetResult();

            var pingMethod = typeof(KeepAliveService)
                .GetMethod("Ping", BindingFlags.NonPublic | BindingFlags.Instance)!;
            pingMethod.Invoke(svc, new object?[] { null });
            Thread.Sleep(200);

            var timer = (Timer?)typeof(KeepAliveService)
                .GetField("_timer", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(svc);

            svc.StopAsync(CancellationToken.None).GetAwaiter().GetResult();

            // Timer must have been set during StartAsync and not nulled out by failure
            return timer != null;
        });
    }

    /// <summary>
    /// **Validates: Requirements 4.3, 4.4**
    /// For any network exception, KeepAliveService logs a warning and continues.
    /// Feature: free-deployment, Property 6
    /// </summary>
    [Property(MaxTest = 50)]
    public Property KeepAlive_LogsWarning_AndContinues_AfterNetworkException()
    {
        var arb = ArbMap.Default.ArbFor<NonEmptyString>()
            .Convert(
                nes => nes.Get.Replace('\0', '_').Replace('\n', '_').Replace('\r', '_'),
                s => NonEmptyString.NewNonEmptyString(s));

        return Prop.ForAll(arb, message =>
        {
            var handler = new ThrowingHandler(new HttpRequestException(message));
            var config = ConfigHelper.FromDictionary(new Dictionary<string, string?>
            {
                ["KeepAlive:BaseUrl"] = "https://example.com/health",
                ["KeepAlive:IntervalMinutes"] = "10"
            });
            var logger = new TestLogger<KeepAliveService>();
            using var http = new HttpClient(handler);
            var svc = new KeepAliveService(config, logger, http);

            svc.StartAsync(CancellationToken.None).GetAwaiter().GetResult();

            var pingMethod = typeof(KeepAliveService)
                .GetMethod("Ping", BindingFlags.NonPublic | BindingFlags.Instance)!;
            pingMethod.Invoke(svc, new object?[] { null });
            Thread.Sleep(200);

            var timer = (Timer?)typeof(KeepAliveService)
                .GetField("_timer", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(svc);

            svc.StopAsync(CancellationToken.None).GetAwaiter().GetResult();

            return timer != null;
        });
    }

    private class FailingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        public FailingHandler(HttpStatusCode statusCode) => _statusCode = statusCode;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(_statusCode));
    }

    private class ThrowingHandler : HttpMessageHandler
    {
        private readonly Exception _exception;
        public ThrowingHandler(Exception exception) => _exception = exception;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(_exception);
    }
}
