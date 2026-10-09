using System.Diagnostics;
using LegalAgent.Corpus.Planning;

namespace LegalAgent.Corpus.Tests.Corpus;

/// <summary>SC-021: the whole recorded corpus is generated into a temporary directory in under ten minutes.</summary>
[Trait("Category", "Performance")]
public sealed class CorpusPerformanceTests : IDisposable
{
    private static readonly TimeSpan Limit = TimeSpan.FromMinutes(10);

    private readonly string _out = Path.Combine(Path.GetTempPath(), "corpus-perf-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_out))
        {
            Directory.Delete(_out, recursive: true);
        }
    }

    [Fact]
    public async Task FullGenerate_TakesLessThanTenMinutes()
    {
        RunParameters parameters = CorpusSampleTests.RecordedParameters() with { OutputDirectory = _out };

        var watch = Stopwatch.StartNew();
        GenerationResult result = await CorpusGenerator.GenerateAsync(parameters, CorpusSampleTests.Options(), TestContext.Current.CancellationToken);
        watch.Stop();

        Assert.NotEmpty(result.Documents);
        Assert.True(watch.Elapsed < Limit, $"generate: {watch.Elapsed}");
    }
}
