using System.Text.RegularExpressions;

namespace LegalAgent.PdfParser.Text;

/// <summary>Detects bold and italic faces from font names and PdfPig flags (research R6).</summary>
internal static partial class FontStyleDetector
{
    private static readonly string[] BoldTokens = ["Bold", "Black", "Heavy", "Semibold", "Demi"];
    private static readonly string[] ItalicTokens = ["Italic", "Oblique"];

    /// <summary>True when the face is bold.</summary>
    /// <param name="fontName">Font name, possibly with a subset prefix such as <c>ABCDEF+</c>.</param>
    /// <param name="pdfPigIsBold">The bold flag reported by PdfPig.</param>
    /// <param name="renderedWithStroke">True for the fill-then-stroke rendering mode (artificial bold).</param>
    public static bool IsBold(string fontName, bool pdfPigIsBold, bool renderedWithStroke)
    {
        if (pdfPigIsBold || renderedWithStroke)
        {
            return true;
        }

        string name = StripSubsetPrefix(fontName);
        return ContainsAny(name, BoldTokens) || name.EndsWith(",B", StringComparison.Ordinal) || name.EndsWith("-B", StringComparison.Ordinal);
    }

    /// <summary>True when the face is italic or oblique.</summary>
    /// <param name="fontName">Font name, possibly with a subset prefix.</param>
    /// <param name="pdfPigIsItalic">The italic flag reported by PdfPig.</param>
    public static bool IsItalic(string fontName, bool pdfPigIsItalic)
    {
        if (pdfPigIsItalic)
        {
            return true;
        }

        string name = StripSubsetPrefix(fontName);
        return ContainsAny(name, ItalicTokens) || name.EndsWith(",It", StringComparison.Ordinal) || name.EndsWith("-It", StringComparison.Ordinal);
    }

    private static string StripSubsetPrefix(string fontName) =>
        string.IsNullOrEmpty(fontName) ? string.Empty : SubsetPrefix().Replace(fontName, string.Empty);

    private static bool ContainsAny(string name, string[] tokens)
    {
        foreach (string token in tokens)
        {
            if (name.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    [GeneratedRegex("^[A-Z]{6}\\+", RegexOptions.CultureInvariant)]
    private static partial Regex SubsetPrefix();
}
