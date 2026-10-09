namespace LegalAgent.Corpus.Pdf;

/// <summary>Makes the trailer /ID of a PDF deterministic (research R4).</summary>
internal static class PdfIdNormalizer
{
    /// <summary>
    /// Replaces both hex strings of the trailer <c>/ID</c> array in place (same length, so xref offsets stay valid)
    /// with a SHA-256 digest of the file computed with the ID digits zeroed. Returns the input unchanged when no /ID exists.
    /// </summary>
    public static byte[] Normalize(byte[] pdf) => throw new NotImplementedException();
}
