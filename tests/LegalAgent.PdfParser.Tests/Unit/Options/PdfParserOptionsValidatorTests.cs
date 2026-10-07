using Microsoft.Extensions.Options;

namespace LegalAgent.PdfParser.Tests.Unit.Options;

public sealed class PdfParserOptionsValidatorTests
{
    private static ValidateOptionsResult Validate(PdfParserOptions options) =>
        new PdfParserOptionsValidator().Validate(Microsoft.Extensions.Options.Options.DefaultName, options);

    public static TheoryData<string, Action<PdfParserOptions, double>> RatioSetters => new()
    {
        { "Artifacts.MarginZoneRatio", (o, v) => o.Artifacts.MarginZoneRatio = v },
        { "Artifacts.MinPageRatio", (o, v) => o.Artifacts.MinPageRatio = v },
        { "Artifacts.PositionTolerance", (o, v) => o.Artifacts.PositionTolerance = v },
        { "Artifacts.Similarity", (o, v) => o.Artifacts.Similarity = v },
        { "Layout.LineOverlapRatio", (o, v) => o.Layout.LineOverlapRatio = v },
        { "Layout.BaselineToleranceRatio", (o, v) => o.Layout.BaselineToleranceRatio = v },
        { "Layout.ShortLineRatio", (o, v) => o.Layout.ShortLineRatio = v },
        { "Layout.GutterMinWidthRatio", (o, v) => o.Layout.GutterMinWidthRatio = v },
        { "Layout.GutterMinHeightRatio", (o, v) => o.Layout.GutterMinHeightRatio = v },
        { "Layout.ColumnMinLineWidthRatio", (o, v) => o.Layout.ColumnMinLineWidthRatio = v },
        { "Headings.CenterTolerance", (o, v) => o.Headings.CenterTolerance = v },
        { "Tables.ColumnTolerance", (o, v) => o.Tables.ColumnTolerance = v },
        { "Footnotes.MaxSizeRatio", (o, v) => o.Footnotes.MaxSizeRatio = v },
    };

    public static TheoryData<string, Action<PdfParserOptions>> NonPositiveValues => new()
    {
        { "Limits.MaxInputBytes", o => o.Limits.MaxInputBytes = 0 },
        { "Limits.MaxPages", o => o.Limits.MaxPages = -1 },
        { "Limits.MaxDuration", o => o.Limits.MaxDuration = TimeSpan.Zero },
        { "Artifacts.MinPages", o => o.Artifacts.MinPages = 0 },
        { "Layout.ParagraphGapFactor", o => o.Layout.ParagraphGapFactor = 0 },
        { "Headings.SizeRatio", o => o.Headings.SizeRatio = -1 },
        { "Headings.SizeClusterTolerance", o => o.Headings.SizeClusterTolerance = 0 },
        { "Headings.MaxLength", o => o.Headings.MaxLength = 0 },
        { "Headings.MaxLines", o => o.Headings.MaxLines = 0 },
        { "Headings.GapFactor", o => o.Headings.GapFactor = 0 },
        { "Lists.IndentTolerance", o => o.Lists.IndentTolerance = 0 },
        { "Tables.CellGapFactor", o => o.Tables.CellGapFactor = 0 },
        { "Tables.MinRows", o => o.Tables.MinRows = 0 },
        { "Tables.RowMergeGapFactor", o => o.Tables.RowMergeGapFactor = -0.5 },
    };

    [Fact]
    public void Defaults_AreValid()
    {
        Assert.True(Validate(new PdfParserOptions()).Succeeded);
    }

    [Fact]
    public void Defaults_MatchSpecification()
    {
        var o = new PdfParserOptions();
        Assert.Equal(104_857_600L, o.Limits.MaxInputBytes);
        Assert.Equal(2000, o.Limits.MaxPages);
        Assert.Equal(TimeSpan.FromSeconds(120), o.Limits.MaxDuration);
        Assert.False(o.AllowPartialResult);
        Assert.Equal(0.08, o.Artifacts.MarginZoneRatio);
        Assert.Equal(0.85, o.Artifacts.Similarity);
        Assert.Equal(0.75, o.Layout.ShortLineRatio);
        Assert.Equal(0.02, o.Layout.GutterMinWidthRatio);
        Assert.Equal(0.6, o.Layout.GutterMinHeightRatio);
        Assert.Equal(0.3, o.Layout.ColumnMinLineWidthRatio);
        Assert.Equal(1.15, o.Headings.SizeRatio);
        Assert.Equal(1.3, o.Headings.GapFactor);
        Assert.Equal(0.05, o.Headings.CenterTolerance);
        Assert.Equal(3, o.Headings.MaxTypographicDepth);
        Assert.Equal(0.03, o.Tables.ColumnTolerance);
        Assert.Equal(1.2, o.Tables.RowMergeGapFactor);
        Assert.True(o.Rendering.PageMarkers);
        Assert.Equal(FootnotesPlacement.EndOfSection, o.Rendering.FootnotesPlacement);
        Assert.Equal(0.9, o.Footnotes.MaxSizeRatio);
    }

    [Theory]
    [MemberData(nameof(RatioSetters))]
    public void Ratio_OutsideZeroExclusiveToOneInclusive_IsInvalid(string name, Action<PdfParserOptions, double> set)
    {
        foreach (double bad in new[] { 0.0, -0.1, 1.01, double.NaN })
        {
            var o = new PdfParserOptions();
            set(o, bad);
            ValidateOptionsResult result = Validate(o);
            Assert.True(result.Failed, $"{name} = {bad} should be invalid");
            Assert.Contains(result.Failures!, f => f.Contains(name, StringComparison.Ordinal));
        }
    }

    [Theory]
    [MemberData(nameof(RatioSetters))]
    public void Ratio_AtUpperBound_IsValid(string name, Action<PdfParserOptions, double> set)
    {
        var o = new PdfParserOptions();
        set(o, 1.0);
        Assert.True(Validate(o).Succeeded, name);
    }

    [Theory]
    [MemberData(nameof(NonPositiveValues))]
    public void NonPositiveCountsAndSizes_AreInvalid(string name, Action<PdfParserOptions> set)
    {
        var o = new PdfParserOptions();
        set(o);
        ValidateOptionsResult result = Validate(o);
        Assert.True(result.Failed, name);
        Assert.Contains(result.Failures!, f => f.Contains(name, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(-1)]
    public void MaxTypographicDepth_OutsideOneToSix_IsInvalid(int depth)
    {
        var o = new PdfParserOptions();
        o.Headings.MaxTypographicDepth = depth;
        ValidateOptionsResult result = Validate(o);
        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("Headings.MaxTypographicDepth", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    public void MaxTypographicDepth_BetweenOneAndSix_IsValid(int depth)
    {
        var o = new PdfParserOptions();
        o.Headings.MaxTypographicDepth = depth;
        Assert.True(Validate(o).Succeeded);
    }

    [Fact]
    public void NullLimits_AreAllowedAndDisableTheLimit()
    {
        var o = new PdfParserOptions();
        o.Limits.MaxInputBytes = null;
        o.Limits.MaxPages = null;
        o.Limits.MaxDuration = null;
        Assert.True(Validate(o).Succeeded);
    }

    [Fact]
    public void MultipleProblems_AreAllReported()
    {
        var o = new PdfParserOptions();
        o.Artifacts.Similarity = 2;
        o.Tables.MinRows = 0;
        ValidateOptionsResult result = Validate(o);
        Assert.Equal(2, result.Failures!.Count());
    }

    [Fact]
    public void Clone_IsDeep()
    {
        var original = new PdfParserOptions();
        original.Normalization.HyphenationExceptions.Add("sport-klub");
        PdfParserOptions copy = original.Clone();

        Assert.NotSame(original, copy);
        Assert.NotSame(original.Limits, copy.Limits);
        Assert.NotSame(original.Normalization, copy.Normalization);
        Assert.NotSame(original.Normalization.HyphenationExceptions, copy.Normalization.HyphenationExceptions);
        Assert.NotSame(original.Artifacts, copy.Artifacts);
        Assert.NotSame(original.Layout, copy.Layout);
        Assert.NotSame(original.Headings, copy.Headings);
        Assert.NotSame(original.Lists, copy.Lists);
        Assert.NotSame(original.Tables, copy.Tables);
        Assert.NotSame(original.Rendering, copy.Rendering);
        Assert.NotSame(original.Footnotes, copy.Footnotes);

        copy.Limits.MaxPages = 5;
        copy.Artifacts.Similarity = 0.5;
        copy.Normalization.HyphenationExceptions.Add("extra");
        copy.Rendering.PageMarkers = false;

        Assert.Equal(2000, original.Limits.MaxPages);
        Assert.Equal(0.85, original.Artifacts.Similarity);
        Assert.DoesNotContain("extra", original.Normalization.HyphenationExceptions);
        Assert.True(original.Rendering.PageMarkers);
        Assert.Contains("sport-klub", copy.Normalization.HyphenationExceptions);
    }
}
