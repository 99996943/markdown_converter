using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Tests.Fixtures;

namespace LegalAgent.PdfParser.Tests.Integration;

/// <summary>
/// Spec 011 — FTN001 warnings of definitions whose reference the parser does not recognise. Replica of an act page
/// without a footnote rule (research.md R1); the geometry follows the act, the text is invented.
/// </summary>
public sealed class FootnoteWarningsIntegrationTests
{
    private const double Left = 57;
    private const double Body = 12;
    private const double Marker = 8;
    private const double Raise = 4.5;
    private const double Note = 10;
    private const double Label = 6.5;
    private const double LabelRaise = 4;

    /// <summary>Body text ending with a raised marker, optionally followed by full-size text glued to it („ogłoszenia³⁾,”).</summary>
    private static SyntheticPdfBuilder MarkedLine(SyntheticPdfBuilder page, double y, string text, string marker, string after = "")
    {
        double end = Left + SyntheticPdfBuilder.TextWidth(text, Body);
        page.Text(Left, y, text, Body).Text(end, y - Raise, marker, Marker);
        return after.Length == 0 ? page : page.Text(end + SyntheticPdfBuilder.TextWidth(marker, Marker), y, after, Body);
    }

    /// <summary>A definition line opening with a raised, smaller label.</summary>
    private static SyntheticPdfBuilder Definition(SyntheticPdfBuilder page, double y, string label, string text) =>
        page.Text(Left, y - LabelRaise, label, Label).Text(Left + SyntheticPdfBuilder.TextWidth(label, Label), y, text, Note);

    [Fact]
    public async Task DefinitionsWithoutRule_WhoseReferenceIsFollowedByPunctuation_AreReportedAsOrphans()
    {
        SyntheticPdfBuilder page = new SyntheticPdfBuilder().Page().Text(Left, 70, "Art. 12.", Body, bold: true);
        double y = 90;
        for (int i = 0; i < 6; i++, y += 15)
        {
            page.Text(Left, y, "Przepisy ustawy stosuje się do pracowników zatrudnionych w urzędach administracji.", Body);
        }

        MarkedLine(page, y, "Termin określa się w drodze ogłoszenia", "2)", ", a jego treść podaje się do wiadomości.");
        MarkedLine(page, y += 15, "Zmiany wprowadza się na zasadach ogólnych", "3)", ", jeżeli ustawa nie stanowi inaczej.");
        MarkedLine(page, y += 15, "Przepis stosuje się odpowiednio do członków korpusu", "8)");
        page.Text(Left, y += 15, "Pozostałe sprawy reguluje statut urzędu.", Body);

        Definition(page, 740, "2)", " Zmiany tekstu jednolitego zostały ogłoszone w dzienniku urzędowym.");
        Definition(page, 755, "3)", " Przepis wprowadzony nowelizacją ustawy.");
        Definition(page, 770, "8)", " Dotyczy członków korpusu służby.");

        using var stream = new MemoryStream(page.Build());
        PdfConversionResult result = await PdfMarkdownConverter.CreateDefault().ConvertAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
        // The Markdown of parser 1.3.0: only the report changes (FR-807). „2)” and „3)” stay glued to their words.
        string paragraph = string.Concat(Enumerable.Repeat("Przepisy ustawy stosuje się do pracowników zatrudnionych w urzędach administracji. ", 6));
        Assert.Equal(
            "<!-- page: 1 -->\n## Art. 12.\n\n" + paragraph
            + "Termin określa się w drodze ogłoszenia2), a jego treść podaje się do wiadomości. "
            + "Zmiany wprowadza się na zasadach ogólnych3), jeżeli ustawa nie stanowi inaczej. "
            + "Przepis stosuje się odpowiednio do członków korpusu[^1] Pozostałe sprawy reguluje statut urzędu.\n\n"
            + "[^1]: Dotyczy członków korpusu służby.\n\n"
            + "[^2]: Zmiany tekstu jednolitego zostały ogłoszone w dzienniku urzędowym.\n\n"
            + "[^3]: Przepis wprowadzony nowelizacją ustawy.\n",
            result.Markdown.Replace("\r\n", "\n", StringComparison.Ordinal));

        ConversionWarning[] orphans = [.. result.Report.Warnings.Where(w => w.Code == "FTN001_OrphanFootnote")];
        Assert.Equal(
            ["Przypis „2)” na stronie 1 nie ma odnośnika w tekście.", "Przypis „3)” na stronie 1 nie ma odnośnika w tekście."],
            orphans.Select(w => w.Message));
    }
}
