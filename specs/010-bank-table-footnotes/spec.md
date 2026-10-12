# Feature Specification: Poprawki tabel i przypisów parsera na regulaminach bankowych (scalone wiersze, odnośniki przed słowem, nagłówki wielowierszowe, macierze czynności)

**Feature Branch**: `010-bank-table-footnotes`

**Created**: 2026-10-12

**Status**: Draft

**Input**: User description: "Poprawki tabel i przypisów parsera na regulaminach bankowych (spec 010, parser 1.3.x). Źródło: porównanie z Doclingiem (publish/docling-eval/PRZYPADKI.md, poza gitem) — Docling odrzucony jako zamiennik, jego wynik służy tylko jako wzorzec oczekiwanego kształtu tabel. Wszystkie przypadki odtwarzamy jako syntetyczne repliki stron w testach (SyntheticPdfBuilder / TableSheet), bez treści i nazw prawdziwego banku; weryfikacja na prywatnym korpusie (LEGALAGENT_PRIVATE_CORPUS) i na regulaminach z publish/mBank.FaqGenerator-win-x64. Wynik może zawierać tylko tekst z PDF. Historie: P1 — scalone wiersze (rowspan) w tabelach z liniami (T11); P1 — przypisy z odnośnikiem przed słowem i definicjami bez nawiasu (T12); P1 — nagłówek kolumny w kilku liniach dzielony na osobne kolumny (T3); P2 — macierz czynności z wielowierszową pierwszą kolumną (T1, T2); P3 — pozostałe (T4–T6). Ograniczenia: nie pogorszyć taryf bez siatki, słowników definicji ani łączenia tabel przez strony; goldeny tylko za zgodą właściciela (FR-163); corpus refresh + verify; commity czerwony/zielony. Poza zakresem: tabela tagów SWIFT, Docling jako zależność."

## Kontekst

Feature poprawia bibliotekę `LegalAgent.PdfParser` po specyfikacji 009 (numeracja wymagań jest kontynuowana od
FR-700 i SC-110). Markdown biblioteki zasila chunker RAG (spec 004), który dzieli długie tabele na wiersze, i
generator FAQ (spec 006), który cytuje tekst jednostek dosłownie. Wiersz tabeli bez kontekstu (pusta pierwsza
kolumna), wartość „TAK” pod złym nagłówkiem albo zapis bez informacji, od kiedy obowiązuje, stają się błędną albo
niepełną odpowiedzią.

Właściciel porównał wynik parsera z wynikiem narzędzia Docling na regulaminach z prywatnego korpusu i na regulaminach
pobranych przez generator FAQ (opis przypadków: `publish/docling-eval/PRZYPADKI.md`, poza repozytorium). Docling
został odrzucony jako zamiennik (gubi strony taryf bez siatki, dzieli tabele na stronach, zamienia słowniki
definicji na tabele), ale w kilku typach tabel daje lepszy kształt. Ten kształt (liczba wierszy i kolumn, przypisanie
komórek) jest wzorcem oczekiwanego wyniku; **tekst komórek pochodzi zawsze z PDF**.

Oznaczenia dokumentów jak w PRZYPADKI.md: corp-*, firm-*, ind-* — prywatny korpus właściciela (z goldenami);
downloads-1…5 — regulaminy pobrane przez generator FAQ (`publish/mBank.FaqGenerator-win-x64/downloads`, bez
goldenów). Numery stron według PDF.

**Przypadki objęte zakresem** (szczegóły w PRZYPADKI.md):

| # | Usterka dziś | Dokumenty (strony) | Skutek |
|---|---|---|---|
| T11 | Scalona pionowo komórka (etykieta grupy wierszy) w tabeli z liniami trafia do jednego wiersza, pozostałe mają pustą 1. kolumnę | firm-5 (13) | Wiersz po podziale przez chunker nie wie, którego przelewu dotyczy |
| T12 | Odnośnik przypisu na początku słowa („³kurs…” → „3kurs”) i na końcu („Tak4”, „TAK/TAK5”); definicje pod kreską tym samym rozmiarem co tekst, z gołą cyfrą („³ Zapis obowiązuje od 26.08.2026 r.”) — zwykłe akapity | firm-5 (13), firm-1 (16–17), downloads-3 (17), macierze ind-* | Giną informacje, od kiedy obowiązuje który zapis; wiersze wyglądają na sprzeczne |
| T3 | Nagłówek kolumny w kilku liniach o różnym wyśrodkowaniu dzielony na kilka kolumn; puste kolumny, wartości pod złym nagłówkiem | downloads-2 (20–21), downloads-3 (18), downloads-4 (16–17), downloads-5 (6–7), firm-3 (8), ind-4 (12–13) | **Błędna treść** („TAK” pod innym kanałem niż w PDF) |
| T1 | Macierz „Co i gdzie możesz zrobić?” z wielowierszową 1. kolumną → format zapasowy TBL001 | ind-4 (42–46), ind-2 (33–36), downloads-5 (26–27), downloads-3 (17), firm-5 (22–23) | Kanały odklejone od czynności |
| T2 | Ta sama macierz (w PDF z liniami — research R1) → częściowo akapity („zmienić typ rachunku TAK/TAK5 TAK TAK”) | firm-1 (16–17) | Większość wierszy jako tekst |
| T4 | Wiersz z wielowierszowymi komórkami w środkowej kolumnie przerywa tabelę | firm-5 (12–13) | Terminy przelewów oderwane od nazw |
| T5 | Mała tabela 3-kolumnowa (w PDF z liniami, komórki wyśrodkowane — research R1) → TBL001 | ind-2 (6–7) | Brak struktury tabeli |
| T6 | Powtórzony nagłówek tabeli na następnej stronie → nagłówek `##` albo sklejony wiersz; komórka urwana na końcu strony | ind-4 (12→13), downloads-5 (7→8) | Fałszywy nagłówek dzieli fragment; urwana lista w komórce |

**Poza zakresem**: T7 (komórka „tekst + lista” dzielona na dwie kolumny), T8 (tabela 2×2 z listami w komórkach) —
tylko jeśli wypadną przy okazji, bez osobnych historii; T10 (tabela tagów SWIFT z wielopoziomowym nagłówkiem) —
znane ograniczenie; Docling jako zależność.

## Clarifications

### Session 2026-10-12

- Decyzja właściciela (w opisie feature'u): scalona pionowo komórka jest w Markdownie powtarzana w każdym objętym
  wierszu — to tekst z PDF, nie dopisany; model dokumentu zna scalenie; zmiana kontraktu wyjścia → nowa wersja
  kontraktu.
- Q: Jak zapisać w Markdownie wiersz grupujący nad nagłówkami kilku kolumn („Gdzie można zrobić?”)? → A: Tekst
  wiersza grupującego na początku każdego objętego nagłówka kolumny (powtórzenie tekstu z PDF jak przy scaleniu
  pionowym); bez osobnego wiersza i bez akapitu nad tabelą.

### Session 2026-10-12 (clarify)

- Q: Czy wykonawca może sam commitować zmiany prywatnych goldenów corp-*/firm-*/ind-* przy czystych zbiorach słów i
  kontroli liczb, czy zatrzymywać się przy każdej zmianie? → A: Wąska zgoda z góry dla prywatnych goldenów
  corp-*/firm-*/ind-*: zmiany wyłącznie w tabelach i przypisach z przypadków T1–T12, czyste zbiory słów (poza
  dozwolonymi powtórzeniami z FR-750), kontrola liczb bez pogorszenia; diff w handoffie. Każda inna zmiana oraz
  każda zmiana `Corpus/acts`, `Corpus/banking`, goldenów fragmentów, korpusu syntetycznego, reg1–3 i regulamin-pdp —
  stop i pytanie do właściciela.

### Session 2026-10-12 (plan)

- Research geometrii (research.md R1): T1, T2 (firm-1), T3, T5 (ind-2) i T6 to tabele **w pełni z liniami** (każda
  komórka ma własne odcinki linii), a nie „bez ramek”, jak podaje PRZYPADKI.md; usterki biorą się z wyznaczania
  kolumn z lewych krawędzi wyśrodkowanego tekstu zamiast z linii pionowych. Wymagania US3–US5 dotyczą więc przede
  wszystkim tabel z liniami; warianty bez linii (US4 scen. 2, FR-741) zostają w zakresie jako P3 — jeśli poprawka
  dla nich pogorszy inne tabele, wynik to opis w „Znanych ograniczeniach”.
- Research chunkera (research.md R3): części tabeli powstają z podzbioru wierszy, więc komórka scalona zapisana raz
  (w pierwszym wierszu) zginęłaby w częściach zaczynających się w środku grupy. Model zapisuje w pierwszym wierszu
  komórkę z liczbą objętych wierszy, a w kolejnych objętych wierszach komórkę-kontynuację z tym samym tekstem i
  znacznikiem kontynuacji (FR-702); chunker i renderer nie wymagają wtedy zmian logiki.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Scalone wiersze w tabelach z liniami (Priority: P1)

Jako twórca aplikacji RAG chcę, żeby każdy wiersz tabeli, w której etykieta grupy obejmuje kilka wierszy, zawierał
tę etykietę, bo chunker dzieli długie tabele na wiersze, a wiersz z pustą pierwszą kolumną („| | ³kurs z momentu
złożenia zlecenia…”) nie mówi, którego przelewu dotyczy.

**Why this priority**: bez tego fragmenty tabel kursów i opłat są dla modelu bezużyteczne albo mylące; poprawka
dotyczy tylko tabel z liniami, w których linie same pokazują scalenie.

**Independent Test**: syntetyczna replika tabeli z liniami (bez nazw prawdziwych banków): 2 kolumny, druga ma linię
poziomą pod każdym wierszem, pierwsza tylko na granicach grup; grupa A obejmuje 3 wiersze, grupa B 4 wiersze, grupa
C 1 wiersz. Markdown: 8 wierszy danych, w każdym wierszu grupy A tekst etykiety A, w każdym wierszu grupy B — B;
model dokumentu podaje dla komórki etykiety A zasięg 3 wierszy. Test nie przechodzi na kodzie sprzed poprawki.

**Acceptance Scenarios**:

1. **Given** tabela z liniami, w której w danej kolumnie między dwoma wierszami nie ma linii poziomej, a w innej
   kolumnie jest, **When** konwersja, **Then** komórka tej kolumny jest jedną komórką obejmującą oba wiersze
   (scalenie pionowe), a jej tekst jest złożony ze wszystkich linii w jej obszarze, w kolejności jak w PDF.
2. **Given** scalona pionowo komórka, **When** renderowanie Markdownu, **Then** jej tekst stoi w każdym objętym
   wierszu w tej samej kolumnie, a model dokumentu zapisuje ją raz, z liczbą objętych wierszy.
3. **Given** tabela z liniami pod każdym wierszem we wszystkich kolumnach albo tabela bez linii, **When**
   konwersja, **Then** wynik jak dotąd (bez scaleń).
4. **Given** podział tabeli ze scaleniem na fragmenty przez chunker, **When** fragment zaczyna się w środku grupy,
   **Then** każdy wiersz fragmentu ma etykietę grupy.

---

### User Story 2 - Przypisy z odnośnikiem przed słowem i definicjami bez nawiasu (Priority: P1)

Jako twórca aplikacji RAG chcę, żeby odnośniki przypisów w regulaminach (także w komórkach tabel) były `[^n]`, a
definicje przypisów `[^n]: …`, bo przypisy rozróżniają wersje zapisów („obowiązuje do 25.08.2026” / „od
26.08.2026”), a dziś „³kurs” wychodzi jako „3kurs”, „Tak⁴” jako „Tak4”, a definicje jako zwykłe akapity.

**Why this priority**: zmieniona liczba w tekście („3kurs”, „TAK/TAK5”) i zgubione powiązanie z datą obowiązywania
dają modelowi pozornie sprzeczne zapisy; mechanizm przypisów już istnieje, kontrakt się nie zmienia.

**Independent Test**: syntetyczna replika strony z tekstem i tabelą 8 pt: komórka zaczynająca się od podniesionej
cyfry 5 pt („³kurs z momentu…”), komórki „Tak⁴” i „TAK/TAK⁵”, pod krótką kreską przy dolnym marginesie definicje
8 pt „³ Zapis obowiązuje od …”, „⁴ Zapis obowiązuje do …”, „5 …” (ostatnia etykieta niepodniesiona); na tej samej
stronie zwykłe liczby: kwota „1 000,00 zł”, data „26.08.2026 r.”, lista „1)”, „2)”, „art. 5¹”. Markdown: `[^n]` w
miejscu każdego odnośnika (także w komórkach), definicje `[^n]: Zapis obowiązuje od …` w istniejącym formacie,
zwykłe liczby i „art. 5¹” bez zmian. Test nie przechodzi na kodzie sprzed poprawki.

**Acceptance Scenarios**:

1. **Given** cyfra wyraźnie mniejsza od tekstu i podniesiona, stojąca na początku słowa lub komórki, do której jest
   definicja przypisu na tej samej albo dalszej stronie, **When** konwersja, **Then** w jej miejscu stoi `[^n]`, a słowo po niej jest
   nietknięte (`[^n]kurs z momentu…`; numer `n` według istniejącej, kolejnej numeracji przypisów).
2. **Given** taka cyfra na końcu słowa w komórce tabeli („Tak⁴”, „TAK/TAK⁵”), **When** konwersja, **Then**
   „Tak[^4]”, „TAK/TAK[^5]”.
3. **Given** obszar pod krótką kreską przy dolnym marginesie z tekstem tego samego rozmiaru co tekst główny, którego
   linie zaczynają się etykietami (cyfrą podniesioną lub nie, bez nawiasu), do których na tej stronie są odnośniki,
   **When** konwersja, **Then** linie obszaru są definicjami przypisów `[^n]: …`, a nie akapitami treści.
4. **Given** obszar pod kreską, którego etykiety nie mają odnośników na stronie, albo tekst bez kreski nad nim,
   **When** konwersja, **Then** wynik jak dotąd (zwykłe akapity).
5. **Given** zwykłe liczby (kwoty, daty, numery list, numery jednostek, „art. 5¹” ze spec 009) i cyfry tej samej
   wielkości co tekst, **When** konwersja, **Then** bez zmian.
6. **Given** przypisy w dotychczasowych formatach (mniejsza czcionka, etykieta „3)”, odnośnik na końcu słowa),
   **When** konwersja, **Then** wynik jak dotąd.

---

### User Story 3 - Nagłówek kolumny w kilku liniach jako jedna komórka (Priority: P1)

Jako czytelnik Markdownu i generator FAQ chcę, żeby nagłówek kolumny złożony z kilku linii („**serwis**” /
„**transakcyjny**” / „(nasza strona internetowa)”) był jedną komórką nad swoją kolumną danych, bo dziś parser
tworzy z niego kilka kolumn, dane trafiają pod zły nagłówek i model odpowiada, że czynność jest dostępna w innym
kanale niż w PDF.

**Why this priority**: jedyny przypadek, w którym parser zmienia treść (przypisanie wartości do kolumny); dotyczy
sześciu dokumentów i macierzy kanałów obecnych w większości regulaminów.

**Independent Test**: syntetyczna replika tabeli: 5 kolumn danych z jednowierszowymi wartościami („TAK”, „NIE”,
„TAK (na życzenie Klienta)”), nagłówek, w którym komórki mają 2–3 linie o różnym wyśrodkowaniu i różnej szerokości
słów; wariant z wierszem grupującym („Gdzie można zrobić?”) nad nagłówkami kolumn 2–5. Markdown: tabela o 5
kolumnach, każdy nagłówek jako jedna komórka („serwis transakcyjny (nasza strona internetowa)”), żadnej pustej
kolumny, każda wartość pod tym nagłówkiem, pod którym stoi w PDF. Test nie przechodzi na kodzie sprzed poprawki.

**Acceptance Scenarios**:

1. **Given** tabela, w której linie nagłówka leżą nad jedną kolumną danych, ale mają inne położenie i szerokość niż
   dane, **When** konwersja, **Then** liczba kolumn tabeli jest wyznaczona przez kolumny danych, a linie nagłówka nad
   jedną kolumną danych są złożone w jedną komórkę, w kolejności czytania.
2. **Given** taka tabela, **When** konwersja, **Then** żadna kolumna nie jest pusta we wszystkich wierszach danych,
   a każda wartość stoi pod nagłówkiem, nad którym leży w PDF.
3. **Given** wartość komórki danych złożona z kilku słów z wyraźnym odstępem („TAK (na życzenie Klienta)”), **When**
   konwersja, **Then** zostaje jedną komórką, nie dwiema kolumnami.
4. **Given** wiersz grupujący nad nagłówkami kilku kolumn („Gdzie można zrobić?”), **When** konwersja, **Then**
   nie tworzy osobnej kolumny ani wiersza i nie przesuwa danych, a jego tekst stoi na początku każdego objętego
   nagłówka kolumny („Gdzie można zrobić? serwis transakcyjny (…)”, „Gdzie można zrobić? BOK”), tak by każda część
   tabeli po podziale przez chunker miała pełny kontekst.
5. **Given** tabela, w której nagłówek i dane mają te same granice kolumn (taryfy, tabele z siatką), **When**
   konwersja, **Then** wynik jak dotąd.

---

### User Story 4 - Macierz czynności z wielowierszową pierwszą kolumną (Priority: P2)

Jako twórca aplikacji RAG chcę, żeby macierz „Co i gdzie możesz zrobić? | serwis transakcyjny (…) | … | BOK |
placówka” była jedną tabelą GFM, bo dziś spada do formatu zapasowego TBL001 (19 linii `a \| b` z rozsypanymi
fragmentami pierwszej kolumny) albo rozsypuje się na akapity, a kanały odklejają się od czynności.

**Why this priority**: najwięcej tabel w regulaminach detalicznych i dla firm (macierz w każdym regulaminie obsługi
klientów), ale skutek to brak struktury, nie błędna treść; częściowo zależy od US3 (nagłówek) i US2 (odnośniki
„TAK/TAK⁵”).

**Independent Test**: syntetyczna replika macierzy ciągnącej się przez dwie strony: 5 kolumn, pierwsza z komórkami
na 2–6 linii, pozostałe jednowierszowe i wyśrodkowane w pionie względem komórki pierwszej kolumny, nagłówek
pogrubiony w 2–3 liniach; trzy warianty: z cienkimi liniami poziomymi między wierszami, bez linii, z wysokim
nagłówkiem zawiniętym na 3 linie (jak firm-1, 4 kolumny). Markdown: jedna tabela GFM N×5 (N×4), każdy wiersz
z pełnym tekstem czynności i wartościami w swoich kolumnach, tabela połączona przez strony jak dotąd, bez TBL001.
Test nie przechodzi na kodzie sprzed poprawki.

**Acceptance Scenarios**:

1. **Given** wiersz, w którym pierwsza kolumna ma kilka linii, a pozostałe kolumny jedną linię wyśrodkowaną w pionie,
   **When** konwersja, **Then** to jeden wiersz tabeli z tekstem pierwszej kolumny złożonym ze wszystkich jej linii.
2. **Given** (P3, research R1) taka macierz bez linii, w której odstęp między wierszami jest niewiele większy od
   odstępu między liniami komórki, **When** konwersja, **Then** granice wierszy wyznaczają linie z wartościami w pozostałych kolumnach i
   odstępy, a żaden wiersz nie zostaje akapitem.
3. **Given** macierz ciągnąca się przez kilka stron, **When** konwersja, **Then** jedna tabela, łączona przez strony
   jak dotąd.
4. **Given** wartości z odnośnikami przypisów („TAK/TAK⁵”, „Tak¹³”) lub dopiskiem („Tak (na życzenie klienta)”),
   **When** konwersja, **Then** zostają w swojej komórce (odnośniki według US2).

---

### User Story 5 - Pozostałe układy tabel: wielowierszowe komórki w środku, mała tabela, powtórzony nagłówek (Priority: P3)

Jako czytelnik Markdownu chcę, żeby tabele terminów przelewów, małe tabele i tabele przechodzące przez
stronę z powtórzonym nagłówkiem były kompletnymi tabelami, bo dziś przerywają się akapitami, spadają do TBL001 albo
dostają fałszywy nagłówek `##`, który dzieli fragment.

**Why this priority**: rzadsze układy (po jednym–dwóch dokumentach na typ), skutek głównie w strukturze.

**Independent Test**: trzy syntetyczne repliki: (T4) 3 kolumny, wiersz, w którym środkowa kolumna ma dwa akapity
(„jeśli nie wymaga…” / „jeśli wymaga…”), a pierwsza i trzecia jedną linię wyśrodkowaną w pionie, i dalsze wiersze;
(T5) tabela 3×4 z liniami i krótkimi komórkami wyśrodkowanymi w kolumnach 2–3 (wariant P3: bez linii) („rodzaj rachunku | umowa | dyspozycja”); (T6) tabela kończąca się na
dole strony komórką z listą urwaną po „…, czyli:”, a na następnej stronie powtórzony nagłówek i ciąg dalszy listy.
Markdown: (T4) jedna tabela ze wszystkimi wierszami; (T5) tabela GFM 3-kolumnowa; (T6) jedna tabela, powtórzony
nagłówek pominięty, komórka z listą w całości w jednym wierszu. Każdy test nie przechodzi na kodzie sprzed poprawki.

**Acceptance Scenarios**:

1. **Given** wiersz, w którym środkowa kolumna ma kilka akapitów, a sąsiednie kolumny po jednej linii, **When**
   konwersja, **Then** to jeden wiersz tabeli, a tabela trwa dalej.
2. **Given** mała tabela z liniami i wyśrodkowanymi komórkami albo (P3) bez linii, o co najmniej 3 kolumnach i 3 wierszach (z nagłówkiem), krótkich komórkach i
   stałych granicach kolumn, **When** konwersja, **Then** tabela GFM, nie TBL001.
3. **Given** tabela przechodząca na następną stronę, której pierwsze linie powtarzają nagłówek z poprzedniej strony,
   **When** konwersja, **Then** powtórzony nagłówek nie jest ani nagłówkiem `##`, ani wierszem danych — jest
   pomijany, a tabela łączona przez strony jak dotąd.
4. **Given** komórka ostatniego wiersza na stronie, której ciąg dalszy jest w tej samej kolumnie pierwszego wiersza
   następnej strony (pozostałe kolumny tego wiersza puste), **When** konwersja, **Then** ciąg dalszy jest doklejony do
   tej komórki zamiast tworzyć nowy wiersz.
5. **Given** powtórzony nagłówek tabeli, **When** pominięcie go w Markdownie, **Then** jego słowa są obecne w
   Markdownie (w nagłówku tabeli z pierwszej strony), więc porównanie zbiorów słów i kontrola liczb nie wykazują
   utraty tekstu.

---

### User Story 6 - Bez regresji, z udokumentowanymi miarami i wydaniem (Priority: P1)

Jako właściciel chcę mieć pewność, że poprawki tabel nie psują taryf bez siatki, słowników definicji, łączenia
tabel przez strony ani aktów prawnych, że żaden tekst nie ginie ani nie jest dopisywany i że wydanie (wersja,
kontrakt, korpus syntetyczny, dokumentacja, publish/) jest spójne.

**Why this priority**: tabele dotykają rdzenia `TableDetection`, z którego korzystają taryfy korpusu syntetycznego,
tabele-dokumenty (spec 002) i słowniki (spec 007); przegląd goldenów kosztuje czas właściciela.

**Independent Test**: pełny zestaw kontroli przed każdym commitem parsera; porównanie zbiorów słów i kontrola liczb
(spec 009) na prywatnym korpusie przed i po; tabela miar przed/po w handoffie; `verify` korpusu syntetycznego czysty
z nową wersją.

**Acceptance Scenarios**:

1. **Given** prywatne goldeny corp-*, firm-*, ind-*, **When** zmiana parsera zmienia je wyłącznie w tabelach i
   przypisach z przypadków T1–T12 przy czystych zbiorach słów i kontroli liczb, **Then** wykonawca commituje nowe
   goldeny i opisuje diff w handoffie (FR-755); **Given** każda inna zmiana goldenów (także `Corpus/acts`,
   `Corpus/banking`, fragmenty, korpus syntetyczny, reg1–3, regulamin-pdp), **Then** wykonawca zatrzymuje się i
   commituje dopiero po zgodzie właściciela.
2. **Given** prywatny korpus, **When** porównanie zbiorów słów Markdownu przed i po, **Then** brak utraconych słów i
   brak słów spoza PDF; jedyne dodatkowe wystąpienia słów to tekst scalonych komórek powtórzony w objętych wierszach
   (US1) i tekst wierszy grupujących powtórzony w objętych nagłówkach (US3).
3. **Given** kontrola liczb ze spec 009, **When** pełne testy prywatnego korpusu, **Then** regulaminy z listy nadal
   mają 0 liczb nadmiarowych, a liczby sklejone dziś z odnośnikami („TAK/TAK5”, „Tak13”, „3kurs”) znikają z liczb
   nadmiarowych w dokumentach spoza listy.
4. **Given** taryfy korpusu syntetycznego, słowniki definicji (format `- 1/ **termin** definicja`) i tabele łączone
   przez strony, **When** `refresh`, `verify` i `CorpusFull`, **Then** bez zmian i bez pogorszenia miar.

### Edge Cases

- Scalenie pionowe w kolumnie innej niż pierwsza (np. wspólna wartość dla kilku wierszy w ostatniej kolumnie) —
  obsługiwane tak samo jak w pierwszej.
- Scalona komórka, której tekst ma mniej linii niż objętych wierszy i jest wyśrodkowany w pionie — tekst należy do
  całej komórki, nie do wiersza, na który wypada jego środek.
- Scalona komórka przechodząca przez granicę strony — część na każdej stronie jest osobną komórką swojej strony
  (scalenie nie przekracza strony); przy łączeniu tabeli przez strony wiersze z drugiej strony dostają tekst części
  z drugiej strony, a jeśli jej nie ma (pusta komórka bez linii nad sobą na początku strony) — zostają puste; taki
  wiersz ma tekst w co najmniej dwóch kolumnach, więc nie jest doklejany do poprzedniej strony (FR-743).
- Brak linii poziomej we wszystkich kolumnach między dwoma rzędami tekstu w tabeli z liniami — to jeden wiersz z
  wielowierszowymi komórkami, nie scalenie.
- Linia pozioma przerwana (krótkie odcinki, przerwa na skrzyżowaniu z linią pionową) — liczy się pokrycie zakresu
  kolumny, nie ciągłość linii.
- Kolumna, w której pod każdym wierszem jest linia, ale komórka jest pusta — pusta komórka, nie scalenie.
- Cyfra podniesiona na początku komórki, do której nie ma definicji przypisu na stronie (np. numer pozycji) — bez
  zmian (zostaje cyfrą).
- Odnośnik przed słowem i ten sam numer także na końcu innego słowa — oba są odnośnikami do tej samej definicji.
- Definicja przypisu zajmująca kilka linii pod kreską — linie bez etykiety są ciągiem dalszym poprzedniej definicji.
- Kilka krótkich definicji, które dziś są sklejane w jeden akapit („1 Zapis obowiązuje do 13.01.2026 r. włącznie
  2 Zapis obowiązuje od 14.01.2026 r.” w goldenie firm-1) albo stoją w jednym wierszu obok siebie — każda etykieta z
  odnośnikiem na stronie zaczyna osobną definicję `[^n]:`.
- Definicja, której etykieta ginie dziś w wyniku (goły tekst „Zapis obowiązuje od 14.01.2026 r.” bez numeru) — po
  poprawce etykieta jest rozpoznana i tekst staje się definicją; jeśli etykiety nie ma w warstwie tekstowej PDF, tekst
  zostaje akapitem.
- Etykiety w obszarze pod kreską niepodniesione i równe rozmiarem tekstowi („8 Zapis…”, „9 Zapis…”) — dozwolone
  tylko w obszarze pod kreską i tylko dla numerów, do których są odnośniki na tej stronie.
- Krótka kreska nad stopką lub nad numerem strony (artefakty), bez etykiet z odnośnikami — nie jest obszarem
  przypisów.
- Odnośnik w nagłówku tabeli lub w nagłówku sekcji — renderowany jak w innych miejscach.
- „art. 5¹”, „§ 7¹” (spec 009) — indeks górny jednostki, nie odnośnik, nawet jeśli na stronie jest przypis „1”.
- Nagłówek kolumny dłuższy niż szerokość kolumny danych, wystający nad sąsiednią kolumnę — przypisany do kolumny
  danych, nad którą leży jego środek.
- Komórka nagłówka nad dwiema kolumnami danych (scalenie poziome w nagłówku) — jedna komórka obejmująca obie
  kolumny w istniejącej postaci scalenia poziomego.
- Macierz, w której pierwsza kolumna ma tyle linii, że wiersz przechodzi przez granicę strony — wiersz dokończony
  jak w US5 (scenariusz 4).
- Tabela z jednym wierszem danych na nowej stronie po powtórzonym nagłówku — dołączona do tabeli z poprzedniej
  strony.
- Mała tabela przerwana granicą strony tak, że na jednej stronie jest tylko nagłówek i jeden wiersz (ind-2
  s. 6–7) — próg wierszy z FR-741 liczy wiersze całej tabeli łączonej przez strony, nie jednej strony.
- Tekst scalonej komórki lub wiersza grupującego zawierający liczby (np. „do 10 000 zł”) — powtórzenie w objętych
  wierszach/nagłówkach nie jest liczbą nadmiarową kontroli liczb (FR-758).
- Tekst identyczny z nagłówkiem tabeli, ale niebędący jej kontynuacją (np. nowa tabela o tym samym nagłówku po
  akapicie) — nie jest pomijany.

## Requirements *(mandatory)*

### Functional Requirements

**Scalone wiersze w tabelach z liniami (US1, T11)**

- **FR-700**: W tabeli z liniami granica wiersza MUSI być rozpatrywana osobno dla każdej kolumny: linia pozioma jest
  granicą wiersza w tych kolumnach, których zakres pokrywa.
- **FR-701**: Gdy w kolumnie między dwoma wierszami nie ma linii, a w co najmniej jednej innej kolumnie jest, komórki
  tej kolumny MUSZĄ tworzyć jedną komórkę scaloną pionowo, obejmującą te wiersze; jej tekst to wszystkie linie w jej
  obszarze, w kolejności jak w PDF.
- **FR-702**: Model dokumentu MUSI zapisywać scalenie pionowe: komórka w pierwszym objętym wierszu podaje liczbę
  objętych wierszy, a każdy kolejny objęty wiersz ma w tej kolumnie komórkę-kontynuację z tym samym tekstem i
  znacznikiem, że kontynuuje komórkę z góry (każdy wiersz zachowuje pełny zestaw kolumn).
- **FR-703**: Markdown MUSI powtarzać tekst scalonej pionowo komórki w każdym objętym wierszu (decyzja właściciela);
  fragmenty chunkera MUSZĄ dziedziczyć tę postać (każdy wiersz fragmentu ma tekst scalonej komórki).
- **FR-704**: Tabele bez linii oraz tabele z liniami pod każdym wierszem we wszystkich kolumnach MUSZĄ dawać wynik jak
  dotąd.

**Przypisy z odnośnikiem przed słowem i definicjami bez nawiasu (US2, T12)**

- **FR-710**: Odnośnikiem przypisu MUSI być także cyfra (ciąg cyfr) wyraźnie mniejsza od tekstu i podniesiona,
  stojąca na początku słowa lub komórki, jeśli jest definicja przypisu o tym numerze na tej samej albo dalszej stronie
  (ta sama reguła co dla odnośnika na końcu słowa); słowo po niej zostaje nietknięte.
- **FR-711**: Odnośniki na końcu słowa w komórkach tabel („Tak⁴”, „TAK/TAK⁵”) MUSZĄ być rozpoznawane tak samo jak w
  tekście ciągłym.
- **FR-712**: Obszar przypisów MUSI być rozpoznawany także wtedy, gdy jego tekst ma rozmiar równy tekstowi głównemu,
  pod warunkiem że leży pod krótką kreską przy dolnym marginesie strony (kreska w dolnych 40% strony, nie dłuższa niż
  60% szerokości strony — progi jak dotąd), a jego linie zaczynają się etykietami, do których na tej stronie są
  odnośniki.
- **FR-713**: W takim obszarze etykieta definicji MOŻE być gołą cyfrą (podniesioną lub nie, bez nawiasu); poza nim
  wymagania etykiet się nie zmieniają.
- **FR-714**: Wynik MUSI używać istniejącego formatu przypisów (`[^n]` w miejscu odnośnika, `[^n]: definicja`), także
  w komórkach tabel; kontrakt przypisów się nie zmienia.
- **FR-715**: Zwykłe liczby (kwoty, daty, numery list i jednostek), cyfry tej samej wielkości co tekst poza obszarem
  przypisów oraz indeksy górne jednostek („art. 5¹”, „§ 7¹” — spec 009) NIE MOGĄ być rozpoznawane jako odnośniki ani
  etykiety; dotychczas rozpoznawane przypisy MUSZĄ dawać wynik jak dotąd.

**Nagłówek kolumny w kilku liniach (US3, T3)**

- **FR-720**: Liczba kolumn tabeli MUSI być wyznaczana przez kolumny danych; linie nagłówka leżące nad jedną kolumną
  danych MUSZĄ być złożone w jedną komórkę nagłówka (w kolejności czytania), niezależnie od wyśrodkowania i odstępów
  słów w poszczególnych liniach nagłówka.
- **FR-721**: Tabela NIE MOŻE zawierać kolumny pustej we wszystkich wierszach danych, która powstała z podziału
  nagłówka; każda wartość MUSI stać pod nagłówkiem, nad którym leży w PDF.
- **FR-722**: Wartość komórki danych złożona z kilku słów („TAK (na życzenie Klienta)”) NIE MOŻE być dzielona na
  kolumny, jeśli leży w zakresie jednej kolumny danych.
- **FR-723**: Wiersz grupujący nad nagłówkami kilku kolumn NIE MOŻE tworzyć kolumn ani wierszy ani przesuwać danych;
  jego tekst MUSI stać na początku każdego objętego nagłówka kolumny (decyzja właściciela, Clarifications).

**Macierz czynności (US4, T1, T2)**

- **FR-730**: Wiersz, w którym jedna kolumna ma kilka linii, a pozostałe po jednej linii wyśrodkowanej w pionie
  względem niej, MUSI być jednym wierszem tabeli — z liniami i bez linii.
- **FR-731**: Macierz czynności (pierwsza kolumna wielowierszowa, pozostałe krótkie wartości) MUSI być tabelą GFM,
  nie formatem zapasowym TBL001 ani akapitami, także z nagłówkiem wyższym niż wiersze danych; łączenie przez strony
  jak dotąd.

**Pozostałe układy (US5, T4–T6)**

- **FR-740**: Wiersz z wielowierszowymi komórkami (także kilkoma akapitami) w kolumnie środkowej NIE MOŻE przerywać
  tabeli.
- **FR-741**: Mała tabela z liniami i wyśrodkowanymi komórkami (ind-2) MUSI być tabelą GFM z kolumnami według linii
  pionowych. (P3, research R1) Mała tabela bez linii (co najmniej 3 kolumny i 3 wiersze z nagłówkiem, stałe granice
  kolumn, krótkie komórki) MUSI być rozpoznana jako tabela GFM, przy czym wiersze liczone są w całej tabeli
  łączonej przez strony; progi NIE MOGĄ sprawić, że zwykłe akapity lub listy z wyrównanymi
  fragmentami (słowniki ze spec 007, listy etykiet) staną się tabelami.
- **FR-742**: Powtórzony na następnej stronie nagłówek tabeli łączonej przez strony MUSI być rozpoznany (te same
  teksty komórek nagłówka w tych samych kolumnach, a jeśli tabela ma wiersz grupujący — także te same teksty wiersza
  grupującego) i pominięty razem z wierszem grupującym — NIE MOŻE stać się nagłówkiem sekcji ani wierszem danych.
- **FR-743**: Pierwszy wiersz danych tabeli na następnej stronie (po pominiętym powtórzonym nagłówku) MUSI zostać
  doklejony do ostatniego wiersza poprzedniej strony, jeśli ma tekst w **dokładnie jednej** kolumnie (pozostałe
  puste), a ostatni wiersz poprzedniej strony ma w tej kolumnie tekst (komórka urwana, np. lista po „…, czyli:”).
  Wiersz z tekstem w dwóch lub więcej kolumnach (np. pusta 1. kolumna i wartości w pozostałych) jest zwykłym
  wierszem, nie doklejeniem.

**Wymagania wspólne**

- **FR-750**: Wynik MUSI zawierać wyłącznie tekst z PDF; struktura wyrażana jest tylko formatowaniem. Jedynymi
  powtórzeniami tekstu są tekst scalonej pionowo komórki w objętych wierszach (FR-703) i tekst wiersza grupującego
  w objętych nagłówkach (FR-723); pominięty powtórzony nagłówek
  (FR-742) jest obecny w nagłówku tabeli z pierwszej strony.
- **FR-751**: Reguły MUSZĄ opierać się wyłącznie na geometrii (linie, położenie, odstępy), typografii i wzorcach
  tekstu, nie na nazwie dokumentu, wydawcy ani treści konkretnych nagłówków.
- **FR-752**: Każda zmiana zachowania parsera MUSI być odtworzona syntetyczną repliką strony (bez treści i nazw
  prawdziwego banku) najpierw jako test, który nie przechodzi na kodzie sprzed poprawki (osobny commit), a potem
  poprawką, z którą przechodzi (osobny commit).
- **FR-753**: Przed każdym commitem zmieniającym parser MUSI przejść pełny zestaw kontroli: testy solucji, prywatny
  korpus właściciela (z kontrolą liczb ze spec 009), `refresh` i `verify` korpusu syntetycznego oraz kategoria
  `CorpusFull`. Wynik na regulaminach downloads-1…5 MUSI być przejrzany względem przypadków z PRZYPADKI.md.
- **FR-754**: Taryfy bez siatki, słowniki definicji (format ze spec 007), tabele-dokumenty (spec 002) i łączenie
  tabel przez strony NIE MOGĄ się pogorszyć.
- **FR-755**: Goldeny prywatnych dokumentów corp-*, firm-* i ind-* wykonawca MOŻE zaktualizować bez osobnej zgody,
  jeśli przejrzał różnice i są one wyłącznie w tabelach i przypisach z przypadków T1–T12, porównanie zbiorów słów
  jest czyste (poza powtórzeniami dozwolonymi w FR-750), a kontrola liczb ze spec 009 i miary się nie pogarszają;
  każdą taką zmianę opisuje w handoffie. Każda inna zmiana tych goldenów oraz każda zmiana `Corpus/acts`,
  `Corpus/banking`, goldenów fragmentów, korpusu syntetycznego, reg1–3 i regulamin-pdp MUSI zatrzymać pracę do
  decyzji właściciela (FR-163); diff i decyzja trafiają do handoffu.
- **FR-756**: Kontrakt wyjścia Markdown MUSI dostać uzupełnienie spec 010 z nową wersją (1.4.0, MINOR: scalenie
  pionowe w modelu i powtarzanie tekstu scalonej komórki w Markdownie, tekst wiersza grupującego w objętych
  nagłówkach); wersja wszystkich
  projektów MUSI zostać podbita do 1.3.0, a manifest korpusu syntetycznego odświeżony (`refresh`) tak, by `verify` był
  czysty.
- **FR-757**: README parsera MUSI opisać zmiany i w „Znanych ograniczeniach” sprawy poza zakresem (T7, T8, T10 oraz
  przypadki, których nie udało się poprawić, z uzasadnieniem); CLAUDE.md i handoff („Stan prac i przekazanie” w
  `plan.md`) MUSZĄ zostać zaktualizowane, a publish/ przebudowany po zmianach.
- **FR-758**: Kontrola liczb ze spec 009 MUSI liczyć tekst scalonej pionowo komórki i wiersza grupującego jeden raz
  (tak jak występuje w PDF), tak by powtórzenia z FR-703 i FR-723 nie dawały liczb nadmiarowych; sprawdzone testem na
  syntetycznym dokumencie z liczbą w scalonej komórce.

### Key Entities

- **Komórka scalona pionowo**: komórka tabeli z liniami obejmująca kilka wierszy (brak linii poziomej w jej kolumnie
  między nimi, przy linii w innej kolumnie); w modelu pierwszy objęty wiersz ma komórkę z liczbą objętych wierszy, a
  każdy kolejny — komórkę-kontynuację z tym samym tekstem (FR-702); w Markdownie tekst w każdym objętym wierszu.
- **Odnośnik przypisu**: mała podniesiona cyfra na początku lub końcu słowa, do której jest definicja na tej samej
  albo dalszej stronie.
- **Obszar przypisów**: tekst pod krótką kreską przy dolnym marginesie; mniejszą czcionką (jak dotąd) albo tym
  samym rozmiarem, jeśli linie zaczynają się etykietami z odnośnikami na stronie.
- **Kolumna danych**: zakres poziomy wyznaczony przez wartości w wierszach danych; określa liczbę kolumn i
  przypisanie linii nagłówka.
- **Wiersz grupujący**: wiersz nagłówka nad kilkoma kolumnami („Gdzie można zrobić?”).
- **Macierz czynności**: tabela, w której pierwsza kolumna opisuje czynność (wiele linii), a pozostałe podają
  dostępność w kanałach (krótkie wartości).
- **Powtórzony nagłówek**: pierwsze linie tabeli na następnej stronie identyczne z nagłówkiem tabeli z poprzedniej
  strony.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-110**: firm-5 s. 13: 10 wierszy tabeli kursów, w każdym niepusta pierwsza kolumna z etykietą swojej grupy
  (dziś 7 wierszy z pustą pierwszą kolumną → 0).
- **SC-111**: firm-5 s. 13 i macierze firm-1, downloads-3, ind-*: 0 odnośników sklejonych z tekstem („3kurs”, „Tak4”,
  „TAK/TAK5”, „Tak13”), każdy odnośnik `[^n]` ma definicję `[^n]:`, a definicje pod kreską nie występują jako
  akapity treści; w regulaminach z listy kontroli liczb nadal 0 liczb nadmiarowych.
- **SC-112**: W tabelach T3 (downloads-2, 3, 4, 5, firm-3, ind-4): liczba kolumn równa liczbie kolumn w PDF (jak
  w kształcie wzorcowym Doclinga), 0 kolumn pustych we wszystkich wierszach danych, 0 wartości pod innym nagłówkiem
  niż w PDF (sprawdzone ręcznie i opisane w handoffie); tabela bez linii, której nie udało się naprawić bez regresji,
  jest wymieniona w „Znanych ograniczeniach” z uzasadnieniem.
- **SC-113**: Macierze T1/T2 (ind-4 s. 42–46, ind-2 s. 33–36, downloads-5 s. 26–27, downloads-3 s. 17, firm-5
  s. 22–23, firm-1 s. 16–17): 0 linii formatu TBL001 i 0 wierszy rozsypanych na akapity; liczba wierszy każdej
  tabeli równa liczbie wierszy w PDF.
- **SC-114**: T4–T6: firm-5 s. 12–13 — tabele terminów kompletne (0 wierszy jako akapity); ind-2 s. 6–7 — tabela GFM
  3-kolumnowa; ind-4 s. 13 i downloads-5 s. 8 — 0 fałszywych nagłówków `##` i 0 sklejonych wierszy z powtórzonego
  nagłówka, komórka „…, czyli:” w całości.
- **SC-115**: Porównanie zbiorów słów prywatnego korpusu przed i po: 0 utraconych słów, 0 słów spoza PDF; każda
  zmiana goldenów mieszcząca się w zgodzie z FR-755 albo zatwierdzona przez właściciela, każda opisana w
  handoffie.
- **SC-116**: Taryfy korpusu syntetycznego, słowniki definicji i tabele łączone przez strony bez zmian; miary
  `CorpusFull` nie pogarszają się; `verify` czysty z wersją 1.3.0; każdy przypadek ma replikę syntetyczną z parą
  commitów czerwony/zielony.
- **SC-117**: Po zakończeniu jest możliwe uruchomienie generatora FAQ na regulaminach z publish/ z nowym parserem bez
  błędów konwersji (wynik konwersji przejrzany pod kątem przypadków T1–T12).

## Assumptions

- Punktem odniesienia są goldeny po merge'u PR #12 i #13 (wersja 1.2.3) i prywatny korpus właściciela (corp-1…6,
  firm-1…6, ind-1…5, reg1–3, regulamin-pdp); downloads-1…5 nie mają goldenów i są weryfikowane przeglądem.
- Wynik Doclinga (`publish/docling-eval/docling/*.json`) służy wyłącznie jako wzorzec kształtu tabel (liczba wierszy i
  kolumn, przypisanie komórek); Docling nie staje się zależnością, a jego tekst nie trafia do testów.
- Scalenie pionowe jest rozpoznawane tylko w tabelach z liniami (linie same pokazują scalenie); w tabelach bez linii
  scalenia nie da się odróżnić od pustej komórki, więc zostają bez zmian.
- Scalenie pionowe nie przekracza granicy strony; przy łączeniu tabeli przez strony każda strona ma własne scalenia.
- Powtórzenie tekstu scalonej komórki w Markdownie zmienia postać wyjścia, a scalenie pionowe dodaje pole do
  publicznego modelu, stąd kontrakt 1.4.0 i wersja projektów 1.3.0 (MINOR); chunker nie wymaga zmian logiki, bo
  każdy objęty wiersz ma w modelu komórkę-kontynuację (research R3).
- Przypisy rozpoznane w komórkach tabel trafiają do fragmentów chunkera tym samym mechanizmem co przypisy w tekście.
- Kontrola liczb ze spec 009 nie liczy odnośników `[^n]` i etykiet definicji, więc nowe przypisy nie dają liczb
  nadmiarowych.
- Kolejność realizacji: US2 i US3 (P1, wspólna podstawa dla macierzy), US1 (P1), US4 (P2), US5 (P3); US6 obowiązuje
  przy każdym commicie parsera. Co nie zdąży, zostaje opisane w handoffie i w „Znanych ograniczeniach”.
- Aplikacja RAG to demo: celem jest poprawny wynik na wymienionych przypadkach, bez wyczerpujących rund jakości na
  innych układach tabel.
