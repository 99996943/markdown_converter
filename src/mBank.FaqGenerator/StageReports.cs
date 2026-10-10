using System.Globalization;
using LegalAgent.Faq.Conversion.Model;
using LegalAgent.Faq.Model;
using LegalAgent.PdfParser.Model;

namespace MBank.FaqGenerator;

/// <summary>Prints the conversion progress and summary (contracts/cli.md → „Przebieg na stdout”).</summary>
internal sealed class ConversionConsoleReport(TextWriter stdout, TextWriter stderr, int count) : IProgress<ConversionEvent>
{
    /// <summary>Prints the heading of the stage.</summary>
    public void Start()
    {
        stdout.WriteLine();
        stdout.WriteLine("Konwersja do Markdown:");
    }

    /// <inheritdoc />
    public void Report(ConversionEvent value)
    {
        ArgumentNullException.ThrowIfNull(value);
        string prefix = string.Create(CultureInfo.InvariantCulture, $"[{value.Index}/{count}]");
        if (value is { Kind: ConversionEventKind.Converted, Document: { } document })
        {
            string details = PolishText.Count(document.PageCount, "strona", "strony", "stron");
            if (document.Warnings.Count > 0)
            {
                details += ", " + PolishText.Count(document.Warnings.Count, "ostrzeżenie", "ostrzeżenia", "ostrzeżeń");
            }

            stdout.WriteLine($"{prefix} {document.PdfFileName} → {document.MarkdownFileName} ({details})");
            foreach (ConversionWarning warning in document.Warnings)
            {
                string page = warning.PageNumber is { } n ? string.Create(CultureInfo.InvariantCulture, $" (strona {n})") : string.Empty;
                stdout.WriteLine($"      ostrzeżenie {warning.Code}{page}: {warning.Message}");
            }
        }
        else if (value is { Kind: ConversionEventKind.Failed, Failure: { } failure })
        {
            stdout.WriteLine($"{prefix} {failure.PdfFileName} — błąd konwersji");
            stderr.WriteLine($"Błąd konwersji {failure.PdfFileName}: {failure.Reason}");
        }
    }

    /// <summary>Prints the summary line and the removed files.</summary>
    public void Summary(ConversionRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        stdout.WriteLine(string.Create(PolishText.Culture, $"Przekonwertowano {run.Documents.Count} z {count} plików."));
        if (run.RemovedMarkdownFiles.Count > 0)
        {
            stdout.WriteLine($"Usunięto pliki Markdown spoza bieżącej listy: {string.Join(", ", run.RemovedMarkdownFiles)}");
        }
    }
}

/// <summary>Prints the FAQ progress and the final line (contracts/cli.md → „Przebieg na stdout”).</summary>
internal sealed class FaqConsoleReport(TextWriter stdout, IReadOnlyDictionary<string, string> fileNames) : IProgress<FaqEvent>
{
    /// <summary>Prints the heading of the stage.</summary>
    public void Start(string model, string deployment)
    {
        stdout.WriteLine();
        stdout.WriteLine($"Generowanie FAQ ({model}, wdrożenie {deployment}):");
    }

    /// <inheritdoc />
    public void Report(FaqEvent value)
    {
        ArgumentNullException.ThrowIfNull(value);
        string? line = value.Kind switch
        {
            FaqEventKind.CandidatesStarted =>
                $"[{value.DocumentId}] kandydaci z {fileNames.GetValueOrDefault(value.DocumentId ?? string.Empty, value.DocumentId ?? string.Empty)} — {Size(value)}…",
            FaqEventKind.CandidatesFinished =>
                $"[{value.DocumentId}] {Candidates(value.Count ?? 0)} ({Usage(value.Usage)})",
            FaqEventKind.SelectionStarted => $"[wybór] {Candidates(value.Count ?? 0)} — {Size(value)}…",
            FaqEventKind.SelectionFinished => $"[wybór] {Questions(value.Count ?? 0)} ({Usage(value.Usage)})",
            _ => null,
        };
        if (line is not null)
        {
            stdout.WriteLine(line);
        }
    }

    /// <summary>Prints the final line.</summary>
    public void Saved(string path, FaqResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        string usage = result.Usage is { } u
            ? $"łącznie wejście {PolishText.Number(u.InputTokens)}, wyjście {PolishText.Number(u.OutputTokens)} tokenów"
            : "zużycie tokenów nieznane";
        stdout.WriteLine();
        stdout.WriteLine($"Zapisano FAQ: {path} ({Questions(result.Items.Count)}; {usage})");
    }

    private static string Size(FaqEvent value) =>
        $"{PolishText.Count(value.Characters, "znak", "znaki", "znaków")} (~{PolishText.Number(value.EstimatedTokens)} tokenów)";

    private static string Candidates(int count) => PolishText.Count(count, "kandydat", "kandydatów", "kandydatów");

    private static string Questions(int count) => PolishText.Count(count, "pytanie", "pytania", "pytań");

    private static string Usage(FaqUsage? usage) => usage is null
        ? "zużycie tokenów nieznane"
        : $"wejście {PolishText.Number(usage.InputTokens)}, wyjście {PolishText.Number(usage.OutputTokens)} tokenów";
}
