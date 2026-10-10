using LegalAgent.Faq.Model;

namespace LegalAgent.Faq;

/// <summary>Renders the FAQ file: OKF front matter and one section per item (contracts/faq-file.md).</summary>
public static class FaqMarkdownRenderer
{
    /// <summary>Renders the result; the same input always gives the same text.</summary>
    /// <param name="result">The FAQ.</param>
    /// <param name="header">Front matter values.</param>
    /// <returns>UTF-8 text with LF line endings, ending with one LF.</returns>
    public static string Render(FaqResult result, FaqFileHeader header) => throw new NotImplementedException();
}
