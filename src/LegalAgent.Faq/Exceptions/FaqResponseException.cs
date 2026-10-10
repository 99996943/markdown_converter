using LegalAgent.Faq.Model;

namespace LegalAgent.Faq;

/// <summary>The model response does not satisfy the rules (data-model.md, „Reguły sprawdzania”).</summary>
public sealed class FaqResponseException : Exception
{
    /// <summary>Creates the exception.</summary>
    public FaqResponseException()
    {
        Problems = [];
    }

    /// <summary>Creates the exception with a message.</summary>
    public FaqResponseException(string message)
        : base(message)
    {
        Problems = [];
    }

    /// <summary>Creates the exception with a message and a cause.</summary>
    public FaqResponseException(string message, Exception innerException)
        : base(message, innerException)
    {
        Problems = [];
    }

    /// <summary>Creates the exception for a rejected response.</summary>
    /// <param name="step">Step of the response.</param>
    /// <param name="documentId">Document of the candidate response; <c>null</c> for the selection.</param>
    /// <param name="problems">All problems found.</param>
    public FaqResponseException(FaqStep step, string? documentId, IReadOnlyList<string> problems)
        : base(FaqMessages.ResponseRejected(step, documentId, problems))
    {
        Step = step;
        DocumentId = documentId;
        Problems = problems;
    }

    /// <summary>Step of the response.</summary>
    public FaqStep Step { get; }

    /// <summary>Document of the candidate response; <c>null</c> for the selection.</summary>
    public string? DocumentId { get; }

    /// <summary>All problems found, e.g. „pozycja 4: dokument D9 nie istnieje”.</summary>
    public IReadOnlyList<string> Problems { get; }
}
