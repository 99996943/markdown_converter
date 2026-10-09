using LegalAgent.PdfParser;

namespace LegalAgent.Corpus.Planning;

/// <summary>How many documents of a poison kind to produce per type.</summary>
/// <param name="Kind">The poison kind id.</param>
/// <param name="PerType">The number of poisoned documents per type.</param>
public sealed record PoisonQuota(string Kind, int PerType);

/// <summary>An inclusive page range.</summary>
/// <param name="Min">The minimum page count.</param>
/// <param name="Max">The maximum page count.</param>
public sealed record PageRange(int Min, int Max);

/// <summary>The parameters of one corpus run (przebieg.json).</summary>
public sealed record RunParameters
{
    /// <summary>Gets the master seed.</summary>
    public ulong Seed { get; init; } = 20261008;

    /// <summary>Gets the reference date for the validity status.</summary>
    public DateOnly ReferenceDate { get; init; } = new(2026, 10, 1);

    /// <summary>Gets the type ids; null means all types.</summary>
    public IReadOnlyList<string>? Types { get; init; }

    /// <summary>Gets the number of documents per type.</summary>
    public int DocumentsPerType { get; init; } = 10;

    /// <summary>Gets the page range.</summary>
    public PageRange Pages { get; init; } = new(20, 30);

    /// <summary>Gets the percent of versioned documents.</summary>
    public int VersionedShare { get; init; } = 30;

    /// <summary>Gets the maximum number of versions.</summary>
    public int MaxVersions { get; init; } = 3;

    /// <summary>Gets the number of outdated documents per type.</summary>
    public int OutdatedPerType { get; init; } = 2;

    /// <summary>Gets the contradiction pairs per type.</summary>
    public int ContradictionPairsPerType { get; init; } = 1;

    /// <summary>Gets the contradiction pairs across types.</summary>
    public int CrossTypeContradictionPairs { get; init; } = 1;

    /// <summary>Gets the poison quotas.</summary>
    public IReadOnlyList<PoisonQuota> Poison { get; init; } = [];

    /// <summary>Gets a value indicating whether non-shared blocks must not repeat.</summary>
    public bool StrictUniqueness { get; init; } = true;

    /// <summary>Gets the maximum percent of shared blocks.</summary>
    public int MaxSharedShare { get; init; } = 20;

    /// <summary>Gets the output directory (relative, '/' separators).</summary>
    public string OutputDirectory { get; init; } = "corpus";

    /// <summary>Gets the content directory (relative, '/' separators).</summary>
    public string ContentDirectory { get; init; } = "corpus/zrodla";

    /// <summary>Gets the parser options.</summary>
    public PdfParserOptions ParserOptions { get; init; } = new();

    /// <summary>Gets the number of versioned documents per type.</summary>
    public int VersionedCount => throw new NotImplementedException();

    /// <summary>Validates the parameters.</summary>
    public void Validate() => throw new NotImplementedException();

    /// <summary>Loads parameters from a JSON file.</summary>
    /// <param name="path">The file path.</param>
    /// <returns>The parameters.</returns>
    public static RunParameters Load(string path) => throw new NotImplementedException();

    /// <summary>Saves the parameters to a JSON file.</summary>
    /// <param name="path">The file path.</param>
    public void Save(string path) => throw new NotImplementedException();

    /// <summary>Serialises the parameters exactly as <see cref="Save"/> writes them.</summary>
    /// <returns>The JSON text.</returns>
    public string ToJson() => throw new NotImplementedException();
}
