using MBank.FaqGenerator.Tests.Fakes;

namespace MBank.FaqGenerator.Tests;

public sealed class ConfigurationTests : IDisposable
{
    private static readonly string[] Urls = [.. Enumerable.Range(1, 5).Select(i => $"https://www.example.test/pdf/reg-{i}.pdf")];

    private readonly AppHarness app = new();

    public void Dispose() => app.Dispose();

    [Fact]
    public async Task MissingEndpoint_ExitCode2_BeforeAddressPrompts()
    {
        app.WriteSettings(new Dictionary<string, object?>(), azure: new Dictionary<string, object?> { ["Endpoint"] = "" });

        AppRun run = await app.RunAsync([], string.Join("\n", Urls) + "\n", Ct);

        Assert.Equal(2, run.Code);
        Assert.Contains(
            "Błąd konfiguracji: brak AzureOpenAI:Endpoint (adres zasobu Azure OpenAI, zob. scripts/azure/create-openai.sh).",
            run.Err,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Podaj adres", run.Out, StringComparison.Ordinal);
        Assert.Empty(app.Http.Requests);
    }

    [Theory]
    [InlineData("AzureOpenAI", "Endpoint", "faq.example.test", "AzureOpenAI:Endpoint")]
    [InlineData("AzureOpenAI", "Endpoint", "http://faq.example.test/", "AzureOpenAI:Endpoint")]
    [InlineData("AzureOpenAI", "Deployment", " ", "AzureOpenAI:Deployment")]
    [InlineData("AzureOpenAI", "TimeoutSeconds", "0", "AzureOpenAI:TimeoutSeconds")]
    [InlineData("AzureOpenAI", "TimeoutSeconds", "-1", "AzureOpenAI:TimeoutSeconds")]
    [InlineData("AzureOpenAI", "Temperature", "2.5", "AzureOpenAI:Temperature")]
    [InlineData("AzureOpenAI", "Temperature", "-0.1", "AzureOpenAI:Temperature")]
    [InlineData("AzureOpenAI", "MaxOutputTokens", "0", "AzureOpenAI:MaxOutputTokens")]
    [InlineData("Faq", "CandidatesPerDocument", "0", "Faq:CandidatesPerDocument")]
    [InlineData("Faq", "CandidatesPerDocument", "31", "Faq:CandidatesPerDocument")]
    [InlineData("Faq", "MaxDocumentTokens", "0", "Faq:MaxDocumentTokens")]
    public async Task InvalidValue_ExitCode2_WithFieldName(string section, string field, string value, string name)
    {
        app.Environment[$"FAQGEN__{section}__{field}"] = value;

        AppRun run = await app.RunAsync(UrlArgs(), "", Ct);

        Assert.Equal(2, run.Code);
        Assert.StartsWith("Błąd konfiguracji: ", run.Err, StringComparison.Ordinal);
        Assert.Contains(name, run.Err, StringComparison.Ordinal);
        Assert.Empty(app.Http.Requests);
    }

    [Fact]
    public async Task ApiKeyInConfiguration_ExitCode2_WithoutEchoingIt()
    {
        app.Environment["FAQGEN__AzureOpenAI__ApiKey"] = "sekret-123";

        AppRun run = await app.RunAsync(UrlArgs(), "", Ct);

        Assert.Equal(2, run.Code);
        Assert.Contains("AzureOpenAI:ApiKey", run.Err, StringComparison.Ordinal);
        Assert.Contains("w konsoli", run.Err, StringComparison.Ordinal);
        Assert.Contains("potokiem", run.Err, StringComparison.Ordinal);
        Assert.DoesNotContain("sekret-123", run.Out + run.Err, StringComparison.Ordinal);
        Assert.Empty(app.Http.Requests);
    }

    [Fact]
    public void EmptyTemperatureVariable_IsNull()
    {
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal) { ["FAQGEN__AzureOpenAI__Temperature"] = "" };

        AppConfiguration configuration = AppSettings.LoadAll(app.ConfigDirectory, environment);

        Assert.Null(configuration.AzureOpenAI.Temperature);
        Assert.Equal(42, configuration.AzureOpenAI.Seed);
    }

    [Fact]
    public void Defaults_MatchContract()
    {
        var settings = new AzureOpenAiSettings();
        var faq = new FaqSettings();

        Assert.Equal(("", "gpt-4o-mini", "gpt-4o-mini", 300d, 0d, 42, 4096), (settings.Endpoint, settings.Deployment, settings.Model, settings.TimeoutSeconds, settings.Temperature, settings.Seed, settings.MaxOutputTokens));
        Assert.Equal(("faq", 10, 100000), (faq.OutputDirectory, faq.CandidatesPerDocument, faq.MaxDocumentTokens));
    }

    [Fact]
    public async Task Help_WorksWithoutAzureConfiguration()
    {
        app.WriteSettings(new Dictionary<string, object?>(), azure: new Dictionary<string, object?> { ["Endpoint"] = "" });

        AppRun run = await app.RunAsync(["--help"], "", Ct);

        Assert.Equal(0, run.Code);
        Assert.Contains("Użycie:", run.Out, StringComparison.Ordinal);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string[] UrlArgs() => [.. Urls.SelectMany(u => new[] { "--url", u })];
}
