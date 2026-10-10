using LegalAgent.Faq.Model;

namespace LegalAgent.Faq;

/// <summary>Kind of a model service failure.</summary>
public enum FaqServiceErrorKind
{
    /// <summary>The key was rejected (401/403).</summary>
    Authentication,

    /// <summary>The deployment does not exist (404).</summary>
    DeploymentNotFound,

    /// <summary>Too many requests (429).</summary>
    RateLimited,

    /// <summary>The content filter blocked the request.</summary>
    ContentFiltered,

    /// <summary>The request exceeds the context length of the model.</summary>
    InputTooLong,

    /// <summary>Server error (5xx).</summary>
    ServiceUnavailable,

    /// <summary>No response in time.</summary>
    Timeout,

    /// <summary>Connection failure.</summary>
    Network,

    /// <summary>Any other failure.</summary>
    Other,
}

/// <summary>The model service failed.</summary>
public sealed class FaqServiceException : Exception
{
    /// <summary>Creates the exception.</summary>
    public FaqServiceException()
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    public FaqServiceException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and a cause.</summary>
    public FaqServiceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Creates the exception for a failed request.</summary>
    /// <param name="kind">Kind of the failure.</param>
    /// <param name="step">Step of the request.</param>
    /// <param name="documentId">Document of the candidate request; <c>null</c> for the selection.</param>
    /// <param name="statusCode">HTTP status, if any.</param>
    /// <param name="message">Polish message; may contain an excerpt of the service response.</param>
    /// <param name="innerException">The cause.</param>
    public FaqServiceException(
        FaqServiceErrorKind kind,
        FaqStep step,
        string? documentId,
        int? statusCode,
        string message,
        Exception? innerException)
        : base(message, innerException)
    {
        Kind = kind;
        Step = step;
        DocumentId = documentId;
        StatusCode = statusCode;
    }

    /// <summary>Kind of the failure.</summary>
    public FaqServiceErrorKind Kind { get; } = FaqServiceErrorKind.Other;

    /// <summary>Step of the request.</summary>
    public FaqStep Step { get; }

    /// <summary>Document of the candidate request; <c>null</c> for the selection.</summary>
    public string? DocumentId { get; }

    /// <summary>HTTP status, if any.</summary>
    public int? StatusCode { get; }
}
