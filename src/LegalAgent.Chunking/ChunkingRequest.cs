using LegalAgent.PdfParser;

namespace LegalAgent.Chunking;

/// <summary>Optional per-call settings of chunking.</summary>
public sealed record ChunkingRequest
{
    /// <summary>Adjusts a copy of the configured options for this call only.</summary>
    public Action<ChunkingOptions>? ConfigureOptions { get; init; }

    /// <summary>Parser settings used when the input is a PDF stream.</summary>
    public PdfConversionRequest? ParserRequest { get; init; }
}
