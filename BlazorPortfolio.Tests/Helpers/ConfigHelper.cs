using Microsoft.Extensions.Configuration;

namespace BlazorPortfolio.Tests.Helpers;

public static class ConfigHelper
{
    /// <summary>
    /// Builds an IConfiguration from a dictionary of key/value pairs.
    /// </summary>
    public static IConfiguration FromDictionary(Dictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

    /// <summary>
    /// Builds an IConfiguration that reads from environment variables only.
    /// </summary>
    public static IConfiguration FromEnvironment() =>
        new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

    /// <summary>
    /// Builds an IConfiguration from a base dictionary, then overlays environment variables.
    /// </summary>
    public static IConfiguration FromDictionaryAndEnvironment(Dictionary<string, string?> baseValues) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(baseValues)
            .AddEnvironmentVariables()
            .Build();
}
