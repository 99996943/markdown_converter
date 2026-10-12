# Tasks: Poprawki tabel i przypisów parsera na regulaminach bankowych (siatka tabel z liniami, scalenia, przypisy)

**Input**: Design documents from `specs/010-bank-table-footnotes/` (plan, spec, research R1–R8, data-model,
contracts/markdown-output.md, quickstart)

**Tests**: wymagane — konstytucja I (TDD) i FR-752: każda zmiana zachowania to osobny commit **red** (test pada na
kodzie sprzed poprawki — sprawdzić uruchomieniem) i **green** (kod). Testy offline; repliki stron przez
`SyntheticPdfBuilder` (`src/LegalAgent.Corpus/Pdf/SyntheticPdfBuilder.cs`: `Text(x, y, text, size, bold)`,
`HLine`/`VLine` z dowolnymi odcinkami, wiele stron; odnośnik = mniejszy `Text` na podniesionej linii bazowej), bez
treści i nazw prawdziwego banku. Geometria replik według research.md (np. tekst 8 pt / odnośnik 5 pt +3,0 pt; tekst
11 pt, interlinia 15,4; linie jako osobne odcinki na komórkę). Prawdziwe PDF-y tylko w prywatnym korpusie
(`LEGALAGENT_PRIVATE_CORPUS`, ścieżka bezwzględna, poza git) i w `publish/`.

**Zasady dla każdego commita zmieniającego parser (FR-753)**: przed commitem green uruchom pełny zestaw z
`quickstart.md` §2 (testy solucji z prywatnym korpusem, `LEGALAGENT_CORPUS_FULL=1`, raportem; `refresh` + `verify`).
Goldeny (FR-755):
- prywatne `mbank-corp-*`, `mbank-firm-*`, `mbank-ind-*` — wykonawca przegląda diff i aktualizuje, jeśli zmiany są
  wyłącznie w tabelach i przypisach z przypadków T1–T12, porównanie słów (quickstart §3) czyste poza powtórzeniami
  scaleń i wiersza grupującego, a kontrola liczb i miary się nie pogarszają; każda zmiana opisana w handoffie;
- każda inna zmiana tych goldenów oraz każda zmiana `Corpus/acts`, `Corpus/banking`, goldenów fragmentów
  (`tests/LegalAgent.Chunking.Tests/Golden/`), `corpus/`, reg1–3, regulamin-pdp — **stop i pytanie do właściciela**
  z przygotowanym diffem; commit dopiero po zgodzie.

Przed każdym commitem: `git branch --show-current` osobnym poleceniem (nigdy `main`); stage'uj tylko jawne ścieżki
(nigdy `git add -A`, bez `git stash -u`); trailer `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
Kod, komentarze i commity po angielsku; spec, handoff i README po polsku.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: można równolegle (inne pliki, brak zależności)
- Etykiety historii: US1 (scalone wiersze), US2 (przypisy), US3 (nagłówek w kilku liniach + wiersz grupujący),
  US4 (macierz czynności), US5 (wielowierszowe komórki, mała tabela, powtórzony nagłówek), US6 (regresja i wydanie)

---

## Phase 1: Setup (punkt odniesienia)

- [X] T001 Skopiuj bieżące goldeny `tests/LegalAgent.PdfParser.Tests/Corpus/private/*.expected.md` (stan po PR #13,
  wersja 1.2.3) do nieśledzonego `tests/LegalAgent.PdfParser.Tests/Corpus/private/baseline-010/`; sprawdź
  `git status`, że nic z `Corpus/private` nie jest śledzone, i że pełny zestaw z prywatnym korpusem jest zielony na
  starcie
- [X] T002 W `tests/LegalAgent.PdfParser.Tests/Corpus/PrivateCorpusTests.cs` ustaw stałą `BaselineDirectory` na
  `"baseline-010"` (dziś `"baseline-009"`, ok. linii 32) i popraw komentarz dokumentujący w liniach ok. 19–23
  (dziś wymienia `baseline-008`/`baseline-009`) — porównanie zbiorów słów i nagłówków jednostek względem
  `baseline-010`; commit pomocniczy testów
- [X] T003 Pomiar wyjściowy w handoffie `specs/010-bank-table-footnotes/plan.md` („Stan prac i przekazanie”): dla
  każdego dokumentu prywatnego kolumny `Liczby +`, `Liczby −`, `Odnośniki sklejone`, liczba tabel i tabel TBL001 z
  raportu; dla downloads-1…5 (`publish/mBank.FaqGenerator-win-x64/downloads/`) stan przypadków z quickstart §4
  (konwersja CLI do katalogu tymczasowego, bez kopiowania treści do repozytorium)

---

## Phase 2: Foundational (sonda zasięgu i siatka)

**Purpose**: ocena ryzyka dla goldenów chronionych (research R8) i pomocnik siatki używany przez US1, US3, US4, US5.

- [X] T004 Sonda zasięgu (test diagnostyczny pomijany domyślnie albo skrypt w `tests/LegalAgent.PdfParser.Tests/Scratch/`):
  policz w `Corpus/acts`, `Corpus/banking`, korpusie syntetycznym (`corpus/`) i prywatnym korpusie (a) tabele, dla
  których `Grid` znajduje siatkę z liniami pionowymi (kandydaci na `RuledTableGrid`), (b) słowa z `FootnoteId`
  wewnątrz linii tabel (`LineRole.Table`), których odnośnik ginie dziś w `RowDraft.Add`
  (`src/LegalAgent.PdfParser/Stages/TableDetectionStage.cs` ok. 1053); wynik (dokumenty, liczby) w handoffie. Jeśli
  (a) lub (b) dotyczy goldenów chronionych — zanotuj w handoffie, że odpowiednie kroki zakończą się pytaniem
- [X] T005 [P] Testy (red) w nowym `tests/LegalAgent.PdfParser.Tests/Unit/Stages/RuledTableGridTests.cs` dla
  `RuledTableGrid` (data-model): kolumny = x linii pionowych występujących w ≥ 2 wierszach siatki, łączone z
  tolerancją 3 pt (odcinki przerywane na każdym wierszu jak w firm-5); granice wierszy = suma y odcinków poziomych
  pokrywających ≥ 80% szerokości pasma; `HasBoundary(column, boundary)` false dla kolumny bez odcinka (scalenie);
  odcinek poziomy przez kilka pasm bez linii pionowej w wierszu → komórka z `ColumnSpan` > 1 (wiersz grupujący
  firm-3: 215,4–555,2 przez 3 kolumny); `Locate(word)` według środka x i linii bazowej (słowo wyśrodkowane, którego
  lewa krawędź leży w sąsiednim paśmie, trafia do pasma swojego środka); siatka niespójna (linie pionowe tylko w
  jednym wierszu) → brak siatki
- [X] T006 Zaimplementuj `src/LegalAgent.PdfParser/Stages/RuledTableGrid.cs` (internal, stałe progi z komentarzem:
  3 pt, 80%, ≥ 2 wiersze) tak, by T005 przechodził; bez wpięcia w `TableDetectionStage` (brak zmiany zachowania —
  commit green bez zmian goldenów)

**Checkpoint**: siatka gotowa i przetestowana w izolacji; ryzyko goldenów chronionych znane.

---

## Phase 3: User Story 2 — Przypisy z odnośnikiem przed słowem i definicjami bez nawiasu (Priority: P1) 🎯 MVP

**Goal**: `[^n]` w miejscu odnośnika (także przed słowem i w komórkach tabel), definicje pod kreską przy rozmiarze
tekstu głównego i etykietach gołą cyfrą jako `[^n]:`, koniec zlepiania definicji w jeden `[^1]`.

**Independent Test**: replika strony z T12 (tekst + tabela 8 pt, odnośniki 5 pt, kreska, definicje 8 pt z
etykietami podniesionymi i zwykłymi) daje `[^n]kurs`, `Tak[^n]`, `TAK/TAK[^n]` i definicje `[^n]:`; zwykłe liczby i
„art. 5¹” bez zmian.

- [X] T007 [US2] Testy etapu (red) w `tests/LegalAgent.PdfParser.Tests/Unit/Stages/FootnoteDetectionStageTests.cs`:
  (a) kreska krótka przy dolnym marginesie, pod nią linie tego samego rozmiaru co tekst główny zaczynające się od
  podniesionych etykiet „³”, „⁴” i zwykłych „8 ”, „9 ” z odnośnikami na stronie → definicje z kluczami 3, 4, 8, 9;
  (b) etykieta podniesiona przyklejona do tekstu („³Zapis…”) → etykieta; (c) kilka linii z etykietami, z których
  pierwsza nie pasowała dotąd (firm-1: „1 obowiązuje…”, „5 O wprowadzeniu…”) → osobne definicje, nie jedna z pustą
  etykietą; (d) odnośnik na początku słowa („³kurs”, 5 pt +3,0 przy 8 pt) z definicją na stronie → słowo
  podzielone w kolejności odnośnik → „kurs”, `FootnoteId` ustawione; wariant z definicją na następnej stronie →
  odnośnik rozpoznany; podniesiona cyfra na początku słowa bez żadnej definicji w dokumencie → bez zmian; (e) ochrona: tekst pod kreską tego samego
  rozmiaru bez etykiet z odnośnikami → nie jest obszarem; goła cyfra „5 ” poza obszarem pod kreską → nie etykieta;
  podniesiona cyfra na początku słowa bez definicji na stronie → bez zmian; podniesione etykiety na początku linii
  obszaru nie są odnośnikami; „art. 5¹” (po `TextNormalization`) i kwoty/daty pełnego rozmiaru → bez zmian;
  dotychczasowe przypadki („1)” pod kreską małą czcionką, „***)”) → jak dotąd
- [X] T008 [P] [US2] Test (red) w `tests/LegalAgent.PdfParser.Tests/Unit/Stages/BlockAssemblyStageTests.cs`:
  słowo-odnośnik przed słowem daje `FootnoteRef` przed tekstem bez spacji (renderowane `[^1]kurs`), odnośnik po
  słowie — jak dotąd (`umowy[^1]`)
- [X] T009 [US2] Replika (red) w nowym `tests/LegalAgent.PdfParser.Tests/Integration/BankTablesIntegrationTests.cs`:
  strona akapitów 8 pt z odnośnikiem przed słowem i na końcu słowa, kreska x 39,7–183,7 przy dolnym marginesie,
  definicje 8 pt z etykietami ³ ⁴ (5 pt, podniesione) i 8, 9 (zwykłe), na stronie także „1 000,00 zł”,
  „26.08.2026 r.”, lista „1)”, „2)”, „art. 5¹”; Markdown z `[^n]` w miejscach odnośników, definicjami `[^n]:` w
  istniejącym formacie, zwykłymi liczbami bez zmian (bez tabeli — komórki w T012)
- [X] T010 [US2] Zaimplementuj w `src/LegalAgent.PdfParser/Stages/FootnoteDetectionStage.cs` (research R5 pkt 1–4,
  data-model „FootnoteDraft”): obszar kandydacki pod kreską wyznaczany przed `FindMarkers`; etykieta geometryczna
  (małe podniesione cyfry na początku linii obszaru, z odstępem lub bez) i goła cyfra `\d{1,3}` ze spacją tylko pod
  kreską i dla kluczy z odnośnikami na stronie; ścieżka z kreską przyjmuje linie większe niż `MaxSizeRatio`, gdy
  linie zaczynające definicje mają takie etykiety; `AddLine` nie dokleja linii z etykietą do szkicu z pustą
  etykietą; `FindMarkers` sprawdza też prefiks słowa (te same progi rozmiaru 0,85 i podniesienia 0,15 em, po
  prefiksie litera lub cyfra pełnego rozmiaru) i pomija etykiety na początku linii obszaru; `LinkMarkers` dzieli
  słowo w kolejności odnośnik → reszta. W `src/LegalAgent.PdfParser/Stages/BlockAssemblyStage.cs`
  (`InlineAccumulator`, ok. 733) `FootnoteRef` przed słowem bez spacji. T007–T009 zielone; pełny zestaw (FR-753);
  goldeny według FR-755 (oczekiwane: prywatne firm-1, firm-5, ind-2, ind-4, ind-1, corp-5 i inne z kolumną
  „Odnośniki sklejone” > 0 — tylko przypisy; dopuszczenia w `number-check.txt` dla scalonych definicji ind-2 „3” i
  ind-4 „1” usuń, jeśli przestały być potrzebne)
- [X] T011 [US2] Aktualizacja prywatnych goldenów (pliki poza git, bez commita) i wpis w handoffie: tabela
  „Odnośniki sklejone” przed/po, przykłady zmian, wynik porównania słów; goldeny chronione — stop i pytanie
- [X] T012 [US2] Test (red) w `tests/LegalAgent.PdfParser.Tests/Unit/Stages/TableDetectionStageTests.cs` i replika
  w `BankTablesIntegrationTests.cs` (tabela z liniami 8 pt, komórki „Tak⁴”, „TAK/TAK⁵”, „³kurs z momentu…”,
  definicje pod kreską): w komórkach `FootnoteRef` (`Tak[^n]`, `TAK/TAK[^n]`, `[^n]kurs …`), definicje przypisane,
  numeracja kolejna; słowo-odnośnik nie tworzy osobnej komórki ani etykiety w `CellsOf`; wariant z odnośnikiem w
  komórce nagłówka tabeli (`Header`) → `[^n]` w nagłówku GFM i definicja przypisana (wariant z `HeaderGroup` w T018)
- [X] T013 [US2] Zaimplementuj przenoszenie `FootnoteId` w `src/LegalAgent.PdfParser/Stages/TableDetectionStage.cs`
  (`Token`, `RowDraft.Add`, `RowDraft.Inlines` — `FootnoteRef` przyklejony do sąsiedniego słowa jak w PDF); T012
  zielony. Pełny zestaw; jeśli zmieniają się `Corpus/acts`, `Corpus/banking` lub `corpus/` (sonda T004) — stop i
  pytanie z diffem przed commitem green

- [X] T013b [US2] (dodane w implementacji) `NumberCheck.Compare(..., LegalDocument?)`: cyfry oryginalnych etykiet
  przypisów (jedna na definicję, jedna na odnośnik `[^n]`) nie są „brakujące”; test w
  `tests/LegalAgent.PdfParser.Tests/Unit/Fixtures/NumberCheckTests.cs`, `PrivateCorpusTests` przekazuje dokument

**Checkpoint**: przypisy T12 poprawne w tekście; w komórkach tabel — po zgodzie na ewentualne goldeny chronione.

- [X] T013a [US3] Sonda geometrii (skrypt w katalogu tymczasowym sesji, poza repozytorium; treści prywatne tylko w
  skrócie) tabel T3 i T1 spoza research: downloads-3 s. 17–18, downloads-4 s. 16–17, downloads-5 s. 6–8 i 26–27,
  firm-5 s. 22–23. Dla każdej tabeli: linie pionowe (x, czy powtarzają się w wierszach), odcinki poziome, środki x
  linii nagłówka względem danych. Wynik (z liniami / bez linii) zapisz w handoffie `plan.md` i w `research.md` (R1).
  Tabele bez linii z nagłówkiem w kilku liniach dostają w T014 replikę wariantu bez linii (P3) — jeśli poprawka bez
  siatki pogorszy taryfy lub słowniki, „Znane ograniczenia” (SC-112); macierze bez linii przechodzą do T029

---

## Phase 4: User Story 3 — Nagłówek kolumny w kilku liniach jako jedna komórka (Priority: P1)

**Goal**: kolumny tabel z liniami według linii pionowych, linie nagłówka nad jedną kolumną w jednej komórce, bez
pustych kolumn; wiersz grupujący na początku objętych nagłówków.

**Independent Test**: replika tabeli 5-kolumnowej z liniami, nagłówki 2–3 linie wyśrodkowane („serwis” /
„transakcyjny” / „(nasza strona internetowa)”), dane „TAK”, „NIE”, „TAK (na życzenie Klienta)” → 5 kolumn, nagłówek
w jednej komórce, każda wartość pod swoim nagłówkiem; wariant z „Gdzie można zrobić?” nad kolumnami 2–4.

- [X] T014 [US3] Repliki (red) w `BankTablesIntegrationTests.cs`: (a) jak downloads-2 s. 20 — linie pionowe 45,7 /
  185,4 / 278,9 / 372,3 / 463,1 / 555,5, nagłówek 11 pt pogrubiony w 2–4 liniach wyśrodkowanych (środek x ≈ 232),
  „Co i gdzie możesz zrobić” na wspólnej linii bazowej z „transakcyjny”, dane wyśrodkowane, w tym „TAK (na życzenie
  Klienta)”; oczekiwane: 5 kolumn, `**serwis transakcyjny** nasza strona internetowa` w jednej komórce, 0 pustych
  kolumn, wartości pod nagłówkami jak w PDF; (b) taryfa z siatką, w której nagłówek i dane mają te same granice →
  wynik jak dotąd (test ochronny, zielony przed i po)
- [X] T015 [US3] Wepnij `RuledTableGrid` w `src/LegalAgent.PdfParser/Stages/TableDetectionStage.cs` (`Build`): gdy
  `Grid` znajduje spójną siatkę z liniami pionowymi, pasma kolumn z siatki zamiast `ColumnClustering.ClusterLefts`,
  słowa przypisywane do komórek przez `Locate` (słowo po słowie, nie całe linie), komórki → `RowDraft` (jeszcze bez
  scaleń pionowych — kolumna bez linii na granicy dalej daje osobne wiersze z pustą komórką, jak dziś), bez TBL001
  dla tabel z siatką; siatka niespójna → dotychczasowa ścieżka. T014 zielony. Pełny zestaw; goldeny prywatne według
  FR-755; zmiany `Corpus/acts`/`Corpus/banking`/`corpus/` — stop i pytanie
- [X] T016 [P] [US3] Testy (red) modelu i renderera w
  `tests/LegalAgent.PdfParser.Tests/Rendering/MarkdownRendererTableTests.cs`: `TableBlock` z `HeaderGroup`
  („Gdzie można zrobić?” z `ColumnSpan` = 3 nad kolumnami 2–4, pusta komórka nad kolumną 1) → nagłówek GFM
  `| Co można zrobić? | Gdzie można zrobić? serwis … | Gdzie można zrobić? BOK | Gdzie można zrobić? placówka |`;
  bez `HeaderGroup` — jak dotąd; format zapasowy ignoruje `HeaderGroup`
- [X] T017 [US3] Dodaj `TableRow? HeaderGroup = null` na końcu `TableBlock` w
  `src/LegalAgent.PdfParser/Model/ContentBlock.cs` (dokumentacja: „tylko gdy `Header` ≠ null i `IsFallback` = false;
  suma `ColumnSpan` = `ColumnCount`; komórki z pustymi `Inlines` oznaczają kolumny bez grupy”) i renderowanie w
  `src/LegalAgent.PdfParser/Rendering/MarkdownRenderer.cs` (tekst grupy + spacja + nagłówek kolumny); T016 zielony
- [X] T018 [US3] Replika (red) w `BankTablesIntegrationTests.cs` jak firm-3 s. 8: linie pionowe 59,2 / 215,2 / 328,6 /
  442,1 / 555,5 (w paśmie wiersza grupującego tylko 59,2 / 215,2 / 555,5), odcinek 215,4–555,2 nad grupą, linia
  pod grupą tylko nad kolumnami 2–4, „Co można zrobić?” w kolumnie 1 przez oba wiersze nagłówka; oczekiwany nagłówek
  z tekstem grupy w kolumnach 2–4, 4 kolumny, dane pod właściwymi nagłówkami; wariant: tabela z wierszem
  grupującym przechodzi na drugą stronę z powtórzonym wierszem grupującym i nagłówkiem → jedna tabela, nagłówek
  raz (część dotycząca łączenia może pozostać czerwona do T033); wariant z odnośnikiem w komórce wiersza grupującego
- [X] T019 [US3] Rozpoznawanie wiersza grupującego w `TableDetectionStage` (research R4): wiersz siatki nad
  nagłówkiem z komórkami obejmującymi kilka pasm, przy kolumnach scalonych pionowo z nagłówkiem → `HeaderGroup`;
  T018 zielony. Pełny zestaw; goldeny według FR-755 (oczekiwane: firm-3 i inne macierze z grupą)

**Checkpoint**: T3 poprawne na replikach; przegląd downloads-2…5, firm-3, ind-4 s. 12 (quickstart §4) w handoffie.

---

## Phase 5: User Story 1 — Scalone wiersze w tabelach z liniami (Priority: P1)

**Goal**: scalenie pionowe w modelu (`RowSpan` + `ContinuesAbove`), tekst w każdym objętym wierszu, kontrola liczb
bez fałszywych nadmiarowych.

**Independent Test**: replika jak firm-5 s. 13 (2 kolumny, linie kolumny 1 tylko na granicach grup 3/4/1 wiersze)
→ 8 wierszy z etykietą grupy w każdym; model: `RowSpan` = 3 i kontynuacje; część chunkera od środka grupy ma
etykietę.

- [X] T020 [P] [US1] Testy (red) w `MarkdownRendererTableTests.cs`: wiersze z komórką `RowSpan` = 3 i dwiema
  `ContinuesAbove` (te same inline'y) → tekst w każdym z trzech wierszy; niezmiennik suma `ColumnSpan` =
  `ColumnCount` w każdym wierszu
- [X] T021 [US1] Dodaj do `src/LegalAgent.PdfParser/Model/TableCell.cs` parametry `int RowSpan = 1` i
  `bool ContinuesAbove = false` (dokumentacja z data-model: „`RowSpan` ≥ 1; > 1 tylko w pierwszym objętym wierszu”,
  „`ContinuesAbove` true tylko w wierszach 2…`RowSpan` grupy; komórka ma `RowSpan` = 1 i ten sam `ColumnSpan` co
  początkowa”, „komórka nagłówka nigdy nie ma `RowSpan` > 1 ani `ContinuesAbove`”) i rozszerz komentarz niezmiennika w
  `src/LegalAgent.PdfParser/Model/TableRow.cs`; T020 zielony (renderer bez zmian logiki)
- [X] T022 [P] [US1] Test (red lub regresji) w `tests/LegalAgent.Chunking.Tests/Unit/SplitTablesTests.cs`: tabela
  ze scaleniem przez 4 wiersze, `MaxChunkLength` dzieli ją w środku grupy → każda część ma tekst scalonej komórki w
  każdym wierszu (bez zmian kodu chunkera). Jeśli test przechodzi od razu po T021 bez zmian kodu — commit samego
  testu jako regresji z adnotacją w handoffie; jeśli wymaga zmian kodu — para commitów red/green
- [X] T023 [US1] Replika (red) w `BankTablesIntegrationTests.cs` jak firm-5 s. 13: linie pionowe 59,3 / 201,1 /
  555,6 przerywane na każdej linii kolumny 2; odcinki poziome kolumny 1 (59,5–200,8) tylko na granicach grup,
  kolumny 2 (201,3–555,3) pod każdym wierszem; etykieta grupy A w jednej linii na środku grupy, B w dwóch liniach
  dzielących linie bazowe z wierszem grupy; tekst 8 pt; oczekiwane: każdy wiersz z etykietą grupy, model `RowSpan`
  i kontynuacje; wariant: tabela z linią pod każdym wierszem we wszystkich kolumnach → bez scaleń
- [X] T024 [US1] Scalenia w siatce (`RuledTableGrid` + `TableDetectionStage`): komórka pasma bez linii na granicy
  przy linii w innym paśmie obejmuje oba wiersze, tekst komórki z wszystkich linii jej obszaru w kolejności linii
  bazowych; `RowDraft` → `TableCell` z `RowSpan` i kontynuacjami (ta sama lista inline'ów); T023 zielony
- [X] T025 [US1] Test (red) w `tests/LegalAgent.PdfParser.Tests/Unit/Fixtures/NumberCheckTests.cs`: syntetyczny PDF
  z liczbą w scalonej komórce („do 10 000 zł” przez 3 wiersze) → z dokumentem 0 nadmiarowych, bez dokumentu —
  nadmiarowe; analogicznie liczba w `HeaderGroup`
- [X] T026 [US1] `Compare(..., LegalDocument? document = null)` w
  `tests/LegalAgent.PdfParser.Tests/Fixtures/NumberCheck.cs` (odejmowanie liczb z komórek `ContinuesAbove` i z komórek
  `HeaderGroup` × (objęte kolumny − 1)); `PrivateCorpusTests.cs` przekazuje `Result.Document`; porównanie słów w
  raporcie odejmuje tak samo słowa z powtórzeń; T025 zielony. Pełny zestaw z prywatnym korpusem; goldeny według
  FR-755 (oczekiwane: firm-5 i inne tabele ze scaleniem); `RegulationNumbers_HaveNoExcess` zielony

**Checkpoint**: SC-110 (firm-5 s. 13: 10 wierszy z etykietą) sprawdzone i wpisane w handoffie.

---

## Phase 6: User Story 4 — Macierz czynności z wielowierszową pierwszą kolumną (Priority: P2)

**Goal**: macierz „Co i gdzie możesz zrobić?” jako jedna tabela GFM N×k przez strony, bez TBL001 i akapitów.

**Independent Test**: replika macierzy z liniami przez dwie strony (1. kolumna 2–6 linii, wartości wyśrodkowane w
pionie, nagłówek 3-liniowy, odnośniki „TAK/TAK⁵”) → jedna tabela, liczba wierszy jak w PDF.

- [ ] T027 [US4] Repliki (red) w `BankTablesIntegrationTests.cs`: (a) jak ind-4 s. 42–43 — linie pionowe 45,1 /
  187,0 / 280,5 / 374,1 / 464,9 / 555,6, tekst 11 pt, interlinia 15,4, wiersze z 1, 2, 3, 6 liniami w kolumnie 1,
  wartości „Tak”/„Nie” na linii bazowej równej średniej linii kolumny 1 (przy nieparzystej liczbie linii wspólna linia
  bazowa ze środkową linią kolumny 1), tabela ciągnie się na drugą stronę z powtórzonym nagłówkiem; (b) jak firm-1
  s. 16 — 4 kolumny, nagłówek „serwis transakcyjny (…)” w 3 liniach wyższy niż wiersze, wartości „TAK/TAK⁵” z
  definicją pod kreską. Oczekiwane: jedna tabela GFM, 0 linii TBL001, 0 akapitów z wierszy, `TAK/TAK[^n]`.
  Repliki dodatkowe według sondy T013a; macierz z SC-113, która okazała się bez linii, przechodzi do T029 (P3)
- [ ] T028 [US4] Poprawki w `TableDetectionStage` potrzebne, by T027 przeszedł po siatce z Phase 4–5 (np. obszar
  tabeli obejmuje nagłówek wyższy niż wiersze — `GrowRegion`/linie nad ziarnem; łączenie przez strony części z
  siatką — `SameColumns` na pasmach z siatki); T027 zielony. Pełny zestaw; goldeny według FR-755 (oczekiwane: ind-1,
  ind-2, ind-4, firm-1, firm-5)
- [ ] T029 [US4] Replika wariantu bez linii (P3, red) w `BankTablesIntegrationTests.cs` (ta sama macierz bez linii,
  odstęp międzywierszowy / interlinia 1,29); próba w ścieżce bez siatki (kolumny z wierszy danych, linie nagłówka
  według środka x, nowy wiersz przy linii z wartościami w kolumnach 2…k); jeśli pogarsza taryfy korpusu
  syntetycznego (`CorpusFull`), słowniki lub inne goldeny — test `Skip` z odwołaniem do „Znanych ograniczeń” i wpis
  w handoffie

**Checkpoint**: SC-113 sprawdzone na ind-4, ind-2, downloads-5, downloads-3, firm-5, firm-1 (quickstart §4).

---

## Phase 7: User Story 5 — Wielowierszowe komórki w środku, mała tabela, powtórzony nagłówek (Priority: P3)

**Goal**: T4–T6 kompletne; powtórzony nagłówek pominięty, urwana komórka dokończona.

**Independent Test**: trzy repliki (T4, T5, T6) dają kompletne tabele bez fałszywych `##`.

- [ ] T030 [P] [US5] Replika T4 (red lub regresji) w `BankTablesIntegrationTests.cs`: 3 kolumny z liniami, wiersz,
  w którym kolumna 2 ma dwa akapity („jeśli nie wymaga…” / „jeśli wymaga…”), kolumny 1 i 3 jedną linię wyśrodkowaną,
  dalsze wiersze → jedna tabela; jeśli zielony po Phase 4–5 bez zmian kodu — commit samego testu jako regresji z
  adnotacją, jeśli wymaga kodu — para red/green; najpierw
  sprawdź geometrię firm-5 s. 12–13 sondą (z liniami czy bez) i zapisz w handoffie
- [ ] T031 [P] [US5] Replika T5 (red) w `BankTablesIntegrationTests.cs` jak ind-2 s. 6–7: linie pionowe 43,7 /
  325,6 / 438,4 / 551,3, wiersze 19,9 pt, 11 pt, kolumny 2–3 wyśrodkowane; s. 6 nagłówek + 1 wiersz na dole strony,
  s. 7 nagłówek powtórzony + 4 wiersze → jedna tabela GFM 3-kolumnowa, 5 wierszy danych, nagłówek raz
- [ ] T032 [P] [US5] Replika T6 (red) w `BankTablesIntegrationTests.cs` jak ind-4 s. 12→13: linie pionowe 59,3 /
  201,1 / 378,3 / 555,5; s. 12 tabela kończy się wierszem z komórką kolumny 3 „…, czyli:” zamkniętym linią; s. 13
  nagłówek powtórzony (te same teksty i x), potem wiersz z pustymi kolumnami 1–2 i listą punktowaną w kolumnie 3 →
  jedna tabela, 0 nagłówków `##`, lista doklejona do komórki „…, czyli:”; testy ochronne: ta sama treść nagłówka w
  nowej tabeli po akapicie (nie na początku strony) → osobna tabela; na s. 13 pierwszy wiersz z pustą kolumną 1 i
  tekstem w kolumnach 2 i 3 → osobny wiersz z pustą 1. kolumną, nie doklejenie (FR-743)
- [ ] T033 [US5] Zaimplementuj w `TableDetectionStage` (research R6): `Append` pomija pierwszy wiersz, gdy teksty
  komórek po normalizacji białych znaków są równe nagłówkowi (porównanie obejmuje `HeaderGroup` i `Header`; pominięte
  oba — FR-742); warunek łączenia „2+ wiersze” liczy wiersze obu części; kontynuacja komórki — pierwszy wiersz danych
  części z następnej strony z tekstem w dokładnie jednej kolumnie (pozostałe puste), niepustej w ostatnim wierszu
  poprzedniej części → doklejony do tej komórki (aktualizacja wszystkich komórek grupy scalenia, data-model
  „Przejścia stanu”); wiersz z tekstem w ≥ 2 kolumnach — zwykły wiersz; T030–T032 i wariant łączenia z T018 zielone. Pełny zestaw; goldeny według
  FR-755 (oczekiwane: ind-2, ind-4)
- [ ] T034 [US5] Mała tabela bez linii (P3, FR-741): replika (red) 3×4 bez linii z krótkimi komórkami; próg wierszy
  dla tabel bez siatki liczony przez strony; jeśli pogarsza słowniki (spec 007), listy etykiet lub taryfy — `Skip` i
  „Znane ograniczenia”

**Checkpoint**: SC-114 sprawdzone (firm-5 s. 12–13, ind-2 s. 6–7, ind-4 s. 13, downloads-5 s. 8).

---

## Phase 8: User Story 6 — Bez regresji, z miarami i wydaniem (Priority: P1) + Polish

**Purpose**: wersja, kontrakt, korpus syntetyczny, dokumentacja, publish/.

- [ ] T035 [US6] Wersja 1.3.0 w `Directory.Build.props`; `refresh` (aktualizuje `parserVersion` w
  `corpus/manifest.json`; `generatorVersion` ręcznie na 1.3.0); `verify` czysty; każda zmiana `corpus/` poza
  manifestem — stop i pytanie (FR-755)
- [ ] T036 [US6] Pełny zestaw z `LEGALAGENT_CORPUS_FULL=1`, prywatnym korpusem i raportem oraz osobno
  `dotnet test LegalAgent.slnx --filter "Category=Performance"` (SC-007 bez regresji); porównanie słów vs
  `baseline-010` (quickstart §3, SC-115) — wynik w handoffie
- [ ] T037 [US6] Przegląd przypadków na prawdziwych dokumentach (quickstart §4): tabela przed/po dla SC-110…SC-114
  (firm-5, firm-1, firm-3, ind-2, ind-4, downloads-1…5) w handoffie `plan.md`, z listą zmienionych goldenów i
  uzasadnieniem; treści prywatne tylko w skrócie
- [ ] T038 [P] [US6] Kontrakt: odnośnik z `specs/001-legal-pdf-parser/contracts/markdown-output.md` (lub
  `specs/001-legal-pdf-parser/data-model.md`, sekcja tabel) do uzupełnienia
  `specs/010-bank-table-footnotes/contracts/markdown-output.md` (1.4.0) i opis pól `RowSpan`, `ContinuesAbove`,
  `HeaderGroup`
- [ ] T039 [P] [US6] README parsera `src/LegalAgent.PdfParser/README.md`: zmiany 1.3.0 (siatka tabel z liniami,
  scalenia, wiersz grupujący, przypisy przed słowem / w komórkach / pod kreską) i „Znane ograniczenia”: T7, T8, T10,
  odnośniki z przecinkiem („BLIK⁹,¹⁰”), „art. 166¹¹²)”, warianty bez linii i inne niezrobione przypadki z
  uzasadnieniem (FR-757)
- [ ] T040 [P] [US6] `CLAUDE.md`: w „Parser architecture” akapit „Spec 010 additions” (`RuledTableGrid`, `RowSpan`/
  `ContinuesAbove`, `HeaderGroup`, przypisy), wersja 1.3.0; w „Chunking architecture” zdanie o komórkach-kontynuacjach
- [ ] T041 [US6] Przebuduj `publish/` (biblioteka, pakiet NuGet 1.3.0, exe generatora FAQ) i uruchom konwersję
  regulaminów z `publish/mBank.FaqGenerator-win-x64` bez błędów (SC-117); wynik w handoffie
- [ ] T042 [US6] Handoff końcowy w `specs/010-bank-table-footnotes/plan.md` („Stan prac i przekazanie”): miary
  przed/po, zmienione goldeny, decyzje właściciela, ograniczenia, kandydaci na następną spec; zaktualizuj
  `tasks.md` (zadania [X]); gałąź niewypchnięta, chyba że właściciel poprosi

---

## Dependencies & Execution Order

- **Phase 1 → Phase 2**: punkt odniesienia przed sondą.
- **Phase 2** blokuje Phase 4–7 (siatka); Phase 3 (US2) zależy tylko od Phase 1 (T004 dla T013).
- **Phase 3 (US2)** niezależna od siatki; T012–T013 korzystają z siatki tylko pośrednio (komórki ze ścieżki
  bez siatki też przenoszą `FootnoteId`).
- **T013a** (sonda T3/T1) przed T014 i T027.
- **Phase 4 (US3)** → **Phase 5 (US1)**: scalenia budują na siatce wpiętej w T015; `TableCell` (T021) przed T024.
  T025–T026 (kontrola liczb) przed aktualizacją goldenów ze scaleniami.
- **Phase 6 (US4)** po Phase 4–5 (macierze potrzebują siatki, przypisów w komórkach i łączenia przez strony).
- **Phase 7 (US5)** po Phase 4; T033 po T024 (doklejanie do komórek scalonych).
- **Phase 8** po wszystkich historiach, które mają być w wydaniu.

## Parallel Opportunities

- T005 (testy siatki) równolegle z T007–T008 (testy przypisów) — inne pliki.
- T016 (renderer `HeaderGroup`) równolegle z T014–T015.
- T020 i T022 (renderer, chunker) równolegle.
- T030, T031, T032 (repliki US5) — ten sam plik testów, ale niezależne metody; pisać kolejno, commit red razem.
- T038, T039, T040 (dokumentacja) równolegle.

## Implementation Strategy

1. **MVP**: Phase 1–3 (przypisy T12) — najmniejsza zmiana, poprawia „Odnośniki sklejone” w 11 regulaminach.
2. **Główna zmiana**: Phase 2 (siatka) + Phase 4 (T3, błędna treść) + Phase 5 (scalenia T11).
3. **Rozszerzenie**: Phase 6 (macierze), Phase 7 (strony), warianty P3 bez linii tylko jeśli bez regresji.
4. **Wydanie**: Phase 8. Co nie zdąży — opis w handoffie i „Znanych ograniczeniach”.
