using LegalAgent.Chunking.Identity;

namespace LegalAgent.Chunking.Tests.Unit;

/// <summary>T008: unit keys (research R6, FR-243).</summary>
public sealed class UnitKeyBuilderTests
{
    [Theory]
    [InlineData("§ 11", "§ 11.", "§ 11")]
    [InlineData("Art. 5.", "Art. 5.", "Art. 5")]
    [InlineData("§  11", "§ 11.", "§ 11")]
    [InlineData(" Rozdział 3 ", "Rozdział 3 Opłaty", "Rozdział 3")]
    [InlineData(null, "I. Opłaty  za prowadzenie rachunku.", "I. Opłaty za prowadzenie rachunku")]
    [InlineData(null, "5. Przebieg procedury", "5. Przebieg procedury")]
    public void Segment_DesignationOrHeadingNormalised(string? designation, string heading, string expected)
    {
        Assert.Equal(expected, UnitKeyBuilder.Segment(designation, heading));
    }

    [Fact]
    public void Build_UniqueUnitsAreKeyedBySeriesAndOwnSegment()
    {
        IReadOnlyList<string> keys = UnitKeyBuilder.Build(
            "BP/REG/05",
            [[UnitKeyBuilder.PreambleSegment], ["Rozdział 1"], ["Rozdział 1", "§ 1"], ["Rozdział 2", "§ 11"]]);

        Assert.Equal(["BP/REG/05 | ~wstep", "BP/REG/05 | Rozdział 1", "BP/REG/05 | § 1", "BP/REG/05 | § 11"], keys);
    }

    [Fact]
    public void Build_RepeatedSegmentIsQualifiedByTheShortestUniqueSuffix()
    {
        IReadOnlyList<string> keys = UnitKeyBuilder.Build(
            "BP/REG/05",
            [["Warunki", "§ 1"], ["Warunki", "§ 2"], ["Oprocentowanie", "§ 1"], ["Oprocentowanie", "§ 2"], ["§ 3"]]);

        Assert.Equal(
            [
                "BP/REG/05 | Warunki > § 1",
                "BP/REG/05 | Warunki > § 2",
                "BP/REG/05 | Oprocentowanie > § 1",
                "BP/REG/05 | Oprocentowanie > § 2",
                "BP/REG/05 | § 3",
            ],
            keys);
    }

    [Fact]
    public void Build_ShorterPathKeepsItsOwnSegmentWhenALongerOneEndsTheSame()
    {
        IReadOnlyList<string> keys = UnitKeyBuilder.Build("X", [["§ 2"], ["Oprocentowanie", "§ 2"]]);

        Assert.Equal(["X | § 2", "X | Oprocentowanie > § 2"], keys);
    }

    [Fact]
    public void Build_IdenticalFullPathsGetOccurrenceNumbersFromTheSecond()
    {
        IReadOnlyList<string> keys = UnitKeyBuilder.Build("dz-u", [["Art. 5"], ["Art. 6"], ["Art. 5"], ["Art. 5"]]);

        Assert.Equal(["dz-u | Art. 5", "dz-u | Art. 6", "dz-u | Art. 5 #2", "dz-u | Art. 5 #3"], keys);
    }

    [Fact]
    public void Build_InsertingAChapterDoesNotChangeParagraphKeys()
    {
        IReadOnlyList<string> before = UnitKeyBuilder.Build("BP/REG/06", [["Rozdział 1", "§ 10"], ["Rozdział 1", "§ 11"]]);
        IReadOnlyList<string> after = UnitKeyBuilder.Build("BP/REG/06", [["Rozdział 1", "§ 10"], ["Rozdział 2"], ["Rozdział 2", "§ 11"]]);

        Assert.Equal(before[1], after[2]);
        Assert.Equal("BP/REG/06 | § 11", after[2]);
    }
}
