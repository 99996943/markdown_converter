using System.Diagnostics;
using System.Text.Json;
using LegalAgent.Chunking.Model;
using LegalAgent.Chunking.Tests.Fixtures;
using LegalAgent.PdfParser;
using LegalAgent.PdfParser.Model;
using Microsoft.Extensions.DependencyInjection;

namespace LegalAgent.Chunking.Tests;

/// <summary>
/// T046 (SC-045): chunking a converted corpus document takes under a second, and chunking the whole corpus adds at
/// most 20% to the time of converting it (the work <c>refresh</c> did before chunk files).
/// </summary>
[Trait("Category", "Performance")]
public sealed class PerformanceTests
{
    [Fact]
    public async Task ChunkAsync_CorpusDocumentsAreFastComparedWithConversion()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ServiceProvider provider = new ServiceCollection().AddLegalAgentChunking().BuildServiceProvider();
        IPdfMarkdownConverter converter = provider.GetRequiredService<IPdfMarkdownConverter>();
        IDocumentChunker chunker = provider.GetRequiredService<IDocumentChunker>();
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(RepoPaths.Corpus("manifest.json")));

        TimeSpan converting = TimeSpan.Zero;
        TimeSpan chunking = TimeSpan.Zero;
        (string Id, TimeSpan Time) slowest = (string.Empty, TimeSpan.Zero);
        foreach (JsonElement entry in manifest.RootElement.GetProperty("documents").EnumerateArray())
        {
            string id = entry.GetProperty("id").GetString()!;
            byte[] pdf = await File.ReadAllBytesAsync(RepoPaths.Corpus(entry.GetProperty("pdf").GetString()!), ct);

            var watch = Stopwatch.StartNew();
            PdfConversionResult conversion = await converter.ConvertAsync(new MemoryStream(pdf), cancellationToken: ct);
            converting += watch.Elapsed;

            await chunker.ChunkAsync(conversion, new DocumentMetadata(id), cancellationToken: ct);
            watch.Restart();
            ChunkedDocument document = await chunker.ChunkAsync(conversion, new DocumentMetadata(id), cancellationToken: ct);
            TimeSpan time = watch.Elapsed;
            chunking += time;
            if (time > slowest.Time)
            {
                slowest = (id, time);
            }

            Assert.NotEmpty(document.Chunks);
        }

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"konwersja {converting.TotalSeconds:F1} s, podział {chunking.TotalSeconds:F2} s ({chunking / converting:P1}); najwolniejszy {slowest.Id}: {slowest.Time.TotalMilliseconds:F0} ms");
        Assert.True(slowest.Time < TimeSpan.FromSeconds(1), $"{slowest.Id}: {slowest.Time.TotalMilliseconds:F0} ms");
        Assert.True(chunking <= converting * 0.2, $"podział {chunking.TotalSeconds:F2} s > 20% konwersji {converting.TotalSeconds:F1} s");
    }
}
