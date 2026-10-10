namespace LegalAgent.Faq.Model;

/// <summary>Kind of a progress event.</summary>
public enum FaqEventKind
{
    /// <summary>A candidate request for a document is about to be sent.</summary>
    CandidatesStarted,

    /// <summary>The candidates of a document were accepted.</summary>
    CandidatesFinished,

    /// <summary>The selection request is about to be sent.</summary>
    SelectionStarted,

    /// <summary>The selection was accepted.</summary>
    SelectionFinished,
}

/// <summary>Progress of the FAQ generation.</summary>
/// <param name="Kind">Kind of the event.</param>
/// <param name="DocumentId">Document identifier for the candidate steps; <c>null</c> for the selection.</param>
/// <param name="Characters">Size of the sent content (Started events).</param>
/// <param name="EstimatedTokens">Estimated tokens of the sent content (Started events).</param>
/// <param name="Count">Accepted candidates or items (Finished); input candidates (SelectionStarted).</param>
/// <param name="Usage">Token usage of the request (Finished events); <c>null</c> when unknown.</param>
public sealed record FaqEvent(
    FaqEventKind Kind,
    string? DocumentId,
    int Characters,
    int EstimatedTokens,
    int? Count,
    FaqUsage? Usage);
