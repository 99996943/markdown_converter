namespace LegalAgent.Chunking.Model;

/// <summary>Metadata of a document supplied by the caller (spec 004, FR-210); copied to every chunk, never interpreted.</summary>
/// <param name="DocumentId">
/// Identifier of this version of the document, unique in the caller's index (for example "REG-05-w1"); must match
/// <c>^[A-Za-z0-9][A-Za-z0-9._-]{0,99}$</c>.
/// </param>
public sealed record DocumentMetadata(string DocumentId)
{
    /// <summary>Designation shared by all versions of the document (for example "BP/REG/05"); the identifier when null.</summary>
    public string? Designation { get; init; }

    /// <summary>Document type, for example "regulaminy" or "akty".</summary>
    public string? Type { get; init; }

    /// <summary>Title; takes precedence over the title detected by the parser.</summary>
    public string? Title { get; init; }

    /// <summary>Version number, greater than zero.</summary>
    public int? Version { get; init; }

    /// <summary>First day the document is in force.</summary>
    public DateOnly? ValidFrom { get; init; }

    /// <summary>Last day the document is in force; not earlier than <see cref="ValidFrom"/>.</summary>
    public DateOnly? ValidTo { get; init; }

    /// <summary>Status, for example "obowiazujacy" or "nieaktualny".</summary>
    public string? Status { get; init; }

    /// <summary><see cref="DocumentId"/> of the previous version.</summary>
    public string? PreviousVersion { get; init; }
}
