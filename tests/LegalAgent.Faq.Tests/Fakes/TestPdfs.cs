using System.Text;
using LegalAgent.Corpus.Pdf;

namespace LegalAgent.Faq.Tests.Fakes;

/// <summary>Synthetic PDFs for conversion and end-to-end tests.</summary>
internal static class TestPdfs
{
    /// <summary>A regulation with „Rozdział 1.”, „§ 1.”, „§ 2.” headings and body text; <paramref name="seed"/> varies the text.</summary>
    public static byte[] Regulation(string seed, string title = "Regulamin rachunku") =>
        RegulationBuilder(seed, title).Build();

    /// <summary>The regulation of <see cref="Regulation"/> with an image, which the parser reports as IMG001_ImagesIgnored.</summary>
    public static byte[] RegulationWithWarning(string seed, string title = "Regulamin rachunku") =>
        RegulationBuilder(seed, title).Image(72, 400, 100, 50).Build();

    /// <summary>The PDF followed by a comment that pads it to <paramref name="size"/> bytes (the parser ignores it).</summary>
    public static byte[] Padded(byte[] pdf, int size)
    {
        if (size <= pdf.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "The padded size must exceed the PDF size.");
        }

        byte[] padded = new byte[size];
        pdf.CopyTo(padded, 0);
        Array.Fill(padded, (byte)'x', pdf.Length, size - pdf.Length);
        padded[pdf.Length] = (byte)'%';
        return padded;
    }

    /// <summary>A PDF with one page and no text.</summary>
    public static byte[] NoText() => new SyntheticPdfBuilder().Page().FilledRect(72, 100, 100, 50).Build();

    /// <summary>Data that starts with the PDF signature but has no readable structure.</summary>
    public static byte[] Corrupted() => Encoding.ASCII.GetBytes("%PDF-1.7\n% uszkodzony\n1 0 obj << >> endobj\ntrailer << >>\n%%EOF\n");

    private static SyntheticPdfBuilder RegulationBuilder(string seed, string title) =>
        new SyntheticPdfBuilder()
            .Title(title)
            .Page()
            .Text(200, 80, title, 14, bold: true)
            .Text(72, 130, "Rozdział 1. Postanowienia ogólne", 12, bold: true)
            .Text(72, 160, "§ 1.", 11, bold: true)
            .Text(72, 180, $"Regulamin określa zasady prowadzenia rachunku {seed}.")
            .Text(72, 210, "§ 2.", 11, bold: true)
            .Text(72, 230, $"Bank pobiera opłaty zgodnie z taryfą {seed}.");
}
