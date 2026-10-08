namespace LegalAgent.PdfParser.Model;

/// <summary>Result of a PDF conversion.</summary>
/// <param name="Document">Structured document model.</param>
/// <param name="Markdown">Markdown rendering of the document; LF line endings.</param>
/// <param name="Report">Diagnostics.</param>
/// <param name="IsComplete">False if and only if the report lists skipped pages.</param>
public sealed record PdfConversionResult(LegalDocument Document, string Markdown, ConversionReport Report, bool IsComplete);
