using LegalAgent.PdfParser.Layout;

namespace LegalAgent.PdfParser.Tests.Unit.Layout;

/// <summary>T074 — column band clustering (FR-061, FR-066).</summary>
public sealed class ColumnClusteringTests
{
    private const double Tol = 17.85;

    private static readonly double[] Lefts = [56, 57.7, 54.8, 85, 85.3, 501.9, 509, 511];

    [Fact]
    public void Lefts_within_tolerance_cluster_into_three_bands()
    {
        IReadOnlyList<double> result = ColumnClustering.ClusterLefts(Lefts, Tol);
        Assert.Equal([54.8, 85, 501.9], result);
    }

    [Fact]
    public void Input_order_does_not_matter()
    {
        IReadOnlyList<double> expected = ColumnClustering.ClusterLefts(Lefts, Tol);
        Assert.Equal(expected, ColumnClustering.ClusterLefts(Lefts.Reverse(), Tol));
        Assert.Equal(expected, ColumnClustering.ClusterLefts([85, 511, 54.8, 501.9, 85.3, 57.7, 509, 56], Tol));
    }

    [Fact]
    public void Value_exactly_at_tolerance_joins_and_just_above_splits()
    {
        Assert.Equal([0.0], ColumnClustering.ClusterLefts([0, 10], 10));
        Assert.Equal([0.0, 10.01], ColumnClustering.ClusterLefts([0, 10.01], 10));
    }

    [Fact]
    public void Empty_input_gives_empty_result()
    {
        Assert.Empty(ColumnClustering.ClusterLefts([], Tol));
    }

    [Fact]
    public void Bands_are_built_from_lefts_and_region_right()
    {
        IReadOnlyList<ColumnBand> bands = ColumnClustering.Bands([56, 211, 400], 550, [], Tol);
        Assert.Equal([new ColumnBand(56, 211), new ColumnBand(211, 400), new ColumnBand(400, 550)], bands);
    }

    [Fact]
    public void Ruling_snaps_boundary()
    {
        IReadOnlyList<ColumnBand> bands = ColumnClustering.Bands([56, 211, 400], 550, [205], Tol);
        Assert.Equal([new ColumnBand(56, 205), new ColumnBand(205, 400), new ColumnBand(400, 550)], bands);
    }

    [Fact]
    public void Ruling_too_far_is_ignored()
    {
        IReadOnlyList<ColumnBand> bands = ColumnClustering.Bands([56, 211, 400], 550, [180, 100], Tol);
        Assert.Equal([new ColumnBand(56, 211), new ColumnBand(211, 400), new ColumnBand(400, 550)], bands);
    }

    [Fact]
    public void Ruling_right_of_left_is_ignored()
    {
        IReadOnlyList<ColumnBand> bands = ColumnClustering.Bands([56, 211, 400], 550, [215], Tol);
        Assert.Equal(new ColumnBand(211, 400), bands[1]);
    }

    [Fact]
    public void Ruling_snaps_first_band_left()
    {
        IReadOnlyList<ColumnBand> bands = ColumnClustering.Bands([56, 211], 550, [50], Tol);
        Assert.Equal([new ColumnBand(50, 211), new ColumnBand(211, 550)], bands);
    }

    [Fact]
    public void Closest_ruling_wins_and_ties_pick_smaller_x()
    {
        Assert.Equal(new ColumnBand(208, 400), ColumnClustering.Bands([56, 211, 400], 550, [200, 208], Tol)[1]);
        Assert.Equal(new ColumnBand(205, 400), ColumnClustering.Bands([56, 211, 400], 550, [217, 205, 217], Tol)[1]);
        Assert.Equal(new ColumnBand(205, 400), ColumnClustering.Bands([56, 211, 400], 550, [217, 205], Tol)[1]);
    }

    private static readonly ColumnBand[] ThreeBands = [new(56, 211), new(211, 400), new(400, 550)];

    [Fact]
    public void BandIndex_finds_band()
    {
        Assert.Equal(0, ColumnClustering.BandIndex(ThreeBands, 60, Tol));
        Assert.Equal(1, ColumnClustering.BandIndex(ThreeBands, 211, Tol));
        Assert.Equal(2, ColumnClustering.BandIndex(ThreeBands, 450, Tol));
    }

    [Fact]
    public void BandIndex_accepts_x_at_left_minus_tolerance()
    {
        Assert.Equal(1, ColumnClustering.BandIndex(ThreeBands, 211 - Tol, Tol));
        Assert.Equal(0, ColumnClustering.BandIndex(ThreeBands, 56 - Tol, Tol));
    }

    [Fact]
    public void BandIndex_far_left_is_minus_one()
    {
        Assert.Equal(-1, ColumnClustering.BandIndex(ThreeBands, 10, Tol));
    }

    [Fact]
    public void Span_is_one_for_normal_cell()
    {
        Assert.Equal(1, ColumnClustering.Span(ThreeBands, 60, 180, Tol));
        Assert.Equal(1, ColumnClustering.Span(ThreeBands, 60, 211 + 5, Tol));
    }

    [Fact]
    public void Span_covers_all_bands_for_centred_section_row()
    {
        Assert.Equal(3, ColumnClustering.Span(ThreeBands, 56, 540, Tol));
        Assert.Equal(2, ColumnClustering.Span(ThreeBands, 60, 300, Tol));
    }

    [Fact]
    public void Span_is_zero_when_out_of_range()
    {
        Assert.Equal(0, ColumnClustering.Span(ThreeBands, 10, 100, Tol));
    }

    [Fact]
    public void Empty_bands_give_minus_one_and_zero()
    {
        Assert.Equal(-1, ColumnClustering.BandIndex([], 50, Tol));
        Assert.Equal(0, ColumnClustering.Span([], 50, 100, Tol));
    }
}
