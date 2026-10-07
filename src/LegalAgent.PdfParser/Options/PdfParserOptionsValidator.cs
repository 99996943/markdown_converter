using Microsoft.Extensions.Options;

namespace LegalAgent.PdfParser;

/// <summary>Validates <see cref="PdfParserOptions"/>: ratios in (0, 1], positive counts and sizes, depth 1-6.</summary>
public sealed class PdfParserOptionsValidator : IValidateOptions<PdfParserOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, PdfParserOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (options.Limits.MaxInputBytes is <= 0)
        {
            failures.Add("Limits.MaxInputBytes musi być dodatnie lub null.");
        }

        if (options.Limits.MaxPages is <= 0)
        {
            failures.Add("Limits.MaxPages musi być dodatnie lub null.");
        }

        if (options.Limits.MaxDuration is { } duration && duration <= TimeSpan.Zero)
        {
            failures.Add("Limits.MaxDuration musi być dodatnie lub null.");
        }

        Ratio(failures, "Artifacts.MarginZoneRatio", options.Artifacts.MarginZoneRatio);
        Ratio(failures, "Artifacts.MinPageRatio", options.Artifacts.MinPageRatio);
        Ratio(failures, "Artifacts.PositionTolerance", options.Artifacts.PositionTolerance);
        Ratio(failures, "Artifacts.Similarity", options.Artifacts.Similarity);
        Positive(failures, "Artifacts.MinPages", options.Artifacts.MinPages);

        Ratio(failures, "Layout.LineOverlapRatio", options.Layout.LineOverlapRatio);
        Ratio(failures, "Layout.BaselineToleranceRatio", options.Layout.BaselineToleranceRatio);
        Positive(failures, "Layout.ParagraphGapFactor", options.Layout.ParagraphGapFactor);
        Ratio(failures, "Layout.ShortLineRatio", options.Layout.ShortLineRatio);
        Ratio(failures, "Layout.GutterMinWidthRatio", options.Layout.GutterMinWidthRatio);
        Ratio(failures, "Layout.GutterMinHeightRatio", options.Layout.GutterMinHeightRatio);
        Ratio(failures, "Layout.ColumnMinLineWidthRatio", options.Layout.ColumnMinLineWidthRatio);

        Positive(failures, "Headings.SizeRatio", options.Headings.SizeRatio);
        Positive(failures, "Headings.SizeClusterTolerance", options.Headings.SizeClusterTolerance);
        Positive(failures, "Headings.MaxLength", options.Headings.MaxLength);
        Positive(failures, "Headings.MaxLines", options.Headings.MaxLines);
        Positive(failures, "Headings.GapFactor", options.Headings.GapFactor);
        Ratio(failures, "Headings.CenterTolerance", options.Headings.CenterTolerance);
        if (options.Headings.MaxTypographicDepth is < 1 or > 6)
        {
            failures.Add("Headings.MaxTypographicDepth musi mieścić się w zakresie 1-6.");
        }

        Positive(failures, "Lists.IndentTolerance", options.Lists.IndentTolerance);

        Positive(failures, "Tables.CellGapFactor", options.Tables.CellGapFactor);
        Positive(failures, "Tables.MinRows", options.Tables.MinRows);
        Ratio(failures, "Tables.ColumnTolerance", options.Tables.ColumnTolerance);
        Positive(failures, "Tables.RowMergeGapFactor", options.Tables.RowMergeGapFactor);

        Ratio(failures, "Footnotes.MaxSizeRatio", options.Footnotes.MaxSizeRatio);

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void Ratio(List<string> failures, string name, double value)
    {
        if (!(value > 0 && value <= 1))
        {
            failures.Add($"{name} musi należeć do przedziału (0, 1].");
        }
    }

    private static void Positive(List<string> failures, string name, double value)
    {
        if (!(value > 0) || double.IsInfinity(value))
        {
            failures.Add($"{name} musi być dodatnie.");
        }
    }
}
