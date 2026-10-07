namespace LegalAgent.PdfParser.Layout;

/// <summary>A table column band: segments whose left edge lies in [Left, Right) belong to it.</summary>
internal readonly record struct ColumnBand(double Left, double Right);

/// <summary>Column band clustering for table detection (FR-061, FR-066).</summary>
internal static class ColumnClustering
{
    /// <summary>
    /// Clusters left edges of cell segments: sort ascending; a value joins the current cluster when
    /// value - (cluster minimum) &lt;= tolerance, otherwise it starts a new cluster. Returns the cluster minimums ascending.
    /// </summary>
    public static IReadOnlyList<double> ClusterLefts(IEnumerable<double> lefts, double tolerance)
        => throw new NotImplementedException();

    /// <summary>Builds bands from cluster lefts, optionally snapping boundaries to vertical rulings.</summary>
    public static IReadOnlyList<ColumnBand> Bands(IReadOnlyList<double> lefts, double regionRight, IEnumerable<double> verticalRulingXs, double tolerance)
        => throw new NotImplementedException();

    /// <summary>Index of the band a segment starting at x belongs to; -1 when left of every band by more than tolerance.</summary>
    public static int BandIndex(IReadOnlyList<ColumnBand> bands, double x, double tolerance)
        => throw new NotImplementedException();

    /// <summary>Number of bands a segment [left, right] covers starting at its band; 0 when out of range.</summary>
    public static int Span(IReadOnlyList<ColumnBand> bands, double left, double right, double tolerance)
        => throw new NotImplementedException();
}
