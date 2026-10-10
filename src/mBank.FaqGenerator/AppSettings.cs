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

/// <summary>Section <c>AzureOpenAI</c> (contracts/cli.md). The API key is deliberately not a setting.</summary>
internal sealed class AzureOpenAiSettings
{
    /// <summary>Address of the Azure OpenAI resource; absolute https.</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Deployment name.</summary>
    public string Deployment { get; set; } = "gpt-4o-mini";

    /// <summary>Model name, informative only (FAQ header).</summary>
    public string Model { get; set; } = "gpt-4o-mini";

    /// <summary>Time limit per request in seconds (may be fractional).</summary>
    public double TimeoutSeconds { get; set; } = 300;

    /// <summary>Temperature 0–2; <c>null</c> means not sent.</summary>
    public double? Temperature { get; set; } = 0;

    /// <summary>Seed; <c>null</c> means not sent.</summary>
    public int? Seed { get; set; } = 42;

    /// <summary>Output token limit per request.</summary>
    public int MaxOutputTokens { get; set; } = 8192;
}

/// <summary>Section <c>Faq</c> (contracts/cli.md).</summary>
internal sealed class FaqSettings
{
    /// <summary>OKF directory with <c>FAQ_mBank.md</c>, relative to the working directory.</summary>
    public string OutputDirectory { get; set; } = "faq";

    /// <summary>Maximum candidates per document, 1–30.</summary>
    public int CandidatesPerDocument { get; set; } = 10;

    /// <summary>Maximum estimated tokens per document.</summary>
    public int MaxDocumentTokens { get; set; } = 100_000;
}

/// <summary>All sections of the application configuration.</summary>
/// <param name="Download">Section <c>Download</c>.</param>
/// <param name="AzureOpenAI">Section <c>AzureOpenAI</c>.</param>
/// <param name="Faq">Section <c>Faq</c>.</param>
internal sealed record AppConfiguration(DownloadSettings Download, AzureOpenAiSettings AzureOpenAI, FaqSettings Faq);

/// <summary>Loads the configuration layers: appsettings.json → appsettings.Local.json → FAQGEN__ variables (research R10).</summary>
internal static class AppSettings
{
    /// <summary>Prefix of environment variables, e.g. <c>FAQGEN__Download__TimeoutSeconds</c>.</summary>
    public const string EnvironmentPrefix = "FAQGEN__";

    /// <summary>Reads all sections; rejects an API key in the configuration (FR-412).</summary>
    /// <exception cref="ConfigurationException">A value cannot be converted, or <c>AzureOpenAI:ApiKey</c> is present.</exception>
    public static AppConfiguration LoadAll(string configDirectory, IReadOnlyDictionary<string, string?> environment)
    {
        var fromEnvironment = environment
            .Where(e => e.Key.StartsWith(EnvironmentPrefix, StringComparison.OrdinalIgnoreCase))
            .Select(e => KeyValuePair.Create(e.Key[EnvironmentPrefix.Length..].Replace("__", ":", StringComparison.Ordinal), e.Value));

        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(configDirectory, "appsettings.json"), optional: true, reloadOnChange: false)
            .AddJsonFile(Path.Combine(configDirectory, "appsettings.Local.json"), optional: true, reloadOnChange: false)
            .AddInMemoryCollection(fromEnvironment)
            .Build();

        // A safeguard against a reflexively stored key; the value itself is never echoed.
        if (configuration.GetSection("AzureOpenAI:ApiKey").Exists())
        {
            throw new ConfigurationException(
                "AzureOpenAI:ApiKey nie jest obsługiwany — usuń go z konfiguracji i zmiennych środowiskowych. Klucz API podaje się "
                + "tylko w konsoli (po pobraniu i konwersji) albo potokiem na standardowe wejście.");
        }

        try
        {
            AzureOpenAiSettings azure = configuration.GetSection("AzureOpenAI").Get<AzureOpenAiSettings>() ?? new AzureOpenAiSettings();

            // The binder skips empty values; an empty Temperature or Seed (JSON null, „FAQGEN__…=”) means „do not send”.
            if (configuration["AzureOpenAI:Temperature"] is { Length: 0 })
            {
                azure.Temperature = null;
            }

            if (configuration["AzureOpenAI:Seed"] is { Length: 0 })
            {
                azure.Seed = null;
            }

            return new AppConfiguration(
                configuration.GetSection("Download").Get<DownloadSettings>() ?? new DownloadSettings(),
                azure,
                configuration.GetSection("Faq").Get<FaqSettings>() ?? new FaqSettings());
        }
        catch (InvalidOperationException e)
        {
            throw new ConfigurationException(e.Message, e);
        }
    }

    /// <summary>Checks the AzureOpenAI and Faq values (contracts/cli.md, data-model.md).</summary>
    /// <exception cref="ConfigurationException">The first invalid value, with the field name.</exception>
    public static void Validate(AppConfiguration configuration)
    {
        AzureOpenAiSettings azure = configuration.AzureOpenAI;
        if (string.IsNullOrWhiteSpace(azure.Endpoint))
        {
            throw new ConfigurationException("brak AzureOpenAI:Endpoint (adres zasobu Azure OpenAI, zob. scripts/azure/create-openai.sh).");
        }

        if (!Uri.TryCreate(azure.Endpoint.Trim(), UriKind.Absolute, out Uri? endpoint) || endpoint.Scheme != Uri.UriSchemeHttps)
        {
            throw new ConfigurationException(
                $"AzureOpenAI:Endpoint musi być bezwzględnym adresem https, np. https://<zasób>.openai.azure.com/ (jest „{azure.Endpoint}”).");
        }

        if (string.IsNullOrWhiteSpace(azure.Deployment))
        {
            throw new ConfigurationException("brak AzureOpenAI:Deployment (nazwa wdrożenia modelu).");
        }

        if (!(azure.TimeoutSeconds > 0) || !double.IsFinite(azure.TimeoutSeconds))
        {
            throw new ConfigurationException("AzureOpenAI:TimeoutSeconds musi być > 0.");
        }

        if (azure.Temperature is { } temperature && !(temperature is >= 0 and <= 2))
        {
            throw new ConfigurationException("AzureOpenAI:Temperature musi mieścić się w zakresie 0–2 (pusta wartość: nie wysyłaj).");
        }

        if (azure.MaxOutputTokens <= 0)
        {
            throw new ConfigurationException("AzureOpenAI:MaxOutputTokens musi być > 0.");
        }

        FaqSettings faq = configuration.Faq;
        if (faq.CandidatesPerDocument is < 1 or > 30)
        {
            throw new ConfigurationException("Faq:CandidatesPerDocument musi mieścić się w zakresie 1–30.");
        }

        if (faq.MaxDocumentTokens <= 0)
        {
            throw new ConfigurationException("Faq:MaxDocumentTokens musi być > 0.");
        }

        if (string.IsNullOrWhiteSpace(faq.OutputDirectory))
        {
            throw new ConfigurationException("brak Faq:OutputDirectory (katalog pliku FAQ).");
        }
    }
}

/// <summary>The configuration cannot be used (exit code 2).</summary>
internal sealed class ConfigurationException : Exception
{
    /// <summary>Creates the exception.</summary>
    public ConfigurationException()
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    public ConfigurationException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and a cause.</summary>
    public ConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
