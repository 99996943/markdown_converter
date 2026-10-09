namespace LegalAgent.Downloads;

/// <summary>The output directory cannot be created, the manifest cannot be written or an old file cannot be removed.</summary>
public sealed class DownloadDirectoryException : IOException
{
    /// <summary>Creates the exception.</summary>
    public DownloadDirectoryException()
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">User-facing message.</param>
    public DownloadDirectoryException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and a cause.</summary>
    /// <param name="message">User-facing message.</param>
    /// <param name="innerException">The cause.</param>
    public DownloadDirectoryException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
