namespace LegalAgent.Corpus;

/// <summary>A planning or generation error (CLI exit code 3).</summary>
public sealed class CorpusGenerationException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="CorpusGenerationException"/> class.</summary>
    public CorpusGenerationException()
    {
    }

    /// <summary>Initializes a new instance with a message.</summary>
    /// <param name="message">The message.</param>
    public CorpusGenerationException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with a message and an inner exception.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The inner exception.</param>
    public CorpusGenerationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Gets or sets the id of the document being planned or generated, when known.</summary>
    public string? DocumentId { get; init; }

    /// <summary>Gets or sets the id of the template involved, when known.</summary>
    public string? Template { get; init; }
}
