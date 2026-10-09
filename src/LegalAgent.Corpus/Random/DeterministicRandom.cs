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
    public static ulong DeriveSeed(ulong seed, string purpose, string id)
    {
        ArgumentNullException.ThrowIfNull(purpose);
        ArgumentNullException.ThrowIfNull(id);
        ulong state = seed ^ Fnv1a64(purpose + "" + id);
        return Mix(ref state);
    }

    /// <summary>FNV-1a 64 of the UTF-8 bytes of <paramref name="text"/>.</summary>
    public static ulong Fnv1a64(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        ulong hash = 0xCBF29CE484222325;
        foreach (byte b in System.Text.Encoding.UTF8.GetBytes(text))
        {
            hash ^= b;
            hash *= 0x100000001B3;
        }

        return hash;
    }

    /// <summary>Next 64-bit value.</summary>
    public ulong NextUInt64() => Mix(ref _state);

    /// <summary>A value in [0, <paramref name="n"/>).</summary>
    public int Next(int n)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(n);
        return (int)(((UInt128)NextUInt64() * (ulong)n) >> 64);
    }

    /// <summary>Shuffles <paramref name="items"/> in place (Fisher–Yates).</summary>
    public void Shuffle<T>(IList<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        for (int i = items.Count - 1; i > 0; i--)
        {
            int j = Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }

    /// <summary>Picks one element of <paramref name="items"/>.</summary>
    public T Pick<T>(IReadOnlyList<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return items[Next(items.Count)];
    }

    private static ulong Mix(ref ulong state)
    {
        state += 0x9E3779B97F4A7C15;
        ulong z = state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EB;
        return z ^ (z >> 31);
    }
}
