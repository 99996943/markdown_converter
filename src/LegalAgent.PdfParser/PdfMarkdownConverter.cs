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
    private readonly PdfParserOptions _options;
    private readonly IMarkdownRenderer _renderer;
    private readonly PdfParserOptionsValidator _validator = new();

    internal PdfMarkdownConverter(IEnumerable<IPipelineStage> stages, PdfParserOptions options, IMarkdownRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(stages);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(renderer);
        _runner = new PipelineRunner(stages);
        _options = options.Clone();
        _renderer = renderer;
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

        PdfInput input = await PdfInputReader.ReadAsync(pdf, options.Limits.MaxInputBytes, cancellationToken).ConfigureAwait(false);
        using OpenedPdf opened = PdfDocumentOpener.Open(input, request?.SourceId, options.Limits);

        var report = new ReportBuilder();
        var context = new PipelineContext(options, opened.Source, report, cancellationToken)
        {
            SourceDocument = opened.Document,
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
        PdfParserOptions options = _options.Clone();
        request?.ConfigureOptions?.Invoke(options);

        ValidateOptionsResult validation = _validator.Validate(Options.DefaultName, options);
        if (validation.Failed)
        {
            throw new OptionsValidationException(Options.DefaultName, typeof(PdfParserOptions), validation.Failures);
        }

        return options;
    }
}
