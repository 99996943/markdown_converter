namespace LegalAgent.Corpus.Cli;

/// <summary>Entry point of the corpus generator command-line application.</summary>
public static class Program
{
    /// <summary>Runs the application and returns the process exit code.</summary>
    public static int Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        return Run(args, Console.Out, Console.Error, Directory.GetCurrentDirectory());
    }

    /// <summary>Runs the application against explicit streams and base directory (testable entry point).</summary>
    /// <param name="args">Command-line arguments.</param>
    /// <param name="stdout">Standard output.</param>
    /// <param name="stderr">Standard error.</param>
    /// <param name="baseDirectory">Directory against which relative paths are resolved.</param>
    /// <returns>The exit code.</returns>
    public static int Run(string[] args, TextWriter stdout, TextWriter stderr, string baseDirectory)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);
        ArgumentNullException.ThrowIfNull(baseDirectory);
        return -1;
    }
}
