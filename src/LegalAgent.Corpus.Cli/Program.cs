using System.Globalization;
using LegalAgent.Corpus.Content;
using LegalAgent.Corpus.Output;
using LegalAgent.Corpus.Planning;

namespace LegalAgent.Corpus.Cli;

/// <summary>Entry point of the corpus generator command-line application (contract: contracts/cli.md).</summary>
public static class Program
{
    private const string DefaultParamsFile = "corpus/przebieg.json";

    private const string Usage =
        """
        Użycie:
          legalagent-corpus generate [opcje]
          legalagent-corpus verify [opcje]
          legalagent-corpus check --template <id> [--out <katalog>] [--pages <min>-<max>]
                                  [--content <katalog>] [--params <plik>]
          legalagent-corpus --help | --version

        Polecenia:
          generate   planuje, składa PDF, konwertuje do Markdown, zapisuje manifest
          verify     odtwarza korpus w pamięci i porównuje z plikami na dysku (niczego nie zapisuje)
          check      sprawdza jeden szablon (zakres stron w każdym układzie, naruszenia reguł)

        Opcje (generate, verify):
          --params <plik>       parametry przebiegu (domyślnie corpus/przebieg.json, jeśli istnieje)
          --out <katalog>       katalog wyjściowy korpusu
          --content <katalog>   katalog źródeł treści
          --seed <n>            ziarno generatora
          --types <a,b>         lista typów dokumentów
          --count <n>           liczba dokumentów na typ
          --pages <min>-<max>   zakres stron dokumentu
          --truth <katalog>     zapis prawdy referencyjnej (JSON na dokument)
          --save-params         po udanym generate zapisz parametry do <out>/przebieg.json

        Kody wyjścia: 0 sukces, 1 różnice (verify), 2 błędne parametry lub źródła, 3 naruszenie reguł korpusu,
          4 nazwa zabroniona, 5 błąd konwersji, 6 błąd wejścia/wyjścia.
        """;

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

        try
        {
            if (args.Length == 0)
            {
                stderr.WriteLine("błąd: brak polecenia. Użyj --help.");
                return 2;
            }

            switch (args[0])
            {
                case "--help":
                case "-h":
                case "help":
                    stdout.WriteLine(Usage);
                    return 0;
                case "--version":
                    stdout.WriteLine(CorpusGenerator.GeneratorVersion);
                    return 0;
                case "generate":
                    return RunCorpus(args[1..], generate: true, stdout, stderr, baseDirectory);
                case "verify":
                    return RunCorpus(args[1..], generate: false, stdout, stderr, baseDirectory);
                case "check":
                    return RunCheck(args[1..], stdout, stderr, baseDirectory);
                default:
                    stderr.WriteLine($"błąd: nieznane polecenie '{args[0]}'. Użyj --help.");
                    return 2;
            }
        }
        catch (UsageException ex)
        {
            stderr.WriteLine("błąd: " + ex.Message);
            return 2;
        }
        catch (RunParametersException ex)
        {
            stderr.WriteLine("błąd: nieprawidłowe parametry: " + ex.Message);
            return 2;
        }
        catch (ContentException ex)
        {
            string where = string.Join(", ", new[]
            {
                ex.File is null ? null : "plik " + ex.File,
                ex.YamlPath is null ? null : "ścieżka YAML " + ex.YamlPath,
            }.Where(s => s is not null));
            stderr.WriteLine("błąd: źródła treści: " + (where.Length > 0 ? where + ": " : string.Empty) + ex.Message);
            return 2;
        }
        catch (CorpusGenerationException ex)
        {
            stderr.WriteLine("błąd: " + ex.Message);
            return 3;
        }
        catch (ForbiddenNameException ex)
        {
            stderr.WriteLine("błąd: " + ex.Message);
            return 4;
        }
        catch (ConversionFailedException ex)
        {
            stderr.WriteLine("błąd: " + ex.Message);
            return 5;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            stderr.WriteLine("błąd wejścia/wyjścia: " + ex.Message);
            return 6;
        }
    }

    private static int RunCorpus(string[] args, bool generate, TextWriter stdout, TextWriter stderr, string baseDirectory)
    {
        Options o = Options.Parse(args, ["--params", "--out", "--content", "--seed", "--types", "--count", "--pages", "--truth"], ["--save-params"]);
        if (o.Flag("--save-params") && !generate)
        {
            throw new UsageException("opcja --save-params dotyczy tylko polecenia generate");
        }

        RunParameters parameters = LoadParameters(o, baseDirectory);
        var options = new CorpusGeneratorOptions
        {
            BaseDirectory = baseDirectory,
            Progress = new SyncProgress(stdout),
            TruthDirectory = o.Value("--truth") is { } truth ? Resolve(baseDirectory, truth) : null,
        };

        if (generate)
        {
            GenerationResult result = CorpusGenerator.GenerateAsync(parameters, options).GetAwaiter().GetResult();
            if (o.Flag("--save-params"))
            {
                string path = Path.Combine(Resolve(baseDirectory, parameters.OutputDirectory), "przebieg.json");
                parameters.Save(path);
            }

            stdout.WriteLine(string.Create(CultureInfo.InvariantCulture, $"gotowe: {result.Documents.Count} dokumentów, usunięto nieaktualnych plików: {result.Deleted.Count}"));
            return 0;
        }

        CorpusDiff diff = CorpusGenerator.VerifyAsync(parameters, options).GetAwaiter().GetResult();
        foreach (string path in diff.Different)
        {
            stderr.WriteLine("różni się: " + path);
        }

        foreach (string path in diff.Missing)
        {
            stderr.WriteLine("brak: " + path);
        }

        foreach (string path in diff.Extra)
        {
            stderr.WriteLine("nadmiarowy: " + path);
        }

        if (diff.Different.Count + diff.Missing.Count + diff.Extra.Count > 0)
        {
            stderr.WriteLine("błąd: korpus na dysku różni się od odtworzonego.");
            return 1;
        }

        stdout.WriteLine("zgodne: korpus na dysku jest identyczny z odtworzonym.");
        return 0;
    }

    private static int RunCheck(string[] args, TextWriter stdout, TextWriter stderr, string baseDirectory)
    {
        Options o = Options.Parse(args, ["--template", "--out", "--pages", "--content", "--params"], []);
        string template = o.Value("--template") ?? throw new UsageException("brak wymaganej opcji --template <id>");
        RunParameters parameters = LoadParameters(o, baseDirectory, applyOut: false);
        string? output = o.Value("--out") is { } outDir ? Resolve(baseDirectory, outDir) : null;
        var options = new CorpusGeneratorOptions { BaseDirectory = baseDirectory, Progress = new SyncProgress(stdout) };
        TemplateCheckReport report = CorpusGenerator.CheckTemplateAsync(parameters, template, output, options).GetAwaiter().GetResult();
        Print(report, stdout);
        bool ok = report.Violations.Count == 0 && report.Layouts.All(l => l.FittedPages is not null);
        if (!ok)
        {
            stderr.WriteLine("błąd: szablon nie spełnia wymagań (patrz raport).");
        }

        return ok ? 0 : 3;
    }

    private static void Print(TemplateCheckReport report, TextWriter w)
    {
        CultureInfo c = CultureInfo.InvariantCulture;
        w.WriteLine(string.Create(c, $"Szablon: {report.Template}"));
        w.WriteLine(string.Create(c, $"Typ: {report.Type}"));
        w.WriteLine(string.Create(c, $"Słowa: wymagane {report.RequiredWords}, opcjonalne {report.OptionalWords}"));
        w.WriteLine(string.Create(c, $"{"Układ",-24} {"Strony min-max",-15} {"Opcj.",6} {"Dopasowanie",-24} {"Wspólne",8}"));
        foreach (LayoutCheck l in report.Layouts)
        {
            string fit = l.FittedPages is { } pages ? pages.ToString(c) + " str." : "błąd: " + l.FitError;
            w.WriteLine(string.Create(c, $"{l.Layout,-24} {l.MinPages + "-" + l.MaxPages,-15} {l.OptionalCapacity,6} {fit,-24} {l.SharedShare * 100:0.#}%"));
        }

        if (report.Violations.Count > 0)
        {
            w.WriteLine("Naruszenia:");
            foreach (var v in report.Violations)
            {
                w.WriteLine("  " + v.Message);
            }
        }

        foreach (string file in report.WrittenFiles)
        {
            w.WriteLine("zapisano: " + file);
        }
    }

    private static RunParameters LoadParameters(Options o, string baseDirectory, bool applyOut = true)
    {
        RunParameters parameters;
        if (o.Value("--params") is { } file)
        {
            string path = Resolve(baseDirectory, file);
            if (!File.Exists(path))
            {
                throw new UsageException($"nie znaleziono pliku parametrów '{path}'");
            }

            parameters = RunParameters.Load(path);
        }
        else
        {
            string path = Resolve(baseDirectory, DefaultParamsFile);
            parameters = File.Exists(path) ? RunParameters.Load(path) : new RunParameters();
        }

        if (o.Value("--seed") is { } seed)
        {
            parameters = parameters with { Seed = ParseUlong("--seed", seed) };
        }

        if (o.Value("--types") is { } types)
        {
            parameters = parameters with { Types = types.Split(',', StringSplitOptions.TrimEntries) };
        }

        if (o.Value("--count") is { } count)
        {
            parameters = parameters with { DocumentsPerType = ParseInt("--count", count) };
        }

        if (o.Value("--pages") is { } pages)
        {
            parameters = parameters with { Pages = ParsePages(pages) };
        }

        if (applyOut && o.Value("--out") is { } outDir)
        {
            parameters = parameters with { OutputDirectory = outDir };
        }

        if (o.Value("--content") is { } content)
        {
            parameters = parameters with { ContentDirectory = content };
        }

        parameters.Validate();
        return parameters;
    }

    private static string Resolve(string baseDirectory, string path) =>
        Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(baseDirectory, path));

    private static int ParseInt(string option, string text) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int value)
            ? value
            : throw new UsageException($"opcja {option} wymaga liczby całkowitej nieujemnej, otrzymano '{text}'");

    private static ulong ParseUlong(string option, string text) =>
        ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out ulong value)
            ? value
            : throw new UsageException($"opcja {option} wymaga liczby całkowitej nieujemnej, otrzymano '{text}'");

    private static PageRange ParsePages(string text)
    {
        string[] parts = text.Split('-');
        if (parts.Length != 2)
        {
            throw new UsageException($"opcja --pages wymaga postaci <min>-<max>, otrzymano '{text}'");
        }

        return new PageRange(ParseInt("--pages", parts[0]), ParseInt("--pages", parts[1]));
    }

    private sealed class UsageException(string message) : Exception(message);

    private sealed class SyncProgress(TextWriter writer) : IProgress<string>
    {
        private readonly object gate = new();

        public void Report(string value)
        {
            lock (gate)
            {
                writer.WriteLine(value);
            }
        }
    }

    private sealed record Options(Dictionary<string, string> Values, HashSet<string> Flags)
    {
        public static Options Parse(string[] args, string[] valued, string[] flags)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            var set = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (flags.Contains(arg, StringComparer.Ordinal))
                {
                    set.Add(arg);
                }
                else if (valued.Contains(arg, StringComparer.Ordinal))
                {
                    if (i + 1 >= args.Length)
                    {
                        throw new UsageException($"brak wartości dla opcji {arg}");
                    }

                    values[arg] = args[++i];
                }
                else
                {
                    throw new UsageException($"nieznana opcja '{arg}'. Użyj --help.");
                }
            }

            return new Options(values, set);
        }

        public string? Value(string name) => Values.TryGetValue(name, out string? v) ? v : null;

        public bool Flag(string name) => Flags.Contains(name);
    }
}
