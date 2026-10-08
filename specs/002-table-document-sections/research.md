# Research: tabela-dokument (spec 002)

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Date**: 2026-10-08

Technologia, zależności i architektura potoku są bez zmian względem `specs/001-legal-pdf-parser/research.md`
(R1–R16). Ten dokument zapisuje decyzje specyficzne dla tabeli-dokumentu oraz pomiary geometrii
wzorcowego `Corpus/private/mbank-reg3.pdf`, wykonane jednorazowym programem na PdfPig 0.1.16
(poza repozytorium) — liczby niżej są punktem odniesienia dla syntetycznego korpusu i progów.

## Pomiary `mbank-reg3.pdf` (A4, 596 × 842 pt, 13 stron)

| Strony | Obserwacja |
|--------|------------|
| 1 | Okładka bez siatki. Tytuł pogrubiony (4 linie, y 233–380), „Obowiązuje od 01.09.2026 r. do 30.11.2026 r.” zwykłą czcionką (y 426), obraz (logo) x 315–542, y 499–725, podpis „mBank.pl” pogrubiony x 409, linia bazowa y 752 — odstęp od dołu obrazu ≈ 17 pt ≈ 1,7 wysokości linii. |
| 2–12 | Siatka: pionowe linie zawsze x = 54 / 181 / 541 (lewa kolumna 127 pt = 26% szerokości tabeli 487 pt). Poziome linie siatki zapisane jako cienkie wypełnione prostokąty, **każda w dwóch kawałkach** (x 55–181 i 181–541). Górna krawędź ramki y = 72, dolna różna na każdej stronie (329–761). |
| 2, 3, 6, 7, 9–12 | Wiersz nazw kolumn „Definicje” (x 65) / „Wyjaśnienie” (x 186), oba pogrubione, y 89, między poziomymi liniami y 72 i 100. |
| 4, 5, 8 | **Brak** wiersza nazw kolumn — wiersz danych zaczyna się od górnej krawędzi ramki (y 72). Strona 4 i 8 zaczynają nową sekcję („Korzyści promocji”, „Warunki/zasady promocji”), strona 5 to kontynuacja (pusta lewa komórka). |
| 9, 11, 12 | Dodatkowe krótkie poziome linie **wewnątrz prawej kolumny** (x 204–474, 222–460, 217–514 itd.) — podkreślenia linków. Na str. 12 również wypełnione prostokąty pod linkami (podświetlenie, x 217–514, wys. 13 pt). |
| 4, 10 | Linia lewej komórki i pierwsza linia prawej na tej samej linii bazowej („Korzyści” + „Jeśli spełnisz…”, „złożyć wniosek w” + „• na stronie…”) — `LineAssemblyStage` łączy je w jedną linię. |
| 7, 11 | Linie lewej komórki o linii bazowej przesuniętej o 1 pt względem prawej (y 147/148, 163/164). |
| 4–12 | Prawa kolumna: tekst od x ≈ 186–208, prawa krawędź tekstu ≤ 529; **wyrównanie do lewej z nierównym prawym brzegiem** (linie kończą się na x 456–529), a nie justowanie — szerokie odstępy na str. 4 to nierówne odstępy edytora. Punktory „•” (SymbolMT) x 186–190, tekst pozycji x 204–208; podpunkty „o” w **CourierNewPSMT** x 226, tekst x 244. Słowo „o” w zdaniu — Verdana. |
| 13 | Brak siatki. „MOJE OŚWIADCZENIA” pogrubione (y 83), lista „1)”, „2)”, linia kropek, „data, miejsce i podpis Uczestnika promocji”, stopka „12/12”. Tekst **pod** tabelą (tabela kończy się na str. 12). |

Tabela obejmuje 11 z 13 stron z tekstem (85%); mediana długości prawej komórki wierszy z nazwą sekcji
> 60 słów. Tabela definicji w `mbank-regulamin-pdp.pdf` (ten sam wiersz „Definicje | Wyjaśnienie”)
obejmuje 3 strony długiego dokumentu i ma krótkie objaśnienia — nie spełnia (c) ani (e) FR-080.

---

## R1. Miejsce w potoku: nowy etap `TableDocumentStage` (Order 560)

- **Decision**: Osobny etap między `StepSequenceStage` (550) a `TableDetectionStage` (600), zbudowany
  wzorem schematów kroków (FR-067): rozpoznaje region, nadaje role liniom, dzieli linie na granicy
  kolumn (`LineSlicer`), przestawia linie strony w kolejność czytania „nazwa sekcji → treść” i
  oznacza je adnotacją `tabledoc.index`. Kolejne etapy (tabele, kolejność czytania) pomijają linie z tą
  adnotacją; listy, nagłówki i akapity przetwarzają treść prawej kolumny jak zwykły tekst.
- **Rationale**: FR-089 (schematy kroków mają pierwszeństwo) wynika z kolejności; treść komórki musi
  przejść normalne wykrywanie list i akapitów (FR-085), czego nie da tabela GFM; `TableDetectionStage`
  (737 linii, dostrojony na korpusie) pozostaje nietknięty, co chroni SC-016.
- **Alternatives considered**: (a) rozszerzenie `TableDetectionStage` — ryzyko regresji taryf i większa
  złożoność jednego etapu; (b) przekształcenie gotowego `TableBlock` w sekcje w `DocumentBuildStage` —
  za późno: komórki są już pocięte na fałszywe kolumny, a treść komórki nie przechodzi list i akapitów.

## R2. Geometria siatki i ramka strony

- **Decision**: Dla każdej strony: pionowe linie siatki grupowane po X (tolerancja 3 pt, kawałki
  łączone w pionie); ramka = dokładnie 3 pionowe linie (lewa krawędź, granica kolumn, prawa krawędź)
  o wspólnym zakresie pionowym (±3 pt). Granice wierszy = poziome linie siatki, których połączone
  kawałki pokrywają ≥ 90% szerokości **obu** kolumn (kawałki z tolerancją 3 pt na stykach); górna i
  dolna krawędź ramki są granicami wierszy. Linie krótsze (podkreślenia linków) i wypełnione prostokąty
  wewnątrz komórek są ignorowane.
- **Rationale**: pomiary str. 2–12 (dwa kawałki na linię, podkreślenia linków na str. 9, 11, 12 —
  dziś to one rozcinają wiersze i dają tryb awaryjny na str. 11).
- **Alternatives considered**: granice wierszy z odstępów pionowych tekstu — zawodne przy długich
  komórkach; dowolna pozioma linia jako granica — błąd obecnej implementacji.

## R3. Region i kryteria FR-080

- **Decision**: Kolejne strony z ramką o zgodnych X trzech pionowych linii (±3 pt) tworzą region.
  Region jest tabelą-dokumentem, gdy: (a) ramka ma 2 kolumny na każdej stronie; (b) szerokość lewej
  kolumny ≤ `TableDocumentMaxLeftColumnRatio` (0,35) szerokości ramki; (c) liczba stron regionu
  ≥ `TableDocumentMinPages` (2) i ≥ `TableDocumentMinPageRatio` (0,5) stron z tekstem; (d) któraś prawa
  komórka przechodzi przez stronę (ostatni wiersz strony i pierwszy wiersz następnej z pustą lewą
  komórką) albo ma linię zaczynającą się od oznaczenia listy (`ListLabelPatterns`, także „o” wg R10)
  albo odstęp między liniami > 1,5 interlinii (≥ 2 akapity); (e) mediana liczby słów prawych komórek
  wierszy z niepustą lewą komórką (bez wiersza nazw kolumn) ≥ `TableDocumentMinMedianWords` (40).
  Linie należące do schematu kroków (`step.scheme`) wykluczają stronę z regionu (FR-089).
- **Rationale**: wartości z clarify (Q1) i pomiarów; (d) i (e) odróżniają od tabeli definicji
  (krótkie objaśnienia) i taryf (wiele kolumn odpada już na (a)).
- **Alternatives considered**: rozpoznanie po tekście „Definicje | Wyjaśnienie” — zabronione (FR-007,
  reguła wydawcy) i niejednoznaczne (ten sam wiersz w tabeli definicji).

## R4. Wiersz nazw kolumn (FR-082)

- **Decision**: Pierwszy wiersz regionu jest wierszem nazw kolumn, gdy obie komórki mają po jednej
  linii o ≤ 5 słowach i wszystkie słowa są pogrubione albo mają odcisk (`LineFingerprint`) równy
  pierwszemu wierszowi kolejnej strony. Na kolejnych stronach pierwszy wiersz o tym samym odcisku
  tekstu (obie komórki) jest powtórzeniem. Linie → `Role = Artifact` + adnotacja `tabledoc.index`;
  raport liczy wystąpienia i zapisuje tekst („Definicje | Wyjaśnienie”).
- **Rationale**: str. 4, 5, 8 nie mają wiersza nazw — powtórzenie nie jest wymagane; reguła tekstowa
  nie zależy od wydawcy.
- **Alternatives considered**: usuwanie przez `ArtifactRemovalStage` — wiersz leży poza strefą marginesu
  (y 89 > 8% wysokości), a ruszanie stref zmieniłoby inne dokumenty.

## R5. Rozcinanie linii na granicy kolumn

- **Decision**: Każda linia w ramce dzielona jest po środku słów względem X granicy kolumn
  (`word.Box.CenterX < divider` → lewa) przez `LineSlicer.Slice`; słowa przypisywane do wiersza wg
  środka pionowego linii między granicami wierszy.
- **Rationale**: str. 4 i 10 — lewa i prawa komórka na jednej linii bazowej są dziś jedną linią
  („**Uczestnik** W promocji mogą uczestniczyć: **promocji**”).

## R6. Nazwa sekcji → nagłówek (FR-083)

- **Decision**: Lewe linie wiersza z niepustą lewą komórką: pierwsza dostaje `Role = Heading` i
  `HeadingInfo(Level: 2, Kind: SectionKind.TableDocumentSection, Designation: null, Number: null,
  Title: nazwa, Text: nazwa)`, kolejne `Role = Heading` bez informacji (jak linie dołączone w FR-044).
  Nazwa = teksty linii lewej komórki złączone spacją, bez znaczników pogrubienia. Nazwa przerwana
  granicą strony: gdy ostatnia linia lewej komórki ostatniego wiersza strony N leży w odległości ≤ 1,5
  wysokości linii od dolnej krawędzi ramki, a pierwszy wiersz strony N+1 (po wierszu nazw kolumn) ma
  niepustą lewą komórkę, jej linie dołączają do tej samej nazwy (jeden nagłówek przed treścią z obu
  stron). `HeadingDetectionStage` nie zmienia tych linii (przetwarza tylko `Unknown`); poziom 2 nie
  jest przeliczany przez klasy rozmiarów.
- **Rationale**: spec Q-level (`##` pod tytułem `#`); niezmiennik „poziom rośnie o ≤ 1” zachowany.
- **Alternatives considered**: oddanie lewych linii `HeadingDetectionStage` z flagą „wymuś” — mieszanie
  rangi z klasami rozmiarów (FR-042) bez korzyści.

## R7. Kolejność linii i kolumna treści

- **Decision**: `page.Lines` przestawiane jak w FR-067: linie spoza ramki zostają na miejscu; w ramce
  dla każdego wiersza z góry na dół: wiersz nazw kolumn (artefakt), linie lewej komórki, linie prawej
  komórki. Linie prawej kolumny dostają `column.left` / `column.right` = najmniejszy lewy i największy
  prawy brzeg słów prawej kolumny w całym regionie (str. 2–12: ≈ 186 / 529). `ReadingOrderStage` i
  `TableDetectionStage` pomijają linie z `tabledoc.index` (wzór: `step.scheme`).
- **Rationale**: `BlockAssemblyStage` i `HeadingDetectionStage` już używają `column.*` do szerokości
  kolumny (FR-032, wyśrodkowanie).

## R8. Nagłówki w dokumencie z tabelą-dokumentem (FR-086, FR-087, FR-088, FR-093)

- **Decision**: W `HeadingDetectionStage`:
  - FR-087/086: linie od początku pierwszej tabeli-dokumentu (`PipelineContext.TableDocuments[0]`:
    strona + górna krawędź ramki) do końca dokumentu nie są kandydatami typograficznymi ani blokiem
    tytułowym aktu; wzorce jednostek redakcyjnych (FR-043) działają jak dotąd.
  - FR-088: nowa lista `LayoutPage.ImageAreas` (prostokąty obrazów z `Page.GetImages()`; Y w dół).
    Linia (lub dwie kolejne) jest podpisem, gdy jej prostokąt zachodzi na obraz albo jej górna krawędź
    leży ≤ 3 wysokości linii pod dolną krawędzią obrazu, a zakres poziomy mieści się w obrazie
    poszerzonym o 10% z każdej strony; podpis nie jest kandydatem ani częścią bloku tytułowego.
    Przełącznik `Headings.DetectImageCaptions` (true).
  - FR-093: linia pierwszej strony za tytułem dokumentu, przed pierwszym nagłówkiem, pasująca do
    `^obowiązuje\s+od\b` (bez rozróżniania wielkości liter, kultura niezmienna) nie jest kandydatem ani
    linią bloku tytułowego. Przełącznik `Headings.ValidityLineAsParagraph` (true).
- **Rationale**: pomiar str. 1 (odstęp podpisu 1,7 wysokości linii → próg 3); reguły ogólne, bez nazw
  wydawcy (FR-007).
- **Alternatives considered**: dołączanie „Obowiązuje od” do tytułu — odrzucone w clarify (Q5:
  akapit, nie część nagłówka).

## R9. Akapity w prawej kolumnie (FR-085, FR-086)

- **Decision**: W `BlockAssemblyStage.ContinuesParagraph` dla linii z `tabledoc.index`: (1) linia nie
  kontynuuje akapitu, gdy pierwsze słowo nowej linii plus odstęp międzywyrazowy (mediana odstępów
  poprzedniej linii, a gdy brak — 0,25 em) mieści się między prawym brzegiem poprzedniej linii a
  `column.right`; (2) zmiana „cała linia pogrubiona” ↔ „nie cała” kończy akapit. Reguła FR-032
  (kropka + linia < 75%) działa nadal. Wieloliniowy pogrubiony śródtytuł pozostaje jednym akapitem
  (inline `**…**` wg FR-046).
- **Rationale**: pomiar — prawy brzeg nierówny (x 438–529), więc próg 75% z pierwotnego FR-085
  dzieliłby akapity w środku zdania (linia „(czyli po przekroczeniu 200” kończy się na 72%); test
  „zmieściłoby się” jest niezależny od wyrównania. Spec FR-085 zaktualizowany w trakcie planowania.
- **Alternatives considered**: próg długości linii — j.w.; zawsze nowy akapit na linię — rozbija
  treść.

## R10. Punktor „o” (FR-085 → FR-050)

- **Decision**: `LayoutGlyph` dostaje opcjonalne `FontName` (nazwa czcionki z PDF; `null`, gdy brak).
  `ListDetectionStage`: pierwsze słowo „o” jest punktorem (`ListLabelKind.Bullet`, oznaczenie „o”),
  gdy rodzina jego czcionki różni się od rodziny czcionki następnego słowa (rodzina = nazwa bez
  prefiksu podzbioru `ABCDEF+` i bez przyrostka stylu po „-” lub „,”), a za nim stoi tekst. Słowo „o”
  w czcionce tekstu pozostaje słowem.
- **Rationale**: pomiar — CourierNewPSMT dla podpunktów, Verdana dla słowa „o”; reguła ogólna dla
  dokumentów z edytorów tekstu (drugi poziom list).
- **Alternatives considered**: „o” po wcięciu — myli się z przyimkiem na początku linii kontynuacji.

## R11. Łącznik w adresie (FR-094)

- **Decision**: `Hyphenation.Decide`: gdy ostatni wyraz linii (od ostatniej spacji) zawiera „://”,
  „www.” lub „/”, wynik to `Compound` (łącznik zostaje, bez spacji).
- **Rationale**: po scaleniu komórek adresy z str. 9 i 12 przechodziłyby przez FR-012 i traciłyby
  łącznik („pierscien-platniczymastercard”), zmieniając słowo (SC-010).

## R12. Model publiczny, opcje i raport

- **Decision**: zmiany addytywne (kontrakt 1.0.0 → **1.1.0**, MINOR): `SectionKind.TableDocumentSection`;
  `ConversionReport.TableDocuments: IReadOnlyList<TableDocumentSummary>`
  (`FirstPage`, `LastPage`, `SectionCount`, `HeaderRowText?`, `DroppedHeaderRows`); opcje w
  `TableOptions` i `HeadingOptions` (data-model §2). Brak nowego ostrzeżenia — tabela-dokument nie
  jest stanem błędu.
- **Rationale**: FR-090; rozszerzenie enum i nowe pole rekordu nie łamią wywołujących (nowa wartość
  enum może zaskoczyć `switch` bez `default` — odnotowane w kontrakcie).

## R13. Testy i korpus

- **Decision**:
  - `SyntheticPdfBuilder`: czcionka **Noto Sans Mono** (OFL, dodana do `Fixtures/Fonts`) przez nowy
    parametr kroju; istniejące `HLine`/`VLine` (linie siatki w kawałkach), `Image`, `bold`.
  - `BankingCorpusGenerator`: nowy dokument `regulamin-promocji-tabela` (okładka z tytułem, linią
    „Obowiązuje od …”, obrazem i podpisem; tabela 2-kolumnowa z siatką na ≥ 4 stronach, wiersz nazw
    kolumn powtarzany na części stron, wiersz przechodzący przez stronę, nazwa sekcji zawinięta na 3
    linie, nazwa przerwana granicą strony, punktory „•” i „o” (mono), pogrubiony śródtytuł
    dwuliniowy, definicje „termin – objaśnienie”, adres z łącznikiem, podkreślenie linku; strona po
    tabeli z „MOJE OŚWIADCZENIA”) + `Truth` (nazwy sekcji w kolejności, słowa wiersza nazw kolumn) +
    `Corpus/banking/regulamin-promocji-tabela.expected.md`. Drugi dokument negatywny: 6-stronicowy
    regulamin z 2-stronicową tabelą definicji z siatką → nadal GFM.
  - Testy jednostkowe etapu na `LayoutFactory`/`StageHarness` (ramka, granice wierszy, kryteria a–e,
    wiersz nazw, rozcinanie, nazwa przez stronę, kolejność), testy `HeadingDetection` (FR-087, 088,
    093), `BlockAssembly` (R9), `ListDetection` (R10), `Hyphenation` (R11), opcje i walidator.
  - Metryki SC-010 – SC-015 na syntetycznym dokumencie (`QualityMetricsTests`), SC-016 przez golden
    `Corpus/acts` i `Corpus/banking` (pliki bez zmian), SC-017 przez CI.
  - `PrivateCorpusTests` (opcjonalny, `LEGALAGENT_PRIVATE_CORPUS`): `mbank-reg3` — 0 tabel, 0 linii
    z „ | ”, nagłówki = tytuł + nazwy sekcji; `mbank-regulamin-pdp`, `mbank-reg1`, `mbank-reg2` —
    wynik równy zapisanym `Corpus/private/*.md` sprzed zmiany z wyjątkiem linii „obowiązuje od”
    (FR-093). Zapisane wyniki sprzed zmiany są punktem odniesienia — przed pierwszym commitem
    implementacji zostają skopiowane do `Corpus/private/baseline/` (poza git).
- **Rationale**: konstytucja I (TDD, offline, deterministycznie), zakaz commitowania dokumentów banku.

## R14. Ryzyka

| Ryzyko | Ograniczenie |
|--------|--------------|
| Reguła podpisu grafiki (FR-088) zmienia inne dokumenty | dziś obrazy ma tylko str. 1 `mbank-reg3` w korpusie; golden + przegląd prywatnych wyników; przełącznik. |
| Reguła „zmieściłoby się” (R9) na treści z twardymi łamaniami w środku zdania | dotyczy tylko linii tabeli-dokumentu; weryfikacja całego `mbank-reg3` ręcznie (SC-014). |
| Punktor „o” w dokumentach z mieszanymi czcionkami (np. przyimek „o” wstawiony inną czcionką) | wymagany tekst za punktorem i różna rodzina czcionki; golden bez zmian. |
| Nowa wartość `SectionKind` w kodzie klientów | kontrakt 1.1.0 opisuje wartość; README. |
