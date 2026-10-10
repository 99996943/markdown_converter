using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Text;

namespace LegalAgent.PdfParser.Tests.Unit.Text;

/// <summary>T066 — list label classification (FR-050).</summary>
public sealed class ListLabelPatternsTests
{
    public static TheoryData<string> BulletChars() => new()
    {
        "•", "▪", "◦", "‣", "*", "●", "■", "□", "○",
        "", "", "", "", "", "", "",
    };

    [Theory]
    [MemberData(nameof(BulletChars))]
    public void Bullet_chars_are_classified(string bullet)
    {
        Assert.True(ListLabelPatterns.IsBulletChar(bullet));
        Assert.True(ListLabelPatterns.TryMatch(bullet + " Treść punktu", out ListLabelMatch? m));
        Assert.Equal(bullet, m.Label);
        Assert.Equal(ListLabelKind.Bullet, m.Kind);
        Assert.Null(m.Ordinal);
        Assert.Equal("Treść punktu", m.Rest);
    }

    [Theory]
    [InlineData("1) Treść", "1)", ListLabelKind.ArabicParen, 1, "Treść")]
    [InlineData("[1) jest obywatelem polskim;]", "[1)", ListLabelKind.ArabicParen, 1, "jest obywatelem polskim;]")]
    [InlineData("<2a. Wymogu nie stosuje się", "<2a.", ListLabelKind.ArabicDot, 2, "Wymogu nie stosuje się")]
    [InlineData("12) Treść", "12)", ListLabelKind.ArabicParen, 12, "Treść")]
    [InlineData("1a) Treść", "1a)", ListLabelKind.ArabicParen, 1, "Treść")]
    [InlineData("4ba) Centralnym Biurze", "4ba)", ListLabelKind.ArabicParen, 4, "Centralnym Biurze")]
    [InlineData("2ab. Treść", "2ab.", ListLabelKind.ArabicDot, 2, "Treść")]
    [InlineData("5¹) Treść", "5¹)", ListLabelKind.ArabicParen, 5, "Treść")]
    [InlineData("a) Treść", "a)", ListLabelKind.LetterParen, 1, "Treść")]
    [InlineData("z) Treść", "z)", ListLabelKind.LetterParen, 26, "Treść")]
    [InlineData("aa) Treść", "aa)", ListLabelKind.LetterParen, 27, "Treść")]
    [InlineData("zb) Treść", "zb)", ListLabelKind.LetterParen, 678, "Treść")]
    [InlineData("i) Treść", "i)", ListLabelKind.LetterParen, 9, "Treść")]
    [InlineData("1. Treść", "1.", ListLabelKind.ArabicDot, 1, "Treść")]
    [InlineData("12. Treść", "12.", ListLabelKind.ArabicDot, 12, "Treść")]
    [InlineData("2a. Treść", "2a.", ListLabelKind.ArabicDot, 2, "Treść")]
    [InlineData("3¹. Treść", "3¹.", ListLabelKind.ArabicDot, 3, "Treść")]
    [InlineData("IV. Treść", "IV.", ListLabelKind.Roman, 4, "Treść")]
    [InlineData("II) Treść", "II)", ListLabelKind.Roman, 2, "Treść")]
    [InlineData("iv) Treść", "iv)", ListLabelKind.Roman, 4, "Treść")]
    [InlineData("ii) Treść", "ii)", ListLabelKind.Roman, 2, "Treść")]
    [InlineData("xii) Treść", "xii)", ListLabelKind.Roman, 12, "Treść")]
    [InlineData("1.2.3. Treść", "1.2.3.", ListLabelKind.Outline, null, "Treść")]
    [InlineData("1.2. Treść", "1.2.", ListLabelKind.Outline, null, "Treść")]
    [InlineData("1.2 Treść", "1.2", ListLabelKind.Outline, null, "Treść")]
    [InlineData("– Treść", "–", ListLabelKind.Dash, null, "Treść")]
    [InlineData("— Treść", "—", ListLabelKind.Dash, null, "Treść")]
    [InlineData("- Treść", "-", ListLabelKind.Dash, null, "Treść")]
    [InlineData("  1)   Treść z  odstępami  ", "1)", ListLabelKind.ArabicParen, 1, "Treść z  odstępami")]
    public void Labels_are_classified(string line, string label, ListLabelKind kind, int? ordinal, string rest)
    {
        Assert.True(ListLabelPatterns.TryMatch(line, out ListLabelMatch? m));
        Assert.Equal(label, m.Label);
        Assert.Equal(kind, m.Kind);
        Assert.Equal(ordinal, m.Ordinal);
        Assert.Equal(rest, m.Rest);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1)")]
    [InlineData("1) ")]
    [InlineData("a)")]
    [InlineData("•")]
    [InlineData("–")]
    [InlineData("1)Treść")]
    [InlineData("a)Treść")]
    [InlineData("•Treść")]
    [InlineData("Art. 5.")]
    [InlineData("Art. 5. Treść")]
    [InlineData("§ 3. Treść")]
    [InlineData("2024. Treść")]
    [InlineData("2024 r. weszła")]
    [InlineData("01.01.2026 r. wynosi 170.103.364 złote.")]
    [InlineData("1.09.2026 r. do 30.11.2026 r.")]
    [InlineData("ust. 2")]
    [InlineData("zł 100")]
    [InlineData("abc) Treść")]
    [InlineData("A) Treść")]
    [InlineData("Zwykły tekst")]
    public void Non_labels_do_not_match(string line)
    {
        Assert.False(ListLabelPatterns.TryMatch(line, out ListLabelMatch? m));
        Assert.Null(m);
    }

    /// <summary>T007 (spec 007, FR-510) — „1/”, „a/” labels of corporate regulations.</summary>
    [Theory]
    [InlineData("1/ gromadzenia środków", "1/", ListLabelKind.ArabicSlash, 1, "gromadzenia środków")]
    [InlineData("12/ Treść", "12/", ListLabelKind.ArabicSlash, 12, "Treść")]
    [InlineData("1a/ Treść", "1a/", ListLabelKind.ArabicSlash, 1, "Treść")]
    [InlineData("a/ zarządzać uprawnieniami", "a/", ListLabelKind.LetterSlash, 1, "zarządzać uprawnieniami")]
    [InlineData("i/ Strony zmienią warunki", "i/", ListLabelKind.LetterSlash, 9, "Strony zmienią warunki")]
    [InlineData("aa/ Treść", "aa/", ListLabelKind.LetterSlash, 27, "Treść")]
    public void Slash_labels_are_classified(string line, string label, ListLabelKind kind, int? ordinal, string rest)
    {
        Assert.True(ListLabelPatterns.TryMatch(line, out ListLabelMatch? m));
        Assert.Equal(label, m.Label);
        Assert.Equal(kind, m.Kind);
        Assert.Equal(ordinal, m.Ordinal);
        Assert.Equal(rest, m.Rest);
    }

    [Theory]
    [InlineData("7/2017 z dnia 1 marca")]
    [InlineData("13/36")]
    [InlineData("13/36 Treść")]
    [InlineData("4/49 Treść")]
    [InlineData("Klient/Klienci mogą")]
    [InlineData("km/h Treść")]
    [InlineData("i/lub wnioskiem")]
    [InlineData("1/")]
    [InlineData("a/")]
    [InlineData("1234/ Treść")]
    [InlineData("abc/ Treść")]
    [InlineData("A/ Treść")]
    public void Slash_non_labels_do_not_match(string line)
    {
        Assert.False(ListLabelPatterns.TryMatch(line, out ListLabelMatch? m));
        Assert.Null(m);
    }

    [Theory]
    [InlineData("•", true)]
    [InlineData("", true)]
    [InlineData("*", true)]
    [InlineData("-", false)]
    [InlineData("–", false)]
    [InlineData("a", false)]
    [InlineData("", false)]
    [InlineData("••", false)]
    public void IsBulletChar_is_true_only_for_a_single_bullet(string text, bool expected)
    {
        Assert.Equal(expected, ListLabelPatterns.IsBulletChar(text));
    }
}
