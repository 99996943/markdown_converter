using System.Net;
using System.Text.Json;
using LegalAgent.Faq;
using LegalAgent.Faq.Model;
using LegalAgent.Faq.Tests.Fakes;
using MBank.FaqGenerator.Tests.Fakes;
using Microsoft.SemanticKernel.ChatCompletion;

namespace MBank.FaqGenerator.Tests;

/// <summary>The real Azure OpenAI connector through a fake HTTP handler (research R7).</summary>
public sealed class ModelTransportTests : IDisposable
{
    private readonly ModelHttpHandler http = new();

    public void Dispose() => http.Dispose();

    [Fact]
    public async Task Request_GoesToDeployment_WithKeyAndStructuredOutput()
    {
        http.Completion(FaqJson.Candidates("D1", 2), 120, 12).Completion(FaqJson.Selection(1, 1), 30, 3);

        FaqResult result = await GenerateAsync(Settings());

        Assert.Equal(2, http.Requests.Count);
        ModelRequest request = http.Requests[0];
        Assert.Equal("https://faq.example.test/openai/deployments/gpt-4o-mini/chat/completions", request.Address.GetLeftPart(UriPartial.Path));
        Assert.Equal("test-key", request.ApiKey);
        JsonElement json = request.Json;
        JsonElement format = json.GetProperty("response_format");
        Assert.Equal("json_schema", format.GetProperty("type").GetString());
        Assert.True(format.GetProperty("json_schema").GetProperty("strict").GetBoolean());
        Assert.Equal(
            JsonSerializer.Serialize(JsonDocument.Parse(FaqSchemas.Candidates).RootElement),
            JsonSerializer.Serialize(format.GetProperty("json_schema").GetProperty("schema")));
        Assert.Equal(0, json.GetProperty("temperature").GetDouble());
        Assert.Equal(42, json.GetProperty("seed").GetInt64());
        int limit = json.TryGetProperty("max_completion_tokens", out JsonElement mct) ? mct.GetInt32() : json.GetProperty("max_tokens").GetInt32();
        Assert.Equal(4096, limit);
        Assert.Equal(
            JsonSerializer.Serialize(JsonDocument.Parse(FaqSchemas.Selection).RootElement),
            JsonSerializer.Serialize(http.Requests[1].Json.GetProperty("response_format").GetProperty("json_schema").GetProperty("schema")));

        Assert.Equal("Pytanie końcowe 1?", Assert.Single(result.Items).Question);
        Assert.Equal(new FaqUsage(150, 15), result.Usage);
    }

    [Fact]
    public async Task NullTemperatureAndSeed_AreNotSent()
    {
        http.Completion(FaqJson.Candidates("D1", 2)).Completion(FaqJson.Selection(1, 1));
        AzureOpenAiSettings settings = Settings();
        settings.Temperature = null;
        settings.Seed = null;

        await GenerateAsync(settings);

        JsonElement json = http.Requests[0].Json;
        Assert.False(json.TryGetProperty("temperature", out _));
        Assert.False(json.TryGetProperty("seed", out _));
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, "Przekroczono limit zapytań wdrożenia (429) przy dokumencie D1.")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "503")]
    public async Task ThrottledOrUnavailable_OneRequestOnly_ExitCode6(HttpStatusCode status, string message)
    {
        using AppHarness app = Connected();
        app.ModelHttp.Error(status, "error", "spróbuj później").Error(status, "error", "spróbuj później").Error(status, "error", "spróbuj później");

        AppRun run = await app.RunAsync(UrlArgs(), "", Ct);

        Assert.Equal(6, run.Code);
        Assert.Contains(message, run.Err, StringComparison.Ordinal);
        Assert.Single(app.ModelHttp.Requests);
    }

    [Fact]
    public async Task SlowService_TimeoutFromConfiguration_ExitCode6()
    {
        using AppHarness app = Connected(new Dictionary<string, object?> { ["TimeoutSeconds"] = 0.5 });
        app.ModelHttp.Delay(TimeSpan.FromSeconds(5), FaqJson.Candidates("D1", 3));

        AppRun run = await app.RunAsync(UrlArgs(), "", Ct);

        Assert.Equal(6, run.Code);
        Assert.Contains("Brak odpowiedzi usługi w ciągu 0,5 s (krok kandydatów, D1).", run.Err, StringComparison.Ordinal);
        Assert.Single(app.ModelHttp.Requests);
    }

    [Fact]
    public async Task NameResolutionFailure_ExitCode6()
    {
        using AppHarness app = Connected();
        app.ModelHttp.Throw(new HttpRequestException("Nie można rozpoznać nazwy hosta (faq.example.test:443)"));

        AppRun run = await app.RunAsync(UrlArgs(), "", Ct);

        Assert.Equal(6, run.Code);
        Assert.Contains("Błąd połączenia z usługą Azure OpenAI: ", run.Err, StringComparison.Ordinal);
        Assert.Contains("Nie można rozpoznać nazwy hosta", run.Err, StringComparison.Ordinal);
        Assert.Single(app.ModelHttp.Requests);
    }

    private static readonly string[] Urls = [.. Enumerable.Range(1, 5).Select(i => $"https://www.example.test/pdf/reg-{i}.pdf")];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string[] UrlArgs() => [.. Urls.SelectMany(u => new[] { "--url", u })];

    private static AppHarness Connected(Dictionary<string, object?>? azure = null)
    {
        var app = new AppHarness { UseConnector = true };
        app.ServeRegulations(Urls);
        if (azure is not null)
        {
            app.WriteSettings(new Dictionary<string, object?>(), azure);
        }

        return app;
    }

    private static AzureOpenAiSettings Settings() => new()
    {
        Endpoint = AppHarness.Endpoint,
        Deployment = "gpt-4o-mini",
        Model = "gpt-4o-mini",
        TimeoutSeconds = 30,
        Temperature = 0,
        Seed = 42,
        MaxOutputTokens = 4096,
    };

    private async Task<FaqResult> GenerateAsync(AzureOpenAiSettings settings)
    {
        IChatCompletionService chat = ChatServiceFactory.Create(settings, "test-key", http);
        var generator = new LegalAgent.Faq.FaqGenerator(
            chat,
            new FaqGeneratorOptions { ItemCount = 1 },
            (step, schema) => ChatServiceFactory.ExecutionSettings(settings, step, schema));
        return await generator.GenerateAsync(
            [new FaqDocumentInput("Regulamin", new Uri("https://example.test/a.pdf"), "# Regulamin\n\nTreść.\n", [])],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
