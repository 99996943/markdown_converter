using LegalAgent.Corpus.Content;

namespace LegalAgent.Corpus.Validation;

/// <summary>A failed corpus check.</summary>
/// <param name="Check">Name of the check.</param>
/// <param name="Message">Human readable description.</param>
/// <param name="DocumentId">Document concerned, if any.</param>
/// <param name="BlockId">Block concerned, if any.</param>
/// <param name="Template">Template concerned, if any.</param>
public sealed record CheckViolation(string Check, string Message, string? DocumentId = null, string? BlockId = null, string? Template = null);

/// <summary>Rendered plain text of a document (or one of its blocks).</summary>
/// <param name="DocumentId">Document id.</param>
/// <param name="BlockId">Block id or null.</param>
/// <param name="Text">Plain text.</param>
public sealed record RenderedText(string DocumentId, string? BlockId, string Text);

/// <summary>Rendered block of a base document.</summary>
/// <param name="DocumentId">Document id.</param>
/// <param name="BlockId">Block id.</param>
/// <param name="Shared">Whether the block is shared.</param>
/// <param name="Text">Plain text.</param>
public sealed record RenderedBlock(string DocumentId, string BlockId, bool Shared, string Text);

/// <summary>Word counts of a document.</summary>
/// <param name="DocumentId">Document id.</param>
/// <param name="TotalWords">All words.</param>
/// <param name="SharedWords">Words in shared blocks.</param>
public sealed record DocumentWords(string DocumentId, int TotalWords, int SharedWords);

/// <summary>Reference the composer could not resolve.</summary>
/// <param name="DocumentId">Document id.</param>
/// <param name="BlockId">Block id.</param>
/// <param name="Target">Unresolved target.</param>
public sealed record UnresolvedReference(string DocumentId, string BlockId, string Target);

/// <summary>Corpus-level validation checks (FR-103a, FR-103b, FR-105, FR-110-FR-112, FR-114).</summary>
public static class CorpusChecks
{
    /// <summary>FR-105: forbidden names, case- and diacritic-insensitive.</summary>
    public static IReadOnlyList<CheckViolation> ForbiddenNames(IEnumerable<RenderedText> texts, IReadOnlyList<string> forbiddenNames)
        => throw new NotImplementedException();

    /// <summary>FR-103b: non-shared block text must not repeat across documents.</summary>
    public static IReadOnlyList<CheckViolation> Uniqueness(IEnumerable<RenderedBlock> blocks)
        => throw new NotImplementedException();

    /// <summary>FR-103a: share of shared words per document.</summary>
    public static IReadOnlyList<CheckViolation> SharedShare(IEnumerable<DocumentWords> documents, double maxShare)
        => throw new NotImplementedException();

    /// <summary>FR-114: unresolved references.</summary>
    public static IReadOnlyList<CheckViolation> References(IEnumerable<UnresolvedReference> unresolved)
        => throw new NotImplementedException();

    /// <summary>FR-110-FR-112: required elements of each template's type.</summary>
    public static IReadOnlyList<CheckViolation> TemplateStructure(ContentLibrary library)
        => throw new NotImplementedException();
}
