using LegalAgent.PdfParser.Model;

namespace LegalAgent.PdfParser.Rendering;

/// <summary>Renders a <see cref="LegalDocument"/> to Markdown (contracts/markdown-output.md).</summary>
public interface IMarkdownRenderer
{
    /// <summary>Renders the document; the result uses LF line endings and ends with a single LF (empty for an empty document).</summary>
    /// <param name="document">The document to render.</param>
    /// <param name="options">Rendering options; the renderer's defaults are used when null.</param>
    string Render(LegalDocument document, RenderingOptions? options = null);
}
