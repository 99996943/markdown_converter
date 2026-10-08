namespace LegalAgent.PdfParser.Model;

/// <summary>Inclusive range of 1-based source page numbers.</summary>
public readonly record struct PageRange
{
    /// <summary>Creates a range; requires <c>1 &lt;= first &lt;= last</c>.</summary>
    /// <param name="first">First page (1-based).</param>
    /// <param name="last">Last page (inclusive).</param>
    /// <exception cref="ArgumentOutOfRangeException">The invariant is violated.</exception>
    public PageRange(int first, int last)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(first, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(last, first);
        First = first;
        Last = last;
    }

    /// <summary>First page (1-based).</summary>
    public int First { get; }

    /// <summary>Last page (inclusive).</summary>
    public int Last { get; }

    /// <summary>Returns the smallest range covering this range and <paramref name="other"/>.</summary>
    /// <param name="other">The other range.</param>
    public PageRange Union(PageRange other) => new(Math.Min(First, other.First), Math.Max(Last, other.Last));
}
