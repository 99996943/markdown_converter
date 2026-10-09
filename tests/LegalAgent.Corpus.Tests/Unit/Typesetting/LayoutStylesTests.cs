using LegalAgent.Corpus.Composition;
using LegalAgent.Corpus.Typesetting;
using UglyToad.PdfPig.Content;
using static LegalAgent.Corpus.Tests.Unit.Typesetting.TypesetFixtures;

namespace LegalAgent.Corpus.Tests.Unit.Typesetting;

public sealed class LayoutStylesTests
{
    public static TheoryData<string> Ids => new(LayoutStyles.Ids);

    private static double Baseline(Word w) => 842 - w.Letters[0].StartBaseLine.Y;

    private static ComposedDocument Sample(string layout, bool recordCard = false)
    {
        var elements = new List<Element> { new HeadingElement(2, "Rozdział 1", T("Postanowienia ogólne")) };
        elements.AddRange(Paragraphs(16));
        FrontMatter front = Front(cover: !recordCard) with
        {
            RecordCard = recordCard,
            Owner = "Departament Operacji",
            ApprovedBy = "Zarząd Banku Przykładowego S.A.",
            ApprovalDate = "15 grudnia 2026 r.",
            History = [new HistoryEntry("1", "15 grudnia 2026 r.", "Wydanie pierwsze.")],
        };
        return new ComposedDocument("DOC-01", layout, front, elements, new Dictionary<int, IReadOnlyList<Inline>>());
    }

    [Fact]
    public void SixStylesExist()
    {
        Assert.Equal(["jedna-kolumna", "dwie-kolumny", "tabela-dokument", "taryfa-siatka", "taryfa-bez-siatki", "procedura"], LayoutStyles.Ids);
        Assert.Throws<ArgumentException>(() => LayoutStyles.Get("nieznany"));
    }

    [Theory]
    [MemberData(nameof(Ids))]
    public void EveryStyle_HasFrontMatterRunningHeaderFooterAndPageNumbers(string id)
    {
        LayoutStyle style = LayoutStyles.Get(id);
        Assert.Equal(id, style.Id);
        TypesetResult result = Typesetter.Typeset(Sample(id, recordCard: id == "procedura"), style);

        List<List<Word>> pages = PageWords(result.Pdf);
        string first = string.Join(' ', pages[0].Select(w => w.Text));
        Assert.Contains("Regulamin rachunku testowego", first, StringComparison.Ordinal);
        Assert.Contains("BP/REG/01", first, StringComparison.Ordinal);
        Assert.Contains("Bank Przykładowy S.A.", string.Join(' ', pages.SelectMany(p => p).Select(w => w.Text)), StringComparison.Ordinal);

        // Page 2 onwards: running header at the top and a page number at the bottom.
        string footer = string.Join(' ', pages[1].Where(w => Baseline(w) >= style.FooterBaseline - 1).Select(w => w.Text));
        Assert.Contains("2", footer, StringComparison.Ordinal);
        Assert.Contains(pages[1], w => Baseline(w) <= style.HeaderBaseline + 1);
        Assert.Contains(result.Truth.Artifacts, a => a.Contains(result.PageCount.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal));
    }

    [Fact]
    public void ProcedureRecordCard_ListsTheFieldsAndTheChangeHistory()
    {
        TypesetResult result = Typesetter.Typeset(Sample("procedura", recordCard: true), LayoutStyles.Get("procedura"));

        string page1 = string.Join(' ', PageWords(result.Pdf)[0].Select(w => w.Text));
        foreach (string text in new[] { "Oznaczenie", "Wersja", "Właściciel", "Departament Operacji", "Zatwierdził", "Data zatwierdzenia", "15 grudnia 2026 r.", "Obowiązuje od", "Historia zmian", "Wydanie pierwsze." })
        {
            Assert.Contains(text, page1, StringComparison.Ordinal);
        }

        Assert.Equal(2, result.Truth.Tables.Count);
    }

    [Fact]
    public void StylesDifferInMarginsOrSizes()
    {
        var signatures = LayoutStyles.Ids.Select(LayoutStyles.Get).Select(s => (s.Left, s.Right, s.BodySize, s.Columns, s.TableDocument, s.TableGrid)).ToList();
        Assert.Equal(signatures.Count, signatures.Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(Ids))]
    public void SameDocumentSameStyle_IdenticalBytes(string id)
    {
        ComposedDocument doc = Sample(id, recordCard: id == "procedura");
        Assert.Equal(Typesetter.Typeset(doc, LayoutStyles.Get(id)).Pdf, Typesetter.Typeset(doc, LayoutStyles.Get(id)).Pdf);
    }
}
