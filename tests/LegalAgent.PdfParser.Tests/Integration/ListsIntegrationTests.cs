using System.Runtime.CompilerServices;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Tests.Fixtures;

namespace LegalAgent.PdfParser.Tests.Integration;

/// <summary>
/// T069 — US3 independent test: an article laid out like an ISAP consolidated text (ust. 1–2 with a first-line indent,
/// points 1)–3) with a hanging indent, letters a)–b) in point 2, tirets, a common part and a point continued on the next
/// page) followed by an article with a bullet list converts to nested Markdown lists with literal labels.
/// </summary>
public sealed class ListsIntegrationTests
{
    private const double Margin = 72;
    private const double Indent = 97;
    private const double Deep = 120;
    private const double Bullet = 85;

    internal static byte[] BuildUs3Pdf()
    {
        var builder = new SyntheticPdfBuilder().PageNumberFooter("{n}");

        builder.Page()
            .Text(Indent, 80, "Art. 1. 1. Ustawa określa zasady świadczenia usług testowych przez")
            .Text(Margin, 94, "dostawców usług.")
            .Text(Indent, 108, "2. Użytkownik usługi testowej ma prawo do:")
            .Text(Margin, 122, "1) informacji o warunkach świadczenia usługi;")
            .Text(Margin, 136, "2) odstąpienia od umowy w przypadku:")
            .Text(Indent, 150, "a) wady usługi,")
            .Text(Indent, 164, "b) zmiany warunków umowy:")
            .Text(Deep, 178, "– ceny,")
            .Text(Deep, 192, "– zakresu usługi")
            .Text(Indent, 206, "– w terminie 14 dni od dnia zawarcia umowy;")
            .Text(Margin, 220, "3) złożenia reklamacji dotyczącej usługi świadczonej przez dostawcę");

        builder.Page()
            .Text(Indent, 80, "w okresie obowiązywania umowy.")
            .Text(Indent, 108, "Art. 2. Dostawca udostępnia usługę za pomocą:")
            .Text(Bullet, 122, "• aplikacji mobilnej,")
            .Text(Bullet, 136, "• serwisu internetowego dostępnego w przeglądarce")
            .Text(Indent, 150, "na urządzeniach stacjonarnych.")
            .Text(Margin, 164, "Dostawca informuje o zmianach na swojej stronie internetowej.");

        return builder.Build();
    }

    private static string ExpectedPath(string name, [CallerFilePath] string thisFile = "") =>
        Path.Combine(Path.GetDirectoryName(thisFile)!, "Expected", name);

    private static async Task<PdfConversionResult> ConvertAsync()
    {
        using var stream = new MemoryStream(BuildUs3Pdf());
        return await PdfMarkdownConverter.CreateDefault().ConvertAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SyntheticAct_ConvertsToNestedListsWithLiteralLabels()
    {
        PdfConversionResult result = await ConvertAsync();

        GoldenFile.AssertMatches(result.Markdown, ExpectedPath("us3-lists.expected.md"));
    }

    [Fact]
    public async Task SyntheticAct_ModelHasNestedItemsAndCommonPart()
    {
        LegalDocument document = (await ConvertAsync()).Document;

        Section art1 = document.Sections[0];
        ListBlock usteps = Assert.IsType<ListBlock>(Assert.Single(art1.Blocks));
        Assert.Equal(new PageRange(1, 2), usteps.Pages);
        Assert.Equal(["1.", "2."], usteps.Items.Select(i => i.Label));

        ListBlock points = Assert.IsType<ListBlock>(Assert.Single(usteps.Items[1].Children));
        Assert.Equal(["1)", "2)", "3)"], points.Items.Select(i => i.Label));

        ListItem point2 = points.Items[1];
        ListBlock letters = Assert.IsType<ListBlock>(point2.Children[0]);
        Assert.Equal(["a)", "b)"], letters.Items.Select(i => i.Label));
        ListBlock tirets = Assert.IsType<ListBlock>(Assert.Single(letters.Items[1].Children));
        Assert.All(tirets.Items, i => Assert.Equal(ListLabelKind.Dash, i.LabelKind));
        Assert.IsType<ParagraphBlock>(point2.Children[1]);

        ListBlock bullets = Assert.IsType<ListBlock>(document.Sections[1].Blocks[1]);
        Assert.All(bullets.Items, i => Assert.Equal(("•", ListLabelKind.Bullet), (i.Label, i.LabelKind)));
    }
}
