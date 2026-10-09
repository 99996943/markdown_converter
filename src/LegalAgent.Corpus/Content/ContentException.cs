namespace LegalAgent.Corpus.Content;

/// <summary>
/// Error in the content source files (<c>corpus/zrodla/</c>) or in a text template: names the file, the YAML
/// path and/or the position in the text. Maps to CLI exit code 2.
/// </summary>
public sealed class ContentException : Exception
{
    /// <summary>Creates the exception.</summary>
    public ContentException()
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    public ContentException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and an inner exception.</summary>
    public ContentException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Source file (relative to the content directory, '/' separators), when known.</summary>
    public string? File { get; init; }

    /// <summary>YAML path inside the file (e.g. <c>bloki[3].elementy[0].ustep</c>), when known.</summary>
    public string? YamlPath { get; init; }

    /// <summary>Zero-based character position in a template text, when known.</summary>
    public int? Position { get; init; }
}
