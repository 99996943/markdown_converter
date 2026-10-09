namespace LegalAgent.Chunking.Identity;

/// <summary>
/// Builds chunk identifiers (research R7): <c>&lt;document id&gt;_&lt;first 16 hex digits of SHA-256(UTF-8 unit key)&gt;_&lt;part&gt;</c>.
/// </summary>
internal static class ChunkIdBuilder
{
    /// <summary>Identifier of part <paramref name="part"/> of the unit <paramref name="unitKey"/> in document <paramref name="documentId"/>.</summary>
    public static string Build(string documentId, string unitKey, int part) => string.Empty;
}
