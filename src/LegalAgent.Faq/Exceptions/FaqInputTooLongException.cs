namespace LegalAgent.Faq;

/// <summary>A document is too long for the model (checked before any request).</summary>
public sealed class FaqInputTooLongException : Exception
{
    /// <summary>Creates the exception.</summary>
    public FaqInputTooLongException()
    {
        DocumentName = string.Empty;
    }

    /// <summary>Creates the exception with a message.</summary>
    public FaqInputTooLongException(string message)
        : base(message)
    {
        DocumentName = string.Empty;
    }

    /// <summary>Creates the exception with a message and a cause.</summary>
    public FaqInputTooLongException(string message, Exception innerException)
        : base(message, innerException)
    {
        DocumentName = string.Empty;
    }

    /// <summary>Creates the exception for a document.</summary>
    /// <param name="documentName">Document name.</param>
    /// <param name="characters">Characters of the document.</param>
    /// <param name="estimatedTokens">Estimated tokens.</param>
    /// <param name="limit">Configured limit of tokens.</param>
    public FaqInputTooLongException(string documentName, int characters, int estimatedTokens, int limit)
        : base(FaqMessages.InputTooLong(documentName, characters, estimatedTokens, limit))
    {
        DocumentName = documentName;
        Characters = characters;
        EstimatedTokens = estimatedTokens;
        Limit = limit;
    }

    /// <summary>Document name.</summary>
    public string DocumentName { get; }

    /// <summary>Characters of the document.</summary>
    public int Characters { get; }

    /// <summary>Estimated tokens.</summary>
    public int EstimatedTokens { get; }

    /// <summary>Configured limit of tokens.</summary>
    public int Limit { get; }
}
