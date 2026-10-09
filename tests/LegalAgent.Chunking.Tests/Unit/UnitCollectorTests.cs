using LegalAgent.Chunking.Model;
using LegalAgent.Chunking.Splitting;
using LegalAgent.PdfParser.Model;
using static LegalAgent.Chunking.Tests.Fixtures.Doc;

namespace LegalAgent.Chunking.Tests.Unit;

/// <summary>T012: units of a document (research R2, FR-220).</summary>
public sealed class UnitCollectorTests
{
    [Fact]
    public void Collect_PreambleAndSectionsWithOwnContentInDocumentOrder()
    {
        LegalDocument document = Document(
            "Regulamin",
            [Para("Wstęp.")],
            Section(2, SectionKind.Chapter, "Rozdział 1", "Rozdział 1 Postanowienia ogólne", 1, 2, children: [Paragraph(1, 1, 1, Para("Pierwszy.")), Paragraph(2, 2, 2, Para("Drugi."))]),
            Section(2, SectionKind.Chapter, "Rozdział 2", "Rozdział 2 Opłaty", 3, 3, [Para("Tekst rozdziału.")], children: [Paragraph(3, 3, 3, Para("Trzeci."))]));

        IReadOnlyList<Splitting.Unit> units = UnitCollector.Collect(document);

        Assert.Equal(
            [ChunkUnitKind.Preamble, ChunkUnitKind.Paragraph, ChunkUnitKind.Paragraph, ChunkUnitKind.Chapter, ChunkUnitKind.Paragraph],
            units.Select(u => u.Kind));
        Assert.Null(units[0].Section);
        Assert.Equal([UnitCollector.PreambleSegment], units[0].SegmentPath);
        Assert.Empty(units[0].SectionPath);
        Assert.Equal(["Rozdział 1", "§ 1"], units[1].SegmentPath);
        Assert.Equal(["Rozdział 1 Postanowienia ogólne", "§ 1."], units[1].SectionPath);
        Assert.Equal(["Rozdział 2"], units[3].SegmentPath);
        Assert.Single(units[3].Blocks);
    }

    [Fact]
    public void Collect_EmptyPreambleAndSectionWithoutOwnContentAreNotUnits()
    {
        LegalDocument document = Document(null, null, Section(2, SectionKind.Chapter, "Rozdział 1", "Rozdział 1", 1, 1, children: [Paragraph(1, 1, 1, Para("Pierwszy."))]));

        IReadOnlyList<Splitting.Unit> units = UnitCollector.Collect(document);

        Assert.Equal(ChunkUnitKind.Paragraph, Assert.Single(units).Kind);
    }

    [Fact]
    public void Collect_SectionWithNeitherContentNorSubsectionsIsAHeadingOnlyUnit()
    {
        LegalDocument document = Document(
            null,
            null,
            Section(2, SectionKind.Typographic, null, "USTAWA z dnia 10 maja 2018 r. o ochronie danych osobowych", 1, 1),
            Section(2, SectionKind.Chapter, "Rozdział 1", "Rozdział 1. Przepisy ogólne", 1, 1, children: [Paragraph(1, 1, 1, Para("Pierwszy."))]));

        IReadOnlyList<Splitting.Unit> units = UnitCollector.Collect(document);

        Assert.Equal([ChunkUnitKind.Typographic, ChunkUnitKind.Paragraph], units.Select(u => u.Kind));
        Assert.Empty(units[0].Blocks);
        Assert.Equal(["USTAWA z dnia 10 maja 2018 r. o ochronie danych osobowych"], units[0].SegmentPath);
    }

    [Fact]
    public void Collect_SkippedPagesAreDroppedAndASectionWithOnlySkippedPagesKeepsItsHeading()
    {
        var skipped = new SkippedPageBlock(new PageRange(2, 2), 2, SkipReason.NoTextLayer);
        LegalDocument document = Document(
            null,
            [skipped],
            Paragraph(1, 1, 2, Para("Pierwszy."), skipped),
            Paragraph(2, 2, 2, skipped));

        IReadOnlyList<Splitting.Unit> units = UnitCollector.Collect(document);

        Assert.Equal(2, units.Count);
        Assert.IsType<ParagraphBlock>(Assert.Single(units[0].Blocks));
        Assert.Empty(units[1].Blocks);
        Assert.Equal(["§ 2"], units[1].SegmentPath);
    }

    [Fact]
    public void Collect_SectionWithOnlyFootnotesIsAUnitAndTypographicSectionUsesHeading()
    {
        LegalDocument document = Document(
            null,
            null,
            Section(2, SectionKind.Typographic, null, "I. Opłaty  za rachunek", 1, 1, footnotes: [Footnote(1, "Przypis.")]));

        Splitting.Unit unit = Assert.Single(UnitCollector.Collect(document));

        Assert.Equal(ChunkUnitKind.Typographic, unit.Kind);
        Assert.Equal(["I. Opłaty za rachunek"], unit.SegmentPath);
        Assert.Single(unit.Footnotes);
    }

    [Fact]
    public void Collect_PreambleFootnotesAloneMakeAPreambleUnit()
    {
        LegalDocument document = Document(null, null, Paragraph(1, 1, 1, Para("Pierwszy."))) with { PreambleFootnotes = [Footnote(1, "Przypis tytułu.")] };

        IReadOnlyList<Splitting.Unit> units = UnitCollector.Collect(document);

        Assert.Equal(ChunkUnitKind.Preamble, units[0].Kind);
        Assert.Empty(units[0].Blocks);
        Assert.Single(units[0].Footnotes);
    }
}
