# Research: Poprawki parsera po spec 007

Badania na prywatnych PDF-ach (poza git) i na całym korpusie: 19 dokumentów prywatnych, 6 aktów `Corpus/acts`,
`Corpus/banking` i ok. 110 PDF-ów korpusu syntetycznego. Sondy (PdfPig i prawdziwy potok z dodatkowymi etapami zrzutu)
w scratchpadzie sesji, nie są częścią repozytorium. Oznaczenia dokumentów jak w spec 007 (D-A = corp-1).

## R1 — Numery stron „N/M” (US1, FR-600–FR-602)

**Przyczyna**: tekst nie jest problemem — `PageNumberPatterns` (`Text/PageNumberPatterns.cs:85`) już rozpoznaje
„N/M” jako numer strony N. Problemem jest położenie: `ArtifactRemovalStage` (`:55-71`) rozważa tylko wiersze w strefie
marginesu, `Box.Top ≥ H·(1 − MarginZoneRatio)`, przy `MarginZoneRatio` = 0,08 (`Options/PdfParserOptions.cs:61`), a
stopka „N/M” leży tuż nad tą strefą.

| | ind-2 (A4 pion, 842 pt) | firm-1 (A4 poziom, 595 pt) |
|---|---|---|
| Strony z „N/M” | 2–36 (bez okładki) | 2–7 (bez okładki) |
| Góra / dół wiersza | 0,9055 / 0,9142 H | 0,9048 / 0,9171 H |
| Poziomo | do prawej, prawa krawędź 541,1 | prawa krawędź 801,9 |
| Krój | 8 pt, zwykły; jedno słowo, sam w wierszu, najniższy wiersz strony | jak obok |
| M / przesunięcie N | M = 36 = liczba stron, N = numer strony | M = 7 = liczba stron, N = numer strony |

Odstęp nad stopką waha się od 2 pt (ind-2 s. 19, tekst kończy się na 0,9032 H) do 205 pt — odstęp nie jest sygnałem;
„najniższy wiersz strony” jest prawdziwy zawsze. Wszystkie inne dokumenty ze stopką „N/M” (corp-1/2/3/5, firm-2…5,
ind-1/4, syntetyczne REG-*/ZAT-* z układu `tabela-dokument`) mają ją w strefie (≥ 0,936 H) i już ją tracą. W całym
korpusie nie ma samodzielnego wiersza `^\d{1,3}/\d{1,3}$` w treści; „N/71” w nagłówku `acts/ustawa-o-sluzbie-cywilnej`
jest częścią dłuższego wiersza i usuwa go klaster powtarzalnych nagłówków. „3/49” nie może zostać etykietą listy
(`ListLabelPatterns.cs:46-56` wymaga tekstu po etykiecie, `ArabicSlashLabel` — końcowego „/”).

- **Decision**: osobny przebieg w `ArtifactRemovalStage` po `RemovePageNumbers`, z tym samym wpisem raportu
  (`ArtifactKind.PageNumber`). Kandydat: cały tekst wiersza pasuje do `^\d{1,3}\s*/\s*\d{1,3}$`, wiersz leży w
  szerszym pasie (dolne lub górne 15% strony, nowa opcja `ArtifactOptions.PageNumberBandRatio` = 0,15 z walidacją) i
  jest najniższym (w pasie górnym — najwyższym) wierszem strony, bez innego wiersza na tej wysokości. Grupa kandydatów
  jest usuwana tylko w całości, gdy: występuje na ≥ 2 stronach, ma tę samą względną wysokość (`PositionTolerance`), to
  samo M równe liczbie stron dokumentu i to samo przesunięcie N − numer strony. Inaczej nic nie jest usuwane.
- **Rationale**: usuwa dokładnie 41 wystąpień (35 + 6); żaden wiersz innego goldenu ani korpusu syntetycznego nie
  spełnia warunków, więc `Corpus/acts`, `Corpus/banking` i `corpus/` się nie zmieniają; warunek M = liczba stron
  wyklucza ułamki, daty i numery uchwał.
- **Alternatives considered**: podniesienie `MarginZoneRatio` do 0,10 (działa z zapasem 0,005 H i wciąga wiersze treści
  z 0,90–0,92 H do klastrów stopek i usuwania gołych liczb — ryzyko zmian goldenów); dołączenie kandydatów do puli
  `RemovePageNumbers` (może przesunąć wybierane przesunięcie, brak sprawdzenia M); poprawka w późniejszym etapie
  (artefakty należą do etapu 400).
- **Ryzyka**: dokument z osobną numeracją załącznika (M ≠ liczba stron) nie zostanie oczyszczony — zgodne z FR-601.
- **Testy**: `Unit/Stages/ArtifactRemovalStageTests.cs` (`LayoutFactory`): stopka na 0,905 H, M ≠ liczba stron, jedna
  strona, N nie rośnie, „1/2 kwoty” / „12/2024” / „1/ etykieta” w treści, „N/M” we wspólnym wierszu z innym tekstem.
  Replika: `Integration/ArtifactCleanupIntegrationTests.cs` z `SyntheticPdfBuilder` (`src/LegalAgent.Corpus/Pdf/`;
  wbudowany `PageNumberFooter` drukuje 40 pt od dołu, czyli w strefie — w replice `.Text(…, "n/3", size: 8)` na
  ≈ 0,905 H i wiersz treści kończący się na ≈ 0,903 H).

## R2 — Fałszywe nagłówki w D-A (US2, FR-610–FR-612)

### (b) Termin słowniczka „15/ …/” — już naprawione

Na s. 4 D-A wiersz terminu ma rolę elementu listy z adnotacjami `deflist.entry=15; deflist.side=term`; golden (wiersz
83) to `- 15/ **umowa rachunku bankowego/ umowa** Zintegrowana…` w § 2 — nie nagłówek. Termin jest zawinięty na drugi
pogrubiony wiersz („umowa”). Wpis w handoffie spec 007 (plan.md:192–193) powstał przed US3 spec 007, które to
naprawiło; osłony: `ListDetectionStage.cs:135` (wiersz `deflist` nigdy nie jest „HeadingLike”), a wiersz z rolą
ListItem nie trafia do `HeadingDetectionStage.Collect` (`:97`).

- **Decision**: bez zmiany kodu; test regresji w `Integration/GlossaryLayoutTests.cs` (termin kończący się „/”,
  zawinięty na drugi pogrubiony wiersz). Spec poprawiony (postać terminu, SC-091).

### (a) „Rozdział I (Prowadzenie…)” — prawdziwa usterka

Golden: wiersz 746 `### Rozdział I. (Prowadzenie…). Jest on załącznikiem…` (Chapter, poziom 3) między „16. Prowizje”
(poziom 3) a § 64; wiersz 744 `1\. Prowizje…` jako akapit, nie element listy; „17.” i „18.” na poziomie 4, § 66–80 na
poziomie 5 (powinno być 3 i 4). W PDF **nie ma kropki po „I”** — dopisuje ją `HeadingDetectionStage.cs:531`
(`$"{unit.Designation}. {heading.Title}"`); zniknie razem z fałszywym nagłówkiem.

| s. 14, tekst 7 pt, interlinia 10,0 | poprzedni wiersz | wiersz „Rozdział I” | prawdziwy rozdział „16.”/„17.” |
|---|---|---|---|
| krój | Regular 7,0 | Regular 7,0 | Bold 9,0 |
| lewa / prawa krawędź | 39,7 / 555,5 (pełna kolumna) | 54,7 / 449,5 | 39,7 |
| odstęp nad | — | 10,0 (1,0 × interlinia) | 24,8 (2,5 ×) |
| koniec wiersza | „Korporacji –” | | |

x = 54,7 to **początek tekstu za etykietą „1.”** poprzedniego wiersza, nie jego lewa krawędź — reguła porównująca tylko
lewe krawędzie przeoczyłaby ten przypadek.

Ścieżka błędu: (1) `ListDetectionStage.cs:131` ustawia `LegalUnit=true` dla każdego dopasowania jednostki (nie gołego
„§”), co przerywa ciąg „N.” (`:262`) — „1.” nie jest przyjęte, a `Visit` zamyka listę (`:370`); (2)
`HeadingDetectionStage.cs:195-202` — `LegalUnitPatterns.TryMatch` (Structural, `LegalUnitPatterns.cs:118`), a osłona
FR-162 `ContinuesSentence` (`:369-377`) nie działa, bo poprzedni wiersz kończy się „–” (nie literą, `:375`) i
`char.IsLower(match.Rest[0])` nie przechodzi dla „(” (`:199`); (3) `AssignLevels` — przy obecności Part i Chapter
rozdział dostaje poziom 3 (`:554-556`), otwarty Chapter (ranga 4) nie jest zamykany przez „17.” (ranga 6, `:606`), więc
„17.” schodzi pod niego (`:620`).

- **Decision**: wewnętrzny pomocnik „wiersz kontynuacji” (`Text/` lub `Layout/`), użyty w dwóch miejscach:
  `ListDetectionStage.cs:131` (wiersz zostaje kontynuacją elementu, „1.” jest przyjęte) i
  `HeadingDetectionStage.cs:199` (akapity poza listami). Wiersz pasujący do wzorca jednostki jest kontynuacją, gdy
  wszystkie warunki: wiersz powyżej na tej samej stronie to tekst (Unknown, ListItem, ListContinuation); odstęp
  ≤ 1,2 × interlinia; ten sam dominujący rozmiar (± 0,5 pt) i to samo `AllBold`; **pierwsze słowo oznaczenia ma to samo
  pogrubienie co ostatnie słowo poprzedniego wiersza**; poprzedni tekst nie kończy się `. : ; ! ?`; lewa krawędź ≈ lewa
  krawędź poprzedniego wiersza albo początek jego tekstu za etykietą (± 2 pt); wiersz nie jest wyśrodkowany. Uogólnia
  `ContinuesSentence`; dotychczasowa reguła FR-162 (mała litera) zostaje.
- **Rationale**: w składzie 7 pt regulaminów dla firm zawinięte wiersze są typograficznie nieodróżnialne od tekstu;
  prawdziwe nagłówki jednostek różnią się pogrubieniem, rozmiarem, wyśrodkowaniem albo odstępem. Skan reguły na
  wszystkich nagłówkach jednostek (19 prywatnych, 6 aktów, cały korpus syntetyczny — 4 869 nagłówków): **dokładnie 1
  trafienie** — wiersz D-A. Prawie-trafienia (3 z 4 warunków): „Art. 42.” po „3. (uchylony)” w
  `ustawa-o-sluzbie-cywilnej` i w syntetycznym `dz-u-2026-823-reklamacje` (odstęp 19,9 ≤ 20,2, poprzedni wiersz kończy
  się „)”) — ratuje je tylko pogrubione słowo oznaczenia; dlatego warunek pogrubienia jest na poziomie słowa, a nie
  tylko `AllBold`.
- **Alternatives considered**: samo usunięcie warunku „mała litera / litera na końcu” w `ContinuesSentence` (nie łapie
  wcięcia za etykietą i nie naprawia listy); wymóg kropki po liczebniku (nagłówki „Rozdział 3” drukuje się bez niej);
  poprawka w BlockAssembly (za późno — poziomy już złe).
- **Poza zakresem**: łączenie oznaczenia i tytułu kropką (`:531`) w prawdziwych nagłówkach jest zachowaniem kontraktu
  001 i dotyczy wielu goldenów; nie zmieniane (zapisane jako znane ograniczenie w handoffie).
- **Oczekiwane zmiany goldenów**: tylko prywatny D-A (element „1.” z odwołaniem jako element listy, rozdziały 17–18 i
  § 66–80 o poziom wyżej). Akty, bankowe i `verify` bez zmian.
- **Testy**: replika w `Integration/CorpusLayoutsIntegrationTests.cs` (obok T085): pogrubiony 9 pt „16.”, wyśrodkowany
  „§ 63”, element „1.” 7 pt na pełną szerokość kończący się „–”, kontynuacja w kolumnie tekstu z tą samą interlinią,
  „2.”, „3.”, potem „17.”, „§ 66”, „18.”; przypadek ochronny: pogrubione „Art. 42.” po „(uchylony)” zostaje nagłówkiem.
  Testy jednostkowe w `Unit/Stages/HeadingDetectionStageTests.cs` i `ListDetectionStageTests.cs`.

## R3 — Wyliczenia „a.” i „i.” (US3, FR-620–FR-624)

Inwentarz goldenów (etykiety zaczynające wiersz PDF):

| Dokument | Ciągi literowe (etykiety) | Ciągi rzymskie (etykiety) | Gdzie dziś |
|---|---|---|---|
| corp-1 (D-A) | 0 | 4 doklejone (18) + 1 para w tabeli zastępczej (2) | i–ii, i–xii, i–ii; `i. \| …`, `ii. \| …` (s. 38) |
| corp-3 | 1 (a–b) | 0 | definicja słowniczka |
| corp-5 | 4 (9) | 0 | a–c, a–b ×3 (także przez stronę 8→9) |
| pozostałe prywatne, akty, bankowe, korpus syntetyczny | 0 | 0 | |

Razem 10 ciągów, 31 etykiet, w 3 dokumentach (**firm-2 nie ma żadnego** — spec poprawiony). Podobne tokeny „litera +
kropka”: przeważa „r.” (rok): prywatne 146, akty 916, syntetyczny 2 265; poza tym „m.”, „d.”, „u.”. Żaden wiersz
goldenu (poza tabelą z D-A) nie zaczyna się pojedynczą literą z kropką; ryzykiem jest zawinięcie „…2024” / „r. …”
(„r.” może zaczynać wiersz PDF) — blokuje je reguła ciągu.

Pomiar (7 pt): każda etykieta zaczyna własny wiersz PDF i jest osobnym słowem; zawsze wysunięte wcięcie, wyliczenie o
≈ 14,2 pt (jedna kolumna etykiet) w prawo od etykiety rodzica; odstęp etykieta–tekst 3,2–10,9 pt (1,1–1,56 em, czyli
≥ `MinCellGapEm` = 1,0 i ≤ `MaxHangingLabelGapEm` = 2,0). Przykłady: rodzic „a)” 190,0 → etykieta 204,2 → tekst
218,3; „a/” 68,0 → 82,2 (i.–xii.) → 96,4; „15.” 39,7 → 53,9 → 68,0; w corp-3 „a.” leży nad wierszem terminu, „b.” na
jego linii bazowej.

**Przyczyna**: `ListLabelPatterns.Classify` (`:77-150`) zna „a)” (`:191`), „a/” (`:199`) i rzymskie z „)” o ≥ 2 znakach
(`:187`); „a.”, „i.”, „ii.” nie są etykietami (decyzja spec 007: zostają tekstem, plan.md:175; T014a/T014b). Wiersz
bez etykiety → `VisitUnlabelled` (`ListDetectionStage.cs:490-495`) dokleja go do otwartego elementu, bo x > etykieta
rodzica. Na s. 38 D-A `CellsOf`/`IsLabel` (`TableDetectionStage.cs:313-347`) i `IsHangingLabelBefore` (`:360-366`) nie
łączą „i.” z tekstem → wiersze wielokomórkowe wciągnięte do tabeli zastępczej (TBL001). `IsHangingLabel` (`:596-599`)
już akceptuje „a.” i „ii.”, ale tylko w `IsHangingList`. Ucieczka: `EscapeListLabel` (`MarkdownEscaper.cs:59-72`) już
daje `a\.` i `ii\.`.

- **Decision**: nowe rodzaje `ListLabelKind.LetterDot` (`^[a-z]\.$`, porządek 1–26) i `RomanDot` (małe rzymskie
  `i`–`xxxix` + „.”); „i.”, „v.”, „x.” dwuznaczne — dopasowanie niesie oba odczyty, rozstrzyga przebieg ciągu. Nie
  używać istniejącego `Roman` (jest przyjmowany od razu, `:144`). Bramka: uogólnione `AcceptArabicDotSequences`
  (`:254-310`, widzi cały dokument po segmentach) — kandydat przyjęty tylko, gdy najbliższy kandydat na tym samym
  wcięciu (tolerancja 1,5 pt) przed nim lub po nim kontynuuje ciąg, a między nimi są tylko wiersze wcięte dalej niż
  etykieta; „i.” po „h.” — litera, inaczej rzymska, jeśli dalej „ii.”; samotne „a.”, „r.”, ciągi „a., c.” / „i., iii.”
  odrzucone; „np.”, „tj.”, wielkie inicjały nie pasują do wzorca. Zagnieżdżenie: `Rank` zwraca null dla nowych rodzajów
  (`:324-331`), więc `IsParent` używa wcięcia (`:558-561`) — działa pod „1.”, „1/”, „a)”, „a/” i terminem słowniczka.
  Dopisać nowe rodzaje do `KeepLabelledBoldTextInLists` (`:200`) i `IsHangingLabelBefore` (`:364`, etykieta łączy się
  z tekstem → koniec tabeli zastępczej s. 38); wyłączyć z `TableDocumentStage.HasListOrParagraphs` (`:374`), który
  liczy każde dopasowanie etykiety (inaczej wiersze „r.” zmienią heurystykę tabel-dokumentów). `GlossaryDetection`
  (`:188`) filtruje rodzaje — bez zmian.
- **Rationale**: układ jest taki sam jak dla „a)”, „a/” (spec 007) — różni się składnia; bramka ciągu na poziomie
  dokumentu już istnieje dla „1.”. 0 wystąpień w aktach, bankowych i korpusie syntetycznym.
- **Alternatives considered**: reguła tylko w `ListDetectionStage` bez nowego rodzaju (gubi zagnieżdżenie po wcięciu i
  ucieczkę); akceptacja „a.” bez bramki (fałszywe listy z „r.”); przerabianie tekstu po renderowaniu (kruche).
- **Kontrakt**: dwie nowe publiczne wartości enum i elementy `- a\.` / `- ii\.` → MINOR 1.2.0 → **1.3.0**.
- **Testy**: `Unit/Text/ListLabelPatternsTests.cs` („a.”, „ii.”, „xii.”; nie-etykiety „r. wynosi”, „np. x”, „A.
  Zdanie”); `Integration/HangingLabelLayoutTests.cs` — rozszerzyć T014a (replika corp-5 s. 8) i T014b (replika D-A
  s. 43) o zagnieżdżone `- a\.` / `- i\.`; przypadek słowniczka w `GlossaryLayoutTests.cs` (corp-3 s. 3); przypadek
  przy tabeli w `TablesIntegrationTests.cs` (D-A s. 38); akapit negatywny („np.”, „tj.”, „J. Kowalski”, „A. …”, samotne
  „a.”, dwuznaczność „h., i.”). Komentarz T014a („stays text”, `HangingLabelLayoutTests.cs:148`) do aktualizacji.

## R4 — Ramki pytanie–odpowiedź (US4, FR-630–FR-635)

firm-2 ma **15 nazwanych ramek w 4 schematach** (plus 2 puste pola przenoszące ramkę przez stronę):

| Schemat | Strony | Dziś |
|---|---|---|
| A (§ 4 pkt 2) | 5–6 | 4 ramki: wplecione w elementy listy, fraza rozcięta, osierocony akapit **rachunek** przerywa element a); jedna poprawna; **punkt „3. Możemy poprosić o dokument tożsamości…” ginie** |
| B (§ 4 pkt 3) | 6 | 3 ramki w jednej tabeli GFM; „Potwierdzamy, że otworzyliśmy rachunek” wplecione |
| C (§ 14 pkt 2) | 10 | 4 ramki w jednej tabeli GFM + osierocone **limit** |
| D (§ 21) | 13–14 | pytania „Jak i kiedy…?”, „Co z tego wynika?” — fragmenty między wierszami elementów / po elemencie; „Jakie zmiany…” poprawne |

Razem: 7 ramek jako tabele GFM (2 tabele), 6 wplecionych lub w złej kolejności, 2 poprawne, 1 utracony punkt. Inne:
firm-4 s. 7 (3 ramki ze strzałkami → tabela GFM), firm-5 s. 14–20 (ok. 25 pól, 18 strzałek, usterki mieszane); ind-1,
ind-2, firm-3 — ten sam układ bez strzałek-glifów, już poprawne; corp-1 s. 26, corp-5 s. 22 — jedno wysokie pole z
kilkoma frazami, inny kształt, poza zakresem.

Geometria (A4): lewe pole wypełnione szarym (Gray 0,851), bez obrysu i linii, x 59,5–201,3 (lub –208,2); fraza Bold
8 pt od x 64,9, 1–3 wiersze, **wyśrodkowana pionowo w polu**; prawa treść 8 pt od x 206,7 (lub 212,9), zagnieżdżone
elementy 224,7/242,7, interlinia 10, wyrównana do góry ≈ 8 pt pod górą pola; wiersze frazy albo na wspólnej linii
bazowej z wierszami prawej strony (sklejane w jeden wiersz), albo przesunięte o 5 pt (przeplatane); między polami
20 pt z **18-punktową strzałką czcionki Wingdings3** (x ≈ 122–156) — staje się wierszem ze znakiem z obszaru
prywatnego; brak wiersza nagłówków (ind-2 ma „Kolejność działań | Wyjaśnienie”); pole bywa niższe niż treść (s. 5:
pole kończy się na 670,4, punkt 3 sięga 689). Słowniczek ma linie poziome dzielone na wspólnym x i brak wypełnienia;
prawdziwa tabela danych (s. 8) ma linie 0,5 pt na każdej kolumnie, pogrubiony wiersz nagłówków, białe wypełnienia
(odrzucane przez `IsWhite`, `PageExtractionStage.cs:260`) i niepogrubioną pierwszą kolumnę. W całym korpusie
prywatnym każde niebiałe pole po lewej zawiera pogrubiony tekst.

**Przyczyna**: istnieje etap dla tego układu — `StepSequenceStage` (FR-067), renderujący ramkę jako pogrubiony akapit
i treść po nim (`BlockAssemblyStage.cs:116-131`). W firm-2 zawodzi na trzy sposoby: (1) **strzałki dzielą schemat** —
`Continues` (`StepSequenceStage.cs:152-163`) traktuje wiersze strzałek jako obce wiersze między polami, pojedyncze
pola nie spełniają `MinBoxes = 2` (`:24`, `:44`); testy i korpus mają strzałki tylko jako obrazy
(`StepSchemesIntegrationTests.cs:39,52`, `StepSchemeLayout.cs:89`); (2) **treść poniżej pola nie ma właściciela** —
własność to „środek wiersza w polu” (`:67`); (3) **ta treść staje się wierszem nagłówków** — `FindHeader` (`:85-121`)
bierze niczyje wiersze nad następnym polem (strzałka + „3.” po prawej) za nazwy kolumn, a `Emit` oznacza je jako
artefakt (`:237-242`) — stąd utrata tekstu. Dalej: niczyje wiersze z lewą i prawą częścią na wspólnej linii bazowej
dają wiersze wielokomórkowe → bez rynny `IsTextColumns` = false (`TableDetectionStage.cs:605-610`) → tabela GFM bez
siatki (`:497-581`); przeplatane wiersze frazy → `ListContinuation` otwartego elementu (`ListDetectionStage.cs:563-567`).
ReadingOrder nie bierze udziału (brak rynny; wiersze kroków wyłączone, `ReadingOrderStage.cs:382-383`).

- **Decision**: rozszerzyć `StepSequenceStage`, bez nowego etapu ani adnotacji:
  1. **Łączniki**: wiersz (albo słowa wiersza na lewo od prawej krawędzi pola) złożony wyłącznie ze znaków bez liter i
     cyfr (obszar prywatny, czcionki symboli), w zakresie x pól, między polami → artefakt-łącznik, pomijany przez
     `Continues` i `FindHeader`; rozdziela też sklejony wiersz strzałka + „3.”.
  2. **Przelew**: niczyje wiersze po prawej (wszystkie słowa na prawo od prawej krawędzi pola) poniżej pola, do
     następnego pola, obcego tekstu po lewej albo końca strony, przy odstępie ≤ 1,5 × wysokość wiersza → objaśnienie
     tego pola.
  3. **Wiersz nagłówków**: musi być w całości pogrubiony (prawda dla wszystkich znanych schematów i `TableSheet`) —
     koniec utraty tekstu.
  4. **Osłona FR-633**: pole niebiałe, węższe niż 50% strony, tekst lewej strony ≥ 80% pogrubionych znaków, brak linii
     w pasie pola; tabele danych odpadają przez białe wypełnienia i linie.
  5. **Pojedyncze pole**: schemat z jednym polem przyjmowany, gdy spełnia osłonę z pkt 4 (FR-633), mimo `MinBoxes`.
  Tekst lewej strony — jeden pogrubiony akapit przez istniejącą ścieżkę StepTitle.
- **Rationale**: etap, adnotacje `step.*` i renderowanie już istnieją; dalsze etapy już pomijają wiersze `step.*`.
- **Alternatives considered**: nowa adnotacja `frame.side` w `GlossaryDetection` (etap 600 — za późno; słowniczki
  opierają się na liniach); przestawianie kolejności w `ReadingOrder` (brak rynny, nie zatrzyma TableDetection, dubluje
  FR-067).
- **Ryzyka**: w korpusie syntetycznym wypełnienia rysuje tylko `StepSchemeLayout` (pola obejmują tekst, strzałki to
  obrazy, nagłówek pogrubiony) — `verify` powinien zostać identyczny; `dwie-kolumny`, `procedura`, `tabela-dokument`
  nie rysują wypełnień. Zmienią się goldeny firm-2, firm-4, firm-5 (dokumenty dla firm — FR-644); ind-1, ind-2, firm-3
  muszą zostać bez zmian.
- **Testy**: `Unit/Stages/StepSequenceStageTests.cs` (wzorzec `:19-90`, `FilledAreas`, sklejony `Row`): wiersz strzałki
  między polami, przelew poniżej pola, brak nagłówka, niepogrubiony wiersz nad polem nie jest nagłówkiem, pojedyncze
  pole. Replika: wzorowana na `BuildSchemePdf` (`StepSchemesIntegrationTests.cs:20-65`) — bez nagłówka, strzałki
  tekstowe, lista po prawej, jedna fraza na wspólnej linii bazowej, jedna przesunięta, punkt poniżej pola.
  `SyntheticPdfBuilder` rysuje pogrubiony tekst, `FilledRect(gray)`, linie i obrazy, ale nie ma czcionki symboli — w
  replice znak strzałki z dostępnej czcionki (np. „▼”, jeśli Noto go ma) albo znak z obszaru prywatnego w testach
  jednostkowych.

## R5 — Wersje, kontrakt i kolejność

- **Decision**: kontrakt Markdown 1.2.0 → **1.3.0** (MINOR: nowe `ListLabelKind.LetterDot`, `RomanDot`; pozostałe
  zmiany to poprawki błędnego wyniku). `<Version>` w `Directory.Build.props` 1.1.0 → **1.2.0** (MINOR — rozszerzenie
  publicznego enum); `generate`/`refresh` zapisuje wersję w `corpus/manifest.json`, `verify` czysty.
- **Kolejność**: US1 (najmniejsze ryzyko, zgoda z góry) → US2 → US3 → US4 (największa zmiana, trzy goldeny) →
  zamknięcie (wersja, manifest, README parsera, CLAUDE.md, handoff z miarami). Pomocnik miar (`LayoutMetrics`)
  rozszerzony na początku o miary 2–4 i porównanie zbiorów słów, żeby punkt odniesienia był zmierzony przed pierwszą
  zmianą.
