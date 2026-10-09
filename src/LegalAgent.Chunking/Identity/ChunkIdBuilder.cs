using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace LegalAgent.Chunking.Identity;

/// <summary>
/// Builds chunk identifiers (research R7): <c>&lt;document id&gt;_&lt;first 16 hex digits of SHA-256(UTF-8 unit key)&gt;_&lt;part&gt;</c>.
/// The identifier depends only on the document, the unit key and the part, so converting the same version again
/// gives the same identifiers; with document ids of at most 100 characters it stays within 120 characters.
/// </summary>
internal static class ChunkIdBuilder
{
    /// <summary>Identifier of part <paramref name="part"/> of the unit <paramref name="unitKey"/> in document <paramref name="documentId"/>.</summary>
    public static string Build(string documentId, string unitKey, int part)
    {
        ArgumentNullException.ThrowIfNull(documentId);
        ArgumentNullException.ThrowIfNull(unitKey);

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(unitKey));
        string hex = Convert.ToHexStringLower(hash, 0, 8);
        return string.Create(CultureInfo.InvariantCulture, $"{documentId}_{hex}_{part}");
    }
}
