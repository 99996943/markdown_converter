using System.Diagnostics;
using LegalAgent.PdfParser.Input;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Pipeline;
using LegalAgent.PdfParser.Rendering;
using Microsoft.Extensions.Options;

namespace LegalAgent.PdfParser;

/// <summary>
/// Default <see cref="IPdfMarkdownConverter"/>: buffers the input, opens the PDF, runs the pipeline stages and
/// renders the resulting <see cref="LegalDocument"/> to Markdown. Stateless; every call has its own context.
/// </summary>
public sealed class PdfMarkdownConverter : IPdfMarkdownConverter
{
    private readonly PipelineRunner _runner;
    private readonly IOptions<PdfParserOptions> _options;
    private readonly IMarkdownRenderer _renderer;
    private readonly PdfParserOptionsValidator _validator = new();

    /// <summary>Creates a converter; intended for dependency injection.</summary>
    /// <param name="stages">Pipeline stages (sorted by <see cref="IPipelineStage.Order"/>).</param>
    /// <param name="options">Configured options; snapshotted (cloned) on every conversion.</param>
    /// <param name="renderer">Markdown renderer.</param>
    public PdfMarkdownConverter(IEnumerable<IPipelineStage> stages, IOptions<PdfParserOptions> options, IMarkdownRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(stages);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(renderer);
        _runner = new PipelineRunner(stages);
        _options = options;
        _renderer = renderer;
    }

    internal PdfMarkdownConverter(IEnumerable<IPipelineStage> stages, PdfParserOptions options, IMarkdownRenderer renderer)
        : this(stages, Options.Create((options ?? throw new ArgumentNullException(nameof(options))).Clone()), renderer)
    {
    }

    /// <summary>Creates a converter with the built-in stages, without a dependency-injection container.</summary>
    /// <param name="configure">Adjusts the default options.</param>
    public static PdfMarkdownConverter CreateDefault(Action<PdfParserOptions>? configure = null)
    {
        var options = new PdfParserOptions();
        configure?.Invoke(options);
        return new PdfMarkdownConverter(BuiltInStages.Create(), options, new MarkdownRenderer());
    }

    /// <inheritdoc />
    public async Task<PdfConversionResult> ConvertAsync(
        Stream pdf,
        PdfConversionRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdf);

        long started = Stopwatch.GetTimestamp();
        PdfParserOptions options = ResolveOptions(request);

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (options.Limits.MaxDuration is { } maxDuration)
        {
            linked.CancelAfter(maxDuration);
        }

        try
        {
            return await ConvertCoreAsync(pdf, request, options, started, linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && linked.IsCancellationRequested)
        {
            long configured = (long)options.Limits.MaxDuration!.Value.TotalMilliseconds;
            long measured = Math.Max(configured, (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            throw new PdfLimitExceededException(PdfLimit.Duration, configured, measured);
        }
    }

    private async Task<PdfConversionResult> ConvertCoreAsync(
        Stream pdf,
        PdfConversionRequest? request,
        PdfParserOptions options,
        long started,
        CancellationToken token)
    {
        PdfInput input = await PdfInputReader.ReadAsync(pdf, options.Limits.MaxInputBytes, token).ConfigureAwait(false);
        using OpenedPdf opened = PdfDocumentOpener.Open(input, request?.SourceId, options.Limits);

        var report = new ReportBuilder();
        var context = new PipelineContext(options, opened.Source, report, token)
        {
            SourceDocument = opened.Document,
            Progress = request?.Progress,
        };

        _runner.Run(context);

        LegalDocument document = context.Document
            ?? throw new InvalidOperationException("Potok nie zbudował dokumentu (brak etapu DocumentBuildStage).");
        string markdown = _renderer.Render(document, options.Rendering);
        ConversionReport conversionReport = report.Build(opened.Source.PageCount, Stopwatch.GetElapsedTime(started));

        return new PdfConversionResult(document, markdown, conversionReport, conversionReport.SkippedPages.Count == 0);
    }

    private PdfParserOptions ResolveOptions(PdfConversionRequest? request)
    {
        PdfParserOptions options = _options.Value.Clone();
        request?.ConfigureOptions?.Invoke(options);

        ValidateOptionsResult validation = _validator.Validate(Options.DefaultName, options);
        if (validation.Failed)
        {
            throw new OptionsValidationException(Options.DefaultName, typeof(PdfParserOptions), validation.Failures);
        }

        return options;
    }
}
