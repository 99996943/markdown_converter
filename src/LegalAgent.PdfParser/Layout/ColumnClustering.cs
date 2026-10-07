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
    {
        ArgumentNullException.ThrowIfNull(lefts);
        List<double> sorted = [.. lefts];
        sorted.Sort();
        List<double> minimums = [];
        foreach (double value in sorted)
        {
            if (minimums.Count == 0 || value - minimums[^1] > tolerance)
            {
                minimums.Add(value);
            }
        }

        return minimums;
    }

    /// <summary>
    /// Builds bands from cluster lefts (ascending): band i = [left_i, left_{i+1}), last band = [left_last, regionRight].
    /// A vertical ruling close to a band's left edge (within tolerance, not right of it) moves that boundary.
    /// </summary>
    public static IReadOnlyList<ColumnBand> Bands(IReadOnlyList<double> lefts, double regionRight, IEnumerable<double> verticalRulingXs, double tolerance)
    {
        ArgumentNullException.ThrowIfNull(lefts);
        ArgumentNullException.ThrowIfNull(verticalRulingXs);
        double[] rulings = [.. verticalRulingXs];
        int n = lefts.Count;
        double[] starts = new double[n];
        for (int i = 0; i < n; i++)
        {
            double left = lefts[i];
            double? best = null;
            foreach (double x in rulings)
            {
                bool inWindow = i == 0
                    ? x >= left - tolerance && x <= left
                    : x > lefts[i - 1] && x <= left && left - x <= tolerance;
                if (!inWindow)
                {
                    continue;
                }

                if (best is null || left - x < left - best.Value || (left - x == left - best.Value && x < best.Value))
                {
                    best = x;
                }
            }

            starts[i] = best ?? left;
        }

        ColumnBand[] bands = new ColumnBand[n];
        for (int i = 0; i < n; i++)
        {
            double right = i + 1 < n ? starts[i + 1] : regionRight;
            bands[i] = new ColumnBand(starts[i], right);
        }

        return bands;
    }

    /// <summary>Index of the band a segment starting at x belongs to: the last band with Left - tolerance &lt;= x; -1 when none.</summary>
    public static int BandIndex(IReadOnlyList<ColumnBand> bands, double x, double tolerance)
    {
        ArgumentNullException.ThrowIfNull(bands);
        for (int i = bands.Count - 1; i >= 0; i--)
        {
            if (bands[i].Left - tolerance <= x)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Number of bands a segment [left, right] covers starting at its band: 1 + count of following bands whose Left is
    /// &lt; right - tolerance (text crossing a column boundary, FR-066). Returns 0 when BandIndex is -1.
    /// </summary>
    public static int Span(IReadOnlyList<ColumnBand> bands, double left, double right, double tolerance)
    {
        int first = BandIndex(bands, left, tolerance);
        if (first < 0)
        {
            return 0;
        }

        int span = 1;
        for (int i = first + 1; i < bands.Count && bands[i].Left < right - tolerance; i++)
        {
            span++;
        }

        return span;
    }
}
