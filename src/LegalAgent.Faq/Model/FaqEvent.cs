namespace LegalAgent.Faq.Model;

/// <summary>Kind of a progress event.</summary>
public enum FaqEventKind
{
    /// <summary>A candidate request for a document is about to be sent.</summary>
    CandidatesStarted,

    /// <summary>The candidates of a document were accepted.</summary>
    CandidatesFinished,

    /// <summary>A candidate was dropped because it is not grounded in its unit (T067d); the reason is in Detail.</summary>
    CandidateDropped,

    /// <summary>The selection request is about to be sent.</summary>
    SelectionStarted,

    /// <summary>The selection was rejected and one correction is requested (T067i); the problems are in Detail.</summary>
    SelectionCorrection,

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
/// <param name="Detail">
/// Why a candidate was dropped (CandidateDropped), starting with „kandydat &lt;id&gt;:”; the problems of the rejected
/// selection (SelectionCorrection).
/// </param>
public sealed record FaqEvent(
    FaqEventKind Kind,
    string? DocumentId,
    int Characters,
    int EstimatedTokens,
    int? Count,
    FaqUsage? Usage,
    string? Detail = null);
