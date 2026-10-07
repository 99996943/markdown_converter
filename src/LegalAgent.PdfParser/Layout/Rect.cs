namespace LegalAgent.PdfParser.Layout;

/// <summary>Axis-aligned rectangle in points; Y grows downwards (0 = top of the page).</summary>
/// <param name="Left">Left edge.</param>
/// <param name="Top">Top edge.</param>
/// <param name="Right">Right edge.</param>
/// <param name="Bottom">Bottom edge.</param>
public readonly record struct Rect(double Left, double Top, double Right, double Bottom)
{
    /// <summary>Width of the rectangle.</summary>
    public double Width => Right - Left;

    /// <summary>Height of the rectangle.</summary>
    public double Height => Bottom - Top;

    /// <summary>Horizontal centre.</summary>
    public double CenterX => (Left + Right) / 2;

    /// <summary>Vertical centre.</summary>
    public double CenterY => (Top + Bottom) / 2;

    /// <summary>Smallest rectangle covering both rectangles.</summary>
    /// <param name="other">The other rectangle.</param>
    public Rect Union(Rect other) => new(
        Math.Min(Left, other.Left),
        Math.Min(Top, other.Top),
        Math.Max(Right, other.Right),
        Math.Max(Bottom, other.Bottom));

    /// <summary>Length of the vertical overlap with <paramref name="other"/> (0 when disjoint).</summary>
    /// <param name="other">The other rectangle.</param>
    public double VerticalOverlap(Rect other) => Math.Max(0, Math.Min(Bottom, other.Bottom) - Math.Max(Top, other.Top));

    /// <summary>Length of the horizontal overlap with <paramref name="other"/> (0 when disjoint).</summary>
    /// <param name="other">The other rectangle.</param>
    public double HorizontalOverlap(Rect other) => Math.Max(0, Math.Min(Right, other.Right) - Math.Max(Left, other.Left));
}
