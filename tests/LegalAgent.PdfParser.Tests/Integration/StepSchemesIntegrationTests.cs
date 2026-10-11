using System.Text.RegularExpressions;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Tests.Fixtures;

namespace LegalAgent.PdfParser.Tests.Integration;

/// <summary>
/// T105 — FR-067, US4 scenario 6: a step scheme laid out like the mBank terms (gray boxes without borders, step names
/// vertically centred between the explanation lines, arrows between the boxes, a bulleted explanation) continued on
/// the next page under a repeated column-name row, in an empty box.
/// </summary>
public sealed partial class StepSchemesIntegrationTests
{
    private const double Name = 70;
    private const double Text = 212;
    private const double Size = 10;
    private const byte Gray = 217;

    private static readonly string[] OrderedFragments =
        ["Zawarcie umowy", "**Składasz wniosek**", "**Potwierdzamy Twoją tożsamość**", "**Sprawdzamy warunek zawarcia umowy**", "**Odbierasz kartę**", "Więcej informacji"];

    internal static byte[] BuildSchemePdf()
    {
        var builder = new SyntheticPdfBuilder().PageNumberFooter("{n}/2");

        builder.Page()
            .Text(72, 80, "Regulamin rachunku", 14, bold: true)
            .Text(72, 115, "Zawarcie umowy wygląda tak:")
            .Text(Name, 140, "Kolejność działań", Size, bold: true).Text(Text, 140, "Wyjaśnienie", Size, bold: true)
            .FilledRect(64, 146, 142, 18, Gray)
            .Text(Name, 159, "Składasz wniosek", Size, bold: true)
            .Text(Text, 159, "Składasz pisemny wniosek o otwarcie rachunku.", Size)
            .Image(127, 168, 13, 13)
            .FilledRect(64, 188, 142, 48, Gray)
            .Text(Text, 202, "Zanim zawrzemy umowę, potwierdzimy Twoją", Size)
            .Text(Name, 209.5, "Potwierdzamy Twoją", Size, bold: true)
            .Text(Text, 217, "tożsamość w sposób opisany w regulaminie", Size)
            .Text(Name, 224.5, "tożsamość", Size, bold: true)
            .Text(Text, 232, "obsługi klientów.", Size)
            .Image(127, 240, 13, 13)
            .FilledRect(64, 258, 142, 82, Gray)
            .Text(Text, 272, "• Złożenie wniosku nie oznacza zawarcia umowy.", Size)
            .Text(Text, 287, "• Sprawdzimy, czy Twój numer PESEL nie jest", Size)
            .Text(Name, 294.5, "Sprawdzamy warunek", Size, bold: true)
            .Text(Text + 10, 302, "zastrzeżony.", Size)
            .Text(Name, 309.5, "zawarcia umowy", Size, bold: true)
            .Text(Text, 317, "• Umowę zawieramy pisemnie,", Size);

        builder.Page()
            .Text(Name, 70, "Kolejność działań", Size, bold: true).Text(Text, 70, "Wyjaśnienie", Size, bold: true)
            .FilledRect(64, 76, 142, 34, Gray)
            .Text(Text + 10, 90, "a kopię otrzymasz e-mailem.", Size)
            .Text(Text, 105, "• Po zawarciu umowy otworzymy rachunek.", Size)
            .Image(127, 114, 13, 13)
            .FilledRect(64, 132, 142, 18, Gray)
            .Text(Name, 145, "Odbierasz kartę", Size, bold: true)
            .Text(Text, 145, "Kartę wyślemy pocztą.", Size)
            .Text(72, 190, "Więcej informacji znajdziesz na stronie internetowej.", Size);

        // Ordinary running text, as in a real document: it sets the typical leading (the interleaved scheme lines alone
        // would make it half a line).
        for (int i = 0; i < 14; i++)
        {
            builder.Text(72, 220 + (15 * i), $"Wiersz {i + 1} zwykłego tekstu regulaminu o stałej długości.", Size);
        }

        return builder.Build();
    }

    private static async Task<PdfConversionResult> ConvertAsync(Action<PdfParserOptions>? configure = null)
    {
        using var stream = new MemoryStream(BuildSchemePdf());
        var request = new PdfConversionRequest { ConfigureOptions = configure };
        return await PdfMarkdownConverter.CreateDefault().ConvertAsync(stream, request, TestContext.Current.CancellationToken);
    }

    [GeneratedRegex(@"- Umowę zawieramy pisemnie,\s+(<!-- page: 2 -->\s+)?a kopię otrzymasz e-mailem\.")]
    private static partial Regex ItemAcrossPages();

    [Fact]
    public async Task Scheme_IsRenderedAsASequenceOfSteps()
    {
        string md = (await ConvertAsync()).Markdown;

        Assert.Contains("**Składasz wniosek**\n\nSkładasz pisemny wniosek o otwarcie rachunku.\n", md, StringComparison.Ordinal);
        Assert.Contains(
            "**Potwierdzamy Twoją tożsamość**\n\nZanim zawrzemy umowę, potwierdzimy Twoją tożsamość w sposób opisany w regulaminie obsługi klientów.\n",
            md,
            StringComparison.Ordinal);
        Assert.Contains(
            "**Sprawdzamy warunek zawarcia umowy**\n\n- Złożenie wniosku nie oznacza zawarcia umowy.\n- Sprawdzimy, czy Twój numer PESEL nie jest zastrzeżony.\n",
            md,
            StringComparison.Ordinal);
        Assert.Matches(ItemAcrossPages(), md);
        Assert.Contains("- Po zawarciu umowy otworzymy rachunek.", md, StringComparison.Ordinal);
        Assert.Contains("**Odbierasz kartę**\n\nKartę wyślemy pocztą.\n", md, StringComparison.Ordinal);

        int[] order = OrderedFragments
            .Select(s => md.IndexOf(s, StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(order.Order(), order);
        Assert.DoesNotContain(order, i => i < 0);
    }

    [Fact]
    public async Task Scheme_HasNoColumnNameRowNoHeadingsAndNoTable()
    {
        PdfConversionResult result = await ConvertAsync();
        string md = result.Markdown;

        Assert.DoesNotContain("Kolejność działań", md, StringComparison.Ordinal);
        Assert.DoesNotContain("Wyjaśnienie", md, StringComparison.Ordinal);
        Assert.Equal(["# Regulamin rachunku"], md.Split('\n').Where(l => l.StartsWith('#')));
        Assert.DoesNotContain("|", md, StringComparison.Ordinal);
        Assert.Equal(0, result.Report.TableCount);
        Assert.DoesNotContain("Krok", md, StringComparison.Ordinal);
    }

    private const double FrameLeft = 59.5;
    private const double FrameWidth = 141.8;
    private const double Phrase = 64.9;
    private const double Content = 206.7;
    private const double Small = 8;

    /// <summary>
    /// T032 — spec 008 US4 (FR-630…FR-635): question–answer frames as printed in the firm terms. Three gray boxes without
    /// a column-name row, joined by arrows set as a symbol-font glyph (a text line without letters or digits); bold 8 pt
    /// phrases vertically centred, the right column a numbered list from x 206.7 with leading 10. The first phrase sits
    /// 5 pt off the baselines of the list (woven between items), the second shares their baselines (merged lines), and
    /// the second box ends above item „3.”, which runs on below it beside the next arrow.
    /// </summary>
    internal static byte[] BuildFramesPdf()
    {
        var builder = new SyntheticPdfBuilder();
        builder.Page()
            .Text(72, 80, "Regulamin rachunku dla firm", 14, bold: true)
            .Text(54, 100, "2) Jeśli otwarcie rachunku wymaga umowy, zrobimy to tak:", Small)
            .FilledRect(FrameLeft, 115, FrameWidth, 40, Gray)
            .Text(Content, 127, "1. przez internet, np. przez naszą stronę internetową,", Small)
            .Text(Content, 137, "2. w biurze obsługi klienta,", Small)
            .Text(Phrase, 142, "Składasz wniosek", Small, bold: true)
            .Text(Content, 147, "3. osobiście, np. w jednej z placówek.", Small)
            .Text(122, 172, "▼", 18, mono: true)
            .FilledRect(FrameLeft, 178, FrameWidth, 27, Gray)
            .Text(Phrase, 190, "Sprawdzamy dokumenty", Small, bold: true)
            .Text(Content, 190, "1. Zanim zawrzemy umowę, sprawdzimy Twoje dokumenty.", Small)
            .Text(Phrase, 200, "rejestrowe", Small, bold: true)
            .Text(Content, 200, "2. Weryfikujemy wpis do rejestru przedsiębiorców.", Small)
            .Text(Content, 210, "3. Możemy poprosić o dokument tożsamości oraz", Small)
            .Text(Content + 15, 220, "dokumenty potwierdzające dane z wniosku.", Small)
            .Text(122, 226, "▼", 18, mono: true)
            .FilledRect(FrameLeft, 232, FrameWidth, 20, Gray)
            .Text(Phrase, 245, "Potwierdzamy otwarcie", Small, bold: true)
            .Text(Content, 245, "Potwierdzenie otwarcia rachunku przekażemy Ci elektronicznie.", Small)
            .Text(54, 275, "3) Po zawarciu umowy otworzymy dla Ciebie rachunek.", Small);

        // Ordinary running text sets the typical leading, as in a real document.
        for (int i = 0; i < 14; i++)
        {
            builder.Text(54, 300 + (10 * i), $"Wiersz {i + 1} zwykłego tekstu regulaminu dla firm o stałej długości.", Small);
        }

        return builder.Build();
    }

    /// <summary>
    /// T032 control — a real data table of the same document: rules on every column, white cell fills, a bold header row
    /// and a plain first column. It stays a GFM table.
    /// </summary>
    internal static byte[] BuildDataTablePdf()
    {
        var builder = new SyntheticPdfBuilder();
        builder.Page()
            .Text(72, 80, "Regulamin rachunku dla firm", 14, bold: true)
            .Text(54, 100, "Opłaty za rachunek znajdziesz w tabeli:", Small);
        double[] rows = [110, 125, 140, 155];
        string[][] cells =
        [
            ["Rodzaj czynności", "Opłata"],
            ["Prowadzenie rachunku", "10 zł miesięcznie"],
            ["Przelew w placówce", "5 zł za przelew"],
        ];
        for (int r = 0; r < cells.Length; r++)
        {
            builder.FilledRect(FrameLeft, rows[r], FrameWidth, 15, 255).FilledRect(FrameLeft + FrameWidth, rows[r], 300, 15, 255);
            builder.Text(Phrase, rows[r] + 10.5, cells[r][0], Small, bold: r == 0).Text(Content, rows[r] + 10.5, cells[r][1], Small, bold: r == 0);
        }

        foreach (double y in rows)
        {
            builder.HLine(FrameLeft, FrameLeft + FrameWidth + 300, y);
        }

        foreach (double x in new[] { FrameLeft, FrameLeft + FrameWidth, FrameLeft + FrameWidth + 300 })
        {
            builder.VLine(x, rows[0], rows[^1]);
        }

        builder.Text(54, 175, "Opłaty pobieramy z rachunku.", Small);
        for (int i = 0; i < 14; i++)
        {
            builder.Text(54, 200 + (10 * i), $"Wiersz {i + 1} zwykłego tekstu regulaminu dla firm o stałej długości.", Small);
        }

        return builder.Build();
    }

    private static async Task<PdfConversionResult> ConvertAsync(byte[] pdf)
    {
        using var stream = new MemoryStream(pdf);
        return await PdfMarkdownConverter.CreateDefault().ConvertAsync(stream, new PdfConversionRequest(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Frames_JoinedByArrowGlyphs_AreEachAPhraseParagraphFollowedByTheirContent()
    {
        PdfConversionResult result = await ConvertAsync(BuildFramesPdf());
        string md = result.Markdown;

        Assert.Contains(
            "**Składasz wniosek**\n\n- 1\\. przez internet, np. przez naszą stronę internetową,\n- 2\\. w biurze obsługi klienta,\n"
            + "- 3\\. osobiście, np. w jednej z placówek.\n",
            md,
            StringComparison.Ordinal);
        Assert.Contains(
            "**Sprawdzamy dokumenty rejestrowe**\n\n- 1\\. Zanim zawrzemy umowę, sprawdzimy Twoje dokumenty.\n"
            + "- 2\\. Weryfikujemy wpis do rejestru przedsiębiorców.\n"
            + "- 3\\. Możemy poprosić o dokument tożsamości oraz dokumenty potwierdzające dane z wniosku.\n",
            md,
            StringComparison.Ordinal);
        Assert.Contains(
            "**Potwierdzamy otwarcie**\n\nPotwierdzenie otwarcia rachunku przekażemy Ci elektronicznie.\n",
            md,
            StringComparison.Ordinal);

        string[] ordered = ["2\\) Jeśli otwarcie", "**Składasz wniosek**", "**Sprawdzamy dokumenty rejestrowe**", "**Potwierdzamy otwarcie**", "3\\) Po zawarciu"];
        int[] order = ordered.Select(s => md.IndexOf(s, StringComparison.Ordinal)).ToArray();
        Assert.DoesNotContain(order, i => i < 0);
        Assert.Equal(order.Order(), order);

        Assert.DoesNotContain("▼", md, StringComparison.Ordinal);
        Assert.DoesNotContain("|", md, StringComparison.Ordinal);
        Assert.Equal(0, result.Report.TableCount);
        Assert.DoesNotContain(result.Report.Warnings, w => w.Code == "TBL001_AmbiguousGrid");
    }

    [Fact]
    public async Task RuledDataTableWithWhiteCellsAndABoldHeaderRow_StaysAGfmTable()
    {
        PdfConversionResult result = await ConvertAsync(BuildDataTablePdf());
        string md = result.Markdown;

        Assert.Equal(1, result.Report.TableCount);
        Assert.Contains("| --- | --- |", md, StringComparison.Ordinal);
        Assert.Contains(md.Split('\n'), l => l.StartsWith('|') && l.Contains("Prowadzenie rachunku", StringComparison.Ordinal) && l.Contains("10 zł miesięcznie", StringComparison.Ordinal));
        Assert.DoesNotContain("**Prowadzenie rachunku**", md, StringComparison.Ordinal);
    }

    private const double StageText = 201.3;

    /// <summary>
    /// T034a — spec 008 US4, the corp terms page 22–23: a scheme of stages (bold „Etap N.” over plain lines in each box)
    /// inside a numbered list ends at the bottom of the page with an explanation item; the next page starts with a bold
    /// question heading at the margin, then the outer list goes on.
    /// </summary>
    internal static byte[] BuildStagesPdf()
    {
        const double size = 7;
        var builder = new SyntheticPdfBuilder();
        builder.Page()
            .Text(72, 80, "Załącznik o wskaźnikach", 14, bold: true)
            .Text(40, 110, "Jak wybieramy wskaźnik alternatywny?", size, bold: true)
            .Text(40, 125, "1. Strony stosują zasady opisane w tym załączniku.", size)
            .Text(40, 135, "2. Wskaźnik alternatywny stosujemy w sytuacjach opisanych w umowie.", size)
            .Text(40, 145, "3. Jak postępujemy, gdy musimy stosować wskaźnik alternatywny?", size)
            .FilledRect(53.9, 680, 141.7, 38, Gray)
            .Text(StageText, 692, "1. Jeżeli strony uzgodniły wskaźnik alternatywny, stosujemy go.", size)
            .Text(60.1, 702, "Etap 1.", size, bold: true)
            .Text(StageText, 702, "2. Jeżeli strony uzgodniły korektę, stosujemy ją.", size)
            .Text(121.5, 734, "▼", 14, mono: true)
            .FilledRect(53.9, 740, 141.7, 58, Gray)
            .Text(StageText, 752, "1. Stosujemy kurs średni ogłaszany przez bank centralny,", size)
            .Text(60.1, 757, "Etap 2.", size, bold: true)
            .Text(StageText + 16, 762, "o ile jest dostępny.", size)
            .Text(60.3, 767, "jeśli etap 1. nie", size)
            .Text(StageText, 772, "2. Brak kursu jest przypadkiem rozwiązania transakcji.", size)
            .Text(74.4, 777, "przyniesie rezultatu", size)
            .Text(StageText, 782, "3. Nie stosujemy korekty.", size);

        builder.Page()
            .Text(40, 60, "Co robimy, gdy wskaźnik nie ma pary walutowej?", size, bold: true)
            .Text(40, 80, "4. Stosujemy jako wskaźnik alternatywny iloraz dwóch kursów.", size)
            .Text(40, 100, "Czy możemy wrócić do wskaźnika?", size, bold: true)
            .Text(40, 120, "5. Wracamy do wskaźnika, gdy znów jest publikowany.", size);
        for (int i = 0; i < 14; i++)
        {
            builder.Text(40, 150 + (10 * i), $"Wiersz {i + 1} zwykłego tekstu załącznika o stałej długości linii.", size);
        }

        return builder.Build();
    }

    [Fact]
    public async Task StageSchemeEndingAtThePageBottom_KeepsTheHeadingOfTheNextPageAndThePlainBoxLines()
    {
        string md = (await ConvertAsync(BuildStagesPdf())).Markdown;
        string[] lines = md.Split('\n');

        Assert.Contains(lines, l => l.StartsWith('#') && l.EndsWith(" Co robimy, gdy wskaźnik nie ma pary walutowej?", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith('#') && l.EndsWith(" Czy możemy wrócić do wskaźnika?", StringComparison.Ordinal));
        Assert.Contains("- 3\\. Nie stosujemy korekty.\n", md, StringComparison.Ordinal);
        Assert.Contains("**Etap 1.**\n", md, StringComparison.Ordinal);
        Assert.Contains("**Etap 2.** jeśli etap 1. nie przyniesie rezultatu\n", md, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Scheme_DetectionDisabled_KeepsTheOldBehaviour()
    {
        string md = (await ConvertAsync(o => o.Tables.DetectStepSequences = false)).Markdown;

        Assert.DoesNotContain("**Składasz wniosek**\n\nSkładasz pisemny", md, StringComparison.Ordinal);
        Assert.Contains("Składasz wniosek", md, StringComparison.Ordinal);
    }
}
