using System.Text.RegularExpressions;
using LegalAgent.Chunking.Identity;

namespace LegalAgent.Chunking.Tests.Unit;

/// <summary>T010: chunk identifiers (research R7, FR-244).</summary>
public sealed class ChunkIdBuilderTests
{
    private static readonly Regex Pattern = new(@"\A[A-Za-z0-9][A-Za-z0-9._-]*_[0-9a-f]{16}_[0-9]+\z", RegexOptions.CultureInvariant);

    [Fact]
    public void Build_IsDocumentIdHashOfUnitKeyAndPart()
    {
        // SHA-256 of the UTF-8 bytes of "BP/REG/05 | § 11", computed independently.
        Assert.Equal("REG-05-w1_a761a1aea4847cbf_2", ChunkIdBuilder.Build("REG-05-w1", "BP/REG/05 | § 11", 2));
    }

    [Fact]
    public void Build_MatchesTheContractPatternAndLengthLimit()
    {
        string id = ChunkIdBuilder.Build(new string('a', 100), "BP/REG/05 | Oprocentowanie > § 2 #3", 12);

        Assert.Matches(Pattern, id);
        Assert.True(id.Length <= 120, id);
    }

    [Fact]
    public void Build_DependsOnDocumentKeyAndPartOnly()
    {
        string id = ChunkIdBuilder.Build("REG-05", "BP/REG/05 | § 11", 1);

        Assert.Equal(id, ChunkIdBuilder.Build("REG-05", "BP/REG/05 | § 11", 1));
        Assert.NotEqual(id, ChunkIdBuilder.Build("REG-05-w1", "BP/REG/05 | § 11", 1));
        Assert.NotEqual(id, ChunkIdBuilder.Build("REG-05", "BP/REG/05 | § 12", 1));
        Assert.NotEqual(id, ChunkIdBuilder.Build("REG-05", "BP/REG/05 | § 11", 2));
    }
}
