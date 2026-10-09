using LegalAgent.Corpus.Content;

namespace LegalAgent.Corpus.Planning;

/// <summary>Validity status of a document at the reference date.</summary>
public enum DocumentStatus
{
    /// <summary>The document is in force.</summary>
    Obowiazujacy,

    /// <summary>The document is outdated.</summary>
    Nieaktualny,
}

/// <summary>Why a fact value of a document differs from the catalogue value.</summary>
public enum OverrideReason
{
    /// <summary>A version change.</summary>
    Wersja,

    /// <summary>A planted contradiction.</summary>
    Sprzecznosc,

    /// <summary>A poison pattern.</summary>
    Zatrucie,
}

/// <summary>A per-document override of a fact value.</summary>
/// <param name="FactId">The fact id.</param>
/// <param name="Value">The overriding value.</param>
/// <param name="Reason">The reason of the override.</param>
public sealed record FactOverride(string FactId, FactValue Value, OverrideReason Reason);

/// <summary>A planted contradiction with another document (FR-122).</summary>
/// <param name="With">The id of the other document.</param>
/// <param name="FactId">The fact both documents state with different values.</param>
public sealed record PlannedContradiction(string With, string FactId);

/// <summary>The plan of one document.</summary>
public sealed record DocumentPlan
{
    /// <summary>Gets the document id, e.g. <c>REG-03</c>.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the type id.</summary>
    public required string Type { get; init; }

    /// <summary>Gets the type prefix.</summary>
    public required string Prefix { get; init; }

    /// <summary>Gets the designation, e.g. <c>BP/REG/03</c>.</summary>
    public required string Designation { get; init; }

    /// <summary>Gets the template id.</summary>
    public required string Template { get; init; }

    /// <summary>Gets the layout style id.</summary>
    public required string Layout { get; init; }

    /// <summary>Gets the version number.</summary>
    public int Version { get; init; } = 1;

    /// <summary>Gets the start of validity.</summary>
    public required DateOnly ValidFrom { get; init; }

    /// <summary>Gets the end of validity, if any.</summary>
    public DateOnly? ValidTo { get; init; }

    /// <summary>Gets the validity status.</summary>
    public DocumentStatus Status { get; init; } = DocumentStatus.Obowiazujacy;

    /// <summary>Gets the id of the previous version, if any.</summary>
    public string? PreviousVersionId { get; init; }

    /// <summary>Gets the ids of the non-shared optional blocks dealt to this document, in ordinal order.</summary>
    public required IReadOnlyList<string> BlockPool { get; init; }

    /// <summary>Gets the target page count.</summary>
    public required int TargetPages { get; init; }

    /// <summary>Gets the seed of the document's random stream.</summary>
    public required ulong Seed { get; init; }

    /// <summary>Gets the fact overrides.</summary>
    public IReadOnlyList<FactOverride> FactOverrides { get; init; } = [];

    /// <summary>Gets the start dates of the earlier versions (version 1 first), for the record card history.</summary>
    public IReadOnlyList<DateOnly> EarlierVersionStarts { get; init; } = [];

    /// <summary>Gets the contradictions planted with other documents.</summary>
    public IReadOnlyList<PlannedContradiction> Contradictions { get; init; } = [];
}

/// <summary>The plan of the whole corpus.</summary>
/// <param name="Documents">The documents, ordered by type and id.</param>
public sealed record CorpusPlan(IReadOnlyList<DocumentPlan> Documents);
