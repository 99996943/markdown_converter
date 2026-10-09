using System.Globalization;
using System.Text;

namespace LegalAgent.Downloads;

/// <summary>One planned download: position, address and target file name.</summary>
/// <param name="Index">1-based position in the list.</param>
/// <param name="Address">Address given by the user.</param>
/// <param name="FileName">Target file name in the output directory.</param>
public sealed record PlannedDownload(int Index, Uri Address, string FileName);

/// <summary>Derives deterministic, file-system-safe file names from addresses (FR-321, research R7).</summary>
public static class FileNamePlanner
{
    private const string Extension = ".pdf";
    private const int MaxStemLength = 120;

    private static readonly HashSet<string> ReservedNames = new(
        ["CON", "PRN", "AUX", "NUL", .. Enumerable.Range(1, 9).SelectMany(i => new[] { $"COM{i}", $"LPT{i}" })],
        StringComparer.OrdinalIgnoreCase);

    /// <summary>Plans file names for the addresses; the result depends only on the list and its order.</summary>
    /// <param name="addresses">Addresses in order.</param>
    /// <returns>One planned download per address, in order.</returns>
    public static IReadOnlyList<PlannedDownload> Plan(IReadOnlyList<Uri> addresses)
    {
        ArgumentNullException.ThrowIfNull(addresses);
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var plan = new List<PlannedDownload>(addresses.Count);
        for (int i = 0; i < addresses.Count; i++)
        {
            int index = i + 1;
            string stem = Stem(addresses[i]);
            if (stem.Length == 0)
            {
                stem = string.Create(CultureInfo.InvariantCulture, $"regulamin-{index}");
            }

            while (!used.Add(stem + Extension))
            {
                stem = string.Create(CultureInfo.InvariantCulture, $"{stem}-{index}");
            }

            plan.Add(new PlannedDownload(index, addresses[i], stem + Extension));
        }

        return plan;
    }

    private static string Stem(Uri address)
    {
        string segment = address.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? string.Empty;
        string name = Uri.UnescapeDataString(segment).Normalize(NormalizationForm.FormC);
        name = Trim(ReplaceForbidden(name));

        if (name.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
        {
            name = Trim(name[..^Extension.Length]);
        }

        if (name.Length > MaxStemLength)
        {
            name = Trim(name[..MaxStemLength]);
        }

        return ReservedNames.Contains(name) ? "_" + name : name;
    }

    private static string ReplaceForbidden(string name)
    {
        var builder = new StringBuilder(name.Length);
        foreach (char c in name)
        {
            bool forbidden = char.IsControl(c) || c is '<' or '>' or ':' or '"' or '/' or '\\' or '|' or '?' or '*';
            char next = forbidden ? '-' : c;
            if (next == '-' && builder.Length > 0 && builder[^1] == '-')
            {
                continue;
            }

            builder.Append(next);
        }

        return builder.ToString();
    }

    private static string Trim(string name) => name.Trim(' ', '.');
}
