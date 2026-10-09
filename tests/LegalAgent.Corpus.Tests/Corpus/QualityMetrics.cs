using System.Globalization;
using System.Text.RegularExpressions;
using LegalAgent.Corpus.Truth;

namespace LegalAgent.Corpus.Tests.Corpus;

/// <summary>Quality measurements of one document: library Markdown against the reference truth (SC-022 – SC-026).</summary>
internal sealed record QualityReport(
    string DocumentId,
    double WordCompleteness,
    int ExtraWords,
    IReadOnlyList<string> ExtraWordSamples,
    double ReadingOrder,
    double HeadingRecall,
    double FalseHeadingShare,
    double ListRecall,
    double RowsIntact,
    double TablesAsSingleGfm,
    double CellAgreement,
    IReadOnlyList<string> Failures);

/// <summary>
/// Compares the Markdown produced by the library with the reference truth recorded while typesetting (FR-162, FR-164).
/// <para>
/// Markdown words: <c>&lt;!-- page: N --&gt;</c> markers (also in the middle of a line), heading <c>#</c> prefixes, list
/// bullets <c>- </c> at the start of a line (after indentation), table pipes and separator rows, <c>**</c>/<c>*</c> emphasis,
/// footnote definition prefixes <c>[^n]:</c> and references <c>[^n]</c> are removed, <c>\X</c> becomes <c>X</c>, the rest is
/// split on whitespace.
/// </para>
/// <para>
/// Token comparison (both sides): leading and trailing characters of <c>.,;:!?()„”"'</c> are trimmed and the rest compared
/// ordinally; tokens that become empty are dropped (so <c>1)</c>, <c>1.</c> and <c>1</c> agree, and the library's
/// <c>Rozdział 1. Postanowienia</c> matches the truth words <c>Rozdział</c> <c>1</c> <c>Postanowienia</c>). Artifacts are
/// never truth words.
/// </para>
/// <para>
/// Headings: a Markdown heading matches a truth heading when its token sequence equals the tokens of
/// <c>Label + " " + Text</c>; levels are compared as ranks among the distinct levels present on each side; matching is
/// greedy in document order and every Markdown heading is used once. List items are Markdown lines <c>^( *)- (.*)$</c>
/// (depth = indentation / 2, label = first token); a truth item matches by label and the first three words (checklist
/// <c>□</c>: by the first words only, which may follow a checkbox of up to two tokens), depths compared as ranks.
/// Tables are maximal runs of lines starting with <c>|</c> (a page marker between two rows does not break the run);
/// a truth row is intact when one Markdown row holds every non-empty truth cell as one of its cells. Shares are 1.0 when
/// the denominator is 0.
/// </para>
/// </summary>
internal static class QualityMetrics
{
    private const int MaxSamples = 20;
    private const int MaxFailuresPerKind = 50;

    private static readonly char[] TrimChars = ['.', ',', ';', ':', '!', '?', '(', ')', '„', '”', '"', '\''];
    private static readonly Regex PageMarker = new(@"<!--\s*page:\s*\d+\s*-->", RegexOptions.CultureInvariant);
    private static readonly Regex FootnoteMark = new(@"\[\^\d+\]:?", RegexOptions.CultureInvariant);
    private static readonly Regex Emphasis = new(@"(?<!\\)\*+", RegexOptions.CultureInvariant);
    private static readonly Regex UnescapedPipe = new(@"(?<!\\)\|", RegexOptions.CultureInvariant);
    private static readonly Regex Escape = new(@"\\(.)", RegexOptions.CultureInvariant | RegexOptions.Singleline);
    private static readonly Regex HeadingLine = new(@"^\s*(#{1,6})\s+(.*)$", RegexOptions.CultureInvariant);
    private static readonly Regex ListLine = new(@"^( *)- (.*)$", RegexOptions.CultureInvariant);
    private static readonly Regex BulletPrefix = new(@"^\s*-\s+", RegexOptions.CultureInvariant);

    private sealed record MdHeading(int Level, string[] Tokens);

    private sealed record MdItem(int Depth, string Label, string[] Rest, string[] All);

    private sealed class MdTable
    {
        public List<string[]> Rows { get; } = [];

        public Dictionary<string, List<int>> Postings { get; } = new(StringComparer.Ordinal);

        public void Index()
        {
            for (int i = 0; i < Rows.Count; i++)
            {
                foreach (string cell in Rows[i].Where(c => c.Length > 0).Distinct(StringComparer.Ordinal))
                {
                    if (!Postings.TryGetValue(cell, out List<int>? list))
                    {
                        Postings[cell] = list = [];
                    }

                    list.Add(i);
                }
            }
        }
    }

    public static QualityReport Measure(string documentId, DocumentTruth truth, string markdown)
    {
        List<string> failures = [];
        string[] lines = markdown.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();

        string[] truthWords = truth.Words.SelectMany(Tokens).ToArray();
        string[] mdWords = lines.SelectMany(MarkdownWordsOfLine).ToArray();

        (double completeness, int extra, List<string> samples, int missing) = CompareWords(truthWords, mdWords);
        if (missing > 0)
        {
            failures.Add($"{documentId}: brakujące słowa: {missing.ToString(CultureInfo.InvariantCulture)}");
        }

        if (extra > 0)
        {
            failures.Add($"{documentId}: słowa spoza PDF: {extra.ToString(CultureInfo.InvariantCulture)} (np. {string.Join(", ", samples.Take(5))})");
        }

        double order = truthWords.Length == 0 ? 1.0 : (double)Lcs(truthWords, mdWords) / truthWords.Length;

        (double headingRecall, double falseShare) = MeasureHeadings(documentId, truth, lines, failures);
        double listRecall = MeasureLists(documentId, truth, lines, failures);
        (double rows, double single, double cells) = MeasureTables(documentId, truth, lines, failures);

        return new QualityReport(documentId, completeness, extra, samples, order, headingRecall, falseShare, listRecall, rows, single, cells, failures);
    }

    // ---- normalisation -------------------------------------------------------------------------------------------

    private static string Unescape(string s) => Escape.Replace(s, "$1");

    private static string CleanInline(string s)
    {
        s = PageMarker.Replace(s, " ");
        s = FootnoteMark.Replace(s, " ");
        s = Emphasis.Replace(s, string.Empty);
        s = UnescapedPipe.Replace(s, " ");
        return Unescape(s);
    }

    private static string Norm(string token) => token.Trim(TrimChars);

    private static string[] Tokens(string text) =>
        text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Select(Norm).Where(t => t.Length > 0).ToArray();

    private static bool IsSeparatorRow(string line)
    {
        string t = line.Trim();
        return t.Contains("---", StringComparison.Ordinal) && t.All(c => c is '|' or '-' or ':' or ' ');
    }

    private static IEnumerable<string> MarkdownWordsOfLine(string raw)
    {
        string line = PageMarker.Replace(raw, " ");
        if (IsSeparatorRow(line))
        {
            return [];
        }

        Match heading = HeadingLine.Match(line);
        line = heading.Success ? heading.Groups[2].Value : BulletPrefix.Replace(line, string.Empty);
        return Tokens(CleanInline(line));
    }

    // ---- words ---------------------------------------------------------------------------------------------------

    private static (double Completeness, int Extra, List<string> Samples, int Missing) CompareWords(string[] truthWords, string[] mdWords)
    {
        Dictionary<string, int> budget = new(StringComparer.Ordinal);
        foreach (string w in truthWords)
        {
            budget[w] = budget.GetValueOrDefault(w) + 1;
        }

        int extra = 0;
        List<string> samples = [];
        foreach (string w in mdWords)
        {
            if (budget.TryGetValue(w, out int left) && left > 0)
            {
                budget[w] = left - 1;
            }
            else
            {
                extra++;
                if (samples.Count < MaxSamples)
                {
                    samples.Add(w);
                }
            }
        }

        int missing = budget.Values.Sum();
        double completeness = truthWords.Length == 0 ? 1.0 : (double)(truthWords.Length - missing) / truthWords.Length;
        return (completeness, extra, samples, missing);
    }

    private static int Lcs(string[] a, string[] b)
    {
        int start = 0;
        while (start < a.Length && start < b.Length && string.Equals(a[start], b[start], StringComparison.Ordinal))
        {
            start++;
        }

        int endA = a.Length;
        int endB = b.Length;
        while (endA > start && endB > start && string.Equals(a[endA - 1], b[endB - 1], StringComparison.Ordinal))
        {
            endA--;
            endB--;
        }

        int common = start + (a.Length - endA);
        Dictionary<string, int> ids = new(StringComparer.Ordinal);
        int[] x = a[start..endA].Select(w => Id(ids, w)).ToArray();
        int[] y = b[start..endB].Select(w => Id(ids, w)).ToArray();
        if (x.Length == 0 || y.Length == 0)
        {
            return common;
        }

        int[] prev = new int[y.Length + 1];
        int[] cur = new int[y.Length + 1];
        foreach (int xi in x)
        {
            for (int j = 1; j <= y.Length; j++)
            {
                cur[j] = xi == y[j - 1] ? prev[j - 1] + 1 : Math.Max(prev[j], cur[j - 1]);
            }

            (prev, cur) = (cur, prev);
        }

        return common + prev[y.Length];
    }

    private static int Id(Dictionary<string, int> ids, string w)
    {
        if (!ids.TryGetValue(w, out int id))
        {
            ids[w] = id = ids.Count;
        }

        return id;
    }

    // ---- headings ------------------------------------------------------------------------------------------------

    private static Dictionary<int, int> Ranks(IEnumerable<int> levels) =>
        levels.Distinct().Order().Select((level, rank) => (level, rank)).ToDictionary(p => p.level, p => p.rank);

    private static (double Recall, double FalseShare) MeasureHeadings(string id, DocumentTruth truth, string[] lines, List<string> failures)
    {
        List<MdHeading> md = [];
        foreach (string raw in lines)
        {
            Match m = HeadingLine.Match(PageMarker.Replace(raw, " "));
            if (m.Success)
            {
                md.Add(new MdHeading(m.Groups[1].Length, Tokens(CleanInline(m.Groups[2].Value))));
            }
        }

        Dictionary<int, int> mdRanks = Ranks(md.Select(h => h.Level));
        Dictionary<int, int> truthRanks = Ranks(truth.Headings.Select(h => h.Level));

        int hits = 0;
        int pointer = 0;
        foreach (TruthHeading h in truth.Headings)
        {
            string display = ((h.Label ?? string.Empty) + " " + h.Text).Trim();
            string[] tokens = Tokens(display);
            int found = -1;
            for (int j = pointer; j < md.Count; j++)
            {
                if (md[j].Tokens.AsSpan().SequenceEqual(tokens))
                {
                    found = j;
                    break;
                }
            }

            if (found < 0)
            {
                failures.Add($"{id}: nagłówek '{display}' nie znaleziony");
                continue;
            }

            pointer = found + 1;
            if (mdRanks[md[found].Level] == truthRanks[h.Level])
            {
                hits++;
            }
            else
            {
                failures.Add($"{id}: nagłówek '{display}' ma zły poziom (Markdown: {md[found].Level.ToString(CultureInfo.InvariantCulture)}, prawda: {h.Level.ToString(CultureInfo.InvariantCulture)})");
            }
        }

        HashSet<string> known = truth.Headings.Select(h => string.Join(' ', Tokens(((h.Label ?? string.Empty) + " " + h.Text).Trim()))).ToHashSet(StringComparer.Ordinal);
        int falseCount = 0;
        foreach (MdHeading h in md)
        {
            if (!known.Contains(string.Join(' ', h.Tokens)))
            {
                falseCount++;
                failures.Add($"{id}: nagłówek spoza prawdy '{string.Join(' ', h.Tokens)}'");
            }
        }

        return (Share(hits, truth.Headings.Count), md.Count == 0 ? 0.0 : (double)falseCount / md.Count);
    }

    // ---- lists ---------------------------------------------------------------------------------------------------

    private static double MeasureLists(string id, DocumentTruth truth, string[] lines, List<string> failures)
    {
        List<MdItem> md = [];
        foreach (string raw in lines)
        {
            Match m = ListLine.Match(PageMarker.Replace(raw, " "));
            if (!m.Success)
            {
                continue;
            }

            string text = CleanInline(m.Groups[2].Value).Trim();
            string[] parts = text.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries);
            string label = parts.Length > 0 ? Norm(parts[0]) : string.Empty;
            string[] rest = parts.Length > 1 ? Tokens(parts[1]) : [];
            md.Add(new MdItem(m.Groups[1].Length / 2, label, rest, Tokens(text)));
        }

        Dictionary<int, int> mdRanks = Ranks(md.Select(i => i.Depth));
        Dictionary<int, int> truthRanks = Ranks(truth.ListItems.Select(i => i.Depth));

        int hits = 0;
        int pointer = 0;
        foreach (TruthListItem item in truth.ListItems)
        {
            string display = (item.Label + " " + item.FirstWords).Trim();
            string[] first = Tokens(item.FirstWords).Take(3).ToArray();
            bool checklist = string.Equals(item.Label, "□", StringComparison.Ordinal);
            string label = Norm(item.Label);
            int found = -1;
            for (int j = pointer; j < md.Count && found < 0; j++)
            {
                bool ok = checklist
                    ? Enumerable.Range(0, 3).Any(off => StartsWithAt(md[j].All, first, off))
                    : string.Equals(md[j].Label, label, StringComparison.Ordinal) && StartsWithAt(md[j].Rest, first, 0);
                if (ok)
                {
                    found = j;
                }
            }

            if (found < 0)
            {
                failures.Add($"{id}: pozycja listy '{display}' nie znaleziona");
                continue;
            }

            pointer = found + 1;
            if (mdRanks[md[found].Depth] == truthRanks[item.Depth])
            {
                hits++;
            }
            else
            {
                failures.Add($"{id}: pozycja listy '{display}' ma złą głębokość");
            }
        }

        return Share(hits, truth.ListItems.Count);
    }

    private static bool StartsWithAt(string[] tokens, string[] prefix, int offset) =>
        tokens.Length >= offset + prefix.Length && tokens.AsSpan(offset, prefix.Length).SequenceEqual(prefix);

    // ---- tables --------------------------------------------------------------------------------------------------

    private static (double Rows, double Single, double Cells) MeasureTables(string id, DocumentTruth truth, string[] lines, List<string> failures)
    {
        List<MdTable> md = ParseTables(lines);

        int rowTotal = 0;
        int rowHits = 0;
        int cellTotal = 0;
        int cellHits = 0;
        int rowFailures = 0;
        int cellFailures = 0;

        foreach (TruthTable table in truth.Tables)
        {
            foreach (IReadOnlyList<string> row in table.Rows)
            {
                string[] cells = row.Select(NormCell).ToArray();
                string[] distinct = cells.Where(c => c.Length > 0).Distinct(StringComparer.Ordinal).ToArray();
                if (distinct.Length == 0)
                {
                    continue;
                }

                string key = row.FirstOrDefault(c => NormCell(c).Length > 0) ?? string.Empty;
                rowTotal++;
                (MdTable? bestTable, int bestRow, int bestScore) = BestRow(md, distinct);
                if (bestScore == distinct.Length)
                {
                    rowHits++;
                }
                else if (rowFailures++ < MaxFailuresPerKind)
                {
                    failures.Add($"{id}: wiersz '{key}' rozbity");
                }

                string[]? mdRow = bestTable?.Rows[bestRow];
                for (int c = 0; c < cells.Length; c++)
                {
                    if (cells[c].Length == 0)
                    {
                        continue;
                    }

                    cellTotal++;
                    if (mdRow is not null && c < mdRow.Length && string.Equals(mdRow[c], cells[c], StringComparison.Ordinal))
                    {
                        cellHits++;
                    }
                    else if (cellFailures++ < MaxFailuresPerKind)
                    {
                        failures.Add($"{id}: komórka '{cells[c]}' (wiersz '{key}', kolumna {(c + 1).ToString(CultureInfo.InvariantCulture)}) niezgodna");
                    }
                }
            }
        }

        int singleTotal = 0;
        int singleHits = 0;
        int number = 0;
        foreach (TruthTable table in truth.Tables)
        {
            number++;
            string[] header = table.Header.Select(NormCell).ToArray();
            if (header.All(h => h.Length == 0))
            {
                continue;
            }

            singleTotal++;
            string name = $"#{number.ToString(CultureInfo.InvariantCulture)} ({string.Join(" | ", table.Header)})";
            List<string[]> rows = table.Rows
                .Select(r => r.Select(NormCell).Where(c => c.Length > 0).Distinct(StringComparer.Ordinal).ToArray())
                .Where(r => r.Length > 0)
                .ToList();
            List<MdTable> holders = md.Where(t => rows.Count(r => TableHasRow(t, r)) >= 0.9 * rows.Count).ToList();
            if (rows.Count == 0)
            {
                singleHits++;
            }
            else if (holders.Count != 1)
            {
                failures.Add($"{id}: tabela {name} nie jest jedną tabelą GFM (tabel z ≥ 90% wierszy: {holders.Count.ToString(CultureInfo.InvariantCulture)})");
            }
            else if (holders[0].Rows.Skip(1).Any(r => r.AsSpan().SequenceEqual(header)))
            {
                failures.Add($"{id}: tabela {name} ma powtórzony nagłówek wewnątrz tabeli GFM");
            }
            else
            {
                singleHits++;
            }
        }

        return (Share(rowHits, rowTotal), Share(singleHits, singleTotal), Share(cellHits, cellTotal));
    }

    private static string NormCell(string text) => string.Join(' ', Tokens(CleanInline(text)));

    private static bool TableHasRow(MdTable table, string[] distinct) =>
        ScoreRows(table, distinct).Any(p => p.Value == distinct.Length);

    private static Dictionary<int, int> ScoreRows(MdTable table, string[] distinct)
    {
        Dictionary<int, int> scores = [];
        foreach (string cell in distinct)
        {
            if (table.Postings.TryGetValue(cell, out List<int>? rows))
            {
                foreach (int r in rows)
                {
                    scores[r] = scores.GetValueOrDefault(r) + 1;
                }
            }
        }

        return scores;
    }

    private static (MdTable? Table, int Row, int Score) BestRow(List<MdTable> tables, string[] distinct)
    {
        (MdTable? Table, int Row, int Score) best = (null, -1, 0);
        foreach (MdTable t in tables)
        {
            foreach ((int row, int score) in ScoreRows(t, distinct).OrderBy(p => p.Key).Select(p => (p.Key, p.Value)))
            {
                if (score > best.Score)
                {
                    best = (t, row, score);
                }
            }
        }

        return best;
    }

    private static bool IsTableLine(string cleaned) => cleaned.TrimStart().StartsWith('|');

    private static List<MdTable> ParseTables(string[] raw)
    {
        int n = raw.Length;
        string[] lines = raw.Select(l => PageMarker.Replace(l, string.Empty)).ToArray();
        bool[] markerOnly = raw.Select((l, i) => lines[i].Trim().Length == 0 && PageMarker.IsMatch(l)).ToArray();
        bool[] blank = lines.Select(l => l.Trim().Length == 0).ToArray();

        List<MdTable> tables = [];
        int i = 0;
        while (i < n)
        {
            if (!IsTableLine(lines[i]))
            {
                i++;
                continue;
            }

            List<string> run = [];
            int j = i;
            while (true)
            {
                while (j < n && IsTableLine(lines[j]))
                {
                    run.Add(lines[j]);
                    j++;
                }

                int k = j;
                bool sawMarker = false;
                while (k < n && (blank[k] || markerOnly[k]))
                {
                    sawMarker |= markerOnly[k];
                    k++;
                }

                bool continues = sawMarker && k < n && IsTableLine(lines[k]) && !(k + 1 < n && IsSeparatorRow(lines[k + 1]));
                if (!continues)
                {
                    break;
                }

                j = k;
            }

            MdTable table = new();
            foreach (string line in run.Where(l => !IsSeparatorRow(l)))
            {
                table.Rows.Add(SplitCells(line));
            }

            table.Index();
            tables.Add(table);
            i = j;
        }

        return tables;
    }

    private static string[] SplitCells(string line)
    {
        List<string> parts = Regex.Split(line.Trim(), @"(?<!\\)\|", RegexOptions.CultureInvariant).ToList();
        if (parts.Count > 0 && parts[0].Trim().Length == 0)
        {
            parts.RemoveAt(0);
        }

        if (parts.Count > 0 && parts[^1].Trim().Length == 0)
        {
            parts.RemoveAt(parts.Count - 1);
        }

        return parts.Select(NormCell).ToArray();
    }

    private static double Share(int hits, int total) => total == 0 ? 1.0 : (double)hits / total;
}
