namespace MBank.FaqGenerator;

/// <summary>Entry point of mBank.FaqGenerator — a thin layer over LegalAgent.Downloads (contracts/cli.md).</summary>
public static class Program
{
    /// <summary>Process entry point.</summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>Exit code.</returns>
    public static async Task<int> Main(string[] args)
    {
        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancellation.Cancel();
        };

        var environment = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            environment[(string)entry.Key] = entry.Value as string;
        }

        return await RunAsync(args, Console.In, Console.Out, Console.Error, environment, AppContext.BaseDirectory, null, cancellation.Token)
            .ConfigureAwait(false);
    }

    /// <summary>Runs the application without touching the console, the process environment or the network.</summary>
    /// <param name="args">Command-line arguments.</param>
    /// <param name="stdin">Standard input (address prompts).</param>
    /// <param name="stdout">Standard output.</param>
    /// <param name="stderr">Standard error.</param>
    /// <param name="environment">Environment variables (<c>FAQGEN__…</c>).</param>
    /// <param name="configDirectory">Directory with <c>appsettings*.json</c>.</param>
    /// <param name="handler">HTTP handler (tests); <c>null</c> uses the network.</param>
    /// <param name="cancellationToken">Cancellation (Ctrl+C).</param>
    /// <returns>Exit code (contracts/cli.md).</returns>
    public static Task<int> RunAsync(
        string[] args,
        TextReader stdin,
        TextWriter stdout,
        TextWriter stderr,
        IReadOnlyDictionary<string, string?> environment,
        string configDirectory,
        HttpMessageHandler? handler = null,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}
