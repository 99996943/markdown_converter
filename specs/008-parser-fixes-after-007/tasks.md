# Tasks: Poprawki parsera po spec 007 (stopki „N/M”, fałszywe nagłówki, wyliczenia „a.”/„i.”, ramki)

**Input**: Design documents from `specs/008-parser-fixes-after-007/` (plan, spec, research, data-model,
contracts/markdown-output.md, quickstart)

**Tests**: wymagane — konstytucja I (TDD) i FR-642: każda zmiana zachowania to osobny commit **red** (test pada na
kodzie sprzed poprawki — sprawdzić uruchomieniem) i **green** (kod). Testy offline; repliki stron przez
`SyntheticPdfBuilder` (`src/LegalAgent.Corpus/Pdf/SyntheticPdfBuilder.cs`), bez nazw prawdziwych banków; prawdziwe
PDF-y tylko w prywatnym korpusie (`LEGALAGENT_PRIVATE_CORPUS`, poza git).

**Zasady dla każdego commita zmieniającego parser (FR-643)**: przed commitem green uruchom pełny zestaw z
`quickstart.md` §2 (testy solucji, prywatny korpus z raportem miar i porównaniem zbiorów słów, `refresh` + `verify`,
`CorpusFull`). Goldeny `Corpus/acts`, `Corpus/banking` i korpus syntetyczny — zmiana tylko za zgodą właściciela.
Prywatne goldeny (FR-644): ind-2 i firm-1 — zgoda z góry wyłącznie na usunięcie stopek „N/M”; dokumenty dla firm
(corp-1…5, firm-1…5) — wykonawca przegląda diff i aktualizuje, jeśli żadna miara się nie pogarsza i zbiory słów są
czyste (opis w handoffie); każda inna zmiana w dokumentach detalicznych (ind-1…5) i dotychczasowych prywatnych — stop
i pytanie do właściciela. Przed każdym commitem: `git branch --show-current` osobnym poleceniem (nigdy `main`);
stage'uj tylko jawne ścieżki (nigdy `git add -A`); trailer `Co-Authored-By: Claude Opus 5.5 <noreplyTanthropic.com>`.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: można równolegle (inne pliki, brak zależności)
- Etykiety historii: US1 („N/M”), US2 (fałszywe nagłówki), US3 („a.”/„i.”), US4 (ramki), US5 (regresja i miary)

---

## Phase 1: Setup (punkt odniesienia)

- [ ] T001 Skopiuj bieżące goldeny `tests/LegalAgent.PdfParser.Tests/Corpus/private/*.expected.md` (19 dokumentów,
  stan po PR #10) do nieśledzonego `tests/LegalAgent.PdfParser.Tests/Corpus/private/baseline-008/`; sprawdź
  `git status`, że nic z `Corpus/private` nie jest śledzone, i że prywatny korpus jest zielony na starcie
- [ ] T002 Utwórz nieśledzony plik `tests/LegalAgent.PdfParser.Tests/Corpus/private/frame-phrases.txt` z frazami
  lewych kolumn ramek (po jednej w wierszu, format `dokument;fraza`) dla firm-2 (15 ramek z research R4), firm-4 (3) i
  firm-5 (ok. 25) — odczytane z PDF-ów (nie z goldenów, bo tam są pocięte); plik nie trafia do git

---

## Phase 2: Foundational (pomiar — blokuje wszystkie historie)

- [ ] T003 [P] Testy (red) nowych miar w `tests/LegalAgent.PdfParser.Tests/Unit/Fixtures/LayoutMetricsTests.cs`:
  `PageNumberParagraphs` (akapit, którego cały tekst to „N/M”; „1/2 kwoty” i „- 1/ tekst” się nie liczą),
  `DotEnumLabelsOutsideList` (tokeny „a.”, „b.” … / „i.”, „ii.” … należące do spójnego ciągu ≥ 2, które nie zaczynają
  elementu listy — w tekście elementu po „:”/„,”, w akapicie, w komórce `a \| b`; „r.”, „np.”, samotne „a.” się nie
  liczą), `UnitHeadingDiff(before, after)` (nagłówki jednostek z `baseline-008` nieobecne po zmianie i nowe po
  zmianie — miara fałszywych nagłówków do SC-091/SC-096), `FramesAsTable` / `FramesWoven` (fraza z listy ramek w wierszu tabeli GFM / wewnątrz elementu listy) oraz
  porównanie zbiorów słów `WordSetDiff(before, after)` (słowa bez interpunkcji i znaczników Markdown, wynik: utracone,
  nowe)
- [ ] T004 Pomocnik (green T003) w `tests/LegalAgent.PdfParser.Tests/Fixtures/LayoutMetrics.cs`
- [ ] T005 Raport w `tests/LegalAgent.PdfParser.Tests/Corpus/PrivateCorpusTests.cs`: przy `LEGALAGENT_CORPUS_REPORT`
  dopisz do `layout-metrics.md` nowe kolumny miar i sekcję „zbiory słów vs baseline-008” (utracone / nowe słowa na
  dokument); wczytaj `frame-phrases.txt`, jeśli istnieje. Uruchom i zapisz wartości wyjściowe w handoffie planu
  (`specs/008-parser-fixes-after-007/plan.md`, „Stan prac i przekazanie”) — oczekiwane: „N/M” 35 (ind-2) / 6 (firm-1),
  „a.”/„i.” poza listą 20 (corp-1) / 2 (corp-3) / 9 (corp-5), ramki firm-2 jako tabela 7 / wplecione 6, fałszywe
  nagłówki jednostek D-A 1 (lista nagłówków z `baseline-008` jako punkt odniesienia `UnitHeadingDiff`)

**Checkpoint**: punkt odniesienia i pomiar gotowe — zmiany parsera mogą się zaczynać.

---

## Phase 3: User Story 1 — Numery stron „N/M” znikają z treści (Priority: P1) 🎯 MVP

**Goal**: samodzielne stopki „N/M” tworzące ciąg na stronach są artefaktem strony (FR-600…FR-602, research R1).

**Independent Test**: replika 3-stronicowa ze stopką „n/3” na ≈ 0,905 wysokości strony (nad strefą marginesu 0,92) i
z „1/2 kwoty”, „uchwała nr 12/2024”, etykietą „1/” w treści daje Markdown bez stopek i z nietkniętą treścią.

- [ ] T006 [P] [US1] Testy (red) etapu w `tests/LegalAgent.PdfParser.Tests/Unit/Stages/ArtifactRemovalStageTests.cs`
  (`LayoutFactory`): (a) samodzielny wiersz „2/3”, „3/3” na 0,905 H, najniższy na stronie, na stronach 2–3 → usunięty,
  wpis raportu `ArtifactKind.PageNumber`; (b) to samo w górnym pasie (najwyższy wiersz) → usunięty; (c) M ≠ liczba
  stron → zostaje; (d) tylko jedna strona z „N/M” → zostaje; (e) N nie rośnie zgodnie ze stronami (stałe przesunięcie
  nie istnieje) → zostaje; (f) „N/M” nie jest najniższym wierszem albo dzieli wysokość z innym wierszem → zostaje;
  (g) „1/2 kwoty”, „12/2024”, „1/ tekst” w treści → zostają; (h) „N/M” jako część dłuższego wiersza stopki → bez
  zmiany względem dotychczasowych reguł; (i) ciąg z luką: „N/M” na stronach 2, 3 i 5 dokumentu 5-stronicowego (bez
  okładki i bez strony 4) → usunięte na stronach 2, 3, 5
- [ ] T007 [P] [US1] Test (red) opcji w `tests/LegalAgent.PdfParser.Tests/Unit/Options/PdfParserOptionsValidatorTests.cs`:
  `Artifacts.PageNumberBandRatio` domyślnie 0,15;
  walidacja „0 < x < 0,5 i ≥ `MarginZoneRatio`” — wartość 0,05 przy `MarginZoneRatio` 0,08 i 0,6 → błąd
- [ ] T008 [P] [US1] Replika (red) w `tests/LegalAgent.PdfParser.Tests/Integration/ArtifactCleanupIntegrationTests.cs`:
  3 strony A4, stopka `.Text(x≈523, y≈765, "n/3", size: 8)` na stronach 2–3 (≈ 0,905 H), na stronie 2 wiersz treści
  kończący się na ≈ 0,903 H, w treści „1/2 kwoty”, „uchwała nr 12/2024” i lista „1/ …”; oczekiwane: brak akapitów
  „2/3”, „3/3”, znaczniki `<!-- page: N -->` są, treść bez zmian
- [ ] T009 [US1] Opcja `PageNumberBandRatio` (double, domyślnie 0,15, „wysokość dolnego i górnego pasa, w którym szuka
  się samodzielnych numerów „N/M””) w `ArtifactOptions` w `src/LegalAgent.PdfParser/Options/PdfParserOptions.cs` i
  reguła w `src/LegalAgent.PdfParser/Options/PdfParserOptionsValidator.cs` (green T007)
- [ ] T010 [US1] Przebieg po `RemovePageNumbers` w `src/LegalAgent.PdfParser/Stages/ArtifactRemovalStage.cs` (green
  T006, T008): kandydat = cały tekst wiersza `^\d{1,3}\s*/\s*\d{1,3}$`, w pasie `PageNumberBandRatio`, najniższy
  (dolny pas) albo najwyższy (górny pas) wiersz strony, bez innego wiersza na tej wysokości; grupa usuwana w całości
  tylko gdy ≥ 2 strony, ta sama względna wysokość (± `PositionTolerance`), to samo M = liczba stron dokumentu i to samo
  przesunięcie N − numer strony; inaczej nic. Wpis raportu jak dla numerów stron
- [ ] T011 [US1] Pełna kontrola FR-643; goldeny `mbank-ind-2.expected.md` i `mbank-firm-1.expected.md` zaktualizuj
  (`UPDATE_GOLDEN=1` na prywatnym korpusie) i sprawdź diffem względem `baseline-008/`, że jedyne zmiany to usunięte
  akapity „N/M” (35 i 6) i ewentualne puste wiersze wokół nich — każda inna różnica: stop; pozostałe 17 dokumentów,
  `Corpus/acts`, `Corpus/banking`, `verify` bez zmian; zapis w handoffie
- [ ] T012 [US1] README parsera `src/LegalAgent.PdfParser/README.md`: stopki „N/M”
  nad strefą marginesu i opcja `Artifacts.PageNumberBandRatio` / `PDFPARSER__Artifacts__PageNumberBandRatio`

**Checkpoint**: US1 gotowe — 0 stopek „N/M” w ind-2 i firm-1 (SC-090).

---

## Phase 4: User Story 2 — Brak fałszywych nagłówków (Priority: P1)

**Goal**: wiersz z oznaczeniem jednostki, który kontynuuje akapit lub element listy, nie jest nagłówkiem
(FR-610, FR-612); termin słowniczka nigdy nie jest nagłówkiem (FR-611 — regresja) (research R2).

**Independent Test**: replika strony D-A z elementem „1.” (wiersz kończy się „–”) i kontynuacją „Rozdział I (…). Jest
on załącznikiem…” w kolumnie tekstu za etykietą daje element listy z całym tekstem, rozdziały 17–18 na poziomie
rozdziału; pogrubione „Art. 42.” po „3. (uchylony)” zostaje nagłówkiem.

- [ ] T013 [P] [US2] Test regresji (przechodzi od razu — commit jako test, bez pary red/green) w
  `tests/LegalAgent.PdfParser.Tests/Integration/GlossaryLayoutTests.cs`: wpis słowniczka „15/” z pogrubionym terminem
  kończącym się „/” i zawiniętym na drugi pogrubiony wiersz (np. „umowa rachunku/” + „umowa”) → `- 15/ **umowa
  rachunku/ umowa** definicja…`, brak nagłówka; zapisz w handoffie, że FR-611 było spełnione po spec 007
- [ ] T014 [P] [US2] Replika (red) w `tests/LegalAgent.PdfParser.Tests/Integration/CorpusLayoutsIntegrationTests.cs`
  (obok T085 spec 007): tekst 7 pt, interlinia 10; pogrubiony 9 pt „16. Prowizje” (x 39,7), wyśrodkowany pogrubiony
  „§ 63”; element „1.” (etykieta x 39,7, tekst x 54,7) na pełną szerokość kończący się „–”; następny wiersz na x 54,7,
  odstęp 10, zwykły 7 pt: „Rozdział I (Prowadzenie rachunków). Jest on załącznikiem do umowy.”; elementy „2.”, „3.”;
  potem pogrubione „17. …”, wyśrodkowane „§ 66”, „18. …”, „§ 67”. Oczekiwane: `- 1\.` z całym tekstem łącznie z
  „Rozdział I (Prowadzenie…)” (bez dopisanej kropki po „I”), brak nagłówka „Rozdział”, „17.” i „18.” na tym samym
  poziomie co „16.”, „§ 66”, „§ 67” na poziomie „§ 63”
- [ ] T015 [P] [US2] Test ochronny (powinien przejść dziś i po zmianie) w tym samym pliku: akt z „3. (uchylony)” i pod
  nim pogrubionym „Art. 42.” (odstęp ≈ 2 × interlinia, poprzedni wiersz kończy się „)”) → „Art. 42.” jest nagłówkiem
- [ ] T016 [P] [US2] Testy (red) jednostkowe pomocnika „wiersz kontynuacji” w
  `tests/LegalAgent.PdfParser.Tests/Unit/Text/ContinuationLineTests.cs` (`LayoutFactory`): kontynuacja gdy wszystkie
  warunki z `data-model.md` („wiersz powyżej rola Unknown/ListItem/ListContinuation; odstęp ≤ 1,2 × interlinia; ten sam
  dominujący rozmiar ± 0,5 pt i to samo `AllBold`; pierwsze słowo oznaczenia = pogrubienie ostatniego słowa
  poprzedniego wiersza; poprzedni wiersz nie kończy się `.`, `:`, `;`, `!`, `?`; lewa krawędź ≈ lewa krawędź
  poprzedniego wiersza albo początek jego tekstu za etykietą ± 2 pt; wiersz nie wyśrodkowany”); po jednym przypadku
  negatywnym na każdy warunek (w tym pogrubione słowo oznaczenia przy niepogrubionym poprzednim wierszu)
- [ ] T017 [US2] Pomocnik w `src/LegalAgent.PdfParser/Text/ContinuationLine.cs` (internal; green T016)
- [ ] T018 [US2] `ListDetectionStage` (`src/LegalAgent.PdfParser/Stages/ListDetectionStage.cs`, ok. l. 131): wiersz
  pasujący do wzorca jednostki, który jest wierszem kontynuacji, nie dostaje `LegalUnit` i zostaje kontynuacją
  elementu (ciąg „N.” się nie przerywa) — green części listowej T014
- [ ] T019 [US2] `HeadingDetectionStage` (`src/LegalAgent.PdfParser/Stages/HeadingDetectionStage.cs`, ok. l. 195–202):
  wiersz kontynuacji nie jest nagłówkiem jednostki (obok istniejącego `ContinuesSentence`, reguła FR-162 zostaje) —
  green T014 w całości, T015 zielony
- [ ] T020 [US2] Pełna kontrola FR-643; golden `mbank-corp-1.expected.md`: oczekiwane tylko element „1.” z odwołaniem
  jako element listy i rozdziały 17–18 oraz § 66–80 o poziom wyżej — przejrzyj diff, miary nie gorsze, zbiory słów
  czyste (poza znikającą kropką po „I”), zaktualizuj; pozostałe dokumenty, akty, bankowe, `verify` bez zmian; handoff

**Checkpoint**: US2 gotowe — SC-091.

---

## Phase 5: User Story 3 — Wyliczenia „a.” i „i.” jako elementy listy (Priority: P2)

**Goal**: spójne ciągi „a.”, „b.” … i „i.”, „ii.” … zaczynające wiersze PDF są zagnieżdżonymi elementami listy
(FR-620…FR-624, research R3, kontrakt 1.3.0).

**Independent Test**: repliki T014a/T014b spec 007 (corp-5 s. 8, D-A s. 43) dają zagnieżdżone `- a\.`, `- i\.` …
`- xii\.`; akapit z „np.”, „tj.”, „J. Kowalski”, „A. …”, samotnym „a.” i zawiniętym „r. …” bez listy.

- [ ] T021 [P] [US3] Testy (red) wzorców w `tests/LegalAgent.PdfParser.Tests/Unit/Text/ListLabelPatternsTests.cs`:
  „a.”, „z.” → `LetterDot` (`^[a-z]\.$`, porządek 1–26); „ii.”, „iv.”, „xii.”, „xxxix.” → `RomanDot` (małe rzymskie
  `i`–`xxxix` + „.”, porządek 1–39); „i.”, „v.”, „x.” → dopasowanie z oboma odczytami (litera i rzymska); NIE etykiety:
  „A.”, „II.”, „np.”, „tj.”, „m.in.”, „r.” bez tekstu po nim, token „a.” bez tekstu po nim
- [ ] T022 [P] [US3] Testy (red) bramki ciągu w `tests/LegalAgent.PdfParser.Tests/Unit/Stages/ListDetectionStageTests.cs`:
  „a.”, „b.”, „c.” na tym samym wcięciu (± 1,5 pt) → elementy; samotne „a.” → tekst; „a.”, „c.” → tekst; „i.”, „iii.”
  → tekst; „h.”, „i.” → litery; „i.”, „ii.” pod „b.” → rzymskie zagnieżdżone pod „b.”; zawinięty wiersz „r. wynosi …”
  → tekst; między dwiema etykietami tylko wiersze wcięte dalej niż etykieta — inaczej ciąg przerwany
- [ ] T023 [P] [US3] Repliki (red) w `tests/LegalAgent.PdfParser.Tests/Integration/HangingLabelLayoutTests.cs`:
  rozszerz T014a (corp-5 s. 8: „15.” x 39,7, „a.” x 53,9, tekst x 68,0, ciąg a–c przechodzący na następną stronę) i
  T014b (D-A s. 43: „a/” x 68,0, „i.”…„xii.” x 82,2, tekst x 96,4) o asercje zagnieżdżonych `- a\.` / `- i\.` …
  `- xii\.`; zaktualizuj komentarz T014a („stays text”); dodaj akapit negatywny z „np.”, „tj.”, „J. Kowalski”,
  „A. Zdanie…”, samotnym „a.” — bez listy
- [ ] T024 [P] [US3] Replika (red) słowniczka w `tests/LegalAgent.PdfParser.Tests/Integration/GlossaryLayoutTests.cs`
  (corp-3 s. 3: termin „16. **klient**” x 45,4, definicja x 187,1, „a.” x 187,2 nad wierszem terminu, „b.” na linii
  bazowej terminu, tekst x 201,3) → `- 16\. **klient**` z zagnieżdżonymi `- a\.`, `- b\.`
- [ ] T025 [P] [US3] Replika (red) przy tabeli w `tests/LegalAgent.PdfParser.Tests/Integration/TablesIntegrationTests.cs`
  (D-A s. 38: element „d)” z dwukolumnowym wierszem, pod nim „i.”, „ii.” z tekstem na x +14,2) → elementy `- i\.`,
  `- ii\.`, brak wiersza `i. \| …` i brak TBL001 z tego obszaru
- [ ] T026 [US3] Wartości `LetterDot`, `RomanDot` (XML-doc) w `src/LegalAgent.PdfParser/Model/ListItem.cs` i wzorce w
  `src/LegalAgent.PdfParser/Text/ListLabelPatterns.cs` (nie przez istniejący `Roman`, który jest przyjmowany od razu)
  — green T021
- [ ] T027 [US3] Bramka w `src/LegalAgent.PdfParser/Stages/ListDetectionStage.cs`: uogólnione
  `AcceptArabicDotSequences` (ok. l. 254–310) na `LetterDot`/`RomanDot` z rozstrzyganiem dwuznacznych „i.”, „v.”, „x.”
  według ciągu; `Rank` = null dla nowych rodzajów (zagnieżdżenie z wcięcia); nowe rodzaje w
  `KeepLabelledBoldTextInLists` — green T022
- [ ] T028 [US3] `TableDetectionStage.IsHangingLabelBefore` (ok. l. 360–366) łączy `LetterDot`/`RomanDot` z tekstem w
  `src/LegalAgent.PdfParser/Stages/TableDetectionStage.cs`; `TableDocumentStage.HasListOrParagraphs` (ok. l. 374)
  pomija nowe rodzaje w `src/LegalAgent.PdfParser/Stages/TableDocumentStage.cs` — green T023–T025
- [ ] T029 [US3] Test w `tests/LegalAgent.Faq.Tests/GroundingTests.cs`: cytat obejmujący dwa elementy
  `- a\. …` / `- b\. …` jest znajdowany w jednostce; jeśli pada (red) — `FaqGrounding.ListLabel` w
  `src/LegalAgent.Faq/FaqGrounding.cs` pomija etykiety „a.”, „ii.” na początku elementu (green, jak T041a spec 007);
  jeśli przechodzi — commit tylko testu
- [ ] T030 [US3] Kontrakt 1.3.0: jak w spec 007, kontraktem jest uzupełnienie
  `specs/008-parser-fixes-after-007/contracts/markdown-output.md` (kontrakt 001 bez zmian); sprawdź, że przykłady w nim
  zgadzają się z wynikiem replik T023–T025 (popraw przykłady, nie kod), a w `CLAUDE.md` (sekcja „Rendering”) zmień
  „contract 1.2.0” na 1.3.0 z dopiskiem o etykietach „a.”/„i.”
- [ ] T031 [US3] Pełna kontrola FR-643; goldeny corp-1, corp-3, corp-5: tylko 31 etykiet z 10 ciągów jako elementy (i
  koniec tabeli zastępczej s. 38 w corp-1) — przejrzyj diff, miary nie gorsze (TBL001 nie rośnie), zbiory słów czyste,
  zaktualizuj; akty, bankowe, korpus syntetyczny (`verify`, golden chunków) bez zmian; handoff

**Checkpoint**: US3 gotowe — SC-092.

---

## Phase 6: User Story 4 — Ramki pytanie–odpowiedź czytane po kolei (Priority: P2)

**Goal**: każda ramka schematu kroków — także ze strzałkami-znakami, pojedyncza i z treścią poniżej pola — to
pogrubiony akapit z frazą, a po nim cała treść prawej kolumny; brak tabel i utraty tekstu (FR-630…FR-635, research R4).

**Independent Test**: replika strony z trzema szarymi polami (Gray ≈ 0,85, x 59,5–201,3), pogrubioną frazą 8 pt
wyśrodkowaną pionowo, prawą treścią od x 206,7 (lista „1.”, „2.”, „3.”), strzałkami-znakami między polami, bez wiersza
nagłówków; jedna fraza na wspólnej linii bazowej z tekstem po prawej, jedna przesunięta o 5 pt, w jednej ramce punkt
„3.” poniżej pola → akapit frazy + lista dla każdej ramki, w kolejności, z punktem „3.”, bez strzałek, tabel i TBL001.

- [ ] T032 [US4] Replika (red, pierwsza w US4 — commit przed T034) w `tests/LegalAgent.PdfParser.Tests/Integration/StepSchemesIntegrationTests.cs`
  (wzorowana na `BuildSchemePdf`): geometria z Independent Test powyżej; strzałka jako znak tekstowy (np. „▼” z
  dostępnej czcionki `SyntheticPdfBuilder`; jeśli brak glifu — dodaj do buildera rysowanie znaku z obszaru prywatnego
  albo użyj innego znaku bez liter i cyfr); oczekiwane wg `contracts/markdown-output.md` („Ramki schematu kroków”),
  brak TBL001; osobny przypadek kontrolny: tabela danych z liniami, białymi komórkami i pogrubionym wierszem nagłówków
  zostaje tabelą GFM (powinna przejść już dziś) — część ramek green po T034–T040 (jeśli pada po nich, popraw w tym
  samym zakresie z osobnym red/green)
- [ ] T033 [P] [US4] Testy (red) w `tests/LegalAgent.PdfParser.Tests/Unit/Stages/StepSequenceStageTests.cs` (wzorzec
  istniejących testów, `FilledAreas`, sklejony `Row`): wiersz ze znakiem strzałki (obszar prywatny Unicode, bez liter i
  cyfr) między dwoma polami → rola `Artifact`, schemat nieprzerwany; sklejony wiersz „strzałka + 3. tekst po prawej” →
  strzałka artefaktem, „3. …” objaśnieniem
- [ ] T034 [US4] Łączniki w `src/LegalAgent.PdfParser/Stages/StepSequenceStage.cs`: wiersz (albo słowa wiersza na lewo
  od prawej krawędzi pola) złożony wyłącznie ze znaków bez liter i cyfr, w zakresie x pól, między polami → `Artifact`;
  pomijany przez `Continues` i `FindHeader` — green T033
- [ ] T035 [P] [US4] Testy (red) w tym samym pliku: niczyje wiersze po prawej (wszystkie słowa na prawo od prawej
  krawędzi pola) poniżej pola, z odstępem ≤ 1,5 × wysokość wiersza, do następnego pola → objaśnienie tego pola;
  przerwa większa albo obcy tekst po lewej → koniec przelewu; ramka przechodząca przez granicę strony (na
  następnej stronie szare pole bez frazy i dalsza treść po prawej) → treść należy do tej samej ramki, fraza tylko raz
  (przed treścią z pierwszej strony), puste pole nie daje pustego akapitu
- [ ] T036 [US4] Przelew w `StepSequenceStage.cs` — green T035
- [ ] T037 [P] [US4] Testy (red) w tym samym pliku: niepogrubiony wiersz nad polem nie jest wierszem nagłówków (zostaje
  objaśnieniem / tekstem, nigdy `Artifact`); w całości pogrubiony wiersz nad pierwszym polem nadal jest nagłówkiem
  (istniejący przypadek FR-067)
- [ ] T038 [US4] `FindHeader` przyjmuje tylko wiersz w całości pogrubiony w `StepSequenceStage.cs` — green T037
  (koniec utraty tekstu, FR-634)
- [ ] T039 [P] [US4] Testy (red) w tym samym pliku: pojedyncze pole spełniające osłonę („wypełnienie niebiałe,
  szerokość < 50% strony, brak linii w pasie pola; tekst w polu ≥ 80% pogrubionych znaków”) → schemat z jedną ramką;
  pole niespełniające któregokolwiek warunku (białe, szerokie, niepogrubiony tekst, linie w pasie) → bez schematu; pole bez
  treści po prawej albo bez tekstu w polu → zwykły tekst, bez schematu
- [ ] T040 [US4] Osłona pola i pojedyncze pole (wyjątek od `MinBoxes` tylko przy spełnionej osłonie) w
  `StepSequenceStage.cs` — green T039
- [ ] T041 [US4] Pełna kontrola FR-643; goldeny firm-2, firm-4, firm-5: ramki jako akapit + treść, odzyskany punkt
  „3. Możemy poprosić…” w firm-2 (jedyne nowe słowa w porównaniu zbiorów), znikają tylko znaki strzałek; ind-1, ind-2,
  firm-3 (schematy już poprawne) bez zmian — każda zmiana w ind-*: stop i pytanie; `verify` (układ `StepSchemeLayout`)
  bez zmian; miary: ramki jako tabela / wplecione = 0; handoff

**Checkpoint**: US4 gotowe — SC-093.

---

## Phase 7: User Story 5 — Regresja, wersja i przekazanie (Priority: P1, przekrojowo)

**Goal**: dowód braku regresji, wersje i dokumentacja (FR-643…FR-647, SC-094…SC-097).

- [ ] T042 [US5] `<Version>` 1.1.0 → 1.2.0 w `Directory.Build.props`; `dotnet run --project src/LegalAgent.Corpus.Cli
  -c Release -- refresh` i `verify` — `corpus/manifest.json` zapisuje 1.2.0, poza tym bez zmian w `corpus/`; commit
  wersji i manifestu
- [ ] T043 [P] [US5] README parsera `src/LegalAgent.PdfParser/README.md`: wyliczenia „a.”/„i.”, ramki schematów ze
  strzałkami-znakami i treścią poniżej pola, wiersz kontynuacji z oznaczeniem jednostki, zaktualizowane znane
  ograniczenia (łączenie oznaczenia i tytułu kropką — `HeadingDetectionStage`, poza zakresem)
- [ ] T044 [P] [US5] `CLAUDE.md`: w opisie komunikacji etapów — `StepSequence` (łączniki-glify, przelew treści poniżej
  pola, pojedyncze pole) i stopki „N/M” w `ArtifactRemoval`; bez zmian w innych sekcjach
- [ ] T045 [US5] Testy wydajności `dotnet test LegalAgent.slnx --filter "Category=Performance"` zielone (SC-007)
- [ ] T046 [US5] Handoff w `specs/008-parser-fixes-after-007/plan.md`: tabela miar przed/po (akapity „N/M”, fałszywe
  nagłówki, etykiety „a.”/„ii.” poza listą, ramki jako tabela / wplecione, utracony tekst, TBL001) dla każdego
  dokumentu z niezerową miarą; wynik porównania zbiorów słów (utracone: tylko „N/M” i strzałki; nowe: tylko odzyskany
  tekst firm-2); lista zmienionych goldenów z uzasadnieniem; stan końcowy testów; aktualizacja pamięci projektu

---

## Dependencies & Execution Order

- **Setup (T001–T002)** → **Foundational (T003–T005)** → historie.
- **US1 (T006–T012)**, **US2 (T013–T020)**, **US3 (T021–T031)**, **US4 (T032–T041)** są od siebie niezależne
  funkcjonalnie, ale każda kończy się pełną kontrolą FR-643 i aktualizacją goldenów — realizuj **sekwencyjnie** w
  kolejności US1 → US2 → US3 → US4, żeby diff goldenów każdego commita zawierał tylko zmiany jednej historii.
  US2 i US3 dotykają `ListDetectionStage.cs` — tym bardziej sekwencyjnie.
- **US5 (T042–T046)** po wszystkich historiach (wersja raz, na końcu).
- W historii: testy red → kod green; repliki stron (T008, T014, T023–T025, T032) powstają i są commitowane jako red
  **przed** kodem swojej historii — czerwień sprawdzana uruchomieniem na kodzie sprzed historii (FR-642).
- T013 i T015 to testy zachowania, które już działa — bez kodu produkcyjnego, więc bez pary red/green (zasada I
  konstytucji dotyczy zmian zachowania); zapisać to w handoffie.

## Parallel Opportunities

- T003 równolegle z T001–T002 (inne pliki).
- US1: T006, T007, T008 (trzy pliki testów) równolegle; potem T009 → T010.
- US2: T013, T014, T015, T016 równolegle (osobne pliki lub niezależne testy); potem T017 → T018 → T019.
- US3: T021–T025 (wszystkie testy red, zanim powstanie kod US3) równolegle; potem T026 → T027 → T028.
- US4: T032 (replika red) najpierw; pary test/kod T033/T034 … T039/T040 dotyczą jednego pliku etapu — sekwencyjnie.
- US5: T043, T044 równolegle.

## Implementation Strategy

- **MVP**: Setup + Foundational + US1 — najmniejsze ryzyko, zgoda właściciela z góry, poprawia dokument detaliczny z
  zestawu prezentacji.
- Dalej przyrostowo: US2 (drzewo sekcji D-A) → US3 (kontrakt 1.3.0) → US4 (największa zmiana, trzy goldeny) → US5.
- Po każdej historii: commit(y) green z pełną kontrolą, wpis w handoffie z miarami; co nie zdąży — opis jako następny
  krok w handoffie.
