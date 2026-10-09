using LegalAgent.Corpus.Composition;
using LegalAgent.Corpus.Truth;

namespace LegalAgent.Corpus.Typesetting;

/// <summary>
/// The body of a document set as one two-column table-document (FR-080), like the reference promotion terms: a frame
/// of rulings at x 54 / 181 / 541 with horizontal rulings drawn in two pieces, a bold column-name row on every page,
/// section names (level-2 headings) in the left cell on the baselines of the first content lines and the content in
/// the right cell. A row crossing a page continues with an empty left cell.
/// </summary>
internal sealed class TableDocumentLayout
{
    private const double FrameLeft = 54;
    private const double Divider = 181;
    private const double FrameRight = 541;
    private const double NameX = 60;
    private const double TextX = 186;
    private const double TextRight = 529;
    private const double BaselineBelowEdge = 17;
    private const double EdgeBelowBaseline = 8;
    private const double HeaderEdgeBelowBaseline = 11;
    private const string LeftColumnName = "Zagadnienie";
    private const string RightColumnName = "Postanowienia";

    private readonly PageWriter _w;
    private readonly List<double> _edges = [];
    private readonly Queue<SetLine> _names = new();
    private double _frameTop;
    private double _lastBaseline;
    private bool _open;
    private bool _rowOnPage;

    public TableDocumentLayout(PageWriter writer)
    {
        _w = writer;
        _w.BodyOverride = (TextX, TextRight - TextX);
        _w.PageStarting = () => Open(_w.Style.Top - BaselineBelowEdge - 3);
        _w.PageEnding = Close;
        _w.LinePlaced = y =>
        {
            _lastBaseline = y;
            if (_names.Count > 0)
            {
                _w.Draw(_names.Dequeue(), NameX, y, _w.Style.BodySize, record: false);
            }
        };

        Open(_w.Y - BaselineBelowEdge);
    }

    /// <summary>Starts a new row with the section name in the left cell.</summary>
    public void Section(HeadingElement heading)
    {
        string label = heading.Label is { Length: > 0 } l ? l + " " : string.Empty;
        string name = label + Inline.PlainText(heading.Text);
        _w.Truth.Headings.Add(new TruthHeading(heading.Level, heading.Label, Inline.PlainText(heading.Text)));
        FlushNames();

        List<SetLine> lines = TextMeasure.Wrap(TextMeasure.Tokenize([new Inline(name, InlineStyle.Bold)]), Divider - NameX - 6, _w.Style.BodySize);
        _w.Need(Math.Max(lines.Count, 3) * _w.Style.Leading);
        if (_rowOnPage)
        {
            double edge = _lastBaseline + EdgeBelowBaseline;
            _edges.Add(edge);
            _w.Y = edge + BaselineBelowEdge;
        }

        _w.Words(name);
        foreach (SetLine line in lines)
        {
            _names.Enqueue(line);
        }

        _rowOnPage = true;
    }

    /// <summary>Draws the name lines that did not get a content line of their own.</summary>
    public void FlushNames()
    {
        while (_names.Count > 0)
        {
            double y = _w.Y;
            _w.Draw(_names.Dequeue(), NameX, y, _w.Style.BodySize, record: false);
            _lastBaseline = y;
            _w.Y += _w.Style.Leading;
        }
    }

    private void Open(double frameTop)
    {
        _frameTop = Math.Max(frameTop, 60);
        _edges.Clear();
        _edges.Add(_frameTop);
        double baseline = _frameTop + BaselineBelowEdge;
        double size = _w.Style.BodySize;
        _w.Builder.Text(NameX + 5, baseline, LeftColumnName, size, bold: true);
        _w.Builder.Text(TextX, baseline, RightColumnName, size, bold: true);
        _w.Truth.Artifacts.Add(LeftColumnName + " " + RightColumnName);
        double edge = baseline + HeaderEdgeBelowBaseline;
        _edges.Add(edge);
        _lastBaseline = baseline;
        _w.Y = edge + BaselineBelowEdge;
        _open = true;
        _rowOnPage = false;
    }

    private void Close()
    {
        if (!_open)
        {
            return;
        }

        FlushNamesOnThisPage();
        double bottom = _lastBaseline + EdgeBelowBaseline;
        if (_edges[^1] < bottom - 0.5)
        {
            _edges.Add(bottom);
        }

        foreach (double edge in _edges)
        {
            _w.Builder.HLine(FrameLeft + 1, Divider, edge, 0.75);
            _w.Builder.HLine(Divider, FrameRight, edge, 0.75);
        }

        foreach (double x in new[] { FrameLeft, Divider, FrameRight })
        {
            _w.Builder.VLine(x, _frameTop, bottom, 0.75);
        }

        _open = false;
    }

    private void FlushNamesOnThisPage()
    {
        // Name lines left at the end of a page continue below the last content line (the row is closed by the page).
        while (_names.Count > 0 && _w.Y <= _w.Limit)
        {
            double y = _w.Y;
            _w.Draw(_names.Dequeue(), NameX, y, _w.Style.BodySize, record: false);
            _lastBaseline = y;
            _w.Y += _w.Style.Leading;
        }
    }
}
