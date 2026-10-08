namespace LegalAgent.PdfParser.Model;

/// <summary>Metadata describing the converted PDF.</summary>
/// <param name="SourceId">Caller-supplied identifier (file name, URL); not interpreted.</param>
/// <param name="PageCount">Number of pages in the PDF.</param>
/// <param name="PdfTitle">Title from the PDF /Info dictionary, if any.</param>
/// <param name="ByteLength">Size of the data read.</param>
/// <param name="Sha256">Lowercase hexadecimal SHA-256 of the input.</param>
public sealed record SourceInfo(string? SourceId, int PageCount, string? PdfTitle, long ByteLength, string Sha256);
