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
/// <param name="Fit">Composition and typesetting (reference truth, element pages).</param>
public sealed record GeneratedDocument(DocumentPlan Plan, byte[] Pdf, string Markdown, Manifest.ManifestDocument Entry, FitResult Fit);

/// <summary>Result of <see cref="CorpusGenerator.GenerateAsync"/>.</summary>
/// <param name="Documents">Generated documents in manifest order.</param>
/// <param name="Manifest">The manifest written.</param>
/// <param name="Deleted">Stale files removed from the managed directories.</param>
public sealed record GenerationResult(IReadOnlyList<GeneratedDocument> Documents, Manifest.Manifest Manifest, IReadOnlyList<string> Deleted);

/// <summary>Result of <see cref="CorpusGenerator.RefreshAsync"/>.</summary>
/// <param name="Manifest">The manifest written.</param>
/// <param name="Converted">Relative paths of the PDFs converted, in manifest order.</param>
public sealed record RefreshResult(Manifest.Manifest Manifest, IReadOnlyList<string> Converted);

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

    /// <summary>
    /// Converts every PDF listed in the manifest of the output directory to Markdown again (nothing is typeset, the content
    /// directory is not read), rewrites the Markdown files and the manifest (parser version, page counts).
    /// </summary>
    public static async Task<RefreshResult> RefreshAsync(RunParameters parameters, CorpusGeneratorOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        options ??= new CorpusGeneratorOptions();
        parameters.Validate();
        string output = Resolve(options.BaseDirectory, parameters.OutputDirectory);
        string manifestPath = Path.Combine(output, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            throw new ContentException("Brak manifestu korpusu (uruchom generate): " + manifestPath) { File = "manifest.json" };
        }

        Manifest.Manifest manifest;
        try
        {
            manifest = ManifestWriter.Read(await File.ReadAllTextAsync(manifestPath, cancellationToken).ConfigureAwait(false));
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new ContentException("Nieczytelny manifest: " + ex.Message, ex) { File = "manifest.json" };
        }

        IReadOnlyList<ManifestDocument> documents = manifest.Documents;
        var converted = new (string Markdown, int Pages)[documents.Count];
        IPdfMarkdownConverter converter = Converter(parameters, options);
        int done = 0;
        await Parallel.ForEachAsync(
            Enumerable.Range(0, documents.Count),
            new ParallelOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = Environment.ProcessorCount },
            async (i, ct) =>
            {
                ManifestDocument entry = documents[i];
                string pdfPath = Resolve(output, entry.Pdf);
                if (!File.Exists(pdfPath))
                {
                    throw new FileNotFoundException("Brak pliku PDF z manifestu: " + pdfPath, pdfPath);
                }

                byte[] pdf = await File.ReadAllBytesAsync(pdfPath, ct).ConfigureAwait(false);
                converted[i] = await ConvertWithPagesAsync(converter, pdf, entry.Pdf, ct).ConfigureAwait(false);
                int n = Interlocked.Increment(ref done);
                options.Progress?.Report($"dokument {n}/{documents.Count}: {entry.Id} ({converted[i].Pages} str.)");
            }).ConfigureAwait(false);

        // T119: acts (zrodla/akty.yaml, akty/ZRODLA.md) are added to the manifest and converted here.
        var files = new List<CorpusFile>();
        var entries = new List<ManifestDocument>(documents.Count);
        for (int i = 0; i < documents.Count; i++)
        {
            entries.Add(documents[i] with { Pages = converted[i].Pages });
            files.Add(new CorpusFile(documents[i].Markdown, CorpusWriter.TextBytes(converted[i].Markdown)));
        }

        var refreshed = new Manifest.Manifest(manifest.Run with { ParserVersion = ParserVersion }, entries);
        IReadOnlyList<string> typeOrder = documents
            .Where(d => d.Type != "akty" && !d.Id.StartsWith("ZAT-", StringComparison.Ordinal))
            .Select(d => d.Type)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        string manifestText = ManifestWriter.Write(refreshed, typeOrder);
        files.Add(new CorpusFile("manifest.json", CorpusWriter.TextBytes(manifestText)));
        CorpusWriter.Write(output, files);
        return new RefreshResult(ManifestWriter.Read(manifestText), documents.Select(d => d.Pdf).ToList());
    }

    /// <summary>Rebuilds the corpus in memory and compares it with the files on disk; writes nothing.</summary>
    public static async Task<CorpusDiff> VerifyAsync(RunParameters parameters, CorpusGeneratorOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        options ??= new CorpusGeneratorOptions();
        Built built = await BuildAsync(parameters, options, cancellationToken).ConfigureAwait(false);
        return CorpusWriter.Compare(Resolve(options.BaseDirectory, parameters.OutputDirectory), built.Files, built.ManagedDirectories);
    }

    /// <summary>
    /// Authoring check of one template: composes it alone in every allowed layout with all candidate optional blocks
    /// of its type and topic, reports the reachable page range and violations, and writes samples to
    /// <paramref name="outputDirectory"/> when given.
    /// </summary>
    public static Task<TemplateCheckReport> CheckTemplateAsync(
        RunParameters parameters,
        string templateId,
        string? outputDirectory,
        CorpusGeneratorOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(templateId);
        options ??= new CorpusGeneratorOptions();
        return CheckTemplateCoreAsync(parameters, templateId, outputDirectory, options, cancellationToken);
    }

    private static async Task<TemplateCheckReport> CheckTemplateCoreAsync(
        RunParameters parameters,
        string templateId,
        string? outputDirectory,
        CorpusGeneratorOptions options,
        CancellationToken cancellationToken)
    {
        ContentLibrary content = ContentLoader.Load(Resolve(options.BaseDirectory, parameters.ContentDirectory));
        DocumentTemplate template = content.Templates.FirstOrDefault(t => t.Id == templateId)
            ?? throw new CorpusGenerationException("Nieznany szablon: " + templateId) { Template = templateId };
        DocumentTypeDef type = content.Types.First(t => t.Id == template.Type);

        // The pool the template would get if it were the only template of its topic.
        var categories = template.Sections.Where(s => s.Optional is not null).SelectMany(s => s.Optional!.Categories).ToHashSet(StringComparer.Ordinal);
        var required = content.Templates.SelectMany(t => t.Sections).SelectMany(s => s.Required).ToHashSet(StringComparer.Ordinal);
        var pool = content.Blocks
            .Where(b => !b.Shared && b.Types.Contains(type.Id) && categories.Contains(b.Category) && !required.Contains(b.Id)
                && (b.Topics.Count == 0 || b.Topics.Contains(template.Topic)))
            .Select(b => b.Id)
            .Order(StringComparer.Ordinal)
            .ToList();

        string id = type.Prefix + "-01";
        var plan = new DocumentPlan
        {
            Id = id,
            Type = type.Id,
            Prefix = type.Prefix,
            Designation = type.DesignationPattern.Replace("{prefiks}", type.Prefix, StringComparison.Ordinal).Replace("{nn}", "01", StringComparison.Ordinal),
            Template = template.Id,
            Layout = template.Layouts[0],
            ValidFrom = new DateOnly(parameters.ReferenceDate.Year, parameters.ReferenceDate.Month, 1).AddMonths(-1),
            BlockPool = pool,
            TargetPages = (parameters.Pages.Min + parameters.Pages.Max) / 2,
            Seed = Random.DeterministicRandom.DeriveSeed(parameters.Seed, "dokument", id),
        };

        var violations = new List<CheckViolation>(CorpusChecks.TemplateStructure(content).Where(v => v.Template == template.Id));
        var layouts = new List<LayoutCheck>();
        var written = new List<string>();
        int requiredWords = 0;
        int optionalWords = 0;
        IPdfMarkdownConverter converter = Converter(parameters, options);
        foreach (string layout in template.Layouts)
        {
            DocumentPlan doc = plan with { Layout = layout };
            LayoutStyle style = LayoutStyles.Get(layout);
            int capacity = Composition.DocumentComposer.OptionalCapacity(doc, content);
            Composition.CompositionResult least = Composition.DocumentComposer.Compose(doc, content, parameters.Seed, 0);
            Composition.CompositionResult most = Composition.DocumentComposer.Compose(doc, content, parameters.Seed, capacity);
            TypesetResult leastSet = Typesetter.Typeset(least.Document, style);
            TypesetResult mostSet = Typesetter.Typeset(most.Document, style);
            if (layouts.Count == 0)
            {
                requiredWords = leastSet.Truth.Words.Count;
                optionalWords = mostSet.Truth.Words.Count - requiredWords;
                violations.AddRange(CorpusChecks.References(most.Unresolved));
                violations.AddRange(CorpusChecks.ForbiddenNames(
                    most.Blocks.Select(b => new RenderedText(id, b.BlockId, b.Text)).Append(new RenderedText(id, null, most.Document.Front.Title)),
                    content.ForbiddenNames));
            }

            FitResult? fit = null;
            string? error = null;
            try
            {
                fit = PageFitter.Fit(doc, content, parameters.Seed, parameters.Pages);
            }
            catch (CorpusGenerationException ex)
            {
                error = ex.Message;
            }

            double share = fit is null ? SharedShare(new FitResult(most, mostSet, 0)) : SharedShare(fit);
            if (fit is not null && share > parameters.MaxSharedShare / 100.0)
            {
                violations.AddRange(CorpusChecks.SharedShare([new DocumentWords(id, fit.Typeset.Truth.Words.Count, SharedWords(fit))], parameters.MaxSharedShare / 100.0));
            }

            layouts.Add(new LayoutCheck(layout, leastSet.PageCount, mostSet.PageCount, capacity, fit?.Typeset.PageCount, error, Math.Round(share, 3)));
            if (outputDirectory is not null)
            {
                TypesetResult sample = fit?.Typeset ?? mostSet;
                string stem = Path.Combine(Resolve(options.BaseDirectory, outputDirectory), template.Id + "." + layout);
                string md = await ConvertAsync(converter, sample.Pdf, template.Id + "." + layout + ".pdf", cancellationToken).ConfigureAwait(false);
                CorpusWriter.WriteAtomic(stem + ".pdf", sample.Pdf);
                CorpusWriter.WriteAtomic(stem + ".md", CorpusWriter.TextBytes(md));
                written.Add(stem + ".pdf");
                written.Add(stem + ".md");
            }
        }

        return new TemplateCheckReport(template.Id, type.Id, requiredWords, optionalWords, layouts, violations, written);
    }

    internal static string Resolve(string baseDirectory, string path) =>
        Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(baseDirectory, path));

    internal static IPdfMarkdownConverter Converter(RunParameters parameters, CorpusGeneratorOptions options) =>
        options.Converter ?? PdfMarkdownConverter.CreateDefault(target => CopyOptions(parameters.ParserOptions, target));

    internal static async Task<string> ConvertAsync(IPdfMarkdownConverter converter, byte[] pdf, string sourceId, CancellationToken cancellationToken) =>
        (await ConvertWithPagesAsync(converter, pdf, sourceId, cancellationToken).ConfigureAwait(false)).Markdown;

    internal static async Task<(string Markdown, int Pages)> ConvertWithPagesAsync(IPdfMarkdownConverter converter, byte[] pdf, string sourceId, CancellationToken cancellationToken)
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

        return (result.Markdown, result.Document.Source.PageCount);
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
        var index = Enumerable.Range(0, plans.Count).ToDictionary(i => plans[i].Id, StringComparer.Ordinal);
        for (int i = 0; i < plans.Count; i++)
        {
            DocumentPlan doc = plans[i];
            FitResult fit = fits[i];
            FitResult? previous = doc.PreviousVersionId is { } p ? fits[index[p]] : null;
            ManifestDocument entry = Entry(content, plans, doc, fit, previous);
            documents.Add(new GeneratedDocument(doc, fit.Typeset.Pdf, markdown[i], entry, fit));
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

    /// <summary>The plan of the corpus described by <paramref name="parameters"/>.</summary>
    public static CorpusPlan Plan(RunParameters parameters, CorpusGeneratorOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        options ??= new CorpusGeneratorOptions();
        parameters.Validate();
        return CorpusPlanner.Plan(ContentLoader.Load(Resolve(options.BaseDirectory, parameters.ContentDirectory)), parameters);
    }

    /// <summary>Builds one document of the corpus in memory (as <see cref="GenerateAsync"/> would), for tests and diagnostics.</summary>
    public static async Task<GeneratedDocument> BuildDocumentAsync(RunParameters parameters, string documentId, CorpusGeneratorOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        options ??= new CorpusGeneratorOptions();
        parameters.Validate();
        ContentLibrary content = ContentLoader.Load(Resolve(options.BaseDirectory, parameters.ContentDirectory));
        CorpusPlan plan = CorpusPlanner.Plan(content, parameters);
        DocumentPlan doc = plan.Documents.FirstOrDefault(d => d.Id == documentId)
            ?? throw new CorpusGenerationException("Brak dokumentu w planie: " + documentId) { DocumentId = documentId };
        FitResult fit = PageFitter.Fit(doc, content, parameters.Seed, parameters.Pages);
        FitResult? previous = doc.PreviousVersionId is { } p
            ? PageFitter.Fit(plan.Documents.First(d => d.Id == p), content, parameters.Seed, parameters.Pages)
            : null;
        string markdown = await ConvertAsync(Converter(parameters, options), fit.Typeset.Pdf, PdfPath(doc), cancellationToken).ConfigureAwait(false);
        return new GeneratedDocument(doc, fit.Typeset.Pdf, markdown, Entry(content, plan.Documents, doc, fit, previous), fit);
    }

    private static ManifestDocument Entry(ContentLibrary content, IReadOnlyList<DocumentPlan> plan, DocumentPlan doc, FitResult fit, FitResult? previous) => new(
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
        Math.Round(SharedShare(fit), 3),
        Changes: previous is null
            ? null
            : NullIfEmpty(ManifestRelations.Changes(content, doc, fit, plan.First(d => d.Id == doc.PreviousVersionId), previous)),
        Contradictions: NullIfEmpty(ManifestRelations.Contradictions(content, doc, fit, plan)),
        Poison: doc.Poison is null ? null : ManifestRelations.Poison(content, doc, fit));

    private static IReadOnlyList<T>? NullIfEmpty<T>(IReadOnlyList<T> list) => list.Count == 0 ? null : list;

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
            // A poisoned document copies the document it imitates (FR-131): its blocks repeat by design.
            problems.AddRange(CorpusChecks.Uniqueness(fits.Where((f, i) => plans[i].Poison is null).SelectMany(f => f.Composition.Blocks)));
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

    internal static string PdfPath(DocumentPlan plan) => Folder(plan) + plan.Id + ".pdf";

    internal static string MarkdownPath(DocumentPlan plan) => Folder(plan) + plan.Id + ".md";

    /// <summary>The directory of a document: its type, or <c>zatrute/&lt;typ&gt;/&lt;rodzaj&gt;/</c> for a poisoned one (FR-130).</summary>
    private static string Folder(DocumentPlan plan) =>
        plan.Poison is { } poison ? $"zatrute/{plan.Type}/{poison.Kind}/" : plan.Type + "/";

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
