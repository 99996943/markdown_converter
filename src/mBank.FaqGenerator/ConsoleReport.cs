using System.Globalization;
using LegalAgent.Downloads.Model;

namespace MBank.FaqGenerator;

/// <summary>Prints progress and the summary (contracts/cli.md → „Postęp i podsumowanie”).</summary>
internal sealed class ConsoleReport(TextWriter stdout, int count) : IProgress<DownloadEvent>
{
    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");
    private readonly Lock gate = new();

    /// <summary>Formats a size in B, KB or MB with one decimal (pl-PL).</summary>
    public static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => string.Create(Polish, $"{bytes} B"),
        < 1024 * 1024 => string.Create(Polish, $"{bytes / 1024.0:0.0} KB"),
        _ => string.Create(Polish, $"{bytes / (1024.0 * 1024.0):0.0} MB"),
    };

    /// <inheritdoc />
    public void Report(DownloadEvent value)
    {
        ArgumentNullException.ThrowIfNull(value);
        string prefix = string.Create(CultureInfo.InvariantCulture, $"[{value.Index}/{count}]");
        string line = value.Result is { } result
            ? $"{prefix} pobrano {result.FileName} ({FormatSize(result.SizeBytes ?? 0)})"
            : $"{prefix} pobieranie {value.Address.AbsoluteUri}";

        lock (gate)
        {
            stdout.WriteLine(line);
        }
    }

    /// <summary>Prints the summary in index order.</summary>
    public void Summary(DownloadRun run, string directory)
    {
        int downloaded = run.Results.Count(r => r.Status == DownloadStatus.Downloaded);
        stdout.WriteLine(string.Create(Polish, $"Pobrano {downloaded} z {count} plików do {directory}:"));
        foreach (DownloadResult result in run.Results)
        {
            stdout.WriteLine(string.Create(Polish, $"  {result.Index}. {result.FileName} — {FormatSize(result.SizeBytes ?? 0)}"));
        }

        stdout.WriteLine($"Manifest: {run.ManifestPath}");
    }
}
