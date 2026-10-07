using System.Globalization;

namespace LegalAgent.PdfParser.Layout;

/// <summary>Well-known keys of <see cref="LayoutLine.Annotations"/> shared between pipeline stages.</summary>
public static class LayoutAnnotations
{
    /// <summary>Left edge (pt) of the text column the line belongs to; set by reading order on multi-column pages.</summary>
    public const string ColumnLeft = "column.left";

    /// <summary>Right edge (pt) of the text column the line belongs to; set by reading order on multi-column pages.</summary>
    public const string ColumnRight = "column.right";

    /// <summary>Stores a coordinate annotation in invariant round-trip format.</summary>
    public static void SetNumber(LayoutLine line, string key, double value)
    {
        ArgumentNullException.ThrowIfNull(line);
        line.Annotations[key] = value.ToString("R", CultureInfo.InvariantCulture);
    }

    /// <summary>Reads a coordinate annotation, or <c>null</c> when absent.</summary>
    public static double? GetNumber(LayoutLine line, string key)
    {
        ArgumentNullException.ThrowIfNull(line);
        return line.Annotations.TryGetValue(key, out string? text)
            && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                ? value
                : null;
    }
}
