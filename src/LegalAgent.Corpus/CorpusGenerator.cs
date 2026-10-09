using System.Reflection;
using System.Text;
using System.Text.Json;
using LegalAgent.Corpus.Content;
using LegalAgent.Corpus.Manifest;
using LegalAgent.Corpus.Output;
using LegalAgent.Corpus.Planning;
using LegalAgent.Corpus.Typesetting;
using LegalAgent.Corpus.Validation;
using LegalAgent.PdfParser;
using LegalAgent.PdfParser.Model;

namespace LegalAgent.Corpus;

/// <summary>Settings of a generator run that are not part of the recorded run parameters.</summary>
public sealed class CorpusGeneratorOptions
{
    /// <summary>Directory against which relative paths of the run parameters are resolved (the repository root).</summary>
    public string BaseDirectory { get; init; } = Directory.GetCurrentDirectory();

    /// <summary>Converter used for Markdown; null = <see cref="PdfMarkdownConverter.CreateDefault"/> with the run's parser options.</summary>
    public IPdfMarkdownConverter? Converter { get; init; }

    /// <summary>Progress messages ("dokument N/M …").</summary>
    public IProgress<string>? Progress { get; init; }

    /// <summary>When set, the reference truth of every document is written there as JSON (diagnostics).</summary>
    public string? TruthDirectory { get; init; }
}

/// <summary>One generated document.</summary>
/// <param name="Plan">Its plan.</param>
/// <param name="Pdf">PDF bytes.</param>
/// <param name="Markdown">Markdown from the library.</param>
/// <param name="Entry">Its manifest entry.</param>
public sealed record GeneratedDocument(DocumentPlan Plan, byte[] Pdf, string Markdown, Manifest.ManifestDocument Entry);

/// <summary>Result of <see cref="CorpusGenerator.GenerateAsync"/>.</summary>
/// <param name="Documents">Generated documents in manifest order.</param>
/// <param name="Manifest">The manifest written.</param>
/// <param name="Deleted">Stale files removed from the managed directories.</param>
public sealed record GenerationResult(IReadOnlyList<GeneratedDocument> Documents, Manifest.Manifest Manifest, IReadOnlyList<string> Deleted);

/// <summary>A conversion failed or was incomplete (FR-160; CLI exit code 5).</summary>
public sealed class ConversionFailedException : Exception
{
    /// <summary>Creates the exception.</summary>
    public ConversionFailedException()
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    public ConversionFailedException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and an inner exception.</summary>
    public ConversionFailedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Generated text contains a forbidden name (FR-105; CLI exit code 4).</summary>
public sealed class ForbiddenNameException : Exception
{
    /// <summary>Creates the exception.</summary>
    public ForbiddenNameException()
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    public ForbiddenNameException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and an inner exception.</summary>
    public ForbiddenNameException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Facade of the corpus generator: <c>generate</c>, <c>verify</c> (contracts/cli.md).</summary>
public static class CorpusGenerator
{
    private static readonly JsonSerializerOptions TruthJson = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Version of the generator library (without build metadata).</summary>
    public static string GeneratorVersion => VersionOf(typeof(CorpusGenerator).Assembly);

    /// <summary>Version of the parser library used for Markdown (without build metadata).</summary>
    public static string ParserVersion => VersionOf(typeof(PdfMarkdownConverter).Assembly);

    /// <summary>Plans, typesets, converts and writes the corpus; removes stale managed files after a successful run.</summary>
    public static async Task<GenerationResult> GenerateAsync(RunParameters parameters, CorpusGeneratorOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        options ??= new CorpusGeneratorOptions();
        Built built = await BuildAsync(parameters, options, cancellationToken).ConfigureAwait(false);
        string output = Resolve(options.BaseDirectory, parameters.OutputDirectory);
        CorpusWriter.Write(output, built.Files);
        IReadOnlyList<string> deleted = CorpusWriter.Cleanup(output, built.Files.Select(f => f.RelativePath), built.ManagedDirectories);
        return new GenerationResult(built.Documents, built.Manifest, deleted);
    }

    /// <summary>Rebuilds the corpus in memory and compares it with the files on disk; writes nothing.</summary>
    public static async Task<CorpusDiff> VerifyAsync(RunParameters parameters, CorpusGeneratorOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        options ??= new CorpusGeneratorOptions();
        Built built = await BuildAsync(parameters, options, cancellationToken).ConfigureAwait(false);
        return CorpusWriter.Compare(Resolve(options.BaseDirectory, parameters.OutputDirectory), built.Files, built.ManagedDirectories);
    }

    internal static string Resolve(string baseDirectory, string path) =>
        Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(baseDirectory, path));

    internal static IPdfMarkdownConverter Converter(RunParameters parameters, CorpusGeneratorOptions options) =>
        options.Converter ?? PdfMarkdownConverter.CreateDefault(target => CopyOptions(parameters.ParserOptions, target));

    internal static async Task<string> ConvertAsync(IPdfMarkdownConverter converter, byte[] pdf, string sourceId, CancellationToken cancellationToken)
    {
        PdfConversionResult result;
        try
        {
            using var stream = new MemoryStream(pdf, writable: false);
            result = await converter.ConvertAsync(stream, new PdfConversionRequest { SourceId = sourceId }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new ConversionFailedException($"Błąd konwersji {sourceId}: {ex.Message}", ex);
        }

        if (!result.IsComplete)
        {
            throw new ConversionFailedException($"Konwersja niekompletna: {sourceId}");
        }

        return result.Markdown;
    }

    private sealed record Built(IReadOnlyList<GeneratedDocument> Documents, Manifest.Manifest Manifest, IReadOnlyList<CorpusFile> Files, IReadOnlyList<string> ManagedDirectories);

    private static async Task<Built> BuildAsync(RunParameters parameters, CorpusGeneratorOptions options, CancellationToken cancellationToken)
    {
        parameters.Validate();
        ContentLibrary content = ContentLoader.Load(Resolve(options.BaseDirectory, parameters.ContentDirectory));
        IReadOnlyList<CheckViolation> structure = CorpusChecks.TemplateStructure(content);
        if (structure.Count > 0)
        {
            throw new ContentException("Szablony bez elementów obowiązkowych typu:\n" + string.Join("\n", structure.Select(v => v.Message)));
        }

        CorpusPlan plan = CorpusPlanner.Plan(content, parameters);
        IReadOnlyList<DocumentPlan> plans = plan.Documents;
        var fits = new FitResult[plans.Count];
        var markdown = new string[plans.Count];
        IPdfMarkdownConverter converter = Converter(parameters, options);
        int done = 0;
        await Parallel.ForEachAsync(
            Enumerable.Range(0, plans.Count),
            new ParallelOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = Environment.ProcessorCount },
            async (i, ct) =>
            {
                DocumentPlan doc = plans[i];
                fits[i] = PageFitter.Fit(doc, content, parameters.Seed, parameters.Pages);
                markdown[i] = await ConvertAsync(converter, fits[i].Typeset.Pdf, PdfPath(doc), ct).ConfigureAwait(false);
                int n = Interlocked.Increment(ref done);
                options.Progress?.Report($"dokument {n}/{plans.Count}: {doc.Id} ({fits[i].Typeset.PageCount} str.)");
            }).ConfigureAwait(false);

        Check(parameters, plans, fits, markdown, content);

        var documents = new List<GeneratedDocument>(plans.Count);
        var files = new List<CorpusFile>();
        for (int i = 0; i < plans.Count; i++)
        {
            DocumentPlan doc = plans[i];
            FitResult fit = fits[i];
            ManifestDocument entry = new(
                doc.Id,
                doc.Type,
                fit.Composition.Document.Front.Title,
                doc.Designation,
                doc.Version,
                doc.ValidFrom,
                doc.ValidTo,
                doc.Status == DocumentStatus.Obowiazujacy ? "obowiazujacy" : "nieaktualny",
                doc.PreviousVersionId,
                PdfPath(doc),
                MarkdownPath(doc),
                fit.Typeset.PageCount,
                doc.Template,
                doc.Layout,
                doc.Seed,
                Math.Round(SharedShare(fit), 3));
            documents.Add(new GeneratedDocument(doc, fit.Typeset.Pdf, markdown[i], entry));
            files.Add(new CorpusFile(entry.Pdf, fit.Typeset.Pdf));
            files.Add(new CorpusFile(entry.Markdown, CorpusWriter.TextBytes(markdown[i])));
            if (options.TruthDirectory is { } truthDirectory)
            {
                Directory.CreateDirectory(truthDirectory);
                File.WriteAllText(Path.Combine(truthDirectory, doc.Id + ".json"), JsonSerializer.Serialize(fit.Typeset.Truth, TruthJson) + "\n", new UTF8Encoding(false));
            }
        }

        var run = new ManifestRun(parameters.Seed, parameters.ReferenceDate, ManifestParameters(parameters, options), ParserVersion, GeneratorVersion, content.ContentHash);
        var manifest = new Manifest.Manifest(run, documents.Select(d => d.Entry).ToList());
        IReadOnlyList<string> typeOrder = content.Types.Select(t => t.Id).ToList();
        string manifestText = ManifestWriter.Write(manifest, typeOrder);
        files.Add(new CorpusFile("manifest.json", CorpusWriter.TextBytes(manifestText)));
        var managed = content.Types.Select(t => t.Id).Append("zatrute").ToList();
        return new Built(documents, ManifestWriter.Read(manifestText), files, managed);
    }

    private static void Check(RunParameters parameters, IReadOnlyList<DocumentPlan> plans, FitResult[] fits, string[] markdown, ContentLibrary content)
    {
        var texts = new List<RenderedText>();
        for (int i = 0; i < plans.Count; i++)
        {
            texts.AddRange(fits[i].Composition.Blocks.Select(b => new RenderedText(plans[i].Id, b.BlockId, b.Text)));
            texts.Add(new RenderedText(plans[i].Id, null, fits[i].Composition.Document.Front.Title));
            texts.Add(new RenderedText(plans[i].Id, null, markdown[i]));
        }

        IReadOnlyList<CheckViolation> forbidden = CorpusChecks.ForbiddenNames(texts, content.ForbiddenNames);
        if (forbidden.Count > 0)
        {
            throw new ForbiddenNameException("Nazwy zabronione w treści:\n" + string.Join("\n", forbidden.Select(v => v.Message)));
        }

        var problems = new List<CheckViolation>();
        problems.AddRange(CorpusChecks.References(fits.SelectMany(f => f.Composition.Unresolved)));
        if (parameters.StrictUniqueness)
        {
            problems.AddRange(CorpusChecks.Uniqueness(fits.SelectMany(f => f.Composition.Blocks)));
        }

        problems.AddRange(CorpusChecks.SharedShare(
            plans.Select((p, i) => new DocumentWords(p.Id, fits[i].Typeset.Truth.Words.Count, SharedWords(fits[i]))),
            parameters.MaxSharedShare / 100.0));
        if (problems.Count > 0)
        {
            throw new CorpusGenerationException("Naruszenia reguł korpusu:\n" + string.Join("\n", problems.Select(v => v.Message)))
            {
                DocumentId = problems[0].DocumentId,
            };
        }
    }

    private static int SharedWords(FitResult fit)
    {
        var shared = fit.Composition.Blocks.Where(b => b.Shared).Select(b => b.BlockId).ToHashSet(StringComparer.Ordinal);
        return fit.Typeset.BlockWordCounts.Where(p => shared.Contains(p.Key)).Sum(p => p.Value);
    }

    private static double SharedShare(FitResult fit) =>
        fit.Typeset.Truth.Words.Count == 0 ? 0 : (double)SharedWords(fit) / fit.Typeset.Truth.Words.Count;

    internal static string PdfPath(DocumentPlan plan) => plan.Type + "/" + plan.Id + ".pdf";

    internal static string MarkdownPath(DocumentPlan plan) => plan.Type + "/" + plan.Id + ".md";

    private static string ManifestParameters(RunParameters parameters, CorpusGeneratorOptions options)
    {
        string Relative(string path)
        {
            if (!Path.IsPathRooted(path))
            {
                return path.Replace('\\', '/');
            }

            string relative = Path.GetRelativePath(options.BaseDirectory, path).Replace('\\', '/');
            return Path.IsPathRooted(relative) ? Path.GetFileName(path) : relative;
        }

        return (parameters with { OutputDirectory = Relative(parameters.OutputDirectory), ContentDirectory = Relative(parameters.ContentDirectory) }).ToJson();
    }

    private static void CopyOptions(PdfParserOptions source, PdfParserOptions target)
    {
        foreach (PropertyInfo property in typeof(PdfParserOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.CanRead && property.CanWrite)
            {
                property.SetValue(target, property.GetValue(source));
            }
        }
    }

    private static string VersionOf(Assembly assembly)
    {
        string version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "0.0.0";
        int plus = version.IndexOf('+', StringComparison.Ordinal);
        return plus < 0 ? version : version[..plus];
    }
}
