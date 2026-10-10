using System.Net;
using LegalAgent.Faq.Tests.Fakes;
using MBank.FaqGenerator.Tests.Fakes;

namespace MBank.FaqGenerator.Tests;

/// <summary>The key is asked for only after a complete download, a complete conversion and the size check (FR-413).</summary>
public sealed class KeyOrderTests : IDisposable
{
    private static readonly string[] Urls = [.. Enumerable.Range(1, 5).Select(i => $"https://www.example.test/pdf/reg-{i}.pdf")];

    private readonly AppHarness app = new();

    public KeyOrderTests()
    {
        app.ServeRegulations(Urls);
    }

    public void Dispose() => app.Dispose();

    [Fact]
    public async Task DownloadIncomplete_KeyNotRead()
    {
        app.Http.Status(Urls[2], HttpStatusCode.NotFound);

        AppRun run = await app.RunAsync(UrlArgs(), "", Ct);

        Assert.Equal(3, run.Code);
        Assert.Equal(0, app.Keys.Reads);
    }

    [Fact]
    public async Task ConversionFailed_KeyNotRead()
    {
        app.Http.Pdf(Urls[3], TestPdfs.Corrupted());

        AppRun run = await app.RunAsync(UrlArgs(), "", Ct);

        Assert.NotEqual(0, run.Code);
        Assert.Equal(0, app.Keys.Reads);
        Assert.Empty(app.Model.Calls);
    }

    [Fact]
    public async Task DocumentTooLong_KeyNotRead()
    {
        app.WriteSettings(new Dictionary<string, object?>(), faq: new Dictionary<string, object?> { ["MaxDocumentTokens"] = 10 });

        AppRun run = await app.RunAsync(UrlArgs(), "", Ct);

        Assert.NotEqual(0, run.Code);
        Assert.Equal(0, app.Keys.Reads);
        Assert.Empty(app.Model.Calls);
    }

    [Fact]
    public async Task Success_KeyReadOnce_AfterConversionSummary()
    {
        AppRun run = await app.RunAsync(UrlArgs(), "", Ct);

        Assert.Equal(0, run.Code);
        Assert.Equal(1, app.Keys.Captures);
        Assert.True(
            run.Out.IndexOf("Klucz API Azure OpenAI: ", StringComparison.Ordinal)
                > run.Out.IndexOf("Przekonwertowano 5 z 5 plików.", StringComparison.Ordinal),
            "the key is asked for after the conversion summary");
        Assert.Equal(1, run.Out.Split("Klucz API Azure OpenAI: ").Length - 1);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string[] UrlArgs() => [.. Urls.SelectMany(u => new[] { "--url", u })];
}
