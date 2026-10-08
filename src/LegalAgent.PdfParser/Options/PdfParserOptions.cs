namespace LegalAgent.PdfParser;

/// <summary>Where footnote definitions are placed in the Markdown output.</summary>
public enum FootnotesPlacement
{
    /// <summary>After the content of the section that references them.</summary>
    EndOfSection,
}

/// <summary>Resource limits.</summary>
public sealed class LimitsOptions
{
    /// <summary>Maximum input size in bytes; null disables the limit.</summary>
    public long? MaxInputBytes { get; set; } = 104_857_600;

    /// <summary>Maximum number of pages; null disables the limit.</summary>
    public int? MaxPages { get; set; } = 2000;

    /// <summary>Maximum conversion time; null disables the limit.</summary>
    public TimeSpan? MaxDuration { get; set; } = TimeSpan.FromSeconds(120);

    internal LimitsOptions Clone() => (LimitsOptions)MemberwiseClone();
}

/// <summary>Text normalisation options.</summary>
public sealed class NormalizationOptions
{
    /// <summary>Compound words whose hyphen is kept when joining hyphenated line ends.</summary>
    public IList<string> HyphenationExceptions { get; } = ["e-mail", "biało-czerwony"];

    /// <summary>Drop rotated text.</summary>
    public bool DropRotatedText { get; set; } = true;

    /// <summary>Drop text with the invisible rendering mode.</summary>
    public bool DropInvisibleText { get; set; } = true;

    internal NormalizationOptions Clone()
    {
        var copy = new NormalizationOptions
        {
            DropRotatedText = DropRotatedText,
            DropInvisibleText = DropInvisibleText,
        };
        copy.HyphenationExceptions.Clear();
        foreach (string item in HyphenationExceptions)
        {
            copy.HyphenationExceptions.Add(item);
        }

        return copy;
    }
}

/// <summary>Running header, footer and page number removal options.</summary>
public sealed class ArtifactOptions
{
    /// <summary>Enable artifact removal.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Fraction of page height at the top and bottom treated as margin zones.</summary>
    public double MarginZoneRatio { get; set; } = 0.08;

    /// <summary>Minimum fraction of pages on which a line must repeat.</summary>
    public double MinPageRatio { get; set; } = 0.5;

    /// <summary>Minimum number of pages for repetition-based removal.</summary>
    public int MinPages { get; set; } = 3;

    /// <summary>Allowed vertical position deviation as a fraction of page height.</summary>
    public double PositionTolerance { get; set; } = 0.02;

    /// <summary>Minimum fingerprint similarity (0-1) to treat lines as the same artifact.</summary>
    public double Similarity { get; set; } = 0.85;

    /// <summary>Analyse odd and even pages separately.</summary>
    public bool SplitOddEven { get; set; } = true;

    /// <summary>Remove page numbers.</summary>
    public bool RemovePageNumbers { get; set; } = true;

    internal ArtifactOptions Clone() => (ArtifactOptions)MemberwiseClone();
}

/// <summary>Line, paragraph and column layout options.</summary>
public sealed class LayoutOptions
{
    /// <summary>Minimum vertical overlap fraction for glyphs to share a line.</summary>
    public double LineOverlapRatio { get; set; } = 0.5;

    /// <summary>Maximum baseline difference as a fraction of the smaller font size.</summary>
    public double BaselineToleranceRatio { get; set; } = 0.3;

    /// <summary>Line gap (in typical leadings) above which a new paragraph starts.</summary>
    public double ParagraphGapFactor { get; set; } = 1.5;

    /// <summary>A sentence-ending line shorter than this fraction of the column width ends a paragraph.</summary>
    public double ShortLineRatio { get; set; } = 0.75;

    /// <summary>Detect multi-column pages.</summary>
    public bool DetectColumns { get; set; } = true;

    /// <summary>Minimum gutter width as a fraction of page width.</summary>
    public double GutterMinWidthRatio { get; set; } = 0.02;

    /// <summary>Minimum gutter height as a fraction of page height.</summary>
    public double GutterMinHeightRatio { get; set; } = 0.6;

    /// <summary>Minimum line width as a fraction of page width for lines to count towards columns.</summary>
    public double ColumnMinLineWidthRatio { get; set; } = 0.25;

    /// <summary>Separate a narrow side-note column at the page edge from the main text (FR-034).</summary>
    public bool DetectSideNotes { get; set; } = true;

    /// <summary>Maximum width of a side-note column as a fraction of page width.</summary>
    public double SideNoteMaxWidthRatio { get; set; } = 0.25;

    /// <summary>Maximum font size of side notes as a fraction of the main text size.</summary>
    public double SideNoteMaxSizeRatio { get; set; } = 0.9;

    internal LayoutOptions Clone() => (LayoutOptions)MemberwiseClone();
}

/// <summary>Heading detection options.</summary>
public sealed class HeadingOptions
{
    /// <summary>Enable heading detection.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Minimum font size ratio to the body text for a typographic heading.</summary>
    public double SizeRatio { get; set; } = 1.15;

    /// <summary>Font size clustering tolerance in points.</summary>
    public double SizeClusterTolerance { get; set; } = 0.5;

    /// <summary>Maximum heading length in characters.</summary>
    public int MaxLength { get; set; } = 120;

    /// <summary>Maximum number of lines of a heading.</summary>
    public int MaxLines { get; set; } = 2;

    /// <summary>Maximum number of typographic heading levels (1-6).</summary>
    public int MaxTypographicDepth { get; set; } = 3;

    /// <summary>Vertical gap (in typical leadings) required around a heading.</summary>
    public double GapFactor { get; set; } = 1.3;

    /// <summary>Tolerance for treating a line as centred, as a fraction of page width.</summary>
    public double CenterTolerance { get; set; } = 0.05;

    /// <summary>Detect legal units (Dział, Rozdział, Art., §).</summary>
    public bool DetectLegalUnits { get; set; } = true;

    internal HeadingOptions Clone() => (HeadingOptions)MemberwiseClone();
}

/// <summary>List detection options.</summary>
public sealed class ListOptions
{
    /// <summary>Enable list detection.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Indentation difference in points treated as the same level.</summary>
    public double IndentTolerance { get; set; } = 1.5;

    internal ListOptions Clone() => (ListOptions)MemberwiseClone();
}

/// <summary>Table detection options.</summary>
public sealed class TableOptions
{
    /// <summary>Enable table detection.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gap (in mean space widths) that separates cells.</summary>
    public double CellGapFactor { get; set; } = 2.0;

    /// <summary>Minimum number of rows for a table.</summary>
    public int MinRows { get; set; } = 3;

    /// <summary>Column alignment tolerance as a fraction of page width.</summary>
    public double ColumnTolerance { get; set; } = 0.03;

    /// <summary>Line gap (in typical leadings) up to which lines are merged into one multi-line row.</summary>
    public double RowMergeGapFactor { get; set; } = 1.2;

    /// <summary>Use ruling lines to detect the grid.</summary>
    public bool UseRulingLines { get; set; } = true;

    /// <summary>Merge tables continued across pages.</summary>
    public bool MergeAcrossPages { get; set; } = true;

    /// <summary>Render step schemes (shaded step boxes with an explanation beside them) as a sequence of steps (FR-067).</summary>
    public bool DetectStepSequences { get; set; } = true;

    internal TableOptions Clone() => (TableOptions)MemberwiseClone();
}

/// <summary>Markdown rendering options.</summary>
public sealed class RenderingOptions
{
    /// <summary>Emit page marker comments.</summary>
    public bool PageMarkers { get; set; } = true;

    /// <summary>Where footnote definitions are placed.</summary>
    public FootnotesPlacement FootnotesPlacement { get; set; } = FootnotesPlacement.EndOfSection;

    /// <summary>Render bold and italic as inline emphasis.</summary>
    public bool EmphasisInline { get; set; } = true;

    internal RenderingOptions Clone() => (RenderingOptions)MemberwiseClone();
}

/// <summary>Footnote detection options.</summary>
public sealed class FootnoteOptions
{
    /// <summary>Enable footnote detection.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Maximum font size ratio to the body text for footnote lines.</summary>
    public double MaxSizeRatio { get; set; } = 0.9;

    internal FootnoteOptions Clone() => (FootnoteOptions)MemberwiseClone();
}

/// <summary>Root options of the PDF parser.</summary>
public sealed class PdfParserOptions
{
    /// <summary>Resource limits.</summary>
    public LimitsOptions Limits { get; set; } = new();

    /// <summary>Return a partial result when some pages cannot be read.</summary>
    public bool AllowPartialResult { get; set; }

    /// <summary>Text normalisation.</summary>
    public NormalizationOptions Normalization { get; set; } = new();

    /// <summary>Running header, footer and page number removal.</summary>
    public ArtifactOptions Artifacts { get; set; } = new();

    /// <summary>Line, paragraph and column layout.</summary>
    public LayoutOptions Layout { get; set; } = new();

    /// <summary>Heading detection.</summary>
    public HeadingOptions Headings { get; set; } = new();

    /// <summary>List detection.</summary>
    public ListOptions Lists { get; set; } = new();

    /// <summary>Table detection.</summary>
    public TableOptions Tables { get; set; } = new();

    /// <summary>Markdown rendering.</summary>
    public RenderingOptions Rendering { get; set; } = new();

    /// <summary>Footnote detection.</summary>
    public FootnoteOptions Footnotes { get; set; } = new();

    /// <summary>Creates a deep copy; changes to the copy do not affect this instance.</summary>
    public PdfParserOptions Clone() => new()
    {
        Limits = Limits.Clone(),
        AllowPartialResult = AllowPartialResult,
        Normalization = Normalization.Clone(),
        Artifacts = Artifacts.Clone(),
        Layout = Layout.Clone(),
        Headings = Headings.Clone(),
        Lists = Lists.Clone(),
        Tables = Tables.Clone(),
        Rendering = Rendering.Clone(),
        Footnotes = Footnotes.Clone(),
    };
}
