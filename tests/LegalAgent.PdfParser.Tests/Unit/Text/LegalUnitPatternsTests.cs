using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Text;

namespace LegalAgent.PdfParser.Tests.Unit.Text;

/// <summary>T055 — legal unit designations at the start of a line (FR-043, research R11).</summary>
public sealed class LegalUnitPatternsTests
{
    [Theory]
    [InlineData("KSIĘGA PIERWSZA", SectionKind.Book, "KSIĘGA PIERWSZA", "PIERWSZA", "")]
    [InlineData("Księga druga", SectionKind.Book, "Księga druga", "druga", "")]
    [InlineData("CZĘŚĆ II", SectionKind.Part, "CZĘŚĆ II", "II", "")]
    [InlineData("DZIAŁ II", SectionKind.Division, "DZIAŁ II", "II", "")]
    [InlineData("Dział IIa", SectionKind.Division, "Dział IIa", "IIa", "")]
    [InlineData("DZIAŁ III PRZEPISY KOŃCOWE", SectionKind.Division, "DZIAŁ III", "III", "PRZEPISY KOŃCOWE")]
    [InlineData("Rozdział 3", SectionKind.Chapter, "Rozdział 3", "3", "")]
    [InlineData("Rozdział 3a", SectionKind.Chapter, "Rozdział 3a", "3a", "")]
    [InlineData("Rozdział I", SectionKind.Chapter, "Rozdział I", "I", "")]
    [InlineData("ROZDZIAŁ 1", SectionKind.Chapter, "ROZDZIAŁ 1", "1", "")]
    [InlineData("Rozdział 3. Ochrona konsumenta", SectionKind.Chapter, "Rozdział 3", "3", "Ochrona konsumenta")]
    [InlineData("Oddział 2", SectionKind.Subchapter, "Oddział 2", "2", "")]
    [InlineData("Art. 5.", SectionKind.Article, "Art. 5", "5", "")]
    [InlineData("Art. 12a.", SectionKind.Article, "Art. 12a", "12a", "")]
    [InlineData("Art. 12¹.", SectionKind.Article, "Art. 12¹", "12¹", "")]
    [InlineData("Art. 5. Ustawa określa zasady.", SectionKind.Article, "Art. 5", "5", "Ustawa określa zasady.")]
    [InlineData("Art. 7. 1. Bank prowadzi rachunki.", SectionKind.Article, "Art. 7", "7", "1. Bank prowadzi rachunki.")]
    [InlineData("§ 7.", SectionKind.Paragraph, "§ 7", "7", "")]
    [InlineData("§ 5¹.", SectionKind.Paragraph, "§ 5¹", "5¹", "")]
    [InlineData("§ 12a. Treść paragrafu.", SectionKind.Paragraph, "§ 12a", "12a", "Treść paragrafu.")]
    [InlineData("§ 2. 1. Czynności podlegające opłatom", SectionKind.Paragraph, "§ 2", "2", "1. Czynności podlegające opłatom")]
    public void TryMatch_RecognisesDesignations(string line, SectionKind kind, string designation, string number, string rest)
    {
        Assert.True(LegalUnitPatterns.TryMatch(line, out LegalUnitMatch? match));
        Assert.Equal(kind, match.Kind);
        Assert.Equal(designation, match.Designation);
        Assert.Equal(number, match.Number);
        Assert.Equal(rest, match.Rest);
    }

    [Theory]
    [InlineData("[Art. 31. 1. Dyrektor generalny urzędu upowszechnia", "[", "Art. 31", "1. Dyrektor generalny urzędu upowszechnia")]
    [InlineData("<Art. 27a. Szef Służby Cywilnej prowadzi system", "<", "Art. 27a", "Szef Służby Cywilnej prowadzi system")]
    [InlineData("<§ 4. Uchyla się", "<", "§ 4", "Uchyla się")]
    public void TryMatch_AmendmentNotation_KeepsTheBracketAsPrefix_FR043(string line, string prefix, string designation, string rest)
    {
        Assert.True(LegalUnitPatterns.TryMatch(line, out LegalUnitMatch? match));
        Assert.Equal((prefix, designation, rest), (match.Prefix, match.Designation, match.Rest));
    }

    [Theory]
    [InlineData("art. 5 ustawy stosuje się odpowiednio")] // lower-case reference
    [InlineData("zgodnie z § 7 regulaminu")] // not at the start of the line
    [InlineData("Art. 5 ust. 2 stosuje się odpowiednio.")] // reference: no period after the number
    [InlineData("§ 7 ust. 1 stosuje się odpowiednio.")]
    [InlineData("Rozdziały 1–3 stosuje się do umów.")]
    [InlineData("Dział kredytów prowadzi ewidencję.")]
    [InlineData("Artykuł prasowy nie jest aktem prawnym.")]
    [InlineData("Rozdział")]
    [InlineData("Części zamienne")]
    [InlineData("")]
    public void TryMatch_RejectsReferencesAndOrdinaryText(string line)
    {
        Assert.False(LegalUnitPatterns.TryMatch(line, out _));
    }
}
