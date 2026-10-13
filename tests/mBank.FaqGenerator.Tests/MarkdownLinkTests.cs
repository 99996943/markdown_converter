namespace MBank.FaqGenerator.Tests;

/// <summary>Address of a document's Markdown relative to the FAQ file (spec 013, R5).</summary>
public sealed class MarkdownLinkTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "faq-links");

    [Fact]
    public void SiblingDirectories()
    {
        Assert.Equal("../downloads/a.md", FaqStage.MarkdownLink(Path.Combine(Root, "faq"), Path.Combine(Root, "downloads", "a.md")));
    }

    [Fact]
    public void SegmentsPercentEncoded()
    {
        Assert.Equal(
            "../downloads/regulamin%20%28a%29.md",
            FaqStage.MarkdownLink(Path.Combine(Root, "faq"), Path.Combine(Root, "downloads", "regulamin (a).md")));
    }

    [Fact]
    public void FaqDeeper()
    {
        Assert.Equal(
            "../../downloads/a.md",
            FaqStage.MarkdownLink(Path.Combine(Root, "wyniki", "faq"), Path.Combine(Root, "downloads", "a.md")));
    }

    [Fact]
    public void OtherDrive_NoLink()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Drives exist only on Windows.");

        Assert.Null(FaqStage.MarkdownLink(@"C:\faq", @"D:\dl\a.md"));
    }
}
