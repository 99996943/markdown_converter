using System.Collections.Concurrent;
using System.Globalization;
using LegalAgent.Chunking.Model;
using LegalAgent.Chunking.Serialization;
using LegalAgent.Chunking.Tests.Fixtures;
using LegalAgent.PdfParser;
using LegalAgent.PdfParser.Model;
using Microsoft.Extensions.DependencyInjection;

namespace LegalAgent.Chunking.Tests.Determinism;

/// <summary>T032: identical output for repeated, concurrent and culture-varied calls (FR-205, FR-272).</summary>
public sealed class DeterminismTests
{
    [Theory]
    [InlineData("regulaminy/REG-06.pdf", "REG-06", "BP/REG/06")]
    [InlineData("taryfy/TAR-04.pdf", "TAR-04", "BP/TAR/04")]
    public async Task ChunkAsync_RepeatedConcurrentAndUnderOtherCulturesGivesTheSameBytes(string pdf, string id, string designation)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ServiceProvider provider = new ServiceCollection().AddLegalAgentChunking().BuildServiceProvider();
        IDocumentChunker chunker = provider.GetRequiredService<IDocumentChunker>();
        await using FileStream stream = File.OpenRead(RepoPaths.Corpus(pdf));
        PdfConversionResult conversion = await provider.GetRequiredService<IPdfMarkdownConverter>().ConvertAsync(stream, cancellationToken: ct);
        var metadata = new DocumentMetadata(id) { Designation = designation, ValidFrom = new DateOnly(2026, 6, 1) };

        string expected = ChunkJson.ToJsonLines(await chunker.ChunkAsync(conversion, metadata, cancellationToken: ct));

        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(expected, ChunkJson.ToJsonLines(await chunker.ChunkAsync(conversion, metadata, cancellationToken: ct)));
        }

        var concurrent = new ConcurrentBag<string>();
        await Parallel.ForEachAsync(Enumerable.Range(0, 16), ct, async (_, token) =>
            concurrent.Add(ChunkJson.ToJsonLines(await chunker.ChunkAsync(conversion, metadata, cancellationToken: token))));
        Assert.All(concurrent, json => Assert.Equal(expected, json));

        foreach (string culture in new[] { "pl-PL", "tr-TR", "ar-SA" })
        {
            string json = await Task.Run(
                async () =>
                {
                    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
                    return ChunkJson.ToJsonLines(await chunker.ChunkAsync(conversion, metadata, cancellationToken: ct));
                },
                ct);
            Assert.Equal(expected, json);
        }

        Assert.NotEmpty(expected);
    }
}
