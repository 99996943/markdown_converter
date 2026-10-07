namespace LegalAgent.PdfParser.Cli;

/// <summary>Punkt wejścia CLI.</summary>
public static class Program
{
    /// <summary>Runs the CLI without touching the console or process environment.</summary>
    /// <param name="args">Command-line arguments.</param>
    /// <param name="stdout">Standard output.</param>
    /// <param name="stderr">Standard error.</param>
    /// <param name="environment">Environment variables.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The process exit code.</returns>
    public static Task<int> RunAsync(
        string[] args,
        TextWriter stdout,
        TextWriter stderr,
        IReadOnlyDictionary<string, string?> environment,
        CancellationToken cancellationToken = default) => throw new NotImplementedException();

    private static int Main(string[] args)
    {
        _ = args;
        return 0;
    }
}
