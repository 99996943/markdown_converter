namespace LegalAgent.PdfParser;

/// <summary>Progress of a conversion: page <paramref name="PageNumber"/> of <paramref name="PageCount"/> in stage <paramref name="Stage"/>.</summary>
/// <param name="PageNumber">1-based page number.</param>
/// <param name="PageCount">Number of pages of the document.</param>
/// <param name="Stage">Name of the pipeline stage.</param>
public readonly record struct ConversionProgress(int PageNumber, int PageCount, string Stage);
