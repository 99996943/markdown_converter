using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LegalAgent.PdfParser.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LegalAgent.PdfParser.Cli;

/// <summary>Punkt wejścia CLI (cienka warstwa nad biblioteką; kontrakt: contracts/cli.md).</summary>
public static class Program
{
    private const string EnvPrefix = "PDFPARSER__";

    private const string Usage =
        """
        Użycie:
          legalagent-pdf convert <wejście.pdf> [-o|--output <wyjście.md>] [--report <raport.json>]
                                 [--no-page-markers] [--allow-partial]
          legalagent-pdf --help | --version

        Opcje:
          -o, --output <plik>   zapisz Markdown do pliku (domyślnie: stdout)
          --report <plik>       zapisz raport konwersji jako JSON
          --no-page-markers     nie wstawiaj znaczników stron
          --allow-partial       dopuść wynik niepełny, gdy strona jest nieczytelna
          -h, --help            pokaż tę pomoc
          --version             pokaż wersję

        Opcje heurystyk: zmienne środowiskowe PDFPARSER__<Grupa>__<Pole>, np. PDFPARSER__Limits__MaxPages=5000.

        Kody wyjścia: 0 sukces, 2 błędne argumenty, 3 nieprawidłowy/zaszyfrowany/bez tekstu PDF,
        4 nieczytelna strona, 5 przekroczony limit, 6 wynik niepełny, 130 przerwano, 1 błąd nieoczekiwany.
        """;

    private static readonly JsonSerializerOptions ReportJson = CreateReportJson();

    /// <summary>Runs the CLI without touching the console or the process environment.</summary>
    /// <param name="args">Command-line arguments.</param>
    /// <param name="stdout">Standard output.</param>
    /// <param name="stderr">Standard error.</param>
    /// <param name="environment">Environment variables.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The process exit code (see contracts/cli.md).</returns>
    public static async Task<int> RunAsync(
        string[] args,
        TextWriter stdout,
        TextWriter stderr,
        IReadOnlyDictionary<string, string?> environment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);
        ArgumentNullException.ThrowIfNull(environment);

        if (args.Length == 1 && args[0] is "--help" or "-h")
        {
            await stdout.WriteAsync(Usage + "\n").ConfigureAwait(false);
            return 0;
        }

        if (args.Length == 1 && args[0] == "--version")
        {
            await stdout.WriteAsync(VersionString() + "\n").ConfigureAwait(false);
            return 0;
        }

        CliArguments? parsed = await ParseAsync(args, stderr).ConfigureAwait(false);
        if (parsed is null)
        {
            return 2;
        }

        if (!File.Exists(parsed.Input))
        {
            await stderr.WriteAsync($"Błąd: plik wejściowy nie istnieje: {parsed.Input}\n").ConfigureAwait(false);
            return 2;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await ConvertAsync(parsed, stdout, stderr, environment, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await stderr.WriteAsync("Przerwano.\n").ConfigureAwait(false);
            return 130;
        }
        catch (PdfEncryptedException ex)
        {
            return await FailAsync(stderr, 3, ex.Message).ConfigureAwait(false);
        }
        catch (InvalidPdfException ex)
        {
            return await FailAsync(stderr, 3, ex.Message).ConfigureAwait(false);
        }
        catch (PdfNoTextException ex)
        {
            return await FailAsync(stderr, 3, ex.Message).ConfigureAwait(false);
        }
        catch (PdfPageReadException ex)
        {
            return await FailAsync(stderr, 4, ex.Message).ConfigureAwait(false);
        }
        catch (PdfLimitExceededException ex)
        {
            return await FailAsync(stderr, 5, ex.Message).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // The CLI boundary maps every unexpected error to exit code 1.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            return await FailAsync(stderr, 1, $"Nieoczekiwany błąd: {ex.Message}").ConfigureAwait(false);
        }
    }

    private static async Task<int> ConvertAsync(
        CliArguments args,
        TextWriter stdout,
        TextWriter stderr,
        IReadOnlyDictionary<string, string?> environment,
        CancellationToken cancellationToken)
    {
        IConfiguration configuration = BuildConfiguration(environment);

        var services = new ServiceCollection();
        services.AddLegalAgentPdfParser(options =>
        {
            configuration.Bind(options);
            if (args.NoPageMarkers)
            {
                options.Rendering.PageMarkers = false;
            }

            if (args.AllowPartial)
            {
                options.AllowPartialResult = true;
            }
        });

        await using ServiceProvider provider = services.BuildServiceProvider();
        IPdfMarkdownConverter converter = provider.GetRequiredService<IPdfMarkdownConverter>();

        PdfConversionResult result;
        await using (FileStream input = File.OpenRead(args.Input))
        {
            result = await converter.ConvertAsync(
                input,
                new PdfConversionRequest { SourceId = Path.GetFileName(args.Input) },
                cancellationToken).ConfigureAwait(false);
        }

        if (args.Output is null)
        {
            await stdout.WriteAsync(result.Markdown).ConfigureAwait(false);
        }
        else
        {
            await WriteAtomicAsync(args.Output, result.Markdown, cancellationToken).ConfigureAwait(false);
        }

        if (args.Report is not null)
        {
            string json = JsonSerializer.Serialize(result.Report, ReportJson) + "\n";
            await WriteAtomicAsync(args.Report, json, cancellationToken).ConfigureAwait(false);
        }

        foreach (ConversionWarning warning in result.Report.Warnings)
        {
            string page = warning.PageNumber is { } n ? $" (strona {n})" : string.Empty;
            await stderr.WriteAsync($"ostrzeżenie {warning.Code}{page}: {warning.Message}\n").ConfigureAwait(false);
        }

        if (!result.IsComplete)
        {
            await stderr.WriteAsync("Wynik niepełny: część stron nie została przekonwertowana.\n").ConfigureAwait(false);
            return 6;
        }

        return 0;
    }

    private static IConfiguration BuildConfiguration(IReadOnlyDictionary<string, string?> environment)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach ((string key, string? value) in environment)
        {
            if (key.StartsWith(EnvPrefix, StringComparison.OrdinalIgnoreCase))
            {
                values[key[EnvPrefix.Length..].Replace("__", ":", StringComparison.Ordinal)] = value;
            }
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static async Task WriteAtomicAsync(string target, string content, CancellationToken cancellationToken)
    {
        string fullPath = Path.GetFullPath(target);
        string directory = Path.GetDirectoryName(fullPath)!;
        string temp = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllTextAsync(temp, content, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
            File.Move(temp, fullPath, overwrite: true);
        }
        catch
        {
            try
            {
                File.Delete(temp);
            }
            catch (IOException)
            {
                // Best effort cleanup; the original error is more useful.
            }
            catch (UnauthorizedAccessException)
            {
                // Same as above.
            }

            throw;
        }
    }

    private static async Task<int> FailAsync(TextWriter stderr, int code, string message)
    {
        await stderr.WriteAsync($"Błąd: {message}\n").ConfigureAwait(false);
        return code;
    }

    private static async Task<CliArguments?> ParseAsync(string[] args, TextWriter stderr)
    {
        string? error = null;
        string? input = null;
        string? output = null;
        string? report = null;
        bool noMarkers = false;
        bool partial = false;

        if (args.Length == 0 || args[0] != "convert")
        {
            error = args.Length == 0 ? "brak polecenia." : $"nieznane polecenie: {args[0]}";
        }
        else
        {
            for (int i = 1; i < args.Length && error is null; i++)
            {
                string a = args[i];
                switch (a)
                {
                    case "-o" or "--output" or "--report":
                        if (i + 1 >= args.Length)
                        {
                            error = $"brak wartości dla opcji {a}.";
                        }
                        else if (a == "--report")
                        {
                            report = args[++i];
                        }
                        else
                        {
                            output = args[++i];
                        }

                        break;
                    case "--no-page-markers":
                        noMarkers = true;
                        break;
                    case "--allow-partial":
                        partial = true;
                        break;
                    default:
                        if (a.StartsWith('-') && a.Length > 1)
                        {
                            error = $"nieznana opcja: {a}";
                        }
                        else if (input is not null)
                        {
                            error = $"nadmiarowy argument: {a}";
                        }
                        else
                        {
                            input = a;
                        }

                        break;
                }
            }

            if (error is null && input is null)
            {
                error = "brak pliku wejściowego.";
            }
        }

        if (error is not null)
        {
            await stderr.WriteAsync($"Błąd: {error}\nUżyj --help, aby zobaczyć sposób użycia.\n").ConfigureAwait(false);
            return null;
        }

        return new CliArguments(input!, output, report, noMarkers, partial);
    }

    private static string VersionString()
    {
        Assembly assembly = typeof(Program).Assembly;
        string? informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return informational ?? assembly.GetName().Version?.ToString() ?? "0.0.0";
    }

    private static JsonSerializerOptions CreateReportJson()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new SecondsTimeSpanConverter());
        return options;
    }

    private static int Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        var environment = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            environment[(string)entry.Key] = entry.Value as string;
        }

        using Stream stdoutStream = Console.OpenStandardOutput();
        using Stream stderrStream = Console.OpenStandardError();
        using var stdout = new StreamWriter(stdoutStream, new UTF8Encoding(false)) { NewLine = "\n" };
        using var stderr = new StreamWriter(stderrStream, new UTF8Encoding(false)) { NewLine = "\n", AutoFlush = true };
        return RunAsync(args, stdout, stderr, environment, cts.Token).GetAwaiter().GetResult();
    }

    private sealed record CliArguments(string Input, string? Output, string? Report, bool NoPageMarkers, bool AllowPartial);

    private sealed class SecondsTimeSpanConverter : JsonConverter<TimeSpan>
    {
        public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            TimeSpan.FromSeconds(reader.GetDouble());

        public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options) =>
            writer.WriteNumberValue(value.TotalSeconds);
    }
}
