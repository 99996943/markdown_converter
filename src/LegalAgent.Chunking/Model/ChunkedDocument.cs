namespace LegalAgent.Chunking.Model;

/// <summary>Document-level data of a chunked document (the <c>document</c> part of a JSON record).</summary>
/// <param name="Metadata">Metadata supplied by the caller.</param>
/// <param name="Title">The title from the metadata, otherwise the title detected by the parser.</param>
/// <param name="DetectedTitle">Title detected by the parser.</param>
/// <param name="SeriesKey">Designation shared by all versions: <see cref="DocumentMetadata.Designation"/> or the document id.</param>
/// <param name="Source">Source data.</param>
public sealed record ChunkedDocumentHeader(
    DocumentMetadata Metadata,
    string? Title,
    string? DetectedTitle,
    string SeriesKey,
    ChunkSource Source);

/// <summary>A document split into chunks.</summary>
/// <param name="Header">Document-level data.</param>
/// <param name="Chunks">Chunks in document order.</param>
public sealed record ChunkedDocument(ChunkedDocumentHeader Header, IReadOnlyList<Chunk> Chunks);
