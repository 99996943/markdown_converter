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
/// <param name="GluedReferences">Footnote references glued to a word in the Markdown, not counted as excess.</param>
public sealed record NumberDiff(
    IReadOnlyDictionary<string, int> Excess,
    IReadOnlyDictionary<string, int> Missing,
    int PdfCount,
    int MarkdownCount,
    string? Skipped,
    int GluedReferences = 0);

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

    /// <summary>Maximum advance gap (in ems of the reference letter) between a superscript digit and the letter before it.</summary>
    private const double MaxSuperscriptGapEm = 0.5;

    /// <summary>Numbers of <paramref name="markdown"/> without page markers, footnote labels and escapes.</summary>
    public static IReadOnlyDictionary<string, int> FromMarkdown(string markdown)
    {
        var numbers = new Dictionary<string, int>(StringComparer.Ordinal);
        AddNumbers(numbers, Clean(markdown));
        return numbers;
    }

    /// <summary>Numbers of the PDF at <paramref name="path"/>; <see langword="null"/> without a text layer.</summary>
    public static IReadOnlyDictionary<string, int>? FromPdf(string path, ConversionReport report) =>
        FromPdf(File.ReadAllBytes(path), report);

    /// <summary>Numbers of <paramref name="pdf"/>; <see langword="null"/> without a text layer.</summary>
    public static IReadOnlyDictionary<string, int>? FromPdf(byte[] pdf, ConversionReport report) =>
        ReadPdf(pdf, report)?.Numbers;

    /// <summary>Compares the numbers of <paramref name="markdown"/> with the PDF at <paramref name="pdfPath"/>.</summary>
    public static NumberDiff Compare(string markdown, string pdfPath, ConversionReport report) =>
        Compare(markdown, File.ReadAllBytes(pdfPath), report, null);

    /// <summary>Compares the numbers of <paramref name="markdown"/> with <paramref name="pdf"/>.</summary>
    /// <remarks>
    /// An excess number glued to a letter in the Markdown („Tak4”, „PESEL17”, „1dodaniu”, „K16,17”) whose value is a
    /// superscript digit run of the PDF is a footnote reference the parser did not link (a known limitation, spec 009):
    /// it is counted in <see cref="NumberDiff.GluedReferences"/>, not as excess. Glued full-size numbers („CO2”, „MT940”)
    /// match the PDF anyway, and a superscript glued to a number („661”, „150.000”) stays excess.
    /// </remarks>
    public static NumberDiff Compare(string markdown, byte[] pdf, ConversionReport report) => Compare(markdown, pdf, report, null);

    /// <summary>Spec 010 (T013b): as <see cref="Compare(string, byte[], ConversionReport, LegalDocument?)"/>.</summary>
    public static NumberDiff Compare(string markdown, string pdfPath, ConversionReport report, LegalDocument? document) =>
        Compare(markdown, File.ReadAllBytes(pdfPath), report, document);

    /// <summary>Compares the numbers of <paramref name="markdown"/> with <paramref name="pdf"/>.</summary>
    /// <remarks>
    /// As <see cref="Compare(string, byte[], ConversionReport)"/>. Spec 010 (T013b): with <paramref name="document"/>,
    /// the digits of the original labels of its footnotes are not missing up to one per definition and one per
    /// <c>[^n]</c> reference — the parser prints them as footnote markers.
    /// </remarks>
    public static NumberDiff Compare(string markdown, byte[] pdf, ConversionReport report, LegalDocument? document)
    {
        IReadOnlyDictionary<string, int> fromMarkdown = FromMarkdown(markdown);
        PdfNumbers? fromPdf = ReadPdf(pdf, report);
        if (fromPdf is null)
        {
            return new NumberDiff(new Dictionary<string, int>(), new Dictionary<string, int>(), 0, fromMarkdown.Values.Sum(), "no text layer");
        }

        Dictionary<string, int> excess = Subtract(fromMarkdown, fromPdf.Numbers);
        int references = 0;
        var superscripts = new Dictionary<string, int>(fromPdf.Superscripts, StringComparer.Ordinal);
        foreach (Match m in GluedReference().Matches(Clean(markdown)))
        {
            string[] parts = m.Value.Split(',');
            if (excess.GetValueOrDefault(m.Value) == 0
                || parts.GroupBy(p => p, StringComparer.Ordinal).Any(g => superscripts.GetValueOrDefault(g.Key) < g.Count()))
            {
                continue;
            }

            foreach (string part in parts)
            {
                superscripts[part]--;
            }

            references += parts.Length;
            if (--excess[m.Value] == 0)
            {
                excess.Remove(m.Value);
            }
        }

        Dictionary<string, int> missing = Subtract(fromPdf.Numbers, fromMarkdown);
        if (document is not null)
        {
            missing = Subtract(missing, FootnoteLabels(markdown, document));
        }

        return new NumberDiff(
            excess,
            missing,
            fromPdf.Numbers.Values.Sum(),
            fromMarkdown.Values.Sum(),
            null,
            references);
    }

    /// <summary>Numeric original labels of the document's footnotes: one per definition and one per reference.</summary>
    private static Dictionary<string, int> FootnoteLabels(string markdown, LegalDocument document)
    {
        IEnumerable<Footnote> Of(Section section) => section.Footnotes.Concat(section.Children.SelectMany(Of));
        Dictionary<int, string> labels = document.PreambleFootnotes.Concat(document.Sections.SelectMany(Of))
            .ToDictionary(f => f.Number, f => f.OriginalLabel.TrimEnd(')'));
        var allowed = new Dictionary<string, int>(StringComparer.Ordinal);
        void Add(int number)
        {
            if (labels.TryGetValue(number, out string? label) && label.Length > 0 && label.All(char.IsAsciiDigit))
            {
                allowed[label] = allowed.GetValueOrDefault(label) + 1;
            }
        }

        foreach (int number in labels.Keys)
        {
            Add(number);
        }

        foreach (Match m in NumberedReference().Matches(markdown))
        {
            Add(int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
        }

        return allowed;
    }

    private static string Clean(string markdown)
    {
        string text = Comment().Replace(markdown, " ");
        text = FootnoteDefinition().Replace(text, " ");
        text = FootnoteReference().Replace(text, " ");
        return text.Replace("\\", string.Empty, StringComparison.Ordinal);
    }

    private static PdfNumbers? ReadPdf(byte[] pdf, ConversionReport report)
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
        var superscripts = new Dictionary<string, int>(StringComparer.Ordinal);
        bool anyText = false;
        using PdfDocument document = PdfDocument.Open(pdf);
        foreach (Page page in document.GetPages())
        {
            List<Token> tokens = Tokenize(page.Letters);
            anyText |= tokens.Count > 0;
            foreach (Token token in tokens.Where(t => t.Superscript))
            {
                superscripts[token.Text] = superscripts.GetValueOrDefault(token.Text) + 1;
            }

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

        return anyText ? new PdfNumbers(numbers, superscripts) : null;
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
        Letter? lastInk = null;
        bool lastSuperscript = false;
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

            bool superscript = reference is not null && lastInk is not null && IsRaisedSmallDigit(letter, reference, lastInk);

            // A small raised punctuation mark between superscript digits (the comma of „⁹,¹⁰”) is not a full letter.
            bool betweenSuperscripts = lastSuperscript
                && char.IsPunctuation(letter.Value[0])
                && IsSmallRaised(letter, reference!, lastInk!);
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
            lastInk = letter;
            lastSuperscript = superscript || betweenSuperscripts;
            if (!superscript && !betweenSuperscripts)
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

    /// <summary>
    /// The parser's superscript rule (<c>TextNormalizationStage</c>): one small digit raised above the previous full
    /// letter, set right after the letter before it (<paramref name="lastInk"/>, which may be a superscript digit) —
    /// small text far to the side of a large logo is not its superscript.
    /// </summary>
    private static bool IsRaisedSmallDigit(Letter letter, Letter reference, Letter lastInk) =>
        letter.Value.Length == 1
        && letter.Value[0] is >= '0' and <= '9'
        && IsSmallRaised(letter, reference, lastInk);

    private static bool IsSmallRaised(Letter letter, Letter reference, Letter lastInk) =>
        letter.Value.Length == 1
        && Math.Abs(letter.StartBaseLine.X - lastInk.EndBaseLine.X) <= MaxSuperscriptGapEm * reference.PointSize
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

    private sealed record PdfNumbers(Dictionary<string, int> Numbers, Dictionary<string, int> Superscripts);

    private sealed record Token(string Text, double X, double Baseline, double Size, bool Superscript);

    [GeneratedRegex("[0-9]+(?:[.,][0-9]+)*", RegexOptions.CultureInvariant)]
    private static partial Regex Number();

    [GeneratedRegex(@"(?<=\p{L})[0-9]{1,3}(?:,[0-9]{1,3})*(?![0-9.,]?[0-9])|(?<![0-9.,\p{L}])[0-9]{1,3}(?=\p{L})", RegexOptions.CultureInvariant)]
    private static partial Regex GluedReference();

    [GeneratedRegex("<!--.*?-->", RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex Comment();

    [GeneratedRegex(@"^\[\^[^\]]+\]:", RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex FootnoteDefinition();

    [GeneratedRegex(@"\[\^[^\]]+\]", RegexOptions.CultureInvariant)]
    private static partial Regex FootnoteReference();

    [GeneratedRegex(@"\[\^(\d+)\](?!:)", RegexOptions.CultureInvariant)]
    private static partial Regex NumberedReference();
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
