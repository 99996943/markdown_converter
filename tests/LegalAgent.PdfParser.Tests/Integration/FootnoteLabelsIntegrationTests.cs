using System.Text.RegularExpressions;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Tests.Fixtures;

namespace LegalAgent.PdfParser.Tests.Integration;

/// <summary>
/// Spec 012 — footnote identifiers in the Markdown are the labels printed in the PDF (FR-900–FR-904). Replicas of a
/// bank regulation page (a ruled table with references „TAK²¹”…„TAK²⁴”, definitions under a short rule) and of an act
/// with two series of „1)” labels; the geometry follows spec 010 and spec 011 replicas, the text is invented.
/// </summary>
public sealed partial class FootnoteLabelsIntegrationTests
{
    private const double Left = 54;
    private const double Body = 8;
    private const double Marker = 5;
    private const double Raise = 3;

    private const double ActLeft = 57;
    private const double ActBody = 12;
    private const double ActMarker = 8;
    private const double ActRaise = 4.5;
    private const double Note = 10;
    private const double Label = 6.5;
    private const double LabelRaise = 4;

    private static async Task<PdfConversionResult> ConvertAsync(byte[] pdf)
    {
        using var stream = new MemoryStream(pdf);
        return await PdfMarkdownConverter.CreateDefault().ConvertAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
    }

    private static string Markdown(PdfConversionResult result) => result.Markdown.Replace("\r\n", "\n", StringComparison.Ordinal);

    /// <summary>Distinct footnote identifiers of the references (without the colon of a definition).</summary>
    private static string[] References(string markdown) =>
        [.. Reference().Matches(markdown).Select(m => m.Groups[1].Value).Distinct()];

    /// <summary>Footnote identifiers of the definitions, in document order.</summary>
    private static string[] Definitions(string markdown) =>
        [.. Definition().Matches(markdown).Select(m => m.Groups[1].Value)];

    private static int Warnings(PdfConversionResult result, string code) => result.Report.Warnings.Count(w => w.Code == code);

    private static SyntheticPdfBuilder TrailingMarker(SyntheticPdfBuilder page, double x, double y, string text, string marker) =>
        page.Text(x, y, text, Body).Text(x + SyntheticPdfBuilder.TextWidth(text, Body), y - Raise, marker, Marker);

    private static SyntheticPdfBuilder Centred(SyntheticPdfBuilder page, double left, double right, double y, string text, bool bold = false) =>
        page.Text(((left + right) / 2) - (SyntheticPdfBuilder.TextWidth(text, Body, bold) / 2), y, text, Body, bold);

    private static SyntheticPdfBuilder Grid(SyntheticPdfBuilder page, double[] xs, double[] borders)
    {
        foreach (double border in borders)
        {
            for (int c = 0; c + 1 < xs.Length; c++)
            {
                page.HLine(xs[c] + 0.2, xs[c + 1] - 0.2, border);
            }
        }

        foreach (double x in xs)
        {
            page.VLine(x, borders[0], borders[^1]);
        }

        return page;
    }

    /// <summary>
    /// A regulation page: a paragraph with the reference „⁷”, a ruled table whose second column holds „TAK²¹”…„TAK²⁴”,
    /// and the definitions 7, 21–24 set small under a short rule (as „TAK/TAK⁵” in spec 010 T2).
    /// </summary>
    private static byte[] RegulationWithTable()
    {
        SyntheticPdfBuilder page = new SyntheticPdfBuilder().Page().Text(Left, 70, "Regulamin kredytu", 14, bold: true);
        for (int i = 0; i < 6; i++)
        {
            page.Text(Left, 100 + (i * 10), "Postanowienia regulaminu stosuje się do kredytów udzielanych przez bank klientom.", Body);
        }

        TrailingMarker(page, Left, 170, "Kredyt spłacasz w ratach miesięcznych w terminach podanych w umowie", "7");
        page.Text(Left, 180, "Sprawdź, co możesz zrobić, gdy korzystasz z różnych sposobów dostępu.", Body);

        double[] xs = [59.5, 300, 555.6];
        double[] borders = [200, 225.2, 250.4, 275.6, 300.8, 326];
        Grid(page, xs, borders);
        Centred(page, xs[0], xs[1], 215, "co chcesz zrobić?", bold: true);
        Centred(page, xs[1], xs[2], 215, "serwis transakcyjny", bold: true);
        string[] names = ["złożyć wniosek o kredyt", "zmienić termin spłaty raty", "spłacić kredyt przed terminem", "wypowiedzieć umowę"];
        for (int r = 0; r < names.Length; r++)
        {
            double baseline = borders[r + 1] + 15;
            page.Text(65.3, baseline, names[r], Body);
            double start = ((xs[1] + xs[2]) / 2) - (SyntheticPdfBuilder.TextWidth("TAK", Body) / 2);
            TrailingMarker(page, start, baseline, "TAK", (21 + r).ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        page.HLine(39.7, 183.7, 744);
        page.Text(40, 752, "7 Harmonogram spłaty doręczamy razem z umową.", 6);
        page.Text(40, 759.3, "21 O dacie wprowadzenia tej funkcjonalności poinformujemy na naszej stronie internetowej.", 6);
        page.Text(40, 766.6, "22 Dotyczy kredytów w złotych.", 6);
        page.Text(40, 773.9, "23 Bez opłaty za wcześniejszą spłatę.", 6);
        page.Text(40, 781.2, "24 Wypowiedzenie składasz na piśmie.", 6);
        return page.Build();
    }

    /// <summary>Act body text ending with a raised marker („korpusu¹⁾”).</summary>
    private static SyntheticPdfBuilder MarkedLine(SyntheticPdfBuilder page, double y, string text, string marker)
    {
        double end = ActLeft + SyntheticPdfBuilder.TextWidth(text, ActBody);
        return page.Text(ActLeft, y, text, ActBody).Text(end, y - ActRaise, marker, ActMarker);
    }

    /// <summary>An act definition line opening with a raised, smaller label.</summary>
    private static SyntheticPdfBuilder ActDefinition(SyntheticPdfBuilder page, double y, string label, string text) =>
        page.Text(ActLeft, y - LabelRaise, label, Label).Text(ActLeft + SyntheticPdfBuilder.TextWidth(label, Label), y, text, Note);

    /// <summary>One act page: an article, body text, references to <paramref name="labels"/> and their definitions.</summary>
    private static SyntheticPdfBuilder ActPage(SyntheticPdfBuilder page, string article, string[] labels, string topic)
    {
        page.Text(ActLeft, 70, article, ActBody, bold: true);
        double y = 90;
        for (int i = 0; i < 6; i++, y += 15)
        {
            page.Text(ActLeft, y, "Przepisy ustawy stosuje się do pracowników zatrudnionych w urzędach administracji.", ActBody);
        }

        for (int i = 0; i < labels.Length; i++, y += 15)
        {
            MarkedLine(page, y, $"Przepis {i + 1} dotyczy spraw {topic}", labels[i]);
        }

        page.Text(ActLeft, y, "Pozostałe sprawy reguluje statut urzędu.", ActBody);
        for (int i = 0; i < labels.Length; i++)
        {
            ActDefinition(page, 740 + (i * 15), labels[i], $" Przypis {i + 1} do spraw {topic}.");
        }

        return page;
    }

    [Fact]
    public async Task RegulationTableReferences_KeepTheLabelsOfThePdf()
    {
        PdfConversionResult result = await ConvertAsync(RegulationWithTable());
        string markdown = Markdown(result);

        Assert.Contains("ratach miesięcznych w terminach podanych w umowie[^7]", markdown, StringComparison.Ordinal);
        Assert.Contains("| złożyć wniosek o kredyt | TAK[^21] |", markdown, StringComparison.Ordinal);
        Assert.Contains("| zmienić termin spłaty raty | TAK[^22] |", markdown, StringComparison.Ordinal);
        Assert.Contains("| spłacić kredyt przed terminem | TAK[^23] |", markdown, StringComparison.Ordinal);
        Assert.Contains("| wypowiedzieć umowę | TAK[^24] |", markdown, StringComparison.Ordinal);
        Assert.Contains("[^21]: O dacie wprowadzenia tej funkcjonalności poinformujemy na naszej stronie internetowej.", markdown, StringComparison.Ordinal);
        Assert.Equal(["7", "21", "22", "23", "24"], References(markdown));
        Assert.Equal(["7", "21", "22", "23", "24"], Definitions(markdown));
        Assert.DoesNotContain("[^1]", markdown, StringComparison.Ordinal);
        Assert.Equal(0, Warnings(result, "FTN001_OrphanFootnote"));
        Assert.Equal(0, Warnings(result, "FTN002_FootnoteWithoutLabel"));
    }

    [Fact]
    public async Task ActWithTwoSeriesOfLabels_SecondSeriesGetsTheOccurrenceSuffix()
    {
        SyntheticPdfBuilder pdf = ActPage(new SyntheticPdfBuilder().Page(), "Art. 1.", ["1)", "2)"], "kadrowych");
        ActPage(pdf.Page(), "Art. 2.", ["1)", "2)"], "płacowych");
        PdfConversionResult result = await ConvertAsync(pdf.Build());
        string markdown = Markdown(result);

        Assert.Contains("Przepis 1 dotyczy spraw kadrowych[^1)]", markdown, StringComparison.Ordinal);
        Assert.Contains("Przepis 2 dotyczy spraw kadrowych[^2)]", markdown, StringComparison.Ordinal);
        Assert.Contains("Przepis 1 dotyczy spraw płacowych[^1)-2]", markdown, StringComparison.Ordinal);
        Assert.Contains("Przepis 2 dotyczy spraw płacowych[^2)-2]", markdown, StringComparison.Ordinal);
        Assert.Contains("[^1)]: Przypis 1 do spraw kadrowych.", markdown, StringComparison.Ordinal);
        Assert.Contains("[^1)-2]: Przypis 1 do spraw płacowych.", markdown, StringComparison.Ordinal);
        Assert.Equal(["1)", "2)", "1)-2", "2)-2"], Definitions(markdown));
        Assert.Equal(0, Warnings(result, "FTN001_OrphanFootnote"));
        Assert.Equal(0, Warnings(result, "FTN002_FootnoteWithoutLabel"));
    }

    [Fact]
    public async Task StarLabel_IsTheIdentifier()
    {
        PdfConversionResult result = await ConvertAsync(ActPage(new SyntheticPdfBuilder().Page(), "Art. 1.", ["*)"], "kadrowych").Build());
        string markdown = Markdown(result);

        Assert.Contains("Przepis 1 dotyczy spraw kadrowych[^*)]", markdown, StringComparison.Ordinal);
        Assert.Contains("[^*)]: Przypis 1 do spraw kadrowych.", markdown, StringComparison.Ordinal);
        Assert.Equal(0, Warnings(result, "FTN001_OrphanFootnote"));
        Assert.Equal(0, Warnings(result, "FTN002_FootnoteWithoutLabel"));
    }

    [GeneratedRegex(@"\[\^([^\]\s]+)\](?!:)", RegexOptions.CultureInvariant)]
    private static partial Regex Reference();

    [GeneratedRegex(@"^\[\^([^\]\s]+)\]:", RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex Definition();
}
