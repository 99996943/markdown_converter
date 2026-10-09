namespace LegalAgent.Corpus.Manifest;

/// <summary>Root of <c>corpus/manifest.json</c> (schema version 1).</summary>
/// <param name="Run">Run description.</param>
/// <param name="Documents">Document entries.</param>
public sealed record Manifest(ManifestRun Run, IReadOnlyList<ManifestDocument> Documents)
{
    /// <summary>Schema version written to the file.</summary>
    public const int SchemaVersion = 1;
}

/// <summary>The <c>run</c> object.</summary>
/// <param name="Seed">Seed.</param>
/// <param name="ReferenceDate">Reference date of the status.</param>
/// <param name="ParametersJson">Exact JSON object text of the run parameters.</param>
/// <param name="ParserVersion">Parser version.</param>
/// <param name="GeneratorVersion">Generator version.</param>
/// <param name="ContentHash">SHA-256 of the sources.</param>
public sealed record ManifestRun(
    ulong Seed,
    DateOnly ReferenceDate,
    string ParametersJson,
    string ParserVersion,
    string GeneratorVersion,
    string ContentHash)
{
    /// <summary>Gets the share of words in blocks repeated across documents in the whole run (non-strict runs only).</summary>
    public double? RepeatedWordShare { get; init; }
}

/// <summary>One entry of <c>documents[]</c>.</summary>
/// <param name="Id">Document id.</param>
/// <param name="Type">Document type.</param>
/// <param name="Title">Cover title.</param>
/// <param name="Designation">Designation from the content.</param>
/// <param name="Version">Version number.</param>
/// <param name="ValidFrom">Start of validity.</param>
/// <param name="ValidTo">End of validity.</param>
/// <param name="Status">Actual status: obowiazujacy or nieaktualny.</param>
/// <param name="PreviousVersion">Id of the previous version.</param>
/// <param name="Pdf">PDF path relative to corpus/.</param>
/// <param name="Markdown">Markdown path relative to corpus/.</param>
/// <param name="Pages">Page count.</param>
/// <param name="Template">Template id (synthetic only).</param>
/// <param name="Layout">Layout id (synthetic only).</param>
/// <param name="Seed">Document seed (synthetic only).</param>
/// <param name="SharedWordShare">Share of words in shared blocks.</param>
/// <param name="RepeatedWordShare">Share of words in repeated blocks.</param>
/// <param name="Changes">Changes against the previous version.</param>
/// <param name="Contradictions">Contradictions with other documents.</param>
/// <param name="Poison">Poison description (poisoned documents only).</param>
/// <param name="Source">Act info (acts only).</param>
/// <param name="Notes">Descriptive note.</param>
public sealed record ManifestDocument(
    string Id,
    string Type,
    string Title,
    string? Designation,
    int? Version,
    DateOnly? ValidFrom,
    DateOnly? ValidTo,
    string Status,
    string? PreviousVersion,
    string Pdf,
    string Markdown,
    int Pages,
    string? Template = null,
    string? Layout = null,
    ulong? Seed = null,
    double? SharedWordShare = null,
    double? RepeatedWordShare = null,
    IReadOnlyList<VersionChange>? Changes = null,
    IReadOnlyList<Contradiction>? Contradictions = null,
    PoisonInfo? Poison = null,
    ActInfo? Source = null,
    string? Notes = null)
{
    /// <summary>Path of the chunk file relative to corpus/ (spec 004), written after <see cref="Markdown"/>.</summary>
    public string? Chunks { get; init; }
}

/// <summary>A change against the previous version.</summary>
/// <param name="Unit">Unit (for example "§ 12 ust. 3").</param>
/// <param name="Page">Page in this document.</param>
/// <param name="Fact">Fact id, if any.</param>
/// <param name="Before">Wording before.</param>
/// <param name="After">Wording after.</param>
public sealed record VersionChange(string Unit, int Page, string? Fact, string Before, string After);

/// <summary>A contradiction with another document.</summary>
/// <param name="With">Id of the related document.</param>
/// <param name="Unit">Unit.</param>
/// <param name="Page">Page in this document.</param>
/// <param name="Fact">Fact id, if any.</param>
/// <param name="This">Wording here.</param>
/// <param name="Other">Wording there.</param>
public sealed record Contradiction(string With, string Unit, int Page, string? Fact, string This, string Other);

/// <summary>Poison description.</summary>
/// <param name="Kind">Kind of problem.</param>
/// <param name="Imitates">Id of the imitated document.</param>
/// <param name="Description">Description.</param>
/// <param name="Places">Places of the poison.</param>
public sealed record PoisonInfo(string Kind, string Imitates, string Description, IReadOnlyList<PoisonPlace> Places);

/// <summary>A place of the poison in the PDF.</summary>
/// <param name="Page">Page.</param>
/// <param name="Unit">Unit.</param>
/// <param name="Element">Element kind.</param>
/// <param name="Text">Verbatim text.</param>
/// <param name="Goal">Goal (AI instructions only).</param>
public sealed record PoisonPlace(int Page, string Unit, string Element, string Text, string? Goal);

/// <summary>Source information of an act.</summary>
/// <param name="Journal">Journal reference.</param>
/// <param name="ConsolidatedTextDate">Consolidated text date.</param>
/// <param name="Url">Source URL.</param>
/// <param name="DownloadedOn">Download date.</param>
/// <param name="Notes">Notes.</param>
public sealed record ActInfo(string Journal, DateOnly ConsolidatedTextDate, string Url, DateOnly DownloadedOn, string? Notes);
