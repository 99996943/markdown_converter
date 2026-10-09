using LegalAgent.Downloads;
using Microsoft.Extensions.Configuration;

namespace MBank.FaqGenerator;

/// <summary>Section <c>Download</c> of the application configuration (contracts/cli.md).</summary>
internal sealed class DownloadSettings
{
    /// <summary>Addresses; an empty list means the user is asked.</summary>
    public string[] Urls { get; set; } = [];

    /// <summary>Allowed hosts (with subdomains).</summary>
    public string[] AllowedHosts { get; set; } = [];

    /// <summary>Whether http addresses are accepted.</summary>
    public bool AllowHttp { get; set; }

    /// <summary>Output directory, relative to the working directory.</summary>
    public string OutputDirectory { get; set; } = "downloads";

    /// <summary>Time limit per file in seconds (may be fractional).</summary>
    public double TimeoutSeconds { get; set; } = 60;

    /// <summary>Size limit per file in megabytes.</summary>
    public double MaxFileSizeMegabytes { get; set; } = 50;

    /// <summary>Maximum number of redirects.</summary>
    public int MaxRedirects { get; set; } = 5;

    /// <summary>User-Agent header.</summary>
    public string? UserAgent { get; set; }

    /// <summary>Converts the settings to library options (validated by the library).</summary>
    public DownloadOptions ToOptions() => new()
    {
        OutputDirectory = OutputDirectory,
        AllowedHosts = AllowedHosts,
        AllowHttp = AllowHttp,
        Timeout = TimeoutSeconds > 0 && double.IsFinite(TimeoutSeconds) ? TimeSpan.FromSeconds(TimeoutSeconds) : TimeSpan.Zero,
        MaxFileSizeBytes = MaxFileSizeMegabytes > 0 && double.IsFinite(MaxFileSizeMegabytes)
            ? (long)(MaxFileSizeMegabytes * 1024 * 1024)
            : 0,
        MaxRedirects = MaxRedirects,
        UserAgent = UserAgent,
    };
}

/// <summary>Loads the configuration layers: appsettings.json → appsettings.Local.json → FAQGEN__ variables (research R10).</summary>
internal static class AppSettings
{
    /// <summary>Prefix of environment variables, e.g. <c>FAQGEN__Download__TimeoutSeconds</c>.</summary>
    public const string EnvironmentPrefix = "FAQGEN__";

    /// <summary>Reads the <c>Download</c> section.</summary>
    /// <exception cref="InvalidOperationException">A value cannot be converted.</exception>
    public static DownloadSettings Load(string configDirectory, IReadOnlyDictionary<string, string?> environment)
    {
        var fromEnvironment = environment
            .Where(e => e.Key.StartsWith(EnvironmentPrefix, StringComparison.OrdinalIgnoreCase))
            .Select(e => KeyValuePair.Create(e.Key[EnvironmentPrefix.Length..].Replace("__", ":", StringComparison.Ordinal), e.Value));

        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(configDirectory, "appsettings.json"), optional: true, reloadOnChange: false)
            .AddJsonFile(Path.Combine(configDirectory, "appsettings.Local.json"), optional: true, reloadOnChange: false)
            .AddInMemoryCollection(fromEnvironment)
            .Build();

        return configuration.GetSection("Download").Get<DownloadSettings>() ?? new DownloadSettings();
    }
}
