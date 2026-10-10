# Feature Specification: Poprawki parsera po spec 007 (stopki „N/M”, fałszywe nagłówki, wyliczenia „a.”/„i.”, ramki pytanie–odpowiedź)

**Feature Branch**: `008-parser-fixes-after-007`

**Created**: 2026-10-10

**Status**: Draft

**Input**: User description: "Poprawki parsera po spec 007: cztery usterki znalezione na prywatnych regulaminach bankowych (detalicznych i dla firm), które zostały otwarte w handoffie spec 007 (specs/007-corporate-regulation-layout/plan.md, „Stan prac i przekazanie”). Cel: czystszy Markdown i poprawne drzewo sekcji dla chunkera RAG i generatora FAQ, bez utraty ani dopisania tekstu. 1. Numery stron „N/M” w tekście (dawne T038, FR-541 spec 007) […]. 2. Fałszywe nagłówki w regulaminie dla firm D-A […]. 3. Wyliczenia „a.”, „b.” i rzymskie „i.”, „ii.”, „iii.” zostają zwykłym tekstem […]. 4. Ramki z pytaniem i odpowiedzią w regulaminie dla firm firm-2 […]. Wymagania wspólne: syntetyczna replika strony, red/green, pełne sprawdzenie FR-534, goldeny bez zmian bez zgody właściciela, porównanie zbiorów słów, miary przed/po w handoffie, kontrakt Markdown wg SemVer, podbicie <Version> i manifestu korpusu."

## Kontekst

Feature poprawia bibliotekę `LegalAgent.PdfParser` po specyfikacji 007 (numeracja wymagań jest kontynuowana od
FR-600 i SC-090). Markdown biblioteki zasila chunker RAG (spec 004), który dzieli tekst po nagłówkach, i generator
FAQ (spec 006), który pozwala modelowi wskazywać tylko jednostki istniejące jako nagłówki i porównuje cytaty z
tekstem jednostki. Liczą się: kompletny tekst w oryginalnym brzmieniu, brak śmieci stron w treści, nagłówki tylko na
prawdziwych granicach jednostek i listy zamiast luźnych wierszy z etykietami.

Handoff spec 007 zostawił cztery otwarte usterki, wszystkie widoczne w prywatnym korpusie właściciela (oznaczenia
dokumentów jak w spec 007: D-A = corp-1, ind-2, firm-1, firm-2):

1. **Numery stron „N/M”** — stopka z numerem strony w postaci „N/M” zostaje w Markdownie jako osobny akapit, np.
   `2/36`, `7/36` w ind-2 (35 wystąpień) i `2/7` … `7/7` w firm-1 (6 wystąpień).
2. **Fałszywe nagłówki w D-A** — (a) odwołanie „Rozdział I (Prowadzenie…). Jest on załącznikiem…” (w PDF bez kropki po
   „I”; Markdown dziś pokazuje „Rozdział I.” z dopisaną kropką), które przypadkiem zaczyna zawinięty wiersz elementu
   listy „1.”, staje się nagłówkiem jednostki: element „1.” przestaje być elementem listy, a rozdziały 17–18 i
   paragrafy § 66–80 schodzą o poziom niżej; (b) termin słowniczka „15/ umowa rachunku bankowego/ umowa” był w handoffie
   spec 007 zgłoszony jako nagłówek — research (R2) pokazał, że US3 spec 007 już to naprawiło (dziś poprawny element
   słowniczka), więc zostaje tylko test regresji.
3. **Wyliczenia „a.”, „b.” i „i.”, „ii.”, „iii.”** — w regulaminach dla firm (m.in. pod punktami „1/” i „1.”) zostają
   zwykłym tekstem, więc zagnieżdżenie znika, a kolejne pozycje zlewają się w akapit. Najczęściej pozycje wyliczenia,
   które w PDF zaczynają osobne wiersze, są doklejane do tekstu elementu nadrzędnego, np. w D-A
   `- a/ złożenia prawidłowego zlecenia …: i. numer rachunku Klienta …, ii. imię i nazwisko … xii. datę realizacji,`
   (12 pozycji w jednym elemencie) i w corp-5 `- 15\. Pełnomocnictwo musi być udzielone: a. w formie pisemnej …, b. …`;
   w słowniczku corp-3 `- 16\. **klient** a. osoba fizyczna …, b. osoba prawna …`; raz w D-A pozycja „ii.” daje
   wiersz tabeli zastępczej (`ii. \| pierwszym dniem …`).
4. **Ramki pytanie–odpowiedź (firm-2, także firm-4 i firm-5)** — ramka (w parserze: pole schematu kroków, FR-067) z lewą kolumną (pytanie lub komunikat, np. „Potwierdzamy, że”) i prawą
   (treść, często lista); dziś tekst lewej kolumny wplata się w element listy z prawej, np.
   `- 1\. Potwierdzenie otwarcia rachunku przekażemy Ci w formie elektronicznej. **Potwierdzamy, że**`.
   Inne ramki tego samego rodzaju (kroki procesu: „Sprawdzamy Twoją tożsamość”, „Składasz wniosek”, „Potwierdzamy, że
   przyznaliśmy/podwyższyliśmy limit”) wychodzą jako tabele GFM, w których tekst obu kolumn jest pocięty na wiersze
   (`| **Sprawdzamy Twoją** | złożyć posiadacz… |` / `| **tożsamość** | 2. Zanim złożysz… |`). W jednej ramce firm-2
   punkt prawej kolumny, który wychodzi poniżej szarej ramki („3. Możemy poprosić o dokument tożsamości…”), **ginie z
   wyniku** (research R4). Ten sam układ (szare pola z frazą po lewej, strzałki-glify między nimi) mają firm-4 (1
   schemat) i firm-5 (ok. 25 pól); ind-1, ind-2 i firm-3 mają podobne schematy, które już dziś konwertują się dobrze.

**Pomiar wyjściowy** (z handoffu spec 007 i goldenów prywatnych; dokładne wartości dla każdej miary ustala pierwszy
krok implementacji tym samym skryptem miar):

| Usterka | Dokument | Stan dziś |
|---|---|---|
| 1. „N/M” jako tekst | ind-2 / firm-1 / pozostałe | 35 / 6 / 0 |
| 2a. Fałszywy nagłówek jednostki z odwołania | D-A | 1 (rozdziały 17–18 i § 66–80 o poziom za nisko) |
| 2b. Termin słowniczka jako nagłówek | D-A | 0 (naprawione w spec 007; tylko test regresji) |
| 3. Etykiety „a.”, „ii.” poza listą (doklejone do elementu nadrzędnego, w tabeli zastępczej) | corp-1 (D-A) / corp-3 / corp-5 / pozostałe | 20 / 2 / 9 / 0 (10 ciągów, 31 etykiet) |
| 4. Ramki wplecione w listę lub w złej kolejności | firm-2 | 6 z 15 ramek |
| 4. Ramki zapisane jako tabela GFM | firm-2 | 7 ramek w 2 tabelach |
| 4. Utracony tekst prawej kolumny ramki | firm-2 | 1 punkt („3. Możemy poprosić…”) |
| 4. Ramki w innych dokumentach | firm-4 / firm-5 | 3 (tabela GFM) / ok. 25 (mieszane usterki) |

## Clarifications

### Session 2026-10-10

- Q: Czy zgoda na zmianę goldenu ind-2 (tylko usunięcie stopek „N/M”) obejmuje też golden firm-1 (6 stopek
  „2/7”…„7/7”)? → A: Tak — ta sama zgoda obejmuje firm-1, o ile zmiana to wyłącznie usunięcie stopek „N/M”.
- Q: Czy ramki firm-2, które dziś wychodzą jako tabele GFM (kroki procesu: „Sprawdzamy Twoją tożsamość”, „Potwierdzamy,
  że przyznaliśmy…”), też mają stać się „akapit lewej kolumny + treść prawej”? → A: Tak — obie postacie (wplecione w
  listę i tabele GFM) są ramkami; reguła odróżnia je od tabel danych układem strony (FR-632, FR-633).
- Q: Jak zatwierdzać różnice w prywatnych goldenach dokumentów dla firm z US2–US4? → A: Wykonawca przegląda i
  commituje, jeśli żadna miara się nie pogarsza i żadne słowo nie ginie, a różnice opisuje w handoffie; detaliczne
  (poza stopkami „N/M”) oraz `Corpus/acts`, `Corpus/banking` i korpus syntetyczny — tylko za zgodą właściciela
  (FR-644).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Numery stron „N/M” znikają z treści (Priority: P1)

Jako twórca aplikacji RAG chcę, żeby numery stron w stopce („3/49”) nie trafiały do tekstu, bo zaśmiecają fragmenty,
wpadają w środek akapitów i list, a model może je cytować jako treść.

**Why this priority**: najczęstsza usterka (41 wystąpień w dwóch dokumentach), w tym w dokumencie detalicznym z zestawu
prezentacji; poprawka jest prosta i dobrze odgraniczona.

**Independent Test**: syntetyczny dokument kilkustronicowy ze stopką „1/3”, „2/3”, „3/3” na każdej stronie oraz z
liczbami „1/2 kwoty”, „uchwała nr 12/2024” i etykietą listy „1/” w treści daje Markdown bez stopek i z nietkniętymi
liczbami w treści.

**Acceptance Scenarios**:

1. **Given** dokument, w którym każda strona (lub prawie każda) ma w stopce albo nagłówku strony samodzielny element
   „N/M”, N rośnie o 1 strona po stronie, a M jest stałe i równe liczbie stron dokumentu, **When** konwersja, **Then**
   żaden z tych elementów nie występuje w Markdownie, a znaczniki stron `<!-- page: N -->` zostają.
2. **Given** w treści „1/2 kwoty”, „uchwała nr 12/2024”, data „3/4” w zdaniu albo etykieta listy „1/”, **When**
   konwersja, **Then** te teksty zostają bez zmian.
3. **Given** dokument, w którym „N/M” pojawia się tylko na jednej stronie albo numery nie rosną strona po stronie,
   **When** konwersja, **Then** nic nie jest usuwane.
4. **Given** element „N/M” w stopce, w tym samym wierszu co inny tekst stopki (np. nazwa dokumentu), **When**
   konwersja, **Then** usuwany jest tylko wtedy, gdy cały wiersz jest artefaktem strony według dotychczasowych reguł
   powtarzalnych stopek; w przeciwnym razie zostaje.

---

### User Story 2 - Brak fałszywych nagłówków w regulaminach dla firm (Priority: P1)

Jako twórca chunkera i generatora FAQ chcę, żeby nagłówki powstawały tylko na prawdziwych granicach jednostek, bo
fałszywy nagłówek przenosi treść do złej sekcji, psuje klucze jednostek i przesuwa poziomy kolejnych rozdziałów.

**Why this priority**: jeden fałszywy nagłówek w D-A psuje drzewo sekcji dwóch rozdziałów (17–18) i kilkunastu
paragrafów (§ 66–80), a więc klucze jednostek i źródła odpowiedzi FAQ w całej końcówce dokumentu; do tego wyprowadza
z listy element „1.” i pokazuje kropkę, której nie ma w PDF.

**Independent Test**: (a) replika strony, na której tekst elementu listy „1.” (wiersz kończy się „–”) zawija się tak,
że drugi wiersz, w kolumnie tekstu za etykietą, zaczyna się od „Rozdział I (Prowadzenie…). Jest on załącznikiem…”, a
niżej są rozdziały 17. i 18. z paragrafami — brak nagłówka „Rozdział I”, element „1.” jest elementem listy z całym
tekstem, rozdziały i paragrafy na właściwym poziomie; (b) replika słowniczka z terminem kończącym się „/” i zawiniętym
na drugi pogrubiony wiersz — termin jest częścią elementu słowniczka, nie nagłówkiem (test regresji).

**Acceptance Scenarios**:

1. **Given** wiersz zaczynający się oznaczeniem jednostki („Rozdział I”, „Art. 5”, „§ 3”), który jest kontynuacją
   akapitu lub elementu listy — ten sam krój, rozmiar i pogrubienie co poprzedni wiersz, wcięcie takie jak początek
   poprzedniego wiersza albo jak kolumna tekstu za jego etykietą, bez dodatkowego odstępu przed nim, a poprzedni
   wiersz nie kończy zdania — **When** konwersja, **Then** wiersz zostaje częścią akapitu lub elementu, nie powstaje
   nagłówek, a poziomy dalszych nagłówków się nie zmieniają.
2. **Given** prawdziwy nagłówek jednostki (wyróżniony, wyśrodkowany, z odstępem przed nim albo po wierszu kończącym
   zdanie), **When** konwersja, **Then** nadal jest nagłówkiem jak dotąd (akty prawne, regulaminy z goldenów).
3. **Given** wpis słowniczka (rozpoznany jako słowniczek w spec 007), którego termin wygląda jak nagłówek
   (pogrubiony, z etykietą, kończy się „/”, zawinięty na drugi pogrubiony wiersz), **When** konwersja, **Then**
   termin nigdy nie jest nagłówkiem, a wpis ma postać `- 15/ **umowa rachunku bankowego/ umowa** definicja…`.

---

### User Story 3 - Wyliczenia „a.” i „i.” jako elementy listy (Priority: P2)

Jako czytelnik Markdown i chunker chcę, żeby wyliczenia literowe „a.”, „b.” i rzymskie „i.”, „ii.”, „iii.” były
zagnieżdżonymi elementami listy, a nie zwykłym tekstem, bo inaczej pozycje zlewają się w akapit i ginie ich
przynależność do punktu nadrzędnego.

**Why this priority**: wpływa na czytelność i podział wielu punktów w regulaminach dla firm, ale nie gubi tekstu ani
nie psuje drzewa sekcji; ryzyko fałszywych list jest większe niż w US1–US2.

**Independent Test**: replika strony z punktem „1/” i pod nim „a.”, „b.”, „c.”, a pod „b.” „i.”, „ii.”, „iii.”, oraz
z akapitem zawierającym „np.”, „tj.”, inicjał „J. Kowalski” i zdanie zaczynające się od „A. …” daje zagnieżdżoną
listę z etykietami `a\.`, `i\.` (w kolejności i z zagnieżdżeniem według wcięcia), a akapit zostaje akapitem.

**Acceptance Scenarios**:

1. **Given** co najmniej dwa kolejne wiersze zaczynające się etykietami spójnego ciągu „a.”, „b.”, „c.” (albo „i.”,
   „ii.”, „iii.”) na tym samym wcięciu, **When** konwersja, **Then** każda pozycja jest elementem listy z etykietą
   dosłownie jak w PDF (ucieczka kropki jak `1\.`), zawinięte wiersze należą do elementu.
2. **Given** taki ciąg pod punktem „1/” lub „1.”, wcięty dalej w prawo, **When** konwersja, **Then** jest zagnieżdżony
   pod tym punktem; „i.”, „ii.” wcięte pod „b.” są zagnieżdżone pod „b.”.
3. **Given** pojedyncza „a.” bez następnika „b.”, skróty „np.”, „tj.”, „m.in.”, inicjał imienia albo zdanie
   zaczynające się od pojedynczej litery z kropką, **When** konwersja, **Then** nie powstaje lista.
4. **Given** niespójny ciąg (np. „a.”, „c.” albo „i.”, „iii.”), **When** konwersja, **Then** nie powstaje lista z
   tych wierszy.
5. **Given** dwuznaczne „i.” po „h.” w ciągu literowym, **When** konwersja, **Then** traktowane jest jako kolejna
   litera, a nie początek ciągu rzymskiego.

---

### User Story 4 - Ramki pytanie–odpowiedź czytane po kolei (Priority: P2)

Jako czytelnik i generator FAQ chcę, żeby tekst lewej kolumny ramki (pytanie, komunikat) był osobnym akapitem przed
treścią prawej kolumny, bo dziś wplata się w element listy albo — w ramkach zapisanych jako tabela GFM — jest pocięty
na wiersze tabeli razem z treścią, co przekłamuje zdania, które model może cytować.

**Why this priority**: dotyczy trzech dokumentów dla firm (firm-2, firm-4, firm-5), zmienia sens cytowanych zdań, a w
firm-2 gubi tekst; ryzyko pomylenia z tabelą lub słowniczkiem wymaga ostrożności.

**Independent Test**: replika strony z trzema ramkami bez wiersza nagłówków, połączonymi strzałką-znakiem: lewa kolumna
— szare pole z pogrubioną frazą („Potwierdzamy, że”), prawa — lista „1.”, „2.” z zawiniętym tekstem; jedna fraza na
wspólnej linii bazowej z tekstem prawej kolumny, jedna przesunięta, a w jednej ramce punkt prawej kolumny wychodzi
poniżej pola. Markdown ma dla każdej ramki akapit z tekstem lewej kolumny, po nim nienaruszoną listę z prawej, ramki w
kolejności strony, cały tekst prawej kolumny, bez tabeli i bez TBL001.

**Acceptance Scenarios**:

1. **Given** ramka z krótkim tekstem w lewej kolumnie i dłuższą treścią (akapit lub lista) w prawej, **When**
   konwersja, **Then** tekst lewej kolumny jest osobnym akapitem bezpośrednio przed treścią prawej kolumny tej samej
   ramki.
2. **Given** kilka ramek jedna pod drugą, **When** konwersja, **Then** kolejność ramek jest zachowana, a treść
   jednej ramki nie miesza się z treścią sąsiedniej.
3. **Given** prawa kolumna z listą, **When** konwersja, **Then** lista ma te same elementy i zagnieżdżenie, co bez
   lewej kolumny, a żaden element nie zawiera tekstu lewej kolumny.
4. **Given** ramka, **When** konwersja, **Then** nie powstaje tabela GFM ani tabela zastępcza i nie ma ostrzeżenia
   TBL001; słowniczki (spec 007) i prawdziwe tabele danych są wykrywane jak dotąd.
5. **Given** ramka, którą parser dziś zapisuje jako dwukolumnową tabelę GFM (pogrubiony tekst lewej kolumny pocięty na
   wiersze, np. „Sprawdzamy Twoją” / „tożsamość”), **When** konwersja, **Then** lewa kolumna jest jednym akapitem
   („**Sprawdzamy Twoją tożsamość**”), a po nim cała treść prawej kolumny w kolejności czytania (akapit lub lista).

---

### User Story 5 - Bez regresji i z udokumentowanymi miarami (Priority: P1)

Jako właściciel chcę mieć pewność, że poprawki nie psują dokumentów, które dziś konwertują się dobrze, że żaden tekst
nie ginie i że postęp jest zmierzony.

**Why this priority**: zestaw detaliczny jest podstawą prezentacji, a przegląd goldenów kosztuje czas właściciela.

**Independent Test**: pełny zestaw kontroli FR-534 przed każdym commitem parsera, porównanie zbiorów słów prywatnego
korpusu przed i po oraz tabela miar przed/po w handoffie.

**Acceptance Scenarios**:

1. **Given** goldeny `Corpus/acts`, `Corpus/banking` i korpus syntetyczny, **When** pełne testy, `refresh` i
   `verify`, **Then** bez zmian, albo każda różnica jest pokazana właścicielowi i przez niego zatwierdzona.
2. **Given** prywatny korpus, **When** porównanie zbiorów słów Markdownu przed i po zmianach, **Then** żadne słowo
   tekstu źródłowego nie znika (poza usuniętymi stopkami „N/M” i znakami strzałek), a nowe słowa pochodzą wyłącznie
   z odzyskanego tekstu PDF (FR-645).
3. **Given** zakończona implementacja, **When** właściciel czyta handoff, **Then** znajduje tabelę miar przed/po dla
   każdej z czterech usterek oraz listę zmienionych goldenów z uzasadnieniem.

### Edge Cases

- Dokument jednostronicowy ze stopką „1/1” — za mało stron, by potwierdzić ciąg; nic nie jest usuwane.
- Stopka „N/M” brakująca na stronie tytułowej albo na kilku stronach (np. strony z pełnoformatową tabelą) — reszta
  ciągu nadal jest rozpoznawana, o ile numery zgadzają się z numerami stron.
- „N/M” z innym M niż liczba stron (np. numeracja tylko części dokumentu, załącznik z własną numeracją) — rozpoznawane
  tylko, jeśli M jest stałe w obrębie ciągu, a N rośnie strona po stronie.
- Numer strony „N/M” w nagłówku strony zamiast stopki — traktowany tak samo.
- Wiersz z oznaczeniem jednostki na początku nowej strony po akapicie z poprzedniej strony — poza zakresem: reguła
  wiersza kontynuacji (FR-610) patrzy tylko na wiersz powyżej na tej samej stronie, a taki wiersz zachowuje się jak
  dotąd (w korpusie nie ma takiego przypadku; ryzyko błędnego pominięcia prawdziwego nagłówka na górze strony aktu).
- Prawdziwy nagłówek jednostki bez odstępu przed nim, ale po wierszu kończącym zdanie i wyróżniony stylem — nadal
  nagłówek.
- Ciąg „a.”, „b.” przechodzący przez granicę strony — jedna lista, elementy w kolejności.
- Ciąg rzymski dłuższy niż „iii.” („iv.”, „v.”, „ix.”, „x.”, „xii.” — w D-A jest ciąg do „xii.”) — rozpoznawany, o ile
  spójny.
- Wyliczenie wewnątrz definicji słowniczka (corp-3: „16. **klient** a. … b. …”) — pozycje zagnieżdżone pod elementem
  definicji (jak FR-521 spec 007), termin i początek definicji zostają w elemencie nadrzędnym.
- Wielkie litery „A.”, „B.” i „I.”, „II.” — w dokumentach to zwykle oznaczenia rozdziałów; ta specyfikacja obejmuje
  tylko małe litery (patrz Założenia).
- Ramka bez prawej kolumny albo z pustą lewą — przetwarzana jak zwykły tekst; ramka z wieloma wierszami w lewej
  kolumnie — cały tekst lewej kolumny jako jeden akapit.
- Ramka przechodząca przez granicę strony — tekst lewej kolumny tylko raz, przed treścią prawej.
- Treść prawej kolumny dłuższa niż szare pole (punkt wychodzi poniżej pola) — należy do tej samej ramki i nie ginie.
- Strzałka między ramkami zapisana znakiem czcionki symboli (nie obrazem) — łącznik schematu, nie tekst; nie przerywa
  schematu i nie trafia do wyniku jako znak spoza tekstu dokumentu.
- Pojedyncza ramka (schemat z jednym polem) — rozpoznawana jak schemat wielu ramek, jeśli spełnia FR-633.
- Fraza lewej kolumny na tej samej linii bazowej co wiersz prawej kolumny — rozdzielona na lewą i prawą stronę ramki,
  nie sklejona w jeden wiersz.

## Requirements *(mandatory)*

### Functional Requirements

**Numery stron „N/M”**

- **FR-600**: Samodzielny element stopki lub nagłówka strony o postaci „N/M” (same cyfry rozdzielone ukośnikiem, bez
  innego tekstu w tym elemencie) MUSI być rozpoznany jako artefakt strony i usunięty z wyniku, jeśli w dokumencie
  tworzy ciąg: występuje na co najmniej dwóch stronach, N rośnie strona po stronie zgodnie z kolejnością stron, a M
  jest stałe.
- **FR-601**: Wystąpienia „N/M” w treści (ułamki, numery uchwał i umów, daty, etykiety list „1/”) oraz elementy „N/M”
  niespełniające warunków FR-600 NIE MOGĄ być usuwane ani zmieniane.
- **FR-602**: Usunięcie numeru strony NIE MOŻE usuwać znaczników stron ani innych elementów tego samego wiersza
  stopki, które dotychczasowe reguły zostawiały w wyniku.

**Fałszywe nagłówki**

- **FR-610**: Wiersz zaczynający się oznaczeniem jednostki redakcyjnej NIE MOŻE stać się nagłówkiem, jeśli jest
  kontynuacją akapitu lub elementu listy: ma ten sam rozmiar i pogrubienie co poprzedni wiersz tekstu ciągłego (także
  pierwsze słowo oznaczenia ma to samo pogrubienie co ostatnie słowo poprzedniego wiersza), zaczyna się tam, gdzie
  poprzedni wiersz, albo w kolumnie tekstu za jego etykietą, nie jest wyśrodkowany, nie ma przed nim odstępu
  większego niż zwykła interlinia, a poprzedni wiersz nie kończy zdania (nie kończy się kropką, dwukropkiem,
  średnikiem, wykrzyknikiem ani pytajnikiem); poprzedni wiersz to wiersz powyżej na tej samej stronie. Taki wiersz
  należy do akapitu lub elementu listy, który kontynuuje.
- **FR-611**: Wiersz rozpoznany jako termin wpisu słowniczka NIGDY nie jest nagłówkiem, niezależnie od kroju,
  etykiety, znaku na końcu i zawinięcia terminu na kolejny wiersz; wpis jest renderowany zgodnie z kontraktem 1.2.0
  (`- 15/ **termin** definicja`). Zachowanie już obowiązuje (spec 007) — wymagany jest test regresji.
- **FR-612**: Prawdziwe nagłówki jednostek i nagłówki typograficzne rozpoznawane dziś w goldenach `Corpus/acts`,
  `Corpus/banking` i w korpusie syntetycznym MUSZĄ zostać nagłówkami.

**Wyliczenia „a.” i „i.”**

- **FR-620**: Wiersz zaczynający się małą literą łacińską z kropką („a.”, „b.”) albo małą liczbą rzymską z kropką
  („i.”, „ii.”, „iii.”, „iv.”) MUSI rozpoczynać element listy, jeśli należy do spójnego ciągu co najmniej dwóch
  kolejnych etykiet na tym samym wcięciu (a., b., c. … albo i., ii., iii. …). Dotyczy to także pozycji, które dziś są
  doklejane do tekstu elementu nadrzędnego (punktu, litery, definicji słowniczka) albo trafiają do tabeli zastępczej.
  Rozstrzyga położenie w PDF: etykieta musi zaczynać wiersz; „a.”, „i.” w środku wiersza tekstu ciągłego (wyliczenie
  w jednym zdaniu) zostają tekstem.
- **FR-621**: Poziom zagnieżdżenia takich elementów MUSI wynikać z wcięcia etykiety względem elementów nadrzędnych
  („1/”, „1.”, „a.”), tak jak dla etykiet z spec 007.
- **FR-622**: Etykieta MUSI być zapisana dosłownie, z ucieczką wymaganą przez kontrakt Markdown (`- a\.`, `- ii\.`);
  NIE WOLNO dopisywać ani zmieniać znaków.
- **FR-623**: NIE MOŻE powstać lista z pojedynczej etykiety bez następnika, z niespójnego ciągu, ze skrótów („np.”,
  „tj.”, „m.in.”, „ust.”), z inicjałów imion ani ze zdań zaczynających się od pojedynczej litery z kropką w tekście
  ciągłym.
- **FR-624**: Etykieta „i.” po „h.” w ciągu literowym MUSI być kolejną literą; „i.” rozpoczynające nowy ciąg pod
  innym elementem — liczbą rzymską, jeśli następuje po niej „ii.”.

**Ramki pytanie–odpowiedź**

- **FR-630**: W obszarze ramki z dwiema kolumnami, z których lewa zawiera krótki tekst (pytanie, komunikat, nazwa
  kroku), a prawa treść (akapit lub listę), tekst lewej kolumny MUSI być osobnym akapitem (wierszami złączonymi w
  jeden akapit) umieszczonym bezpośrednio przed treścią prawej kolumny tej samej ramki.
- **FR-631**: Kolejność ramek na stronie i między stronami MUSI być zachowana; treść prawej kolumny (w tym lista z jej
  elementami i zagnieżdżeniem) NIE MOŻE zawierać tekstu lewej kolumny.
- **FR-632**: Ramka NIE MOŻE być zapisana jako tabela GFM ani tabela zastępcza i NIE MOŻE wywołać ostrzeżenia TBL001 —
  dotyczy to także ramek, które parser dziś zapisuje jako dwukolumnowe tabele GFM; słowniczki (spec 007),
  tabele-dokumenty (spec 002) i prawdziwe tabele danych MUSZĄ być wykrywane jak dotąd.
- **FR-633**: Ramkę od dwukolumnowej tabeli danych odróżnia wyłącznie układ strony: lewa kolumna zawiera krótki,
  wyróżniony (np. pogrubiony) tekst, który jest jedną frazą rozłożoną na jeden lub kilka wierszy, w wyróżnionym
  (niebiałym) polu, prawa — tekst ciągły lub listę, a obszar nie ma siatki linii dzielących komórki tabeli danych.
  Wiersz nazw kolumn nad schematem jest dopuszczalny tylko w całości pogrubiony (jak w schematach kroków FR-067) i
  nie zmienia rozpoznania; niepogrubiony wiersz nad ramką jest treścią, nie nagłówkiem.
- **FR-634**: Cały tekst prawej kolumny ramki MUSI trafić do wyniku, także wiersze wychodzące poniżej wyróżnionego
  pola lewej kolumny (należą do tej ramki aż do następnej ramki, obcego tekstu po lewej albo większego odstępu); żaden
  wiersz treści NIE MOŻE zostać uznany za wiersz nagłówków kolumn ani artefakt.
- **FR-635**: Strzałki i inne łączniki między ramkami zapisane znakami czcionek symboli (bez liter i cyfr) są
  elementem graficznym schematu: nie przerywają ciągu ramek i nie trafiają do wyniku.

**Wymagania wspólne**

- **FR-640**: Wynik MUSI zawierać wyłącznie tekst z PDF; poprawki wyrażają strukturę tylko formatowaniem Markdown i
  NIE WOLNO im dopisywać słów (zasada wierności źródłu); usuwane są wyłącznie numery stron z FR-600 i znaki
  łączników schematu z FR-635 (elementy graficzne, nie tekst); nowy tekst w wyniku może pochodzić tylko z PDF
  (odzyskany tekst, FR-634).
- **FR-641**: Reguły MUSZĄ opierać się wyłącznie na geometrii i typografii strony oraz wzorcach tekstu, nie na nazwie
  dokumentu, wydawcy ani adresie.
- **FR-642**: Każda poprawka MUSI być odtworzona syntetyczną repliką strony (bez nazw prawdziwych banków) najpierw jako
  test, który nie przechodzi na kodzie sprzed poprawki (osobny commit), a potem poprawką, z którą przechodzi (osobny
  commit).
- **FR-643**: Przed każdym commitem zmieniającym parser MUSI przejść pełny zestaw kontroli z FR-534 spec 007: testy
  solucji, prywatny korpus właściciela, `refresh` i `verify` korpusu syntetycznego oraz kategoria `CorpusFull`.
- **FR-644**: Goldeny `Corpus/acts`, `Corpus/banking` i korpus syntetyczny NIE MOGĄ się zmienić bez zgody właściciela
  (FR-163). W prywatnym korpusie właściciel z góry akceptuje zmianę goldenu ind-2 polegającą wyłącznie na usunięciu
  stopek „N/M”; ta sama zgoda obejmuje zmianę goldenu firm-1 polegającą wyłącznie na usunięciu jego stopek
  „2/7”…„7/7”. Goldeny prywatnych dokumentów dla firm (corp-1…5, firm-1…5) wykonawca MOŻE zaktualizować bez
  osobnej zgody, jeśli przejrzał różnice, żadna miara z tabeli miar się nie pogarsza, a porównanie zbiorów słów
  (FR-645) jest czyste; różnice opisuje w handoffie. Każda inna zmiana goldenu dokumentu detalicznego (ind-1…5) i
  dotychczasowych dokumentów prywatnych wymaga osobnej zgody właściciela przed commitem.
- **FR-645**: Porównanie zbiorów słów Markdownu prywatnego korpusu przed i po zmianach (słowa bez znaków
  interpunkcyjnych i formatowania Markdown) MUSI wykazać brak utraconych słów (poza numerami stron z FR-600 i
  znakami łączników z FR-635) i brak dopisanych słów spoza PDF; nowe słowa są dopuszczalne tylko jako odzyskany tekst
  PDF (np. utracony dziś punkt ramki firm-2), każde wymienione w handoffie.
- **FR-646**: Jeśli zmienia się postać wyjścia opisana w kontrakcie Markdown (np. nowe rodzaje etykiet list), kontrakt
  MUSI zostać podbity zgodnie z SemVer i opisany w addendum tej specyfikacji.
- **FR-647**: Po zmianie parsera wersja wszystkich projektów MUSI zostać podbita, a manifest korpusu syntetycznego
  odświeżony tak, by zapisywał nową wersję (`verify` czysty).

### Key Entities

- **Ciąg numerów stron**: wystąpienia „N/M” w stopkach lub nagłówkach stron jednego dokumentu z rosnącym N i stałym M;
  rozpoznany ciąg jest artefaktem strony.
- **Wiersz kontynuacji**: wiersz tekstu ciągłego z tym samym stylem co poprzedni, zaczynający się jak poprzedni albo
  w kolumnie tekstu za jego etykietą, bez odstępu, po wierszu niekończącym zdania; nie może rozpocząć nagłówka.
- **Ciąg wyliczenia**: co najmniej dwie kolejne spójne etykiety „a.”, „b.” … albo „i.”, „ii.” … na tym samym wcięciu;
  każda rozpoczyna element listy z poziomem z wcięcia.
- **Ramka pytanie–odpowiedź**: obszar z lewą kolumną (krótki, wyróżniony tekst — pytanie, komunikat, nazwa kroku) i
  prawą (treść, także wychodząca poniżej pola); bez nagłówków kolumn i siatki tabeli danych; ramki łączą strzałki;
  renderowana jako akapit lewej kolumny, a po nim treść prawej.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-090**: Liczba samodzielnych akapitów „N/M” w Markdownie spada do 0 w ind-2 (z 35) i w firm-1 (z 6); liczba
  wystąpień „N/M” w treści pozostałych dokumentów prywatnych, goldenów i korpusu syntetycznego nie zmienia się.
- **SC-091**: W D-A nie ma nagłówka „Rozdział I” z odwołania (ani dopisanej po „I” kropki); element „1.” z tym
  odwołaniem jest elementem listy; rozdziały 17–18 mają ten sam poziom co pozostałe rozdziały dokumentu, a § 66–80 —
  co pozostałe paragrafy; termin „15/ umowa rachunku bankowego/ umowa” nie jest nagłówkiem (test regresji).
- **SC-092**: W dokumentach dla firm liczba etykiet „a.”, „b.” … lub „i.”, „ii.” …, które w PDF zaczynają wiersz i
  należą do spójnego ciągu, a w Markdownie nie zaczynają elementu listy (osobny akapit, tekst doklejony do elementu
  nadrzędnego, komórka tabeli zastępczej), spada do 0; nie powstaje żadna lista ze skrótów,
  inicjałów ani pojedynczej „a.” (sprawdzone przeglądem wszystkich nowych list w prywatnym korpusie).
- **SC-093**: W firm-2 (15 ramek), firm-4 i firm-5 liczba elementów listy zawierających tekst lewej kolumny ramki
  spada do 0, liczba ramek zapisanych jako tabela GFM spada do 0 (firm-2: z 7), każda ramka ma akapit z tekstem lewej
  kolumny przed swoją treścią, utracony punkt „3. Możemy poprosić…” jest w wyniku, a liczba TBL001 nie rośnie;
  schematy ind-1, ind-2 i firm-3 bez zmian.
- **SC-094**: Goldeny `Corpus/acts`, `Corpus/banking` i korpus syntetyczny bez zmian (albo z różnicami zatwierdzonymi
  przez właściciela); miary `CorpusFull` nie pogarszają się.
- **SC-095**: Porównanie zbiorów słów prywatnego korpusu przed i po: 0 utraconych słów poza numerami stron „N/M”,
  0 dopisanych słów.
- **SC-096**: Handoff zawiera tabelę miar przed/po dla czterech usterek (liczba „N/M” w tekście, fałszywe nagłówki,
  etykiety „a.”/„ii.” poza listą, ramki wplecione lub jako tabela, utracony tekst) dla każdego dokumentu prywatnego, w którym miara jest
  niezerowa przed lub po.
- **SC-097**: Przebieg generatora FAQ i chunkera na poprawionym Markdownie nie wymaga zmian w tych komponentach
  (format wyjścia pozostaje zgodny z kontraktem, w nowej wersji MINOR), z jednym dopuszczonym wyjątkiem: jeśli test
  cytatu obejmującego elementy `- a\.` / `- ii\.` wykaże, że porównanie cytatów FAQ nie pomija nowych etykiet,
  porównanie zostaje uzupełnione (jak dla etykiet „1/”, „a/” w spec 007).

## Assumptions

- Oznaczenia dokumentów (D-A = corp-1, ind-2, firm-1, firm-2) i prywatny korpus z goldenami są takie, jak po
  zakończeniu spec 007 (19 dokumentów); punktem odniesienia są goldeny po merge'u PR #10 i wersja 1.1.0.
- Wyliczenia obejmują tylko małe litery i małe liczby rzymskie z kropką; wielkie „A.”, „I.” to w tych dokumentach
  zwykle oznaczenia rozdziałów i działów i pozostają obsługiwane jak dotąd. Etykiety „a)”, „a/” obsługuje już parser.
- Nowe rodzaje etykiet list („a.”, „i.”) prawdopodobnie zmieniają postać wyjścia w sposób zgodny wstecz — oczekiwane
  podbicie kontraktu Markdown do 1.3.0 (MINOR) i wersji projektów do 1.2.0 (MINOR); ostateczną decyzję zapisuje plan.
- Poprawki US2–US4 zmienią goldeny prywatne D-A, corp-3, corp-5, firm-2, firm-4 i firm-5; te
  różnice są oczekiwane i rozstrzygane według FR-644 (dla firm: przegląd wykonawcy, miary i zbiory słów; detaliczne:
  zgoda właściciela).
- Kolejność realizacji: US1 i US2 (P1), potem US3 i US4 (P2); US5 obowiązuje przy każdym commicie parsera. Co nie
  zdąży, zostaje opisane w handoffie jako następny krok.
- Miary liczy ten sam skrypt co w spec 007 (`LayoutMetrics`, raport w `PrivateCorpusTests`), rozszerzony o miary
  usterek 2–4.
