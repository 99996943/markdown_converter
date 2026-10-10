# LegalAgent.PdfParser

Biblioteka .NET 9, która zamienia PDF na model dokumentu (`LegalDocument`) i Markdown. Wejściem są polskie akty
prawne (ISAP, Dziennik Ustaw) oraz regulaminy i taryfy bankowe. Markdown trafia do chunkera RAG
(`LegalAgent.Chunking`), który tnie dokument po nagłówkach.

Wynik zawiera **wyłącznie tekst z PDF-u**. Strukturę wyrażają tylko znaczniki Markdown: nagłówki, listy, tabele,
pogrubienia i przypisy. Parser nigdy nie dopisuje słów, takich jak „Krok 1:” czy etykiety.

## Użycie

```csharp
var services = new ServiceCollection()
    .AddLegalAgentPdfParser(o => o.Tables.MergeAcrossPages = true)
    .BuildServiceProvider();
var converter = services.GetRequiredService<IPdfMarkdownConverter>();

await using FileStream pdf = File.OpenRead("regulamin.pdf");
PdfConversionResult result = await converter.ConvertAsync(pdf, new PdfConversionRequest { SourceId = "regulamin.pdf" });

string markdown = result.Markdown;          // CommonMark + GFM
LegalDocument document = result.Document;    // tytuł, preambuła, drzewo sekcji, przypisy
ConversionReport report = result.Report;     // ostrzeżenia, usunięte artefakty, liczniki
```

- `ConvertAsync` czyta strumień od bieżącej pozycji. Wywołania na jednej instancji mogą działać równolegle.
- `PdfConversionRequest` przyjmuje opcjonalnie: `SourceId`, zmianę opcji dla jednego wywołania (`ConfigureOptions`)
  i postęp per strona (`Progress`).
- Etapy potoku można podmieniać przez `AddPdfParserStage<T>()`, `ReplacePdfParserStage<TOld, TNew>()` i
  `RemovePdfParserStage<T>()`.
- **Błędy** dziedziczą po `PdfParserException`:
  - `InvalidPdfException` — pusty plik, nie-PDF albo uszkodzona struktura;
  - `PdfEncryptedException` — plik zaszyfrowany;
  - `PdfNoTextException` — brak warstwy tekstowej;
  - `PdfPageReadException` — nie da się odczytać strony;
  - `PdfLimitExceededException` — przekroczony limit rozmiaru, liczby stron albo czasu (`Limits`).
- Z `AllowPartialResult = true` nieczytelna strona jest pomijana zamiast przerywać konwersję. Wynik ma wtedy
  `IsComplete = false`.

**CLI** (`src/LegalAgent.PdfParser.Cli`, polecenie `legalagent-pdf`):
- `convert in.pdf -o out.md --report out.report.json` — Markdown i raport;
- `chunk …` — chunki JSON Lines.

Opcje heurystyk ustawia się zmiennymi `PDFPARSER__<Grupa>__<Pole>`, np. `PDFPARSER__Limits__MaxPages=5000`.

**Grupy opcji** (`PdfParserOptions`):

| Grupa | Za co odpowiada |
|---|---|
| `Limits` | limity rozmiaru, liczby stron i czasu |
| `Normalization` | wyjątki dzielenia wyrazów, odrzucanie tekstu obróconego i niewidocznego |
| `Artifacts` | wykrywanie nagłówków i stopek stron |
| `Layout` | akapity, kolumny, adnotacje boczne |
| `Headings` | progi nagłówków typograficznych, jednostki prawne |
| `Lists` | wykrywanie list |
| `Tables` | tabele, schematy kroków, tabele-dokumenty |
| `Rendering` | znaczniki stron, położenie przypisów |
| `Footnotes` | wykrywanie przypisów |

## Zasada działania

`PdfMarkdownConverter` uruchamia uporządkowaną listę etapów (`IPipelineStage`) na wspólnym `PipelineContext`.
Kolejność wyznaczają wartości z `Pipeline/StageOrder.cs`.

Etapy porozumiewają się głównie przez wiersze strony (`LayoutPage.Lines`). Oznaczają je:
- rolą `LayoutLine.Role`: `Artifact`, `Footnote`, `Table`, `ListItem`, `Heading`, `StepTitle`, `SideNote`…;
- informacją o nagłówku `LayoutLine.Heading`;
- adnotacjami tekstowymi z `Layout/LayoutAnnotations.cs`, np. `table.index`, `list.id`, `tabledoc.index`,
  `deflist.entry` / `deflist.side` (słowniczek, spec 007).

Późniejszy etap zwykle pomija wiersze, które wcześniejszy już „zajął”.

| Kolejność | Etap | Co robi |
|---|---|---|
| 100 | `PageExtractionStage` | Czyta litery, linie siatki (rulings) i obecność obrazów. Odrzuca tekst obrócony i niewidoczny. Strona bez warstwy tekstowej jest pomijana. |
| 200 | `TextNormalizationStage` | Rozwija ligatury, zamienia specjalne spacje, scala znaki łączące i normalizuje do NFC (nigdy NFKC). Podniesione cyfry po `Art. N`/`§ N` zamienia na indeks górny („§ 5¹”). |
| 300 | `LineAssemblyStage` | Składa litery w słowa i wiersze wizualne. Duże odstępy poziome dzielą wiersz na segmenty (przyszłe komórki tabel). Wyznacza strefy marginesów, styl tekstu głównego i kolumnę adnotacji bocznych. |
| 400 | `ArtifactRemovalStage` | Usuwa nagłówki i stopki powtarzające się na stronach oraz numery stron ze stref marginesów. Zapisuje je w raporcie. |
| 500 | `FootnoteDetectionStage` | Odnośniki to mniejsze, podniesione znaki. Definicje to drobny tekst pod kreską separatora na dole strony, także kontynuowany na kolejnej stronie. |
| 550 | `StepSequenceStage` | Schematy kroków: cieniowane ramki z nazwami kroków i objaśnienia po prawej. Porządkuje je w pary nazwa → objaśnienie. |
| 560 | `TableDocumentStage` | Dokument w całości złożony z wielostronicowej tabeli z pełną siatką: wąska lewa kolumna z nazwami sekcji, szeroka prawa z treścią (spec 002). |
| 600 | `TableDetectionStage` | Najpierw słowniczki (`GlossaryDetection`, spec 007): termin z lewej, definicja z prawej, linie poziome dzielone na granicy kolumn — wiersze dostają adnotacje `deflist.*` zamiast roli tabeli. Potem tabele z wierszy, których segmenty układają się w pasy kolumn (wspomagane liniami siatki). Łączy tabele przechodzące przez strony. |
| 700 | `ReadingOrderStage` | Strony dwukolumnowe: lewa kolumna przed prawą. Wiersze tabel, schematów i słowniczków zostają na miejscu. |
| 800 | `ListDetectionStage` | Pozycje list i ich kontynuacje jako drzewo (ustęp „1.” → punkt „1)” lub „1/” → litera „a)” lub „a/” → tiret „–”), wcięcia wiszące, część wspólna; wpis słowniczka jako jeden element. |
| 900 | `HeadingDetectionStage` | Jednostki prawne, nagłówki typograficzne, tytuł dokumentu i nadanie poziomów. |
| 1000 | `BlockAssemblyStage` | Akapity, łączenie akapitów przez granice stron, przeniesienia wyrazów, listy i tabele jako bloki, znaczniki zmiany strony. |
| 1100 | `DocumentBuildStage` | Drzewo sekcji z poziomów nagłówków, preambuła, zakresy stron, przypisy. |

**Dlaczego własne składanie tekstu z liter** (research R3 w `specs/001-legal-pdf-parser/research.md`):
- potrzebny jest styl każdej litery, żeby pogrubiony fragment stał się `**…**`;
- duże odstępy poziome muszą zostać jako granice komórek tabel;
- progi mają być konfigurowalne i deterministyczne.

Moduł `DocumentLayoutAnalysis` z PdfPig łączy słowa w bloki i „rozsypuje” tabele opłat, a podział XY tnie wiersze
tabel na osobne bloki.

## Od liter do Markdown

1. **Litery → słowa → wiersze.** Litery na wspólnej linii bazowej tworzą wiersz. Wiersz dzieli się na segmenty
   tam, gdzie odstęp przekracza kilka średnich szerokości spacji (`Tables.CellGapFactor`).
2. **Kolumny i kolejność czytania.** Pas wolny od tekstu przez większą część wysokości strony, z długimi wierszami po
   obu stronach (`Layout.Gutter*`), to rynna między kolumnami. Tekst czyta się kolumnami.
3. **Artefakty.** Wiersz w strefie marginesu (`Artifacts.MarginZoneRatio`), który z niewielkimi różnicami
   (`Similarity`) powtarza się na co najmniej połowie stron, jest nagłówkiem lub stopką strony. Odcisk wiersza
   (`LineFingerprint`) zastępuje cyfry znakiem `#`, więc „Strona 3” i „Strona 4” to ten sam wzorzec. Numer strony
   jest rozpoznawany w formatach `3`, `- 3 -`, `3 / 40`, `Strona 3 z 40`, `iv`.
4. **Przypisy.** Odnośnik przyklejony do słowa jest odcinany i dostaje identyfikator. Definicja trafia do sekcji,
   która pierwsza się do niej odwołuje. Numeracja `[^n]` jest globalna, w kolejności pierwszego odwołania.
5. **Tabele.**
   - Tabela powstaje, gdy co najmniej `MinRows` (3) wierszy ma po kilka komórek wyrównanych do wspólnych pasów
     kolumn (albo gdy jest siatka linii). Powstaje wtedy tabela GFM.
   - Gdy siatka jest niejednoznaczna (różna liczba komórek w wierszach), tabela zostaje zapisana wierszami
     `a \| b \| c` z ostrzeżeniem `TBL001_AmbiguousGrid`.
   - Tabela kończąca stronę łączy się z tabelą na początku następnej strony, a powtórzony nagłówek jest pomijany.
6. **Listy.** Etykieta jest zachowana dosłownie, z ucieczką, żeby Markdown nie przenumerował listy:
   `- 1\) treść`, `  - a\) treść`. Tiret ma postać `- – treść`. Punktor jest pomijany: `- treść`.
   - **Etykiety z ukośnikiem** (spec 007): „1/”, „a/” (`ListLabelKind.ArabicSlash`, `LetterSlash`) bez ucieczki:
     `- 1/ treść`. Etykieta „1.”, „1/”, „a/” w wysuniętej kolumnie (odstęp do tekstu ≤ 2 em) łączy się z tekstem
     w jedną komórkę, więc taki układ nie jest tabelą; wiersze, w których każda komórka poza ostatnią to samotna
     etykieta (także „a.”, „ii.”), są listą, nie tabelą zastępczą. Zagnieżdżenie wynika z kolumny etykiety, więc
     ustępy „1.” cytowane pod punktem „1/” są jego dziećmi.
   - **Słowniczek:** każda definicja to jeden element `- 1/ **termin** definicja…`, wyliczenia definicji są
     zagnieżdżone, a część wspólna po nich jest akapitem elementu.
7. **Nagłówki.** Rozważane są tylko wiersze, których nie zajął wcześniejszy etap, więc wiersz tabeli ani przypisu
   nigdy nie zostanie nagłówkiem.
   - **Jednostki prawne** (`Text/LegalUnitPatterns.cs`):
     - `Księga`, `Część`, `Dział`, `Rozdział`, `Oddział` z numerem;
     - `Art. 5.` i `§ 5.` z kropką po numerze, co odróżnia jednostkę od odwołania „art. 5 ust. 2”;
     - goły wiersz „§ 5” (spec 007) tylko, gdy jest wyróżniony: wyśrodkowany (w kolumnie albo na stronie) albo
       odosobniony i pogrubiony lub powiększony; nagłówek to wiersz tak jak w PDF (`#### § 5`, „§25”);
     - wyśrodkowany pogrubiony „§ 3. Porady ogólne” to jeden nagłówek z tytułem;
     - „Rozdział 3” łączy się z tytułem w następnym wierszu: `## Rozdział 3. Tytuł`;
     - wiersz „Art. 5. Treść…” jest dzielony na nagłówek i pierwszy akapit.
   - **Nagłówki typograficzne:** krótki wiersz (`MaxLength` 120), oddzielony odstępem (`GapFactor`), który jest
     powiększony (`SizeRatio` 1,15 × tekst główny), pogrubiony na tle niepogrubionego tekstu, pisany wersalikami
     lub wyśrodkowany.
     - Może mieć do `MaxLines` (2) wierszy w tym samym stylu.
     - Wiersz kończący się przecinkiem lub średnikiem jest nagłówkiem tylko wtedy, gdy dołączone wiersze go
       domykają (T067f).
     - Podpis obrazka, wiersz „Obowiązuje od…” i wpis spisu treści z kropkami prowadzącymi nie są nagłówkami.
   - **Tytuł dokumentu** to wiersz pierwszej strony zaczynający się typem aktu („USTAWA”, „OBWIESZCZENIE”…) albo
     największy nagłówek na początku dokumentu, z dołączonymi wierszami bloku tytułowego.
   - **Tabela-dokument:** nazwy sekcji z lewej kolumny stają się nagłówkami.
   - **Poziomy** (`AssignLevels`):
     - tytuł ma poziom 1;
     - jednostki strukturalne obecne w dokumencie dostają kolejne poziomy od 2 (Księga → … → Oddział);
     - artykuły i paragrafy są o poziom niżej niż najgłębsza jednostka strukturalna;
     - numerowany rozdział typograficzny („2. Rachunki…”, spec 007) jest rodzeństwem poprzedniego numerowanego
       rozdziału, a jednostka pod nim (także pod jego nienumerowanym śródtytułem) — poziom niżej;
     - nagłówki typograficzne dostają poziomy według klas wielkości czcionki;
     - poziom nigdy nie przekracza poziomu rodzica + 1, czyli w hierarchii nie ma luk;
     - nagłówek „Spis treści” nigdy nie jest rodzicem rozdziałów (T067g).
8. **Bloki.**
   - Kolejne wiersze łączą się w akapit, dopóki odstęp nie przekroczy `ParagraphGapFactor`.
   - Akapit przechodzi przez granicę strony.
   - Przeniesienie „rachun-/ku” jest scalane, z wyjątkami w rodzaju „e-mail”.
   - W miejscu zmiany strony powstaje znacznik strony.
9. **Drzewo i rendering** (`Rendering/MarkdownRenderer.cs`, kontrakt
   `specs/001-legal-pdf-parser/contracts/markdown-output.md`).
   - **Kolejność:** `#` tytuł → preambuła → sekcje rekurencyjnie (nagłówek, treść, podsekcje, przypisy sekcji).
   - **Znaczniki stron:** `<!-- page: N -->` w osobnym wierszu przed pierwszym blokiem strony, a w środku akapitu
     między słowami.
   - **Pogrubienia:** `**…**` per fragment.
   - **Ucieczka znaków** na początku wiersza (`2024\. r.`), żeby akapit nie stał się listą.
   - **Pominięta strona:** `<!-- page N skipped: no-text-layer -->`.
   - **Schemat kroków:** pogrubiona oryginalna nazwa kroku i objaśnienie jako zwykły tekst, bez dopisanych słów.

**Przykład.** Strona regulaminu:
- u góry na każdej stronie powtarza się „mBank S.A. – Regulamin rachunków”;
- poniżej jest pogrubiony, oddzielony odstępem wiersz „6. Rachunki wspólne”;
- potem akapit z wyliczeniem „1) … a) … b) …”;
- na dole stopka z numerem strony w strefie marginesu.

Wynik:

```markdown
<!-- page: 13 -->
## 6. Rachunki wspólne

- 1\) Posiadacze rachunku wspólnego zgadzają się na to, aby każdy z nich mógł samodzielnie:
  - a\) dysponować pieniędzmi na rachunku,
  - b\) wypowiedzieć umowę.
```

Nagłówek i stopka trafiają do `Report.RemovedArtifacts`, nie do treści.

## Ostrzeżenia i raport

| Kod | Znaczenie |
|---|---|
| `PDF001_NoTextLayer` | Strona bez warstwy tekstowej (np. skan) została pominięta. |
| `PDF002_PageReadError` | Strony nie dało się odczytać; pominięta (przy `AllowPartialResult`). |
| `TXT001_UnmappedGlyphs` | Znaki bez odwzorowania Unicode na stronie. |
| `IMG001_ImagesIgnored` | Na stronie są obrazy; ich treść nie jest odczytywana. |
| `TBL001_AmbiguousGrid` | Tabela bez jednoznacznej siatki kolumn, zapisana wierszami z separatorem ` \| `. |
| `FTN001_OrphanFootnote` | Definicja przypisu bez odnośnika w tekście. |

`ConversionReport` zawiera też:
- liczbę stron i pominięte strony;
- usunięte artefakty (wzorzec, rodzaj, strony);
- liczby nagłówków per poziom, list, tabel, tabel zastępczych i przypisów;
- tabele-dokumenty i czas konwersji.

CLI zapisuje go jako JSON (`--report`).

## Testy i goldeny

Testy są w `tests/LegalAgent.PdfParser.Tests`:
- `Unit/` — etapy, tekst, renderowanie;
- `Integration/` — tabele, listy, nagłówki, schematy, tabele-dokumenty, CLI;
- `Corpus/` — goldeny: akty z ISAP w `acts/`, syntetyczne regulaminy w `banking/`, przypadki błędów w `errors/`.

Zasady:
- **Goldeny** `*.expected.md` porównują pełny Markdown. `UPDATE_GOLDEN=1` je przepisuje, a przy niezgodności test
  zapisuje `*.actual.md`. Zmiana goldenu wymaga zgody właściciela (FR-163).
- **Błędy z prawdziwych PDF-ów** odtwarza się jako syntetyczną replikę strony (`Fixtures/`: `PageSketch`,
  `LayoutFactory`, `TableSheet`, generator syntetycznych PDF-ów).
- **Prywatny korpus:** `LEGALAGENT_PRIVATE_CORPUS=<katalog>` uruchamia prywatne PDF-y właściciela z ich goldenami.
  Trzeba to zrobić przed każdym commitem zmieniającym parser.
- **Korpus syntetycznego banku** (`corpus/`): po zmianie parsera uruchom `LegalAgent.Corpus.Cli refresh`, a potem
  `verify` (to robi CI).

## Znane ograniczenia

- **Numery stron w treści:** w regulaminie rachunków mBanku numery stron „13/36” zostają w tekście (czasem w środku
  zdania), choć format `N/M` jest wśród rozpoznawanych wzorców; przyczyna nie została jeszcze zbadana.
- **Wiersze tabel jako nagłówki:** pogrubiony wiersz tabeli bez siatki, np. nagłówek kolumn, bywa uznany za nagłówek
  typograficzny („## waluta transakcji kurs referencyjny…”). Zakończenie schematu kroków bywa nagłówkiem
  („## i… już dziecko może korzystać z karty”).
- **Przypisy w niektórych bankowych PDF-ach:** trafiają do jednego bloku razem z numerami stron, a znak odnośnika
  zostaje przyklejony do słowa („bilansujący2”).
- **Spis treści** zostaje w treści jako lista lub akapit z kropkami prowadzącymi (jego wpisy nie są już nagłówkami).
- **Wyliczenia „a.”, „ii.”** (litera lub rzymska z kropką) nie są etykietami list: nie tworzą tabeli, ale zostają
  tekstem elementu nadrzędnego.
- **Ramki pytanie–odpowiedź** w regulaminach dla firm: pytanie z lewej kolumny ramki bywa wplecione w tekst
  elementu listy z prawej.
- **Odwołanie „Rozdział I.” na początku zawiniętego wiersza** bywa uznane za nagłówek rozdziału (regulamin
  zintegrowanego rachunku).
- **Obrazy** nie są odczytywane (brak OCR), tylko zgłaszane (`IMG001`).
- **Nagłówek wciągnięty do tabeli:** w regulaminie kart dla firm (część II, rozdz. 3) nagłówek obok ramek „etap”
  trafia do tabeli.
