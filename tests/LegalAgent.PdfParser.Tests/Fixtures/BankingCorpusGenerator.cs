using System.Globalization;
using System.Text;

namespace LegalAgent.PdfParser.Tests.Fixtures;

internal sealed record ListTruth(string Label, int Depth);

internal sealed record TableRowTruth(string Service, string Fee, string Frequency);

internal sealed record DocumentTruth(IReadOnlyList<ListTruth> ListItems, IReadOnlyList<TableRowTruth> TableRows);

/// <summary>
/// Deterministic, in-memory generator of four synthetic banking documents (generic names, no real
/// bank branding) used by golden tests. Built only with <see cref="SyntheticPdfBuilder"/>.
/// </summary>
internal static class BankingCorpusGenerator
{
    private const string Bank = "Bank Przykładowy S.A.";
    private const string Registry = "Bank Przykładowy S.A., ul. Przykładowa 1, 00-001 Warszawa, KRS 0000000000";
    private const double Left = 72;
    private const double Right = 523;
    private const double Top = 100;
    private const double Bottom = 735;

    /// <summary>name (file stem) -> PDF bytes; stable order.</summary>
    public static IReadOnlyList<(string Name, byte[] Pdf)> Documents() =>
        Names.Select(n => (n, Build(n).Pdf)).ToArray();

    /// <summary>Ground truth recorded while the named document is generated.</summary>
    public static DocumentTruth Truth(string name) => Build(name).Truth;

    private static readonly string[] Names =
        ["regulamin-rachunku", "taryfa-z-siatka", "taryfa-bez-siatki", "regulamin-dwie-kolumny"];

    private static (byte[] Pdf, DocumentTruth Truth) Build(string name)
    {
        var rec = new Recorder();
        byte[] pdf = name switch
        {
            "regulamin-rachunku" => RegulaminRachunku(rec),
            "taryfa-z-siatka" => TaryfaZSiatka(rec),
            "taryfa-bez-siatki" => TaryfaBezSiatki(rec),
            "regulamin-dwie-kolumny" => RegulaminDwieKolumny(rec),
            _ => throw new ArgumentException("Unknown document: " + name, nameof(name)),
        };
        return (pdf, new DocumentTruth(rec.ListItems, rec.TableRows));
    }

    private sealed class Recorder
    {
        public List<ListTruth> ListItems { get; } = [];
        public List<TableRowTruth> TableRows { get; } = [];

        public void Row(string[] service, string fee, string freq) =>
            TableRows.Add(new TableRowTruth(string.Join(' ', service), fee, freq));
    }

    // ---------------------------------------------------------------- 1. regulamin-rachunku

    private static byte[] RegulaminRachunku(Recorder rec)
    {
        // Two passes: the first one only counts pages so "Strona {n} z {N}" can state the total.
        int total = BuildRegulamin(0, new Recorder()).Pages;
        return BuildRegulamin(total, rec).Pdf;
    }

    private static (byte[] Pdf, int Pages) BuildRegulamin(int total, Recorder rec)
    {
        var b = new SyntheticPdfBuilder()
            .Title("Regulamin rachunku")
            .RunningHeader("Regulamin rachunku – " + Bank)
            .PageNumberFooter("Strona {n} z " + total.ToString(CultureInfo.InvariantCulture));

        string? footnote = null;
        var f = new Flow(b, [Left], Right - Left, rec)
        {
            OnPageEnd = flow =>
            {
                if (footnote is not null)
                {
                    flow.Builder.HLine(Left, Left + 120, 758);
                    flow.Builder.Text(Left, 771, footnote, 8);
                    footnote = null;
                }

                flow.Builder.Text(Left, 822, Registry, 8);
            },
        };

        f.Line("Regulamin rachunku oszczędnościowo-rozliczeniowego", 0, 16, true, leading: 22);
        f.Line("obowiązuje od 1 stycznia 2027 r.", 0, 11, italic: true, leading: 14);
        f.Gap(14);

        f.Line("Rozdział 1", 0, 12, true, leading: 15);
        f.Line("Postanowienia ogólne", 0, 12, true, leading: 15);
        f.Gap(6);
        Paragraph(f, "§ 1.");
        f.Item("1.", "Regulamin określa zasady otwierania, prowadzenia i zamykania rachunków oszczędnościowo-rozliczeniowych prowadzonych przez Bank Przykładowy S.A. dla klientów będących konsumentami.", 0, 18);
        f.Item("2.", "Ilekroć w Regulaminie jest mowa o:", 0, 18);
        f.Item("1)", "Banku – należy przez to rozumieć Bank Przykładowy S.A. z siedzibą w Warszawie;", 18, 36);
        f.Item("2)", "Kliencie – należy przez to rozumieć osobę fizyczną posiadającą pełną zdolność do czynności prawnych, która zawarła z Bankiem umowę rachunku;", 18, 36);
        f.Item("3)", "Rachunku – należy przez to rozumieć rachunek prowadzony w złotych polskich lub w walutach obcych, w tym:", 18, 36);
        f.Item("a)", "rachunek podstawowy, służący do przechowywania środków pieniężnych i dokonywania rozliczeń;", 36, 54);
        f.Item("b)", "rachunek oszczędnościowy, na którym środki pieniężne są oprocentowane według stawki zmiennej.", 36, 54);
        f.Gap(6);

        Paragraph(f, "§ 2.");
        f.Item("1.", "Umowa rachunku zostaje zawarta na czas nieokreślony, chyba że strony postanowią inaczej.", 0, 18);
        f.Item("2.", "Warunkiem zawarcia umowy jest przedłożenie dokumentu potwierdzającego tożsamość oraz złożenie wzoru podpisu lub zaakceptowanie umowy w systemie bankowości internetowej.", 0, 18);
        f.Item("3.", "Bank może odmówić zawarcia umowy rachunku, jeżeli Klient nie dostarczy informacji niezbędnych do zastosowania środków bezpieczeństwa finansowego.", 0, 18);
        f.Gap(10);

        f.Line("Rozdział 2", 0, 12, true, leading: 15);
        f.Line("Prowadzenie rachunku", 0, 12, true, leading: 15);
        f.Gap(6);
        Paragraph(f, "§ 3.");
        f.Item("1.", "Bank prowadzi rachunek zgodnie z przepisami prawa oraz postanowieniami umowy, a środki zgromadzone na rachunku są oprocentowane według stawki określonej w Tabeli oprocentowania.¹", 0, 18);
        footnote = "¹ Aktualna Tabela oprocentowania jest dostępna w placówkach Banku oraz w serwisie internetowym.";
        f.Item("2.", "Klient może dysponować środkami zgromadzonymi na rachunku w granicach salda, z uwzględnieniem blokad i zajęć dokonanych na podstawie przepisów prawa. Dyspozycje składane przez Klienta w placówce, w systemie bankowości internetowej lub za pośrednictwem aplikacji mobilnej są realizowane w terminach określonych w Regulaminie realizacji przelewów, przy czym Bank zastrzega sobie prawo do wcześniejszego zweryfikowania tożsamości Klienta, jeżeli wymagają tego przepisy o przeciwdziałaniu praniu pieniędzy oraz finansowaniu terroryzmu.", 0, 18);
        f.Item("3.", "Bank udostępnia Klientowi zestawienie operacji na rachunku w formie elektronicznej, nie rzadziej niż raz w miesiącu.", 0, 18);
        f.Gap(6);

        Paragraph(f, "§ 4.");
        f.Item("1.", "Klient jest zobowiązany do niezwłocznego powiadomienia Banku o:", 0, 18);
        f.Item("1)", "utracie, kradzieży lub przywłaszczeniu instrumentu płatniczego;", 18, 36);
        f.Item("2)", "nieautoryzowanym użyciu instrumentu płatniczego lub nieuprawnionym dostępie do rachunku;", 18, 36);
        f.Item("3)", "zmianie danych osobowych lub adresowych podanych w umowie.", 18, 36);
        f.Item("2.", "Zgłoszenia, o których mowa w ust. 1, można dokonać całodobowo w serwisie telefonicznym Banku lub w dowolnej placówce w godzinach jej pracy.", 0, 18);
        f.Gap(10);

        f.Line("Rozdział 3", 0, 12, true, leading: 15);
        f.Line("Opłaty i prowizje", 0, 12, true, leading: 15);
        f.Gap(6);
        Paragraph(f, "§ 5.");
        f.Item("1.", "Za czynności związane z prowadzeniem rachunku Bank pobiera opłaty i prowizje w wysokości określonej w Taryfie opłat i prowizji obowiązującej w dniu wykonania czynności.", 0, 18);
        f.Item("2.", "Opłaty i prowizje są pobierane z rachunku Klienta, chyba że Taryfa stanowi inaczej. W przypadku braku wystarczających środków na rachunku Bank może pobrać należne opłaty z kolejnych wpływów.", 0, 18);
        f.Item("3.", "Bank informuje Klienta o zmianie Taryfy co najmniej na dwa miesiące przed dniem wejścia w życie zmian, w sposób ustalony w umowie.", 0, 18);
        f.Gap(6);

        Paragraph(f, "§ 6.");
        f.Item("1.", "Każda ze stron może wypowiedzieć umowę rachunku z zachowaniem terminów określonych w umowie.", 0, 18);
        f.Item("2.", "Klient może wypowiedzieć umowę w każdym czasie ze skutkiem na koniec miesiąca kalendarzowego, w którym złożono wypowiedzenie, o ile nie ma na rachunku zaległych zobowiązań.", 0, 18);
        f.Item("3.", "Bank może wypowiedzieć umowę z ważnych powodów, w szczególności w razie rażącego naruszenia postanowień Regulaminu, podania nieprawdziwych danych lub prowadzenia rachunku w sposób sprzeczny z prawem. Wypowiedzenie następuje w formie pisemnej lub na trwałym nośniku, z podaniem przyczyny.", 0, 18);
        f.Item("4.", "Po rozwiązaniu umowy Bank wypłaca Klientowi środki zgromadzone na rachunku wraz z należnymi odsetkami, w terminie siedmiu dni roboczych od dnia złożenia dyspozycji.", 0, 18);
        f.Gap(10);

        f.Line("Rozdział 4", 0, 12, true, leading: 15);
        f.Line("Zmiana regulaminu i rozwiązanie umowy", 0, 12, true, leading: 15);
        f.Gap(6);
        Paragraph(f, "§ 7.");
        f.Item("1.", "Bank może dokonać zmiany Regulaminu w przypadku wystąpienia co najmniej jednej z następujących przyczyn:", 0, 18);
        f.Item("1)", "zmiany powszechnie obowiązujących przepisów prawa mających wpływ na treść Regulaminu;", 18, 36);
        f.Item("2)", "wydania orzeczeń sądów, decyzji lub zaleceń organów nadzoru dotyczących usług objętych Regulaminem;", 18, 36);
        f.Item("3)", "rozszerzenia, zmiany lub wycofania usług oferowanych przez Bank, w tym zmiany funkcjonalności systemu bankowości internetowej;", 18, 36);
        f.Item("4)", "zmian w zakresie bezpieczeństwa usług płatniczych wynikających z postępu technicznego.", 18, 36);
        f.Item("2.", "O zmianie Regulaminu Bank zawiadamia Klienta na trwałym nośniku, w sposób ustalony w umowie, co najmniej na dwa miesiące przed proponowanym dniem wejścia w życie zmian, przekazując jednocześnie tekst jednolity Regulaminu wraz ze wskazaniem wprowadzanych zmian.", 0, 18);
        f.Item("3.", "Klient może przed proponowanym dniem wejścia w życie zmian zgłosić sprzeciw wobec tych zmian albo wypowiedzieć umowę bez ponoszenia opłat. Brak sprzeciwu zgłoszonego w tym terminie oznacza, że Klient wyraził zgodę na zmiany.", 0, 18);
        f.Gap(6);

        Paragraph(f, "§ 8.");
        f.Item("1.", "Zgłoszenie przez Klienta sprzeciwu, o którym mowa w § 7 ust. 3, bez jednoczesnego wypowiedzenia umowy, powoduje wygaśnięcie umowy z dniem poprzedzającym dzień wejścia w życie zmian, bez pobierania opłat z tego tytułu.", 0, 18);
        f.Item("2.", "Bank informuje Klienta w zawiadomieniu o zmianie Regulaminu o skutkach braku sprzeciwu oraz o prawie do wypowiedzenia umowy.", 0, 18);
        f.Item("3.", "Zmiana Regulaminu, która jest wyłącznie korzystna dla Klienta, może nastąpić bez zachowania terminu, o którym mowa w § 7 ust. 2, z dniem poinformowania Klienta o zmianie.", 0, 18);
        f.Gap(6);

        Paragraph(f, "§ 9.");
        f.Item("1.", "Umowa rachunku ulega rozwiązaniu w następujących przypadkach:", 0, 18);
        f.Item("1)", "upływu okresu wypowiedzenia umowy przez jedną ze stron;", 18, 36);
        f.Item("2)", "śmierci Klienta, z zastrzeżeniem przepisów o dziedziczeniu środków zgromadzonych na rachunku;", 18, 36);
        f.Item("3)", "wygaśnięcia umowy zgodnie z § 8 ust. 1;", 18, 36);
        f.Item("4)", "zakończenia działalności Banku w zakresie prowadzenia rachunków, na zasadach określonych w przepisach prawa.", 18, 36);
        f.Item("2.", "Rozwiązanie umowy nie wpływa na obowiązek uregulowania przez Klienta należności wynikających z umowy, które powstały przed dniem jej rozwiązania. Bank jest uprawniony do potrącenia tych należności ze środków zgromadzonych na rachunku, a pozostałą kwotę wypłaca lub przekazuje na wskazany przez Klienta rachunek.", 0, 18);
        f.Gap(6);

        Paragraph(f, "§ 10.");
        f.Item("1.", "Klient może składać reklamacje dotyczące usług świadczonych przez Bank w formie pisemnej, ustnie, telefonicznie lub w postaci elektronicznej, w tym za pośrednictwem systemu bankowości internetowej.", 0, 18);
        f.Item("2.", "Bank rozpatruje reklamację bez zbędnej zwłoki, nie później jednak niż w terminie 15 dni roboczych od dnia jej otrzymania. W szczególnie skomplikowanych przypadkach termin ten może zostać przedłużony do 35 dni roboczych, o czym Bank informuje Klienta.", 0, 18);
        f.Item("3.", "Odpowiedź na reklamację Bank przekazuje w postaci papierowej albo, na wniosek Klienta, za pomocą innego trwałego nośnika.", 0, 18);
        f.Item("4.", "W sprawach nieuregulowanych w Regulaminie stosuje się przepisy powszechnie obowiązującego prawa, w szczególności ustawy o usługach płatniczych oraz Kodeksu cywilnego.", 0, 18);

        f.Finish();
        return (b.Build(), f.Pages);

        static void Paragraph(Flow flow, string number) => flow.Line(number, 230, 11, true, leading: 16);
    }

    // ---------------------------------------------------------------- 2. taryfa-z-siatka

    private static byte[] TaryfaZSiatka(Recorder rec)
    {
        string[] cols = ["Usługa", "Opłata", "Częstotliwość"];
        double[] x = [72, 300, 420, 523];
        (string[] Service, string Fee, string Freq)[] rows =
        [
            (["Prowadzenie rachunku", "oszczędnościowo-rozliczeniowego"], "0,00 zł", "miesięcznie"),
            (["Wpłata gotówki we wpłatomacie"], "–", "za operację"),
            (["Wpłata gotówki w placówce", "(powyżej 5 000 zł)"], "0,5% min. 10 zł", "za operację"),
            (["Wypłata gotówki z bankomatu", "Banku"], "0,00 zł", "za operację"),
            (["Wypłata gotówki z bankomatu", "innego banku w kraju"], "5,00 zł", "za operację"),
            (["Wypłata gotówki z bankomatu", "za granicą"], "1,5% min. 10 zł", "za operację"),
            (["Przelew wewnętrzny", "w systemie bankowości internetowej"], "0,00 zł", "za operację"),
            (["Przelew zewnętrzny", "w systemie bankowości internetowej"], "1,00 zł", "za operację"),
            (["Przelew zewnętrzny", "złożony w placówce"], "10,00 zł", "za operację"),
            (["Zlecenie stałe", "ustanowienie lub zmiana"], "5,00 zł", "jednorazowo"),
            (["Zlecenie stałe", "realizacja"], "1,50 zł", "za operację"),
            (["Polecenie zapłaty", "realizacja"], "2,00 zł", "za operację"),
            (["Wydanie karty debetowej", "(pierwsza karta)"], "0,00 zł", "jednorazowo"),
            (["Wydanie duplikatu karty", "debetowej"], "30,00 zł", "jednorazowo"),
            (["Obsługa karty debetowej"], "6,00 zł", "miesięcznie"),
            (["Wyciąg z rachunku", "w formie elektronicznej"], "–", "miesięcznie"),
            (["Wyciąg z rachunku", "w formie papierowej"], "8,00 zł", "miesięcznie"),
            (["Zaświadczenie o posiadaniu", "rachunku i jego saldzie"], "20,00 zł", "za dokument"),
            (["Wydanie opinii bankowej"], "50,00 zł", "za dokument"),
            (["Blokada karty", "na wniosek Klienta"], "0,00 zł", "za operację"),
            (["Zmiana limitów transakcji", "w placówce"], "10,00 zł", "za operację"),
            (["Zamknięcie rachunku"], "–", "jednorazowo"),
            (["Powiadomienia SMS", "o operacjach na rachunku"], "2,00 zł", "miesięcznie"),
            (["Wystawienie potwierdzenia", "przelewu w placówce"], "5,00 zł", "za dokument"),
            (["Przelew natychmiastowy", "w systemie bankowości internetowej"], "3,00 zł", "za operację"),
            (["Przelew walutowy", "w ramach EOG"], "0,2% min. 15 zł", "za operację"),
        ];

        var b = new SyntheticPdfBuilder()
            .Title("Taryfa opłat i prowizji (z siatką)")
            .RunningHeader("Taryfa opłat i prowizji – " + Bank)
            .PageNumberFooter("Strona {n}");

        bool inTable = false;
        double tableTop = 0;
        var f = new Flow(b, [Left], Right - Left, rec);

        void DrawHeader(Flow flow)
        {
            tableTop = flow.Y;
            flow.Builder.HLine(x[0], x[3], tableTop);
            for (int i = 0; i < cols.Length; i++)
            {
                flow.Builder.Text(x[i] + 6, tableTop + 14, cols[i], 10, bold: true);
            }

            flow.Y = tableTop + 20;
            flow.Builder.HLine(x[0], x[3], flow.Y);
        }

        void CloseGrid(Flow flow)
        {
            foreach (double vx in x)
            {
                flow.Builder.VLine(vx, tableTop, flow.Y);
            }
        }

        f.OnPageStart = flow =>
        {
            if (inTable)
            {
                DrawHeader(flow);
            }
        };
        f.OnPageEnd = flow =>
        {
            if (inTable)
            {
                CloseGrid(flow);
            }
        };

        f.Line("Taryfa opłat i prowizji dla klientów indywidualnych", 0, 14, true, leading: 20);
        f.Item("", "Poniższa tabela zawiera wysokość opłat i prowizji pobieranych przez Bank Przykładowy S.A. za najczęściej wykonywane czynności. Wszystkie kwoty są kwotami brutto.", 0, 0);
        f.Gap(10);

        inTable = true;
        DrawHeader(f);
        foreach ((string[] service, string fee, string freq) in rows)
        {
            rec.Row(service, fee, freq);
            double h = (service.Length * 13) + 8;
            if (f.Y + h > Bottom)
            {
                f.Break();
            }

            double y0 = f.Y;
            for (int k = 0; k < service.Length; k++)
            {
                b.Text(x[0] + 6, y0 + 14 + (k * 13), service[k], 10);
            }

            b.Text(x[1] + 6, y0 + 14, fee, 10);
            b.Text(x[2] + 6, y0 + 14, freq, 10);
            f.Y = y0 + h;
            b.HLine(x[0], x[3], f.Y);
        }

        CloseGrid(f);
        inTable = false;
        f.Gap(16);
        f.Item("", "Opłaty i prowizje, które nie zostały wymienione w tabeli, ustalane są indywidualnie w umowie z Klientem. Bank zastrzega sobie prawo do zmiany Taryfy na zasadach określonych w Regulaminie rachunku.", 0, 0);

        f.Finish();
        return b.Build();
    }

    // ---------------------------------------------------------------- 3. taryfa-bez-siatki

    private static byte[] TaryfaBezSiatki(Recorder rec)
    {
        double[] x = [72, 360, 450];
        (string Section, (string[] Service, string Fee, string Freq)[] Rows)[] sections =
        [
            ("I. Rachunki",
            [
                (["Prowadzenie rachunku", "oszczędnościowo-rozliczeniowego"], "0,00 zł", "miesięcznie"),
                (["Prowadzenie rachunku walutowego"], "5,00 zł", "miesięcznie"),
                (["Wyciąg z rachunku", "w formie papierowej"], "8,00 zł", "miesięcznie"),
                (["Zaświadczenie o posiadaniu", "rachunku i jego saldzie"], "20,00 zł", "za dokument"),
                (["Zamknięcie rachunku"], "–", "jednorazowo"),
                (["Zmiana wzoru podpisu", "w placówce"], "10,00 zł", "jednorazowo"),
            ]),
            ("II. Karty płatnicze",
            [
                (["Wydanie karty debetowej", "(pierwsza karta)"], "0,00 zł", "jednorazowo"),
                (["Wydanie duplikatu karty"], "30,00 zł", "jednorazowo"),
                (["Obsługa karty debetowej"], "6,00 zł", "miesięcznie"),
                (["Wypłata gotówki z bankomatu", "innego banku w kraju"], "5,00 zł", "za operację"),
                (["Wypłata gotówki z bankomatu", "za granicą"], "1,5% min. 10 zł", "za operację"),
                (["Zastrzeżenie karty"], "0,00 zł", "za operację"),
            ]),
            ("III. Przelewy",
            [
                (["Przelew wewnętrzny", "w systemie bankowości internetowej"], "0,00 zł", "za operację"),
                (["Przelew zewnętrzny", "w systemie bankowości internetowej"], "1,00 zł", "za operację"),
                (["Przelew zewnętrzny", "złożony w placówce"], "10,00 zł", "za operację"),
                (["Przelew natychmiastowy"], "3,00 zł", "za operację"),
                (["Zlecenie stałe", "ustanowienie lub zmiana"], "5,00 zł", "jednorazowo"),
                (["Polecenie zapłaty", "realizacja"], "2,00 zł", "za operację"),
            ]),
            ("IV. Usługi dodatkowe",
            [
                (["Powiadomienia SMS", "o operacjach na rachunku"], "2,00 zł", "miesięcznie"),
                (["Wydanie opinii bankowej"], "50,00 zł", "za dokument"),
                (["Potwierdzenie przelewu", "wydane w placówce"], "5,00 zł", "za dokument"),
                (["Wypłata gotówki w placówce", "(powyżej 5 000 zł)"], "0,5% min. 10 zł", "za operację"),
                (["Zmiana limitów transakcji"], "–", "za operację"),
            ]),
        ];

        var b = new SyntheticPdfBuilder()
            .Title("Taryfa opłat i prowizji (bez siatki)")
            .RunningHeader("Taryfa opłat i prowizji – " + Bank)
            .PageNumberFooter("Strona {n}");

        bool inTable = false;
        var f = new Flow(b, [Left], Right - Left, rec);

        static void DrawHeader(Flow flow, double[] cx)
        {
            flow.Builder.Text(cx[0], flow.Y, "Usługa", 10, bold: true);
            flow.Builder.Text(cx[1], flow.Y, "Opłata", 10, bold: true);
            flow.Builder.Text(cx[2], flow.Y, "Częstotliwość", 10, bold: true);
            flow.Y += 24;
        }

        f.OnPageStart = flow =>
        {
            if (inTable)
            {
                DrawHeader(flow, x);
            }
        };

        f.Line("Taryfa opłat i prowizji – zestawienie skrócone", 0, 14, true, leading: 20);
        f.Item("", "Zestawienie obejmuje opłaty za podstawowe usługi dostępne w ofercie Banku Przykładowego S.A. dla klientów indywidualnych.", 0, 0);
        f.Gap(14);

        inTable = true;
        DrawHeader(f, x);
        foreach ((string section, var rows) in sections)
        {
            f.Gap(6);
            if (f.Y + 60 > Bottom)
            {
                f.Break();
            }

            b.Text(x[0], f.Y, section, 10.5, bold: true);
            f.Y += 22;

            foreach ((string[] service, string fee, string freq) in rows)
            {
                rec.Row(service, fee, freq);
                double h = ((service.Length - 1) * 12) + 22;
                if (f.Y + h > Bottom)
                {
                    f.Break();
                }

                b.Text(x[0], f.Y, service[0], 10);
                for (int k = 1; k < service.Length; k++)
                {
                    b.Text(x[0] + 8, f.Y + (k * 12), service[k], 10);
                }

                b.Text(x[1], f.Y, fee, 10);
                b.Text(x[2], f.Y, freq, 10);
                f.Y += h;
            }
        }

        inTable = false;
        f.Finish();
        return b.Build();
    }

    // ---------------------------------------------------------------- 4. regulamin-dwie-kolumny

    private static byte[] RegulaminDwieKolumny(Recorder rec)
    {
        double[] twoCols = [72, 343]; // each column 240 pt wide, 31 pt gutter
        var b = new SyntheticPdfBuilder()
            .Title("Regulamin korzystania z bankowości elektronicznej")
            .RunningHeader("Regulamin bankowości elektronicznej – " + Bank)
            .PageNumberFooter("Strona {n}");

        var f = new Flow(b, twoCols, 240, rec);

        f.Line("Regulamin bankowości elektronicznej", 0, 12, true, leading: 18);
        f.Gap(4);

        Paragraph(f, "§ 1.");
        f.Item("", "Regulamin określa zasady korzystania z systemu bankowości internetowej i aplikacji mobilnej udostępnianych przez Bank Przykładowy S.A.", 0, 0, 10);
        f.Item("", "Za pośrednictwem systemu Klient może w szczególności:", 0, 0, 10);
        f.Item("•", "sprawdzać saldo i historię operacji na rachunkach prowadzonych w Banku;", 6, 18, 10);
        f.Item("•", "składać dyspozycje przelewów, zleceń stałych oraz poleceń zapłaty;", 6, 18, 10);
        f.Item("•", "zarządzać limitami transakcji kartowych oraz blokować i odblokowywać kartę;", 6, 18, 10);
        f.Item("•", "zawierać umowy dotyczące produktów bankowych udostępnianych w kanale elektronicznym.", 6, 18, 10);
        f.Gap(6);

        Paragraph(f, "§ 2.");
        f.Item("", "Dostęp do systemu wymaga uwierzytelnienia Klienta za pomocą identyfikatora oraz hasła, a w przypadku operacji o podwyższonym ryzyku – dodatkowego składnika uwierzytelnienia.", 0, 0, 10);
        f.Item("", "Klient zobowiązuje się do:", 0, 0, 10);
        f.Item("•", "nieujawniania hasła ani kodów autoryzacyjnych osobom trzecim;", 6, 18, 10);
        f.Item("•", "korzystania z oprogramowania zabezpieczającego urządzenie, na którym uruchamia system;", 6, 18, 10);
        f.Item("•", "niezwłocznego zgłoszenia podejrzenia nieuprawnionego dostępu do konta.", 6, 18, 10);
        f.Gap(6);

        Paragraph(f, "§ 3.");
        f.Item("", "Bank dokłada należytej staranności, aby system działał nieprzerwanie, z wyłączeniem przerw technicznych zapowiadanych z odpowiednim wyprzedzeniem w komunikatach w systemie.", 0, 0, 10);
        f.Item("", "Bank nie ponosi odpowiedzialności za skutki niedostępności systemu wynikające z przyczyn niezależnych od Banku, w szczególności awarii sieci telekomunikacyjnych.", 0, 0, 10);
        f.Gap(6);

        Paragraph(f, "§ 4.");
        f.Item("", "Dyspozycje złożone w systemie są realizowane w terminach określonych w Regulaminie realizacji przelewów. Klient może odwołać dyspozycję do momentu jej przyjęcia do realizacji przez Bank.", 0, 0, 10);
        f.Item("", "Bank może zablokować dostęp do systemu, jeżeli:", 0, 0, 10);
        f.Item("•", "zachodzi podejrzenie próby nieuprawnionego użycia konta;", 6, 18, 10);
        f.Item("•", "Klient trzykrotnie podał błędne dane uwierzytelniające;", 6, 18, 10);
        f.Item("•", "wymagają tego przepisy prawa lub żądanie uprawnionego organu.", 6, 18, 10);
        f.Gap(6);

        Paragraph(f, "§ 5.");
        f.Item("", "Reklamacje dotyczące działania systemu Klient może składać w formie pisemnej, telefonicznie lub elektronicznie. Bank rozpatruje reklamację w terminie 30 dni od dnia jej otrzymania.", 0, 0, 10);
        f.Item("", "Zgłoszenie reklamacji nie zwalnia Klienta z obowiązku terminowego regulowania zobowiązań wobec Banku.", 0, 0, 10);

        // Closing page: single column.
        f.NewPage([Left], Right - Left);
        f.Line("Postanowienia końcowe", 0, 12, true, leading: 18);
        f.Gap(4);
        Paragraph(f, "§ 6.", 230);
        f.Item("", "W sprawach nieuregulowanych w Regulaminie zastosowanie mają przepisy powszechnie obowiązującego prawa, w tym ustawy Prawo bankowe oraz ustawy o usługach płatniczych.", 0, 0, 10.5);
        f.Item("", "Regulamin jest dostępny w placówkach Banku oraz na stronie internetowej Banku. Klient może w każdej chwili otrzymać jego tekst na trwałym nośniku.", 0, 0, 10.5);
        f.Gap(6);
        Paragraph(f, "§ 7.", 230);
        f.Item("", "Bank może zmienić Regulamin z ważnych powodów, w szczególności w razie zmiany przepisów prawa, rozszerzenia funkcjonalności systemu lub zmiany zasad bezpieczeństwa. O zmianach Bank informuje Klienta na co najmniej dwa miesiące przed ich wejściem w życie.", 0, 0, 10.5);
        f.Item("", "Klient, który nie zgadza się na zmiany, może wypowiedzieć umowę bez ponoszenia opłat przed dniem ich wejścia w życie.", 0, 0, 10.5);

        f.Finish();
        return b.Build();

        static void Paragraph(Flow flow, string number, double indent = 0) => flow.Line(number, indent, 10.5, true, leading: 16);
    }

    // ---------------------------------------------------------------- layout helper

    /// <summary>Minimal text-flow helper: wraps text, advances Y, moves between columns and pages.</summary>
    private sealed class Flow
    {
        private double[] _columns;
        private double _width;
        private int _column;

        private readonly Recorder _rec;

        public Flow(SyntheticPdfBuilder builder, double[] columns, double width, Recorder rec)
        {
            _rec = rec;
            Builder = builder;
            _columns = columns;
            _width = width;
            builder.Page();
            Pages = 1;
            Y = Top;
        }

        public SyntheticPdfBuilder Builder { get; }
        public double Y { get; set; }
        public int Pages { get; private set; }
        public Action<Flow>? OnPageStart { get; set; }
        public Action<Flow>? OnPageEnd { get; set; }

        public void Gap(double points) => Y += points;

        public void Line(string text, double indent = 0, double size = 10.5, bool bold = false, bool italic = false, double leading = 14)
        {
            if (Y > Bottom)
            {
                Break();
            }

            Builder.Text(_columns[_column] + indent, Y, text, size, bold, italic);
            Y += leading;
        }

        /// <summary>Hanging-indent item: <paramref name="marker"/> at <paramref name="markerIndent"/>, wrapped text at <paramref name="textIndent"/>.</summary>
        public void Item(string marker, string text, double markerIndent, double textIndent, double size = 10.5)
        {
            const double leading = 14;
            if (marker.Length > 0)
            {
                _rec.ListItems.Add(new ListTruth(marker, (int)(markerIndent / 18)));
            }

            int chars = (int)((_width - textIndent) / (size * 0.54));
            bool first = true;
            foreach (string line in Wrap(text, chars))
            {
                if (Y > Bottom)
                {
                    Break();
                }

                if (first && marker.Length > 0)
                {
                    Builder.Text(_columns[_column] + markerIndent, Y, marker, size);
                }

                Builder.Text(_columns[_column] + textIndent, Y, line, size);
                Y += leading;
                first = false;
            }

            Y += 3;
        }

        /// <summary>Continues in the next column, or on a new page when the last column is full.</summary>
        public void Break()
        {
            if (_column + 1 < _columns.Length)
            {
                _column++;
                Y = Top;
                return;
            }

            StartPage(_columns, _width);
        }

        public void NewPage(double[] columns, double width) => StartPage(columns, width);

        public void Finish() => OnPageEnd?.Invoke(this);

        private void StartPage(double[] columns, double width)
        {
            OnPageEnd?.Invoke(this);
            Builder.Page();
            Pages++;
            _columns = columns;
            _width = width;
            _column = 0;
            Y = Top;
            OnPageStart?.Invoke(this);
        }

        private static IEnumerable<string> Wrap(string text, int maxChars)
        {
            var line = new StringBuilder();
            foreach (string word in text.Split(' '))
            {
                if (line.Length > 0 && line.Length + 1 + word.Length > maxChars)
                {
                    yield return line.ToString();
                    line.Clear();
                }

                if (line.Length > 0)
                {
                    line.Append(' ');
                }

                line.Append(word);
            }

            if (line.Length > 0)
            {
                yield return line.ToString();
            }
        }
    }
}
