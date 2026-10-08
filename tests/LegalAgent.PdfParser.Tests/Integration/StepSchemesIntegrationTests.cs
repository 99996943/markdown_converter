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
        ["Zawarcie umowy", "**Krok 1", "**Krok 2", "**Krok 3", "**Krok 4", "Więcej informacji"];

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

        Assert.Contains("**Krok 1: Składasz wniosek**\n\nSkładasz pisemny wniosek o otwarcie rachunku.\n", md, StringComparison.Ordinal);
        Assert.Contains(
            "**Krok 2: Potwierdzamy Twoją tożsamość**\n\nZanim zawrzemy umowę, potwierdzimy Twoją tożsamość w sposób opisany w regulaminie obsługi klientów.\n",
            md,
            StringComparison.Ordinal);
        Assert.Contains(
            "**Krok 3: Sprawdzamy warunek zawarcia umowy**\n\n- Złożenie wniosku nie oznacza zawarcia umowy.\n- Sprawdzimy, czy Twój numer PESEL nie jest zastrzeżony.\n",
            md,
            StringComparison.Ordinal);
        Assert.Matches(ItemAcrossPages(), md);
        Assert.Contains("- Po zawarciu umowy otworzymy rachunek.", md, StringComparison.Ordinal);
        Assert.Contains("**Krok 4: Odbierasz kartę**\n\nKartę wyślemy pocztą.\n", md, StringComparison.Ordinal);

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
        Assert.Equal(4, Regex.Count(md, @"\*\*Krok \d: "));
    }

    [Fact]
    public async Task Scheme_DetectionDisabled_KeepsTheOldBehaviour()
    {
        string md = (await ConvertAsync(o => o.Tables.DetectStepSequences = false)).Markdown;

        Assert.DoesNotContain("Krok 1", md, StringComparison.Ordinal);
        Assert.Contains("Składasz wniosek", md, StringComparison.Ordinal);
    }
}
