using MBank.FaqGenerator.Tests.Fakes;

namespace MBank.FaqGenerator.Tests;

/// <summary>Atomic writing of FAQ_mBank.md (FR-433).</summary>
public sealed class FaqWriteTests : IDisposable
{
    private static readonly string[] Urls = [.. Enumerable.Range(1, 5).Select(i => $"https://www.example.test/pdf/reg-{i}.pdf")];

    private readonly AppHarness app = new();

    public FaqWriteTests()
    {
        app.ServeRegulations(Urls);
        Directory.CreateDirectory(app.FaqDirectory);
    }

    public void Dispose() => app.Dispose();

    [Fact]
    public async Task ExistingFaq_IsReplacedEntirely_WithoutTempFile()
    {
        await File.WriteAllTextAsync(app.FaqFile, "stara treść\n" + new string('x', 100_000), Ct);

        AppRun run = await app.RunAsync(UrlArgs(), "", Ct);

        Assert.Equal(0, run.Code);
        string faq = await File.ReadAllTextAsync(app.FaqFile, Ct);
        Assert.StartsWith("---\ntype: faq\n", faq, StringComparison.Ordinal);
        Assert.DoesNotContain("stara treść", faq, StringComparison.Ordinal);
        Assert.DoesNotContain("xxx", faq, StringComparison.Ordinal);
        Assert.False(File.Exists(app.FaqFile + ".tmp"));
    }

    [Fact]
    public async Task MoveFails_ExitCode4_OldFileKept_TempRemoved()
    {
        AppRun run;
        if (OperatingSystem.IsWindows())
        {
            await File.WriteAllTextAsync(app.FaqFile, "stara treść\n", Ct);
            using (new FileStream(app.FaqFile, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                run = await app.RunAsync(UrlArgs(), "", Ct);
            }

            Assert.Equal("stara treść\n", await File.ReadAllTextAsync(app.FaqFile, Ct));
        }
        else
        {
            Directory.CreateDirectory(app.FaqFile);
            run = await app.RunAsync(UrlArgs(), "", Ct);
        }

        Assert.Equal(4, run.Code);
        Assert.Contains($"Nie można zapisać FAQ_mBank.md w {app.FaqDirectory}: ", run.Err, StringComparison.Ordinal);
        Assert.False(File.Exists(app.FaqFile + ".tmp"));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string[] UrlArgs() => [.. Urls.SelectMany(u => new[] { "--url", u })];
}
