namespace LegalAgent.Chunking.Model;

/// <summary>Source data of a chunked document, taken from the parser result.</summary>
/// <param name="PageCount">Number of pages of the PDF.</param>
/// <param name="Sha256">SHA-256 of the PDF bytes (lower-case hex).</param>
/// <param name="IsComplete">False when the parser skipped pages.</param>
/// <param name="SkippedPages">Pages the parser did not convert, ascending.</param>
public sealed record ChunkSource(int PageCount, string Sha256, bool IsComplete, IReadOnlyList<int> SkippedPages);
