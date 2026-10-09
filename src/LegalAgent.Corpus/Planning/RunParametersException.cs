namespace LegalAgent.Corpus.Planning;

/// <summary>Invalid run parameters (a field out of range, an unknown field, or malformed JSON).</summary>
public sealed class RunParametersException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="RunParametersException"/> class.</summary>
    public RunParametersException()
    {
    }

    /// <summary>Initializes a new instance with a message.</summary>
    /// <param name="message">The message.</param>
    public RunParametersException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with a message and an inner exception.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The inner exception.</param>
    public RunParametersException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Initializes a new instance naming the invalid field.</summary>
    /// <param name="field">The camelCase JSON name of the field.</param>
    /// <param name="message">The message.</param>
    public RunParametersException(string? field, string message)
        : base(field is null ? message : $"{field}: {message}")
    {
        Field = field;
    }

    /// <summary>Gets the camelCase JSON name of the invalid field, when known.</summary>
    public string? Field { get; }
}
