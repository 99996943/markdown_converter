using System.Globalization;
using System.Text;

namespace LegalAgent.PdfParser.Tests.Fixtures;

/// <summary>
/// Spec 002 documents: a promotion terms document made of one multi-page, two-column bordered table (the layout of the
/// reference promotion terms, research.md „Pomiary”) and an ordinary document with a short two-column definitions table.
/// </summary>
internal static partial class BankingCorpusGenerator
{
    // ---------------------------------------------------------------- 5. regulamin-promocji-tabela

    private static byte[] RegulaminPromocjiTabela(Recorder rec)
    {
        var b = new SyntheticPdfBuilder().Title("Regulamin promocji");

        // Cover: title, validity line, logo with its caption below.
        b.Page();
        b.Text(54, 233, "Regulamin promocji „Konto firmowe", 20, bold: true);
        b.Text(54, 262, "z korzyściami – edycja 1”", 20, bold: true);
        b.Text(54, 310, "Obowiązuje od 01.09.2026 r. do 30.11.2026 r.", 12);
        b.Image(315, 360, 227, 226);
        b.Text(409, 611, "bank.example", 10, bold: true);

        // Pages 2-5: the table; the column-name row is not repeated on page 4 (as in the reference document).
        rec.HeaderRowWords.AddRange(["Definicje", "Wyjaśnienie"]);
        var t = new TableDocumentWriter(b, rec, headerOnPage: [true, true, false, true]);

        t.Row(["Organizator", "promocji"],
        [
            P("Promocję organizuje Bank Przykładowy S.A. z siedzibą w Warszawie przy ul. Przykładowej 1, wpisany do rejestru przedsiębiorców Krajowego Rejestru Sądowego pod numerem KRS 0000000000, posiadający numer identyfikacji podatkowej NIP: 000-000-00-00, o wpłaconym w całości kapitale zakładowym. Na bieżąco sprawdzamy, czy przy promocji nie wystąpił konflikt interesów między nami a naszymi pracownikami lub naszymi klientami."),
        ]);
        t.Row(["Uczestnik", "promocji"],
        [
            P("W promocji mogą uczestniczyć:"),
            B("osoby fizyczne, które w dniu składania wniosku o otwarcie rachunku bieżącego prowadziły działalność gospodarczą,"),
            B("spółki jawne,"),
            B("spółki partnerskie,"),
            B("spółki z ograniczoną odpowiedzialnością,"),
            P("które w dniu składania wniosku prowadziły działalność gospodarczą zarejestrowaną nie dłużej niż 12 miesięcy."),
            H("Nie możesz uczestniczyć w promocji, jeśli:"),
            B("już raz skorzystałeś z tej promocji,"),
            B("masz już jakikolwiek rachunek w Banku dla danego numeru NIP."),
        ]);
        t.EndPage();

        t.Row(["Ważne pojęcia"],
        [
            D("Bank – Bank Przykładowy S.A."),
            D("Rachunek bieżący – Rachunek Firmowy Standard"),
            D("Regulamin promocji – ten regulamin"),
            D("Terminal POS – urządzenie do przyjmowania płatności kartami"),
            D("Kod rabatowy – e-kod na zakup pierścienia płatniczego"),
        ]);
        t.Row(["Korzyści", "promocji"],
        [
            P("Jeśli spełnisz warunki promocji, otrzymasz do nowo otwartego rachunku bieżącego:"),
            H("Korzyści obowiązujące przez pierwsze 24 miesiące od dnia otwarcia rachunku bieżącego:"),
            B("0 zł za prowadzenie rachunku bieżącego, jeśli w danym miesiącu wykonasz przelew do ZUS na dowolną kwotę lub będziesz miał wpływ na rachunek co najmniej 1000 zł,"),
            B("nielimitowane darmowe przelewy internetowe w PLN z rachunku bieżącego,"),
            B("1 zł netto miesięcznie za usługę księgową przez okres 3 miesięcy, jeśli na wniosku o rachunek wybierzesz:"),
            O("pakiet Komfort – księgowość uproszczona,"),
            O("pakiet Start – księgowość pełna,"),
            B("możliwość zamówienia terminala POS:"),
            O("0 zł przez 24 miesiące od dnia podpisania umowy,"),
            O("0 zł za instalację i aktywację terminala,"),
            P("Warunki promocyjne terminala obowiązują do osiągnięcia łącznego obrotu bezgotówkowego w wysokości 200 tysięcy zł brutto w ciągu 24 miesięcy od dnia zawarcia umowy o terminal. Po przekroczeniu tego limitu obowiązują opłaty zgodne z aktualną taryfą opłat i prowizji, o której zmianach informujemy w serwisie transakcyjnym i o której treści możesz dowiedzieć się w każdej placówce Banku.", breakAfterLines: 2),
            P("Wszystkie korzyści przysługują wyłącznie do rachunku bieżącego otwartego w promocji."),
        ]);
        t.Row(["Warunki/zasady", "promocji"],
        [
            B("Jeśli spełnisz wszystkie warunki przystąpienia do promocji oraz na wniosku o rachunek bieżący wyrazisz zgodę na otrzymywanie komunikacji elektronicznej, kod rabatowy udostępnimy Ci w serwisie transakcyjnym oraz w aplikacji mobilnej.", breakAfterLines: 1),
            B("Dla pozostałych usług, które nie wchodzą w zakres promocji, obowiązują opłaty i prowizje zgodne z naszą aktualną taryfą opłat i prowizji."),
            B("Utracisz warunki promocyjne, jeśli w trakcie promocji zawnioskujesz o zmianę typu rachunku bieżącego lub o kredyt w rachunku bieżącym."),
        ]);
        t.Row(["Jak możesz", "złożyć", "reklamację", "dotyczącą", "promocji?"],
        [
            N("1)", "Reklamacje związane z uczestnictwem w promocji możesz składać na zasadach opisanych w regulaminie reklamacji Banku Przykładowego S.A."),
            N("2)", "Jeśli reklamację wysyłasz listownie, złóż ją z dopiskiem „Promocja – edycja 1”."),
            N("3)", "Szczegółowe zasady zwrotu pierścieni są dostępne na stronie internetowej pod adresem: https://example.org/products/pierscien-platniczy-mastercard i nie podlegają reklamacji w Banku."),
        ],
        nameOffset: 1);
        t.EndPage();

        // After the table: statements and the signature line.
        b.Page();
        b.Text(54, 83, "MOJE OŚWIADCZENIA", 10, bold: true);
        Statement(115, "1)", ["Wiem, że po tym, jak przystąpię do promocji, Bank może organizować inne, podobne", "promocje pod taką samą nazwą, lecz o innych parametrach."]);
        Statement(161, "2)", ["Otrzymałem regulamin promocji „Konto firmowe z korzyściami – edycja 1”, przeczytałem", "i akceptuję jego postanowienia."]);
        b.Text(54, 282, "……………………………………………………", 10);
        b.Text(54, 314, "data, miejsce i podpis Uczestnika promocji", 10);
        TableDocumentWriter.PageNumber(b, 5);

        return b.Build();

        void Statement(double y, string label, string[] lines)
        {
            rec.ListItems.Add(new ListTruth(label, 0));
            b.Text(54, y, label, 10);
            for (int i = 0; i < lines.Length; i++)
            {
                b.Text(72, y + (i * 15), lines[i], 10);
            }
        }
    }

    private static CellItem P(string text, int breakAfterLines = 0) => new(CellItemKind.Paragraph, string.Empty, text, breakAfterLines);

    private static CellItem B(string text, int breakAfterLines = 0) => new(CellItemKind.Bullet, "•", text, breakAfterLines);

    private static CellItem O(string text) => new(CellItemKind.SubBullet, "o", text, 0);

    private static CellItem H(string text) => new(CellItemKind.Subtitle, string.Empty, text, 0);

    private static CellItem D(string text) => new(CellItemKind.Definition, string.Empty, text, 0);

    private static CellItem N(string label, string text) => new(CellItemKind.Numbered, label, text, 0);

    private enum CellItemKind
    {
        Paragraph,
        Bullet,
        SubBullet,
        Subtitle,
        Definition,
        Numbered,
    }

    /// <summary>One block of a right cell; <see cref="BreakAfterLines"/> &gt; 0 moves the rest of the row to the next page.</summary>
    private sealed record CellItem(CellItemKind Kind, string Label, string Text, int BreakAfterLines);

    /// <summary>A line of a right cell, optionally preceded on its baseline by a list label.</summary>
    private sealed record CellLine(double X, string Text, bool Bold, string? Label = null, double LabelX = 0, bool LabelMono = false);

    /// <summary>
    /// Lays out the table-document like the reference: a frame of rulings (outer edges and the column divider at
    /// x 54 / 181 / 541; horizontal rulings drawn in two pieces), an optional bold column-name row, and rows whose name
    /// stands in the left cell on the baselines of the first content lines. Right-cell text is wrapped on measured
    /// widths with a ragged right edge; one-letter words stay with the following word (Polish typesetting) and an
    /// address breaks only after a hyphen. A forced break closes the page and continues the row on the next one.
    /// </summary>
    private sealed class TableDocumentWriter(SyntheticPdfBuilder builder, Recorder rec, bool[] headerOnPage)
    {
        private const double FrameLeft = 54;
        private const double Divider = 181;
        private const double FrameRight = 541;
        private const double FrameTop = 72;
        private const double NameX = 60;
        private const double TextX = 186;
        private const double TextRight = 529;
        private const double Size = 10;
        private const double Leading = 15;
        private const double ItemGap = 4;
        private const double BaselineBelowEdge = 17;
        private const double EdgeBelowBaseline = 8;
        private const double LastBaseline = 745;

        private readonly List<double> _edges = [];
        private int _tablePage;
        private bool _open;
        private double _y;
        private double _lastBaseline;

        public static void PageNumber(SyntheticPdfBuilder b, int number) =>
            b.Text(517, 804, number.ToString(CultureInfo.InvariantCulture) + "/5", 8);

        public void Row(string[] name, CellItem[] items, double nameOffset = 0)
        {
            if (!_open)
            {
                StartPage();
            }

            rec.SectionNames.Add(string.Join(' ', name));
            var names = new Queue<string>(name);
            for (int k = 0; k < items.Length; k++)
            {
                CellItem item = items[k];
                if (k > 0 && item.Kind != CellItemKind.Definition)
                {
                    _y += ItemGap;
                }

                List<CellLine> lines = Layout(item);
                for (int i = 0; i < lines.Count; i++)
                {
                    if (_y > LastBaseline)
                    {
                        throw new InvalidOperationException($"Table page {_tablePage} overflows; add a forced break.");
                    }

                    if (names.Count > 0)
                    {
                        builder.Text(NameX, _y + nameOffset, names.Dequeue(), Size, bold: true);
                    }

                    Draw(lines[i]);
                    _lastBaseline = _y;
                    _y += Leading;

                    if (item.BreakAfterLines > 0 && i + 1 == item.BreakAfterLines)
                    {
                        EndPage();
                        StartPage();
                    }
                }
            }

            while (names.Count > 0)
            {
                builder.Text(NameX, _y + nameOffset, names.Dequeue(), Size, bold: true);
                _lastBaseline = _y;
                _y += Leading;
            }

            double edge = _lastBaseline + EdgeBelowBaseline;
            _edges.Add(edge);
            _y = edge + BaselineBelowEdge;
        }

        /// <summary>Draws the frame of the current page (bottom edge below the last line) and its page number.</summary>
        public void EndPage()
        {
            double bottom = _lastBaseline + EdgeBelowBaseline;
            if (_edges[^1] < bottom - 0.5)
            {
                _edges.Add(bottom);
            }

            foreach (double edge in _edges)
            {
                builder.HLine(FrameLeft + 1, Divider, edge, 0.75);
                builder.HLine(Divider, FrameRight, edge, 0.75);
            }

            foreach (double x in new[] { FrameLeft, Divider, FrameRight })
            {
                builder.VLine(x, FrameTop, bottom, 0.75);
            }

            PageNumber(builder, _tablePage);
            _open = false;
        }

        private void StartPage()
        {
            builder.Page();
            _open = true;
            _edges.Clear();
            _edges.Add(FrameTop);
            _y = FrameTop + BaselineBelowEdge;
            if (headerOnPage[_tablePage++])
            {
                builder.Text(65, _y, "Definicje", Size, bold: true);
                builder.Text(TextX, _y, "Wyjaśnienie", Size, bold: true);
                double edge = _y + 11;
                _edges.Add(edge);
                _y = edge + BaselineBelowEdge;
            }
        }

        private void Draw(CellLine line)
        {
            if (line.Label is not null)
            {
                builder.Text(line.LabelX, _y, line.Label, Size, mono: line.LabelMono);
            }

            builder.Text(line.X, _y, line.Text, Size, bold: line.Bold);

            // A link underline inside the cell: shorter than the cell, so it must not split the row.
            string? link = line.Text.Split(' ').FirstOrDefault(w => w.Contains("example.org", StringComparison.Ordinal) || w.StartsWith("mastercard", StringComparison.Ordinal));
            if (link is not null)
            {
                double start = line.X + SyntheticPdfBuilder.TextWidth(line.Text[..line.Text.IndexOf(link, StringComparison.Ordinal)], Size);
                builder.HLine(start, start + SyntheticPdfBuilder.TextWidth(link, Size), _y + 2, 0.5);
            }
        }

        private List<CellLine> Layout(CellItem item)
        {
            switch (item.Kind)
            {
                case CellItemKind.Definition:
                    if (TextX + SyntheticPdfBuilder.TextWidth(item.Text, Size) > TextRight)
                    {
                        throw new InvalidOperationException("A definition must fit one line: " + item.Text);
                    }

                    return [new CellLine(TextX, item.Text, false)];

                case CellItemKind.Paragraph or CellItemKind.Subtitle:
                    bool bold = item.Kind == CellItemKind.Subtitle;
                    return Wrap(item.Text, TextRight - TextX, bold).Select(l => new CellLine(TextX, l, bold)).ToList();

                case CellItemKind.Bullet:
                    rec.ListItems.Add(new ListTruth("•", 0));
                    return Hanging(item.Text, "•", 190, 208, mono: false);

                case CellItemKind.SubBullet:
                    rec.ListItems.Add(new ListTruth("o", 1));
                    return Hanging(item.Text, "o", 226, 244, mono: true);

                default:
                    rec.ListItems.Add(new ListTruth(item.Label, 0));
                    return Hanging(item.Text, item.Label, TextX, 204, mono: false);
            }
        }

        private static List<CellLine> Hanging(string text, string label, double labelX, double textX, bool mono) =>
            Wrap(text, TextRight - textX, bold: false)
                .Select((l, i) => i == 0 ? new CellLine(textX, l, false, label, labelX, mono) : new CellLine(textX, l, false))
                .ToList();

        /// <summary>
        /// Greedy wrap on measured widths. A one-letter word is bound to the next word; a word that contains „/” and
        /// does not fit may break after one of its hyphens (the hyphen stays at the line end).
        /// </summary>
        private static IEnumerable<string> Wrap(string text, double width, bool bold)
        {
            var units = new List<string>();
            string[] words = text.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                string unit = words[i];
                while (unit.Length == 1 && char.IsLetter(unit[0]) && i + 1 < words.Length)
                {
                    unit += " " + words[++i];
                }

                units.Add(unit);
            }

            var line = new StringBuilder();
            bool Fits(string candidate) => SyntheticPdfBuilder.TextWidth(candidate, Size, bold) <= width;

            var queue = new Queue<string>(units);
            while (queue.Count > 0)
            {
                string unit = queue.Dequeue();
                string candidate = line.Length == 0 ? unit : line + " " + unit;
                if (Fits(candidate))
                {
                    line.Clear().Append(candidate);
                    continue;
                }

                if (unit.Contains('/', StringComparison.Ordinal))
                {
                    int cut = unit.LastIndexOf('-', unit.Length - 2);
                    while (cut > 0 && !Fits((line.Length == 0 ? string.Empty : line + " ") + unit[..(cut + 1)]))
                    {
                        cut = unit.LastIndexOf('-', cut - 1);
                    }

                    if (cut > 0)
                    {
                        yield return (line.Length == 0 ? string.Empty : line + " ") + unit[..(cut + 1)];
                        line.Clear().Append(unit[(cut + 1)..]);
                        continue;
                    }
                }

                if (line.Length == 0)
                {
                    throw new InvalidOperationException("A word does not fit the cell: " + unit);
                }

                yield return line.ToString();
                line.Clear().Append(unit);
            }

            if (line.Length > 0)
            {
                yield return line.ToString();
            }
        }
    }

    // ---------------------------------------------------------------- 6. regulamin-z-tabela-definicji

    private static byte[] RegulaminZTabelaDefinicji(Recorder rec)
    {
        double[] x = [72, 185, 523];
        (string Term, string Explanation)[] definitions =
        [
            ("Bank", "Bank Przykładowy S.A. z siedzibą w Warszawie."),
            ("Klient", "osoba fizyczna, która zawarła z Bankiem umowę rachunku."),
            ("Rachunek", "rachunek oszczędnościowo-rozliczeniowy prowadzony przez Bank dla Klienta."),
            ("Karta", "karta debetowa wydana do rachunku na podstawie umowy."),
            ("Przelew", "polecenie przelewu złożone przez Klienta w placówce lub w systemie."),
            ("System", "system bankowości internetowej udostępniany Klientowi przez Bank."),
            ("Taryfa", "taryfa opłat i prowizji obowiązująca w Banku."),
            ("Dzień roboczy", "dzień, w którym Bank prowadzi obsługę klientów, z wyjątkiem sobót i dni ustawowo wolnych od pracy."),
            ("Saldo", "stan środków pieniężnych na rachunku na koniec dnia."),
            ("Limit", "kwota, do której Klient może wykonywać transakcje kartą w ciągu dnia."),
            ("Placówka", "oddział Banku, w którym obsługiwani są klienci."),
            ("Umowa", "umowa rachunku zawarta między Bankiem a Klientem."),
        ];

        var b = new SyntheticPdfBuilder()
            .Title("Regulamin rachunku z tabelą definicji")
            .RunningHeader("Regulamin rachunku – " + Bank)
            .PageNumberFooter("Strona {n}");
        var f = new Flow(b, [Left], Right - Left, rec);

        f.Line("Regulamin rachunku dla klientów indywidualnych", 0, 16, true, leading: 24);
        Text(f, "Rozdział 1. Postanowienia ogólne", 1);
        f.NewPage([Left], Right - Left);
        Text(f, "Rozdział 2. Rachunek", 2);

        // Pages 3-4: the definitions table (column-name row repeated on page 4).
        f.NewPage([Left], Right - Left);
        f.Line("Rozdział 3. Definicje", 0, 12, true, leading: 20);
        double top = 0;
        void Header()
        {
            top = f.Y;
            b.HLine(x[0], x[2], top);
            b.Text(x[0] + 6, top + 14, "Definicje", 10, bold: true);
            b.Text(x[1] + 6, top + 14, "Wyjaśnienie", 10, bold: true);
            f.Y = top + 20;
            b.HLine(x[0], x[2], f.Y);
        }

        void Grid()
        {
            foreach (double vx in x)
            {
                b.VLine(vx, top, f.Y);
            }
        }

        Header();
        for (int i = 0; i < definitions.Length; i++)
        {
            if (i == 7)
            {
                Grid();
                f.NewPage([Left], Right - Left);
                Header();
            }

            (string term, string explanation) = definitions[i];
            List<string> lines = WrapPlain(explanation, x[2] - x[1] - 12, 10);
            double y0 = f.Y;
            b.Text(x[0] + 6, y0 + 14, term, 10, bold: true);
            for (int k = 0; k < lines.Count; k++)
            {
                b.Text(x[1] + 6, y0 + 14 + (k * 13), lines[k], 10);
            }

            f.Y = y0 + (lines.Count * 13) + 8;
            b.HLine(x[0], x[2], f.Y);
        }

        Grid();
        f.Gap(20);
        f.Item("", "Pojęcia niezdefiniowane w tabeli mają znaczenie nadane im w przepisach powszechnie obowiązującego prawa.", 0, 0);

        f.NewPage([Left], Right - Left);
        Text(f, "Rozdział 4. Karty i przelewy", 3);
        f.NewPage([Left], Right - Left);
        Text(f, "Rozdział 5. Postanowienia końcowe", 4);
        f.Finish();
        return b.Build();

        static void Text(Flow flow, string chapter, int number)
        {
            flow.Line(chapter, 0, 12, true, leading: 20);
            for (int p = 1; p <= 4; p++)
            {
                flow.Item("", $"Postanowienie {number}.{p}. Bank prowadzi rachunek zgodnie z przepisami prawa oraz postanowieniami umowy, a Klient korzysta z rachunku w sposób zgodny z jego przeznaczeniem. Szczegółowe zasady wykonywania dyspozycji określa umowa oraz komunikaty publikowane przez Bank w placówkach i w systemie bankowości internetowej.", 0, 0);
                flow.Gap(6);
            }
        }

        static List<string> WrapPlain(string text, double width, double size)
        {
            var lines = new List<string>();
            var line = new StringBuilder();
            foreach (string word in text.Split(' '))
            {
                string candidate = line.Length == 0 ? word : line + " " + word;
                if (line.Length > 0 && SyntheticPdfBuilder.TextWidth(candidate, size) > width)
                {
                    lines.Add(line.ToString());
                    line.Clear().Append(word);
                }
                else
                {
                    line.Clear().Append(candidate);
                }
            }

            lines.Add(line.ToString());
            return lines;
        }
    }
}
