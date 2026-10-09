using LegalAgent.Chunking.Model;

namespace LegalAgent.Chunking.Serialization;

/// <summary>A record of the chunk JSON contract: the document data and one chunk.</summary>
/// <param name="Document">Document-level data.</param>
/// <param name="Chunk">The chunk.</param>
public sealed record ChunkRecord(ChunkedDocumentHeader Document, Chunk Chunk);

/// <summary>
/// JSON form of chunked documents (spec 004, contracts/chunks-json.md): one self-contained record per chunk, written as
/// JSON Lines with a fixed field order, UTF-8 characters written as is, null fields left out and LF line endings.
/// </summary>
public static class ChunkJson
{
    /// <summary>Version of the record schema written and the highest one read.</summary>
    public const int SchemaVersion = 1;

    /// <summary>One line per chunk, each ending with LF; empty for a document without chunks.</summary>
    public static string ToJsonLines(ChunkedDocument document) => string.Empty;

    /// <summary>Reads JSON Lines written by <see cref="ToJsonLines"/>; blank lines are skipped.</summary>
    /// <exception cref="FormatException">A line is not a valid record; the message gives its number.</exception>
    public static IReadOnlyList<ChunkRecord> ReadLines(string jsonLines) => [];
}
