using System.Runtime.CompilerServices;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Tests.Fixtures;

namespace LegalAgent.PdfParser.Tests.Integration;

/// <summary>
/// T060 — US2 independent test: a synthetic act with a title block, divisions, chapters with titles, articles
/// (including „Art. 3a.”), a footnote and a division without chapters converts to the expected section hierarchy.
/// </summary>
public sealed class HeadingsIntegrationTests
{
    private const double Left = 72;

    /// <summary>Generous width estimate (0.62 em per character) so a reference marker never overlaps its word.</summary>
    private static double After(string text, double size = 11) => Left + (text.Length * 0.62 * size) + 1;

    /// <remarks>The text column is 72–417 pt (centre ≈ 245 pt); the title block lines are centred on it.</remarks>
    internal static byte[] BuildUs2Pdf()
    {
        const string art2Line2 = "przez organy administracji publicznej.";

        var builder = new SyntheticPdfBuilder().PageNumberFooter("{n}");

        builder.Page()
            .Text(211, 80, "USTAWA", 16, bold: true)
            .Text(184, 100, "z dnia 1 stycznia 2026 r.")
            .Text(190, 116, "o usługach testowych")
            .Text(275, 150, "DZIAŁ I", bold: true)
            .Text(245, 164, "PRZEPISY OGÓLNE", bold: true)
            .Text(270, 190, "Rozdział 1", bold: true)
            .Text(255, 204, "Zakres ustawy", bold: true)
            .Text(Left, 230, "Art. 1. Ustawa określa zasady świadczenia usług testowych przez")
            .Text(Left, 244, "dostawców usług oraz prawa i obowiązki użytkowników tych usług.")
            .Text(Left, 260, "Art. 2. Ustawy nie stosuje się do usług świadczonych nieodpłatnie")
            .Text(Left, 274, art2Line2)
            .Text(After(art2Line2), 270, "1)", 7)
            .Text(270, 300, "Rozdział 2", bold: true)
            .Text(265, 314, "Definicje", bold: true)
            .Text(Left, 340, "Art. 3. Ilekroć w ustawie jest mowa o usłudze testowej, rozumie się")
            .Text(Left, 354, "przez to usługę świadczoną drogą elektroniczną.")
            .Text(Left, 370, "Art. 3a. Użytkownikiem jest każdy, kto korzysta z usługi testowej.")
            .HLine(Left, Left + 150, 755)
            .Text(Left, 770, "1) Niniejsza ustawa wdraża dyrektywę Parlamentu Europejskiego i Rady (UE) 2026/1.", 8);

        builder.Page()
            .Text(272, 80, "DZIAŁ II", bold: true)
            .Text(240, 94, "PRZEPISY KOŃCOWE", bold: true)
            .Text(Left, 120, "Art. 4. Ustawa wchodzi w życie po upływie 14 dni od dnia ogłoszenia.");

        return builder.Build();
    }

    private static string ExpectedPath(string name, [CallerFilePath] string thisFile = "") =>
        Path.Combine(Path.GetDirectoryName(thisFile)!, "Expected", name);

    private static async Task<PdfConversionResult> ConvertAsync()
    {
        using var stream = new MemoryStream(BuildUs2Pdf());
        return await PdfMarkdownConverter.CreateDefault().ConvertAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SyntheticAct_ConvertsToTheExpectedHierarchy()
    {
        PdfConversionResult result = await ConvertAsync();

        GoldenFile.AssertMatches(result.Markdown, ExpectedPath("us2-headings.expected.md"));
    }

    [Fact]
    public async Task SyntheticAct_ModelHasTitlePathsAndPageRanges()
    {
        LegalDocument document = (await ConvertAsync()).Document;

        Assert.Equal("USTAWA z dnia 1 stycznia 2026 r. o usługach testowych", document.Title);
        Assert.Equal(["DZIAŁ I. PRZEPISY OGÓLNE", "DZIAŁ II. PRZEPISY KOŃCOWE"], document.Sections.Select(s => s.HeadingText));

        Section art3 = document.Sections[0].Children[1].Children[0];
        Assert.Equal((SectionKind.Article, "Art. 3", "3"), (art3.Kind, art3.Designation, art3.Number));
        Assert.Equal(["DZIAŁ I. PRZEPISY OGÓLNE", "Rozdział 2. Definicje", "Art. 3."], art3.Path);

        Section art3a = document.Sections[0].Children[1].Children[1];
        Assert.Equal(("Art. 3a", "3a"), (art3a.Designation, art3a.Number));

        Section art4 = Assert.Single(document.Sections[1].Children);
        Assert.Equal((3, new PageRange(2, 2)), (art4.Level, art4.Pages));
        Assert.Equal(new PageRange(1, 1), document.Sections[0].Pages);
    }
}
