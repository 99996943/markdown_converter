using System.Globalization;
using System.Text.RegularExpressions;

namespace LegalAgent.Chunking.Identity;

/// <summary>
/// Builds unit keys (research R6): the designation shared by all versions, then the shortest suffix of the unit's
/// segment path that is unique in the document; identical full paths get " #n" from the second occurrence.
/// Paragraphs and articles are numbered through the whole document, so their key does not depend on the chapter
/// and survives a chapter inserted in a later version.
/// </summary>
internal static partial class UnitKeyBuilder
{
    /// <summary>Segment of the preamble (metadata only, never content).</summary>
    public const string PreambleSegment = "~wstep";

    private const string KeySeparator = " | ";
    private const string PathSeparator = " > ";

    /// <summary>Path segment of a section: its designation, or its heading text when it has none, normalised.</summary>
    public static string Segment(string? designation, string headingText)
    {
        string text = string.IsNullOrWhiteSpace(designation) ? headingText : designation;
        return Whitespace().Replace(text, " ").Trim().TrimEnd('.').TrimEnd();
    }

    /// <summary>Keys of the units of one document, given their segment paths from the root, in document order.</summary>
    public static IReadOnlyList<string> Build(string seriesKey, IReadOnlyList<IReadOnlyList<string>> paths)
    {
        ArgumentNullException.ThrowIfNull(seriesKey);
        ArgumentNullException.ThrowIfNull(paths);

        var keys = new List<string>(paths.Count);
        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < paths.Count; i++)
        {
            string path = UniqueSuffix(paths, i);
            int occurrence = occurrences.TryGetValue(path, out int seen) ? seen + 1 : 1;
            occurrences[path] = occurrence;
            string suffix = occurrence == 1 ? string.Empty : string.Create(CultureInfo.InvariantCulture, $" #{occurrence}");
            keys.Add(seriesKey + KeySeparator + path + suffix);
        }

        return keys;
    }

    private static string UniqueSuffix(IReadOnlyList<IReadOnlyList<string>> paths, int index)
    {
        IReadOnlyList<string> own = paths[index];
        for (int length = 1; length < own.Count; length++)
        {
            string candidate = Suffix(own, length);
            bool unique = true;
            for (int j = 0; j < paths.Count && unique; j++)
            {
                unique = j == index || !string.Equals(Suffix(paths[j], length), candidate, StringComparison.Ordinal);
            }

            if (unique)
            {
                return candidate;
            }
        }

        // The full path; identical full paths are told apart by the occurrence number.
        return Suffix(own, own.Count);
    }

    private static string Suffix(IReadOnlyList<string> path, int length) =>
        string.Join(PathSeparator, path.Skip(Math.Max(0, path.Count - length)));

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();
}
