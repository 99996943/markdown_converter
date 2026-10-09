using LegalAgent.PdfParser;

namespace LegalAgent.Chunking;

/// <summary>Options of the chunker.</summary>
public sealed class ChunkingOptions
{
    /// <summary>Maximum number of characters of a chunk's content, at least 200 (FR-221).</summary>
    public int MaxChunkLength { get; set; } = 2000;

    /// <summary>Rendering options of chunk content; the parser's options when null. Page markers are always off.</summary>
    public RenderingOptions? Rendering { get; set; }

    internal ChunkingOptions Clone() => new()
    {
        MaxChunkLength = MaxChunkLength,
        Rendering = Rendering is null ? null : CopyRendering(Rendering),
    };

    internal static RenderingOptions CopyRendering(RenderingOptions source) => new()
    {
        PageMarkers = source.PageMarkers,
        FootnotesPlacement = source.FootnotesPlacement,
        EmphasisInline = source.EmphasisInline,
    };
}
