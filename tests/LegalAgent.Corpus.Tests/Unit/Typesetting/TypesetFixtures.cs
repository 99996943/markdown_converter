using LegalAgent.Corpus.Composition;
using LegalAgent.Corpus.Typesetting;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace LegalAgent.Corpus.Tests.Unit.Typesetting;

/// <summary>Small builders of composed documents and PdfPig helpers for typesetting tests.</summary>
internal static class TypesetFixtures
{
    public const string LongSentence =
        "Bank prowadzi rachunek zgodnie z przepisami prawa oraz postanowieniami umowy, a Klient korzysta z rachunku w sposób zgodny z jego przeznaczeniem i z zachowaniem zasad bezpieczeństwa.";

    public static LayoutStyle Style(Func<LayoutStyle, LayoutStyle>? change = null)
    {
        var style = new LayoutStyle { Id = "jedna-kolumna" };
        return change is null ? style : change(style);
    }

    public static IReadOnlyList<Inline> T(string text) => [new Inline(text)];

    public static FrontMatter Front(bool cover = false) =>
        new("Bank Przykładowy S.A.", "Regulamin rachunku testowego", "BP/REG/01", 1, "1 stycznia 2027 r.", null) { Cover = cover };

    public static ComposedDocument Doc(IReadOnlyList<Element> elements, IReadOnlyDictionary<int, IReadOnlyList<Inline>>? footnotes = null, bool cover = false, string layout = "jedna-kolumna") =>
        new("REG-01", layout, Front(cover), elements, footnotes ?? new Dictionary<int, IReadOnlyList<Inline>>());

    public static ParagraphElement P(string text, string id = "") => new(T(text)) { Id = id };

    public static IReadOnlyList<Element> Paragraphs(int count) =>
        Enumerable.Range(1, count).Select(i => (Element)P($"Akapit {i}. {LongSentence} {LongSentence}", "p" + i)).ToList();

    /// <summary>Words of every page as PdfPig extracts them, in content-stream order.</summary>
    public static List<List<Word>> PageWords(byte[] pdf)
    {
        using PdfDocument doc = PdfDocument.Open(pdf);
        return doc.GetPages().Select(p => p.GetWords().ToList()).ToList();
    }

    /// <summary>All letters of every page.</summary>
    public static List<List<Letter>> PageLetters(byte[] pdf)
    {
        using PdfDocument doc = PdfDocument.Open(pdf);
        return doc.GetPages().Select(p => p.Letters.ToList()).ToList();
    }

    /// <summary>Distance from the top edge of the page to a word's baseline.</summary>
    public static double Top(Word w, double pageHeight = 842) => pageHeight - w.BoundingBox.Bottom;
}
