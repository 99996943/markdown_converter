namespace LegalAgent.Faq.Tests;

/// <summary>Heading anchors as VS Code and GitHub compute them (spec 013, research R3).</summary>
public sealed class MarkdownAnchorsTests
{
    [Theory]
    [InlineData("§ 26. Tytuł", "-26-tytuł")]
    [InlineData("8. Mobilna wpłata", "8-mobilna-wpłata")]
    [InlineData("Rozdział 1. Przepisy ogólne", "rozdział-1-przepisy-ogólne")]
    [InlineData("Art. 5¹", "art-5")]
    [InlineData("„Konto z premią”", "konto-z-premią")]
    [InlineData("A  B", "a--b")]
    [InlineData("snake_case", "snake_case")]
    [InlineData("Część II – Zasady", "część-ii--zasady")]
    [InlineData("  Ogólne (1) ", "ogólne-1")]
    [InlineData("Tab\tulator", "tab-ulator")]
    public void Slug_AsVsCode(string heading, string expected)
    {
        Assert.Equal(expected, MarkdownAnchors.Slug(heading));
    }

    [Fact]
    public void ForDocument_HeadingsInOrder_WithoutMarkersAndEscapes()
    {
        const string markdown = "# Regulamin\n\n<!-- page: 1 -->\n\n## Rozdział 1. Ogólne\n\ntekst\n\n### Art\\. 5\\) **pogrubiony** *kursywa*\n";

        Assert.Equal(["regulamin", "rozdział-1-ogólne", "art-5-pogrubiony-kursywa"], MarkdownAnchors.ForDocument(markdown));
    }

    [Fact]
    public void ForDocument_RepeatsGetCounter_IncludingTitle()
    {
        const string markdown = "# § 1.\n\n## § 1.\n\n## § 1.\n\n## Inny\n";

        Assert.Equal(["-1", "-1-1", "-1-2", "inny"], MarkdownAnchors.ForDocument(markdown));
    }

    [Fact]
    public void ForDocument_CounterSkipsTakenAnchors()
    {
        const string markdown = "## x-1\n\n## x\n\n## x\n";

        Assert.Equal(["x-1", "x", "x-2"], MarkdownAnchors.ForDocument(markdown));
    }

    [Fact]
    public void ForDocument_IgnoresCodeBlocksAndHashWithoutSpace()
    {
        const string markdown = "## A\n\n```\n## B\n```\n\n#bez spacji\n\n####### siedem\n\n###### Sześć\n";

        Assert.Equal(["a", "sześć"], MarkdownAnchors.ForDocument(markdown));
    }

    [Fact]
    public void ForDocument_CrLfLineEndings()
    {
        Assert.Equal(["a", "b"], MarkdownAnchors.ForDocument("## A\r\n\r\ntekst\r\n\r\n## B\r\n"));
    }
}
