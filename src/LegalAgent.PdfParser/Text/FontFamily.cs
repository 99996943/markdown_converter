namespace LegalAgent.PdfParser.Text;

/// <summary>Font family of a PDF font name (R10).</summary>
internal static class FontFamily
{
    /// <summary>
    /// The font name without the subset prefix „ABCDEF+” and without the style suffix after the first „-” or „,”;
    /// null when <paramref name="fontName"/> is null or empty.
    /// </summary>
    public static string? Of(string? fontName) => throw new NotImplementedException();
}
