using LegalAgent.Corpus.Random;

namespace LegalAgent.Corpus.Tests.Unit.Random;

public sealed class DeterministicRandomTests
{
    [Fact]
    public void NextUInt64_Seed0_MatchesSplitMix64ReferenceVector()
    {
        var rng = new DeterministicRandom(0);
        ulong[] expected = [0xE220A8397B1DCDAF, 0x6E789E6AA1B965F4, 0x06C45D188009454F, 0xF88BB8A8724C81EC, 0x1B39896A51A8749B];
        Assert.Equal(expected, Enumerable.Range(0, 5).Select(_ => rng.NextUInt64()).ToArray());
    }

    [Fact]
    public void NextUInt64_CorpusSeed_MatchesReferenceVector()
    {
        var rng = new DeterministicRandom(20261008);
        ulong[] expected = [0xB0CFE15D6D6ED722, 0xFBB44824DE13244E, 0x91EFD3B1CFBA6E09, 0x4D0FA144EBD284DF, 0xFB329213568FFAC2];
        Assert.Equal(expected, Enumerable.Range(0, 5).Select(_ => rng.NextUInt64()).ToArray());
    }

    [Fact]
    public void Fnv1a64_HashesUtf8Bytes()
    {
        Assert.Equal(0x3A50AE3E22B55B09UL, DeterministicRandom.Fnv1a64("zażółć"));
        Assert.Equal(0xCBF29CE484222325UL, DeterministicRandom.Fnv1a64(string.Empty));
    }

    [Fact]
    public void DeriveSeed_IsStableAndDependsOnPurposeAndId()
    {
        ulong a = DeterministicRandom.DeriveSeed(20261008, "dokument", "REG-03");
        Assert.Equal(a, DeterministicRandom.DeriveSeed(20261008, "dokument", "REG-03"));
        Assert.Equal(0x50D603CF173CF3BFUL, a);
        Assert.NotEqual(a, DeterministicRandom.DeriveSeed(20261008, "przydzial-blokow", "REG-03"));
        Assert.NotEqual(a, DeterministicRandom.DeriveSeed(20261008, "dokument", "REG-04"));
        Assert.NotEqual(a, DeterministicRandom.DeriveSeed(7, "dokument", "REG-03"));
        // purpose/id boundary is part of the key: ("ab","c") != ("a","bc")
        Assert.NotEqual(DeterministicRandom.DeriveSeed(1, "ab", "c"), DeterministicRandom.DeriveSeed(1, "a", "bc"));
    }

    [Fact]
    public void Next_StaysInRangeAndCoversIt()
    {
        var rng = new DeterministicRandom(42);
        var seen = new HashSet<int>();
        for (int i = 0; i < 1000; i++)
        {
            int v = rng.Next(7);
            Assert.InRange(v, 0, 6);
            seen.Add(v);
        }

        Assert.Equal(7, seen.Count);
        Assert.Equal(0, new DeterministicRandom(1).Next(1));
    }

    [Fact]
    public void Shuffle_IsDeterministicPermutation()
    {
        int[] a = Enumerable.Range(0, 20).ToArray();
        int[] b = Enumerable.Range(0, 20).ToArray();
        new DeterministicRandom(5).Shuffle(a);
        new DeterministicRandom(5).Shuffle(b);
        Assert.Equal(a, b);
        Assert.NotEqual(Enumerable.Range(0, 20), a);
        Assert.Equal(Enumerable.Range(0, 20), a.Order());
    }
}
