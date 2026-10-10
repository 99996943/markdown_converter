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
