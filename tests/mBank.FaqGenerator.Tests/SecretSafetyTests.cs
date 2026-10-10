using System.Collections;
using System.Net;
using System.Text;
using LegalAgent.Faq.Tests.Fakes;
using MBank.FaqGenerator.Tests.Fakes;

namespace MBank.FaqGenerator.Tests;

/// <summary>The API key never leaves the process memory (US2, FR-410–412).</summary>
public sealed class SecretSafetyTests : IDisposable
{
    private const string Key = "KLUCZ-7f3a9c-TEST";

    private static readonly string[] Urls = [.. Enumerable.Range(1, 5).Select(i => $"https://www.example.test/pdf/reg-{i}.pdf")];

    private readonly AppHarness app = new();
    private readonly Dictionary<string, string?> processEnvironmentBefore = ProcessEnvironment();

    public SecretSafetyTests()
    {
        app.ServeRegulations(Urls);
        app.Keys.Type(Key).Enter();
    }

    public void Dispose() => app.Dispose();

    [Fact]
    public async Task Success_KeyNowhere()
    {
        AppRun run = await app.RunAsync(UrlArgs(), "", Ct);

        Assert.Equal(0, run.Code);
        AssertKeyNowhere(run);
    }

    [Fact]
    public async Task ServiceRejectsKey_ResponseEchoesIt_KeyNowhere()
    {
        app.UseConnector = true;
        app.ModelHttp.Error(HttpStatusCode.Unauthorized, "401", $"Access denied: invalid subscription key {Key} or wrong API endpoint.");

        AppRun run = await app.RunAsync(UrlArgs(), "", Ct);

        Assert.NotEqual(0, run.Code);
        Assert.Equal(Key, app.ModelHttp.Requests[0].ApiKey);
        AssertKeyNowhere(run);
    }

    [Fact]
    public async Task ResponseRejected_KeyNowhere()
    {
        app.Model.Respond("to nie jest JSON " + Key);

        AppRun run = await app.RunAsync(UrlArgs(), "", Ct);

        Assert.NotEqual(0, run.Code);
        AssertKeyNowhere(run);
    }

    [Fact]
    public async Task CancelledDuringRequest_KeyNowhere()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        app.Model.Respond(FaqJson.Candidates("D1", 3)).Hang(started);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Ct);

        Task<AppRun> running = app.RunAsync(UrlArgs(), "", cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(30), Ct);
        await cancellation.CancelAsync();
        AppRun run = await running;

        Assert.Equal(130, run.Code);
        AssertKeyNowhere(run);
    }

    private void AssertKeyNowhere(AppRun run)
    {
        Assert.DoesNotContain(Key, run.Out, StringComparison.Ordinal);
        Assert.DoesNotContain(Key, run.Err, StringComparison.Ordinal);
        byte[] needle = Encoding.UTF8.GetBytes(Key);
        foreach (string file in Directory.EnumerateFiles(app.Root.Path, "*", SearchOption.AllDirectories))
        {
            Assert.True(File.ReadAllBytes(file).AsSpan().IndexOf(needle) < 0, $"the key was written to {file}");
        }

        Assert.DoesNotContain(app.Environment.Values, v => v is not null && v.Contains(Key, StringComparison.Ordinal));
        Assert.Equal(processEnvironmentBefore, ProcessEnvironment());
        Assert.Equal(Key.Length, run.Out.Count(c => c == '*'));
    }

    private static Dictionary<string, string?> ProcessEnvironment()
    {
        var variables = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in System.Environment.GetEnvironmentVariables())
        {
            variables[(string)entry.Key] = entry.Value as string;
        }

        return variables;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string[] UrlArgs() => [.. Urls.SelectMany(u => new[] { "--url", u })];
}
