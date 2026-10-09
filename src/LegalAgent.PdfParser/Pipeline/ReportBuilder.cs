using System.Globalization;
using LegalAgent.PdfParser.Model;

namespace LegalAgent.PdfParser.Pipeline;

/// <summary>Collects diagnostics during a conversion and produces a deterministic <see cref="ConversionReport"/>.</summary>
public sealed class ReportBuilder
{
    private readonly List<ConversionWarning> _warnings = [];
    private readonly List<SkippedPage> _skipped = [];
    private readonly Dictionary<(ArtifactKind Kind, string Pattern), List<int>> _artifacts = [];
    private readonly SortedDictionary<int, int> _headings = [];
    private readonly List<TableDocumentSummary> _tableDocuments = [];

    /// <summary>Number of letters dropped as rotated or invisible.</summary>
    public int DroppedTextCount { get; private set; }

    /// <summary>Number of detected lists.</summary>
    public int ListCount { get; private set; }

    /// <summary>Number of detected tables (including fallback tables).</summary>
    public int TableCount { get; private set; }

    /// <summary>Number of tables rendered in fallback mode.</summary>
    public int FallbackTableCount { get; private set; }

    /// <summary>Number of footnotes.</summary>
    public int FootnoteCount { get; private set; }

    /// <summary>Adds a warning.</summary>
    /// <param name="code">Stable warning code.</param>
    /// <param name="pageNumber">Related page, if any.</param>
    /// <param name="message">Human-readable message.</param>
    public void AddWarning(string code, int? pageNumber, string message)
    {
        ArgumentException.ThrowIfNullOrEmpty(code);
        ArgumentNullException.ThrowIfNull(message);
        _warnings.Add(new ConversionWarning(code, pageNumber, message));
    }

    /// <summary>Records a skipped page.</summary>
    /// <param name="pageNumber">Page number (1-based).</param>
    /// <param name="reason">Why it was skipped.</param>
    /// <param name="message">Human-readable detail.</param>
    public void AddSkippedPage(int pageNumber, SkipReason reason, string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        _skipped.Add(new SkippedPage(pageNumber, reason, message));
    }

    /// <summary>Records one removed artifact occurrence.</summary>
    /// <param name="kind">Kind of artifact.</param>
    /// <param name="pattern">Fingerprint pattern of the removed text.</param>
    /// <param name="pageNumber">Page on which it was removed.</param>
    public void AddRemovedArtifact(ArtifactKind kind, string pattern, int pageNumber)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        if (!_artifacts.TryGetValue((kind, pattern), out List<int>? pages))
        {
            pages = [];
            _artifacts[(kind, pattern)] = pages;
        }

        pages.Add(pageNumber);
    }

    /// <summary>Counts a heading of the given level.</summary>
    /// <param name="level">Heading level (1-6).</param>
    public void AddHeading(int level)
    {
        _headings.TryGetValue(level, out int count);
        _headings[level] = count + 1;
    }

    /// <summary>Counts letters dropped as rotated or invisible.</summary>
    /// <param name="count">Number of letters.</param>
    public void AddDroppedText(int count) => DroppedTextCount += count;

    /// <summary>Counts a list.</summary>
    public void AddList() => ListCount++;

    /// <summary>Counts a table.</summary>
    /// <param name="isFallback">True for a fallback table.</param>
    public void AddTable(bool isFallback)
    {
        TableCount++;
        if (isFallback)
        {
            FallbackTableCount++;
        }
    }

    /// <summary>Records a recognised table-document (spec 002, FR-090).</summary>
    /// <param name="firstPage">First page of the table.</param>
    /// <param name="lastPage">Last page of the table.</param>
    /// <param name="sectionCount">Number of sections.</param>
    /// <param name="headerRowText">Text of the dropped column-name row, or null.</param>
    /// <param name="droppedHeaderRows">Number of dropped occurrences of the column-name row.</param>
    public void AddTableDocument(int firstPage, int lastPage, int sectionCount, string? headerRowText, int droppedHeaderRows) =>
        _tableDocuments.Add(new TableDocumentSummary(firstPage, lastPage, sectionCount, headerRowText, droppedHeaderRows));

    /// <summary>Counts a footnote.</summary>
    public void AddFootnote() => FootnoteCount++;

    /// <summary>Builds the report with deterministic ordering of all collections.</summary>
    /// <param name="pageCount">Number of pages in the PDF.</param>
    /// <param name="elapsed">Conversion time.</param>
    public ConversionReport Build(int pageCount, TimeSpan elapsed)
    {
        List<ArtifactSummary> artifacts = _artifacts
            .Select(kv =>
            {
                List<int> pages = kv.Value.Distinct().Order().ToList();
                return new ArtifactSummary(kv.Key.Pattern, kv.Key.Kind, kv.Value.Count, pages);
            })
            .OrderBy(a => a.Pages[0])
            .ThenBy(a => a.Pattern, StringComparer.Ordinal)
            .ThenBy(a => a.Kind)
            .ToList();

        List<SkippedPage> skipped = _skipped
            .OrderBy(s => s.PageNumber)
            .ThenBy(s => s.Reason)
            .ThenBy(s => s.Message, StringComparer.Ordinal)
            .ToList();

        List<ConversionWarning> warnings = _warnings
            .OrderBy(w => w.PageNumber ?? 0)
            .ThenBy(w => w.Code, StringComparer.Ordinal)
            .ThenBy(w => w.Message, StringComparer.Ordinal)
            .ToList();

        var headings = new Dictionary<int, int>(_headings);

        return new ConversionReport(
            pageCount,
            skipped,
            artifacts,
            headings,
            ListCount,
            TableCount,
            FallbackTableCount,
            FootnoteCount,
            DroppedTextCount,
            warnings,
            elapsed)
        {
            TableDocuments = _tableDocuments.OrderBy(t => t.FirstPage).ThenBy(t => t.LastPage).ToList(),
        };
    }

    /// <inheritdoc />
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"ReportBuilder: {_warnings.Count} warnings, {_skipped.Count} skipped pages");
}
