namespace LegalAgent.PdfParser.Layout;

/// <summary>Vertical zone of a page a line belongs to.</summary>
public enum LineZone
{
    /// <summary>Top margin zone.</summary>
    Header,

    /// <summary>Main body.</summary>
    Body,

    /// <summary>Bottom margin zone.</summary>
    Footer,
}

/// <summary>Role assigned to a line by the pipeline stages.</summary>
public enum LineRole
{
    /// <summary>Not classified yet.</summary>
    Unknown,

    /// <summary>Page artifact (running header or footer, page number) to be removed.</summary>
    Artifact,

    /// <summary>Footnote definition line.</summary>
    Footnote,

    /// <summary>Table line.</summary>
    Table,

    /// <summary>First line of a list item.</summary>
    ListItem,

    /// <summary>Continuation of a list item.</summary>
    ListContinuation,

    /// <summary>Heading line.</summary>
    Heading,

    /// <summary>Ordinary body text.</summary>
    Body,

    /// <summary>Line of a side-note column at the page edge (FR-034), set by line assembly.</summary>
    SideNote,

    /// <summary>Line of a step name in a step scheme (FR-067), set by step sequence detection.</summary>
    StepTitle,
}

/// <summary>A line of text.</summary>
public sealed class LayoutLine
{
    /// <summary>Creates a line.</summary>
    /// <param name="words">Words of the line in reading order.</param>
    /// <param name="box">Bounding box.</param>
    /// <param name="baseline">Baseline Y coordinate.</param>
    public LayoutLine(IReadOnlyList<LayoutWord> words, Rect box, double baseline)
    {
        ArgumentNullException.ThrowIfNull(words);
        Words = words;
        Box = box;
        Baseline = baseline;
    }

    /// <summary>Words of the line in reading order.</summary>
    public IReadOnlyList<LayoutWord> Words { get; }

    /// <summary>Bounding box.</summary>
    public Rect Box { get; set; }

    /// <summary>Baseline Y coordinate.</summary>
    public double Baseline { get; set; }

    /// <summary>Parts of the line split on large horizontal gaps.</summary>
    public IList<LineSegment> Segments { get; } = [];

    /// <summary>Page zone of the line.</summary>
    public LineZone Zone { get; set; } = LineZone.Body;

    /// <summary>Role assigned by the stages; once set it is not changed by later stages.</summary>
    public LineRole Role { get; set; } = LineRole.Unknown;

    /// <summary>Free-form annotations set by stages (for example a list label).</summary>
    public IDictionary<string, string> Annotations { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Heading described by this line; set by heading detection on the first line of a heading. Further lines merged
    /// into the same heading (e.g. a chapter title under „Rozdział 3”) have <see cref="Role"/> = Heading and no info.
    /// </summary>
    public HeadingInfo? Heading { get; set; }

    /// <summary>Line text with words joined by single spaces.</summary>
    public string Text => string.Join(' ', Words.Select(w => w.Text));
}
