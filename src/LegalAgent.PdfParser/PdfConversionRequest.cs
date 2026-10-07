namespace LegalAgent.PdfParser;

/// <summary>Optional per-call settings of a conversion.</summary>
public sealed record PdfConversionRequest
{
    /// <summary>Identifier of the source (file name, URL); copied to <see cref="Model.SourceInfo.SourceId"/>, never interpreted.</summary>
    public string? SourceId { get; init; }

    /// <summary>Adjusts a copy of the configured options for this call only.</summary>
    public Action<PdfParserOptions>? ConfigureOptions { get; init; }

    /// <summary>Receives progress per processed page (FR-072).</summary>
    public IProgress<ConversionProgress>? Progress { get; init; }
}
