namespace LegalAgent.PdfParser.Layout;

/// <summary>A straight line segment (for example a table ruling) in points; Y grows downwards.</summary>
/// <param name="X1">Start X.</param>
/// <param name="Y1">Start Y.</param>
/// <param name="X2">End X.</param>
/// <param name="Y2">End Y.</param>
public readonly record struct Segment(double X1, double Y1, double X2, double Y2)
{
    private const double Epsilon = 0.5;

    /// <summary>True when the segment is horizontal within half a point.</summary>
    public bool IsHorizontal => Math.Abs(Y1 - Y2) <= Epsilon && Math.Abs(X1 - X2) > Epsilon;

    /// <summary>True when the segment is vertical within half a point.</summary>
    public bool IsVertical => Math.Abs(X1 - X2) <= Epsilon && Math.Abs(Y1 - Y2) > Epsilon;

    /// <summary>Length of the segment.</summary>
    public double Length => Math.Sqrt(((X2 - X1) * (X2 - X1)) + ((Y2 - Y1) * (Y2 - Y1)));
}
