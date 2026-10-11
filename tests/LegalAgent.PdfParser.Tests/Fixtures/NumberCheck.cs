using System.Text.RegularExpressions;
using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace LegalAgent.PdfParser.Tests.Fixtures;

/// <summary>
/// Numbers of a converted document compared with the PDF text layer (spec 009, US2): multisets value → count.
/// </summary>
/// <param name="Excess">Numbers occurring more often in the Markdown than in the PDF (changed, glued).</param>
/// <param name="Missing">Numbers occurring more often in the PDF than in the Markdown (outside removed artifacts).</param>
/// <param name="PdfCount">Number tokens of the PDF.</param>
/// <param name="MarkdownCount">Number tokens of the Markdown.</param>
/// <param name="Skipped">Why the document was not measured (no text layer), or <see langword="null"/>.</param>
public sealed record NumberDiff(
    IReadOnlyDictionary<string, int> Excess,
    IReadOnlyDictionary<string, int> Missing,
    int PdfCount,
    int MarkdownCount,
    string? Skipped);

/// <summary>
/// Reads numbers <c>[0-9]+(?:[.,][0-9]+)*</c> from Markdown and directly from the PDF letters (PdfPig, not the parser,
/// so parser errors are not repeated). Small raised digits (superscripts, footnote references) are not numbers;
/// raw PDF lines whose fingerprint is a removed artifact of their page are skipped (spec 009, research R2).
/// </summary>
public static partial class NumberCheck
{
    private const double GapEm = 0.15;
    private const double BaselineShiftRatio = 0.2;
    private const double MaxSuperscriptSizeRatio = 0.8;
    private const double MinRaiseRatio = 0.15;

    /// <summary>Numbers of <paramref name="markdown"/> without page markers, footnote labels and escapes.</summary>
    public static IReadOnlyDictionary<string, int> FromMarkdown(string markdown)
    {
        string text = Comment().Replace(markdown, " ");
        text = FootnoteDefinition().Replace(text, " ");
        text = FootnoteReference().Replace(text, " ");
        text = text.Replace("\\", string.Empty, StringComparison.Ordinal);
        var numbers = new Dictionary<string, int>(StringComparer.Ordinal);
        AddNumbers(numbers, text);
        return numbers;
    }

    /// <summary>Numbers of the PDF at <paramref name="path"/>; <see langword="null"/> without a text layer.</summary>
    public static IReadOnlyDictionary<string, int>? FromPdf(string path, ConversionReport report) =>
        FromPdf(File.ReadAllBytes(path), report);

    /// <summary>Numbers of <paramref name="pdf"/>; <see langword="null"/> without a text layer.</summary>
    public static IReadOnlyDictionary<string, int>? FromPdf(byte[] pdf, ConversionReport report)
    {
        var artifacts = new HashSet<(int Page, string Pattern)>();
        foreach (ArtifactSummary artifact in report.RemovedArtifacts)
        {
            foreach (int page in artifact.Pages)
            {
                artifacts.Add((page, artifact.Pattern));
            }
        }

        var numbers = new Dictionary<string, int>(StringComparer.Ordinal);
        bool anyText = false;
        using PdfDocument document = PdfDocument.Open(pdf);
        foreach (Page page in document.GetPages())
        {
            List<Token> tokens = Tokenize(page.Letters);
            anyText |= tokens.Count > 0;
            foreach (List<Token> line in RawLines(tokens))
            {
                string fingerprint = LineFingerprint.Compute(string.Join(' ', line.Select(t => t.Text)));
                if (artifacts.Contains((page.Number, fingerprint)))
                {
                    continue;
                }

                foreach (Token token in line)
                {
                    AddNumbers(numbers, token.Text);
                }
            }
        }

        return anyText ? numbers : null;
    }

    /// <summary>Compares the numbers of <paramref name="markdown"/> with the PDF at <paramref name="pdfPath"/>.</summary>
    public static NumberDiff Compare(string markdown, string pdfPath, ConversionReport report) =>
        Compare(markdown, File.ReadAllBytes(pdfPath), report);

    /// <summary>Compares the numbers of <paramref name="markdown"/> with <paramref name="pdf"/>.</summary>
    public static NumberDiff Compare(string markdown, byte[] pdf, ConversionReport report)
    {
        IReadOnlyDictionary<string, int> fromMarkdown = FromMarkdown(markdown);
        IReadOnlyDictionary<string, int>? fromPdf = FromPdf(pdf, report);
        if (fromPdf is null)
        {
            return new NumberDiff(new Dictionary<string, int>(), new Dictionary<string, int>(), 0, fromMarkdown.Values.Sum(), "no text layer");
        }

        return new NumberDiff(
            Subtract(fromMarkdown, fromPdf),
            Subtract(fromPdf, fromMarkdown),
            fromPdf.Values.Sum(),
            fromMarkdown.Values.Sum(),
            null);
    }

    /// <summary>Values with counts ordered by value: <c>661×1, 682×1</c>.</summary>
    public static string Format(IReadOnlyDictionary<string, int> numbers) =>
        string.Join(", ", numbers.Where(n => n.Value > 0).OrderBy(n => n.Key, StringComparer.Ordinal).Select(n => $"{n.Key}×{n.Value}"));

    /// <summary>Entries of <paramref name="a"/> with the counts of <paramref name="b"/> taken away (positive only).</summary>
    public static Dictionary<string, int> Subtract(IReadOnlyDictionary<string, int> a, IReadOnlyDictionary<string, int> b)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach ((string value, int count) in a)
        {
            int left = count - b.GetValueOrDefault(value);
            if (left > 0)
            {
                result[value] = left;
            }
        }

        return result;
    }

    private static void AddNumbers(Dictionary<string, int> numbers, string text)
    {
        foreach (Match m in Number().Matches(text))
        {
            numbers[m.Value] = numbers.GetValueOrDefault(m.Value) + 1;
        }
    }

    /// <summary>Letters in content-stream order split at white space, gaps, baseline shifts and superscript digits.</summary>
    private static List<Token> Tokenize(IReadOnlyList<Letter> letters)
    {
        var tokens = new List<Token>();
        var text = new System.Text.StringBuilder();
        Letter? previous = null;
        Letter? reference = null;
        bool currentSuperscript = false;
        double x = 0, baseline = 0, size = 0;

        void Flush()
        {
            if (text.Length > 0)
            {
                tokens.Add(new Token(text.ToString(), x, baseline, size, currentSuperscript));
                text.Clear();
            }
        }

        foreach (Letter letter in letters)
        {
            if (string.IsNullOrWhiteSpace(letter.Value))
            {
                Flush();
                previous = null;
                continue;
            }

            bool superscript = reference is not null && IsRaisedSmallDigit(letter, reference);
            if (previous is not null && (superscript != currentSuperscript || Breaks(previous, letter)))
            {
                Flush();
            }

            if (text.Length == 0)
            {
                x = letter.StartBaseLine.X;
                baseline = letter.StartBaseLine.Y;
                size = letter.PointSize;
                currentSuperscript = superscript;
            }

            text.Append(letter.Value);
            previous = letter;
            if (!superscript)
            {
                reference = letter;
            }
        }

        Flush();
        return tokens;
    }

    private static bool Breaks(Letter previous, Letter letter)
    {
        double em = Math.Max(previous.PointSize, letter.PointSize);
        double gap = letter.StartBaseLine.X - previous.EndBaseLine.X;
        return Math.Abs(gap) > GapEm * em
            || Math.Abs(letter.StartBaseLine.Y - previous.StartBaseLine.Y) > BaselineShiftRatio * em;
    }

    /// <summary>The parser's superscript rule (<c>TextNormalizationStage</c>): one small digit raised above the previous full letter.</summary>
    private static bool IsRaisedSmallDigit(Letter letter, Letter reference) =>
        letter.Value.Length == 1
        && letter.Value[0] is >= '0' and <= '9'
        && letter.PointSize <= reference.PointSize * MaxSuperscriptSizeRatio
        && letter.StartBaseLine.Y >= reference.StartBaseLine.Y + (reference.PointSize * MinRaiseRatio);

    /// <summary>Full-size tokens grouped by baseline (top to bottom), each line ordered by x; superscripts are left out.</summary>
    private static List<List<Token>> RawLines(List<Token> tokens)
    {
        var lines = new List<List<Token>>();
        foreach (Token token in tokens.Where(t => !t.Superscript).OrderByDescending(t => t.Baseline).ThenBy(t => t.X))
        {
            List<Token>? line = lines.Count > 0 ? lines[^1] : null;
            if (line is null || Math.Abs(line[0].Baseline - token.Baseline) > BaselineShiftRatio * Math.Max(line[0].Size, token.Size))
            {
                lines.Add([token]);
            }
            else
            {
                line.Add(token);
            }
        }

        foreach (List<Token> line in lines)
        {
            line.Sort((a, b) => a.X.CompareTo(b.X));
        }

        return lines;
    }

    private sealed record Token(string Text, double X, double Baseline, double Size, bool Superscript);

    [GeneratedRegex("[0-9]+(?:[.,][0-9]+)*", RegexOptions.CultureInvariant)]
    private static partial Regex Number();

    [GeneratedRegex("<!--.*?-->", RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex Comment();

    [GeneratedRegex(@"^\[\^[^\]]+\]:", RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex FootnoteDefinition();

    [GeneratedRegex(@"\[\^[^\]]+\]", RegexOptions.CultureInvariant)]
    private static partial Regex FootnoteReference();
}

/// <summary>
/// Logic of the private number gate (spec 009, contracts/number-check.md): a list of documents
/// <c>name[;allowed,…]</c> with <c>#</c> comments; a listed document fails on excess numbers not allowed by the list.
/// </summary>
public static class RegulationGate
{
    /// <summary>One failure message per listed document, in list order.</summary>
    public static IReadOnlyList<string> Evaluate(string listText, IReadOnlyDictionary<string, NumberDiff> diffs)
    {
        var failures = new List<string>();
        foreach (string raw in listText.Split('\n'))
        {
            int comment = raw.IndexOf('#', StringComparison.Ordinal);
            string line = (comment >= 0 ? raw[..comment] : raw).Trim();
            if (line.Length == 0)
            {
                continue;
            }

            string[] parts = line.Split(';', 2);
            string name = parts[0].Trim();
            var allowed = new Dictionary<string, int>(StringComparer.Ordinal);
            if (parts.Length == 2)
            {
                foreach (string value in parts[1].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                {
                    allowed[value] = allowed.GetValueOrDefault(value) + 1;
                }
            }

            if (!diffs.TryGetValue(name, out NumberDiff? diff))
            {
                failures.Add($"{name}: no such PDF");
                continue;
            }

            if (diff.Skipped is not null)
            {
                failures.Add($"{name}: pominięty ({diff.Skipped})");
                continue;
            }

            Dictionary<string, int> excess = NumberCheck.Subtract(diff.Excess, allowed);
            if (excess.Count > 0)
            {
                failures.Add($"{name}: nadmiarowe {NumberCheck.Format(excess)}");
            }
        }

        return failures;
    }
}
