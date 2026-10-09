using System.Text.RegularExpressions;
using LegalAgent.Chunking.Model;

namespace LegalAgent.Chunking.Tests.Fixtures;

/// <summary>
/// FR-234 / SC-041: the chunks of a document hold every word of its Markdown (without page markers, skipped-page
/// comments and the title line) as many times as the document does; words beyond that may only come from the
/// repetitions the spec allows — the unit heading in continuation parts (FR-231), table header rows with their GFM
/// separator (FR-223) and
/// footnote definitions (FR-232).
/// </summary>
public static partial class WordCoverage
{
    /// <summary>Asserts the coverage of <paramref name="chunks"/> against the Markdown of the whole document.</summary>
    public static void AssertCovers(string documentMarkdown, bool hasTitle, IReadOnlyList<Chunk> chunks)
    {
        ArgumentNullException.ThrowIfNull(documentMarkdown);
        ArgumentNullException.ThrowIfNull(chunks);

        string text = PageMarkers().Replace(documentMarkdown, " ");
        if (hasTitle && text.StartsWith("# ", StringComparison.Ordinal))
        {
            text = text[(text.IndexOf('\n', StringComparison.Ordinal) + 1)..];
        }

        Dictionary<string, int> expected = Count(Words(text));
        Dictionary<string, int> actual = Count(chunks.SelectMany(c => Words(c.Content)));
        Dictionary<string, int> repeatable = Count(chunks.SelectMany(Repeatable));

        var missing = expected.Where(e => actual.GetValueOrDefault(e.Key) < e.Value).Select(e => $"{e.Key} ×{e.Value - actual.GetValueOrDefault(e.Key)}").ToList();
        var extra = actual
            .Where(a => a.Value - expected.GetValueOrDefault(a.Key) > repeatable.GetValueOrDefault(a.Key))
            .Select(a => $"{a.Key} ×{a.Value - expected.GetValueOrDefault(a.Key)}")
            .ToList();

        Assert.True(missing.Count == 0, "Słowa dokumentu brakujące we fragmentach: " + string.Join(", ", missing.Take(20)));
        Assert.True(extra.Count == 0, "Słowa nadmiarowe we fragmentach: " + string.Join(", ", extra.Take(20)));
    }

    private static IEnumerable<string> Repeatable(Chunk chunk)
    {
        string[] lines = chunk.Content.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            bool heading = i == 0 && chunk.Part > 1 && lines[i].StartsWith('#');
            bool separator = lines[i].StartsWith("| ---", StringComparison.Ordinal);
            bool tableHeader = separator || (i + 1 < lines.Length && lines[i].StartsWith('|') && lines[i + 1].StartsWith("| ---", StringComparison.Ordinal));
            bool footnote = lines[i].StartsWith("[^", StringComparison.Ordinal);
            if (heading || tableHeader || footnote)
            {
                foreach (string word in Words(lines[i]))
                {
                    yield return word;
                }
            }
        }
    }

    private static string[] Words(string text) => text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    private static Dictionary<string, int> Count(IEnumerable<string> words)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (string word in words)
        {
            counts[word] = counts.GetValueOrDefault(word) + 1;
        }

        return counts;
    }

    [GeneratedRegex(@"<!-- page: \d+ -->|<!-- page \d+ skipped: [a-z-]+ -->", RegexOptions.CultureInvariant)]
    private static partial Regex PageMarkers();
}
