namespace LegalAgent.Corpus.Random;

/// <summary>
/// Platform-independent pseudo-random generator (SplitMix64) whose streams are derived from
/// (seed, purpose, id) with FNV-1a 64, so every decision of the corpus generator is reproducible
/// byte for byte on any .NET version and operating system (research R3).
/// </summary>
public sealed class DeterministicRandom
{
    private ulong _state;

    /// <summary>Creates a generator whose first output is SplitMix64(<paramref name="seed"/>).</summary>
    public DeterministicRandom(ulong seed) => _state = seed;

    /// <summary>A generator for one decision: <paramref name="purpose"/> (e.g. "dokument") of <paramref name="id"/> (e.g. "REG-03").</summary>
    public static DeterministicRandom Derive(ulong seed, string purpose, string id) => new(DeriveSeed(seed, purpose, id));

    /// <summary>The seed of the stream for (<paramref name="seed"/>, <paramref name="purpose"/>, <paramref name="id"/>).</summary>
    public static ulong DeriveSeed(ulong seed, string purpose, string id) => throw new NotImplementedException();

    /// <summary>FNV-1a 64 of the UTF-8 bytes of <paramref name="text"/>.</summary>
    public static ulong Fnv1a64(string text) => throw new NotImplementedException();

    /// <summary>Next 64-bit value.</summary>
    public ulong NextUInt64() => throw new NotImplementedException();

    /// <summary>A value in [0, <paramref name="n"/>).</summary>
    public int Next(int n) => throw new NotImplementedException();

    /// <summary>Shuffles <paramref name="items"/> in place (Fisher–Yates).</summary>
    public void Shuffle<T>(IList<T> items) => throw new NotImplementedException();
}
