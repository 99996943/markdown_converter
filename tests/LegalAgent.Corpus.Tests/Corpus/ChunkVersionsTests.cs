using System.Text.RegularExpressions;
using LegalAgent.Chunking.Serialization;
using LegalAgent.Corpus.Manifest;

namespace LegalAgent.Corpus.Tests.Corpus;

/// <summary>
/// Spec 004, T043 (FR-273, SC-043): for every change recorded between two versions of a corpus document, the chunk
/// holding the changed unit exists in both versions with the same unit key. Reads the committed manifest and chunk
/// files (kept current by <c>verify</c>).
/// </summary>
public sealed partial class ChunkVersionsTests
{
    [Fact]
    public void EveryRecordedChangeIsInChunksWithTheSameUnitKeyInBothVersions()
    {
        string corpus = Path.Combine(CorpusSampleTests.RepoRoot(), "corpus");
        Manifest.Manifest manifest = ManifestWriter.Read(File.ReadAllText(Path.Combine(corpus, "manifest.json")));
        var byId = manifest.Documents.ToDictionary(d => d.Id, StringComparer.Ordinal);
        var chunkCache = new Dictionary<string, IReadOnlyList<ChunkRecord>>(StringComparer.Ordinal);
        IReadOnlyList<ChunkRecord> Chunks(ManifestDocument d) =>
            chunkCache.TryGetValue(d.Id, out IReadOnlyList<ChunkRecord>? cached)
                ? cached
                : chunkCache[d.Id] = ChunkJson.ReadLines(File.ReadAllText(Path.Combine(corpus, d.Chunks!)));

        var failures = new List<string>();
        int checkedChanges = 0;
        foreach (ManifestDocument newer in manifest.Documents.Where(d => d.PreviousVersion is not null && d.Changes is { Count: > 0 }))
        {
            ManifestDocument older = byId[newer.PreviousVersion!];
            foreach (VersionChange change in newer.Changes!)
            {
                checkedChanges++;
                var candidates = Chunks(newer)
                    .Where(r => r.Chunk.Content.Contains(change.After, StringComparison.Ordinal))
                    .Where(r => HoldsUnit(r, change))
                    .ToList();
                if (candidates.Count == 0)
                {
                    failures.Add($"{newer.Id}: „{change.Unit}” (str. {change.Page}) — brak fragmentu z „{change.After}”");
                    continue;
                }

                HashSet<string> keys = [.. candidates.Select(r => r.Chunk.UnitKey)];
                bool inOlder = Chunks(older).Any(r => keys.Contains(r.Chunk.UnitKey) && r.Chunk.Content.Contains(change.Before, StringComparison.Ordinal));
                if (!inOlder)
                {
                    failures.Add($"{newer.Id} → {older.Id}: „{change.Unit}” — w starszej wersji brak fragmentu o kluczu {string.Join(" / ", keys)} z „{change.Before}”");
                }
            }
        }

        Assert.True(checkedChanges > 0, "manifest nie zawiera zmian między wersjami");
        Assert.True(failures.Count == 0, $"{failures.Count} z {checkedChanges} zmian:\n" + string.Join("\n", failures));
    }

    /// <summary>
    /// Whether the chunk holds the changed unit: „§ N …”/„Art. N …” by its citation, „poz. N” by the table row
    /// „| N. |”, „krok X” by the step label; other units (a tariff section) by the page of the change. The manifest
    /// page of a tariff position is the first page of its table, so it is not used for positions.
    /// </summary>
    private static bool HoldsUnit(ChunkRecord record, VersionChange change)
    {
        if (LegalUnit().Match(change.Unit) is { Success: true } legal)
        {
            return record.Chunk.Citation == legal.Value;
        }

        if (Position().Match(change.Unit) is { Success: true } position)
        {
            return record.Chunk.Content.Contains($"| {position.Groups[1].Value}. |", StringComparison.Ordinal);
        }

        if (Step().Match(change.Unit) is { Success: true } step)
        {
            return record.Chunk.Content.Contains(step.Groups[1].Value + @"\.", StringComparison.Ordinal);
        }

        return record.Chunk.Pages.First <= change.Page && change.Page <= record.Chunk.Pages.Last;
    }

    [GeneratedRegex(@"^(§|Art\.) \d+[a-z]*", RegexOptions.CultureInvariant)]
    private static partial Regex LegalUnit();

    [GeneratedRegex(@"^poz\. (\d+)$", RegexOptions.CultureInvariant)]
    private static partial Regex Position();

    [GeneratedRegex(@"^krok ([\d.]+)$", RegexOptions.CultureInvariant)]
    private static partial Regex Step();
}
