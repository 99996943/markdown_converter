using LegalAgent.PdfParser.Rendering;

namespace LegalAgent.PdfParser.Tests.Rendering;

public sealed class MarkdownEscaperTests
{
    [Theory]
    [InlineData("a\\b", "a\\\\b")]
    [InlineData("a*b", "a\\*b")]
    [InlineData("a_b", "a\\_b")]
    [InlineData("a[b]c", "a\\[b\\]c")]
    [InlineData("a<b>c", "a\\<b\\>c")]
    [InlineData("a`b", "a\\`b")]
    public void EscapeText_EscapesInlineSpecialCharacters(string input, string expected)
    {
        Assert.Equal(expected, MarkdownEscaper.EscapeText(input));
    }

    [Fact]
    public void EscapeText_LeavesPlainTextAndPolishLettersUntouched()
    {
        const string text = "Bank pobiera opłatę za zażółć gęślą jaźń (art. 5).";
        Assert.Equal(text, MarkdownEscaper.EscapeText(text));
    }

    [Theory]
    [InlineData("# nagłówek", "\\# nagłówek")]
    [InlineData("+ plus", "\\+ plus")]
    [InlineData("- minus", "\\- minus")]
    [InlineData("> cytat", "\\> cytat")]
    [InlineData("= równa się", "\\= równa się")]
    [InlineData("2024. r.", "2024\\. r.")]
    [InlineData("12) punkt", "12\\) punkt")]
    public void EscapeText_AtLineStart_EscapesBlockMarkers(string input, string expected)
    {
        Assert.Equal(expected, MarkdownEscaper.EscapeText(input, atLineStart: true));
    }

    [Theory]
    [InlineData("a # b")]
    [InlineData("a - b")]
    [InlineData("w 2024. roku")]
    [InlineData("a + b = c")]
    public void EscapeText_NotAtLineStart_LeavesBlockMarkersInTheMiddle(string input)
    {
        Assert.Equal(input, MarkdownEscaper.EscapeText(input, atLineStart: false));
    }

    [Fact]
    public void EscapeText_AtLineStart_DoesNotEscapeNumbersWithoutDelimiter()
    {
        Assert.Equal("2024 rok", MarkdownEscaper.EscapeText("2024 rok", atLineStart: true));
    }

    [Fact]
    public void EscapeText_InTable_EscapesPipe()
    {
        Assert.Equal("a \\| b", MarkdownEscaper.EscapeText("a | b", inTable: true));
    }

    [Fact]
    public void EscapeText_OutsideTable_KeepsPipe()
    {
        Assert.Equal("a | b", MarkdownEscaper.EscapeText("a | b"));
    }

    [Theory]
    [InlineData("1)", "1\\)")]
    [InlineData("2.", "2\\.")]
    [InlineData("a)", "a\\)")]
    [InlineData("12)", "12\\)")]
    [InlineData("1.2.3.", "1.2.3\\.")]
    public void EscapeListLabel_EscapesTrailingDelimiter(string label, string expected)
    {
        Assert.Equal(expected, MarkdownEscaper.EscapeListLabel(label));
    }

    [Theory]
    [InlineData("–")]
    [InlineData("•")]
    public void EscapeListLabel_LeavesDashAndBulletUntouched(string label)
    {
        Assert.Equal(label, MarkdownEscaper.EscapeListLabel(label));
    }

    [Fact]
    public void EscapeText_EmptyString_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, MarkdownEscaper.EscapeText(string.Empty, atLineStart: true));
    }
}
