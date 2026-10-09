# Research: Podział dokumentów na fragmenty (spec 004)

Decyzje fazy 0. Każda ma: decyzję, uzasadnienie, odrzucone alternatywy. Odwołania do kodu dotyczą stanu
`main` po PR #4.

## R1. Renderowanie treści fragmentu istniejącym rendererem

**Decyzja**: dla każdego fragmentu budujemy w pamięci mały `LegalDocument`: `Title = null`, pusta
preambuła (albo preambuła = bloki fragmentu wstępu), jedna `Section` z tym samym `Level`, `Kind`,
`HeadingText` co jednostka, `Blocks` = bloki części (z przyciętymi listami i tabelami, R3), `Footnotes` =
przypisy części (R5), `Children = []`. Renderujemy go publicznym `IMarkdownRenderer.Render` z
`RenderingOptions { PageMarkers = false }` (pozostałe opcje jak w konwersji). `SkippedPageBlock` nie
trafia do fragmentów (jego komentarz nie jest tekstem PDF). Gdy część zaczyna się od pozycji
zagnieżdżonej listy (R3), jej pozostałe pozycje każdego poziomu są osobnymi `ListBlock`-ami (od
najgłębszego) renderowanymi od kolumny 0 — bez wcięcia, bo wcięcie ≥ 4 spacji na początku treści
CommonMark czyta jako blok kodu (spec FR-222, doprecyzowanie z T006); zagnieżdżenie wewnątrz pozycji
zostaje, a położenie części opisują `listLabels`.

**Uzasadnienie**: renderer jest czystą funkcją modelu; nie trzeba zmieniać jego API ani wyniku (FR-204,
SC-046). Heading, escaping, przypisy i tabele GFM wychodzą identyczne jak w pliku `.md`, więc treść
fragmentów jest podciągiem Markdown dokumentu (poza znacznikami stron), co upraszcza test FR-234.

**Alternatywy**: (a) cięcie gotowego Markdown po liniach — odrzucone przez opis featury (podział na
modelu, nie na Markdown) i kruche przy listach/tabelach; (b) nowa metoda `RenderSection` w parserze —
niepotrzebna, publiczny `Render` wystarcza; zostaje jako awaryjna opcja addytywna.

## R2. Jednostki

**Decyzja**: przechodzimy drzewo sekcji w kolejności dokumentu. Jednostką jest: wstęp
(`LegalDocument.Preamble` + `PreambleFootnotes`, gdy niepusty) oraz każda `Section`, która ma własne
bloki (poza `SkippedPageBlock`) lub własne przypisy. Sekcja bez własnej treści nie tworzy jednostki, ale
jej nagłówek jest w `sectionPath` potomków (z `Section.Path`). Rodzaj jednostki = `SectionKind` albo
`Preamble`. Tytuł dokumentu (`# …` z `LegalDocument.Title`) nie jest jednostką — trafia do metadanych
(`detectedTitle`), spec FR-234.

**Uzasadnienie**: wprost z FR-220; zero heurystyk struktury (FR-203) — model parsera decyduje, co jest
artykułem, paragrafem, sekcją taryfy czy procedury.

**Alternatywy**: łączenie bardzo krótkich jednostek z sąsiednimi — odrzucone (spec, przypadki brzegowe).

## R3. Algorytm podziału

**Decyzja**: część to ciąg „atomów” jednostki, pakowany zachłannie do limitu (domyślnie 2000 znaków
UTF-16 wyrenderowanej treści, łącznie z nagłówkiem i przypisami części):

1. Najpierw próbujemy całą jednostkę; jeśli mieści się w limicie — jeden fragment (FR-221).
2. W przeciwnym razie idziemy po blokach. Blok, który mieści się w pozostałym miejscu, dodajemy w
   całości. Blok, który się nie mieści:
   - `ParagraphBlock` — atom: zamyka bieżącą część (jeśli niepusta) i otwiera nową; jeśli sam przekracza
     limit, tworzy część z `exceedsLimit = true` (FR-224);
   - `ListBlock` — rozkładamy na pozycje najwyższego poziomu; pozycja, która się nie mieści, jest
     rozkładana rekurencyjnie: jej własny tekst (z akapitami-dziećmi) jest atomem, a jej zagnieżdżone
     listy — kolejnymi pozycjami. Część zaczynająca się od pozycji zagnieżdżonej jest bez wcięcia (R1), a jej
     `listLabels` to etykiety pozycji i jej przodków (FR-241);
   - `TableBlock` — atomami są wiersze; każda część tabeli zaczyna się od `Header` (jeśli jest), FR-223.
     Tabela zastępcza (`IsFallback`) tak samo.
3. Każda część zaczyna się od nagłówka jednostki (FR-231); wstęp nie ma nagłówka.
4. Długość mierzymy, renderując kandydata (R1). Koszt O(n²) w obrębie jednej długiej jednostki jest
   pomijalny (jednostki mają dziesiątki atomów); gdyby nie był, sumujemy długości atomów z separatorami.

**Uzasadnienie**: spełnia FR-222 – FR-225 bez zgadywania struktury; zachłanne wypełnianie daje
przewidywalne, deterministyczne granice, łatwe do opisania w testach.

**Alternatywy**: równoważenie długości części (np. dzielenie na pół) — więcej logiki, brak wartości dla
demo; podział akapitu po zdaniach — zabronione przez FR-222.

## R4. Zakres stron fragmentu

**Decyzja**: strona początkowa = strona pierwszej treści części po nagłówku: `Pages.First` pierwszego
bloku, a dla części zaczynającej się w środku listy — ostatni `PageBreak` napotkany w poprzedzających
pozycjach tej listy (albo `ListBlock.Pages.First`); dla wiersza tabeli — nowe pole `TableRow.Page`
(poniżej). Strona końcowa = maksimum stron treści części (bloki całe: `Pages.Last`; pozycje: ostatni
`PageBreak` w ich treści; wiersze: `TableRow.Page`). Zakres jest przycinany do `Section.Pages`
(FR-242). Pierwsza część jednostki zaczyna się na `Section.Pages.First`, jeśli tam stoi nagłówek.

**Addytywne rozszerzenie parsera**: `TableRow` dostaje opcjonalną właściwość
`public int? Page { get; init; }` (strona, na której zaczyna się wiersz), ustawianą w
`TableDetectionStage` (jedyne miejsce tworzenia `TableRow`). Rekord pozostaje zgodny źródłowo, renderer
jej nie czyta, więc Markdown i pliki wzorcowe się nie zmieniają (SC-046). Gdy `Page` jest `null`, zakres
części tabeli = `TableBlock.Pages` przycięty do jednostki.

**Uzasadnienie**: wielostronicowe taryfy (np. TAR-04, tabela przez kilka stron) — bez strony wiersza
frontend otwierałby PDF na początku tabeli zamiast na stronie stawki.

**Alternatywy**: zakres całej tabeli — prostsze, ale słabsze dla podglądu; przenoszenie stron do
komórek jako `PageBreak` — zmieniłoby model treści komórek i ryzykowało wynik renderera.

## R5. Przypisy

**Decyzja**: przypisy jednostki = `Section.Footnotes` (wstęp: `PreambleFootnotes`). Część dostaje
przypisy, do których prowadzi `FootnoteRef` w jej blokach (także w komórkach i pozycjach list), w
kolejności numerów; przypis z odwołaniami w kilku częściach jest w każdej. Przypisy jednostki bez
żadnego odwołania w jednostce (np. `IsOrphan`) trafiają do ostatniej części (spec FR-232).

## R6. Klucz jednostki

**Decyzja**: `unitKey = <oznaczenie wspólne dla wersji> + " | " + <ścieżka jednostki>`, gdzie:

- oznaczenie wspólne dla wersji (`SeriesKey`, w JSON `document.designation`) = `DocumentMetadata.Designation`
  albo `DocumentId`, gdy brak (FR-210);
- segment sekcji = `Section.Designation` (bez kropki końcowej, spacje znormalizowane), a gdy brak —
  `HeadingText` (znormalizowany tak samo); wstęp = stały segment `~wstep` (metadane, nie treść);
- ścieżka jednostki = **najkrótszy sufiks** ścieżki segmentów (od jednostki w górę), który jest unikalny
  wśród jednostek dokumentu, połączony `" > "`; jeśli nawet pełna ścieżka się powtarza, dopisujemy
  `" #n"` (n = numer wystąpienia od 2 w kolejności dokumentu).

Przykłady: `BP/REG/05 | § 11`; w tabeli-dokumencie z numeracją od nowa w sekcjach
`BP/REG/05 | Oprocentowanie > § 2`; sekcja taryfy `BP/TAR/04 | I. Opłaty za prowadzenie rachunku`.

**Uzasadnienie**: paragrafy i artykuły mają numerację w całym dokumencie, więc ich klucz nie zależy od
rozdziału — wstawienie rozdziału w nowej wersji nie zmienia kluczy paragrafów. Reguła jest
deterministyczna i działa tak samo w każdej wersji o tej samej strukturze. Zmiana numeracji między
wersjami rozdziela klucze — zaakceptowane w specyfikacji.

**Alternatywy**: zawsze pełna ścieżka — kruche przy zmianach rozdziałów; skrót treści — zmienia się
przy każdej zmianie postanowienia, a więc nie łączy wersji.

## R7. Identyfikator fragmentu

**Decyzja**: `chunkId = <documentId> + "_" + hex16(SHA-256(UTF-8(unitKey))) + "_" + <part>`, np.
`REG-05-w1_3f9a2c1d4e5b6a70_1`. `documentId` musi pasować do `^[A-Za-z0-9][A-Za-z0-9._-]{0,99}$`
(walidacja, FR-206). Unikalność w korpusie: identyfikatory dokumentów są unikalne w manifeście, klucze
— w dokumencie.

**Uzasadnienie**: FR-244 — zależy tylko od dokumentu, klucza i części; bezpieczny zestaw znaków i
długość ≤ 120; czytelny prefiks pomaga przy debugowaniu indeksu.

**Alternatywy**: GUID — niedeterministyczny; sam klucz jednostki jako identyfikator — znaki `§`, spacje,
`>` niewygodne dla indeksów.

## R8. Serializacja JSON

**Decyzja**: `System.Text.Json` (BCL, bez nowych zależności) — zapis ręczny `Utf8JsonWriter` w stałej
kolejności pól, `JavaScriptEncoder.UnsafeRelaxedJsonEscaping` (polskie znaki dosłownie, jak w
`ManifestWriter`), daty `yyyy-MM-dd`, liczby całkowite, LF, bez wcięć w JSONL. Odczyt `JsonDocument` z
walidacją `schemaVersion`. Rekordem kontraktu jest jedna linia JSONL (`schemaVersion`, `document`,
`chunk`) — spec FR-251; serwis może zwrócić tablicę takich rekordów (ta sama postać). Kontrakt:
`contracts/chunks-json.md`.

**Alternatywy**: `JsonSerializer` z kontekstem źródłowym — kolejność pól zależna od deklaracji i mniej
kontroli nad `null`; osobny plik dokumentu — odrzucone w Clarifications.

## R9. API biblioteki

**Decyzja**: `IDocumentChunker` z dwiema metodami `ChunkAsync` (wynik parsera / strumień PDF),
`DocumentMetadata`, `ChunkingRequest` (`ConfigureOptions`, `ParserRequest`), `ChunkingOptions`
(`MaxChunkLength = 2000`), walidator opcji, `AddLegalAgentChunking(configure)` — idempotentne, wywołuje
`AddLegalAgentPdfParser()` i rejestruje `DocumentChunker` jako singleton. Biblioteka referuje
`LegalAgent.PdfParser` i te same pakiety `Microsoft.Extensions.*` co parser. Szczegóły:
`contracts/library-api.md`.

**Uzasadnienie**: ten sam styl co `IPdfMarkdownConverter`/`PdfConversionRequest`/`AddLegalAgentPdfParser`
(FR-201); singleton bez stanu współdzielonego jest bezpieczny wątkowo (FR-205).

## R10. Narzędzia

**Decyzja**:
- CLI parsera: polecenie `chunk` w `legalagent-pdf` (kontrakt `contracts/cli.md`), zapis atomowy (plik
  tymczasowy + przeniesienie), kody wyjścia jak `convert` plus 2 dla błędów metadanych.
- Generator korpusu: wewnętrzna konwersja zwraca `PdfConversionResult` zamiast samego Markdown; po
  konwersji każdego dokumentu (także aktów i zatrutych) `IDocumentChunker` dzieli wynik z metadanymi
  z wpisu manifestu (`Id`, `Designation`, `Type`, `Title`, `Version`, `ValidFrom`, `ValidTo`, `Status`,
  `PreviousVersion`); plik `<stem>.chunks.jsonl` obok `<stem>.md`; nowe pole wpisu manifestu `chunks`
  (ścieżka). Pliki trafiają do listy `CorpusFile`, więc `refresh` je zapisuje, a `verify` porównuje bez
  dodatkowego kodu. Manifest zostaje w `schemaVersion: 1` (pole addytywne), kontrakt 003
  `manifest.md` dostaje opis pola.

## R11. Testy

**Decyzja**:
- Nowy projekt `tests/LegalAgent.Chunking.Tests` (xUnit v3/MTP jak pozostałe): testy jednostkowe na
  modelach `LegalDocument` budowanych w kodzie (granice, przypisy, listy, tabele, strony, klucze,
  identyfikatory, walidacja, anulowanie), serializacja (round-trip, kolejność pól, znaki), determinizm
  (wielokrotny i równoległy podział — porównanie bajtów JSONL), DI.
- Pliki wzorcowe: `tests/LegalAgent.Chunking.Tests/Golden/<id>.chunks.jsonl` dla REG-06 (regulamin
  jednokolumnowy z wersjami), REG-05 (tabela-dokument), TAR-04 (taryfa z wielostronicową tabelą), PRO-07
  (procedura), `dz-u-2019-1781-ochrona-danych` (akt z przypisami); wejście: PDF z `corpus/`, metadane z
  manifestu; `UPDATE_GOLDEN=1` i `*.actual.jsonl` jak w parserze; zmiana pliku wzorcowego wymaga zgody
  właściciela (FR-271).
- Test pokrycia słów (FR-234) na plikach wzorcowych i — w kategorii `CorpusFull` — na całym korpusie.
- Test manifestu (FR-273) w `LegalAgent.Corpus.Tests`, czyta zacommitowane `corpus/**/*.chunks.jsonl`
  (aktualność gwarantuje `verify`): dla każdego wpisu z `previousVersion` i `changes` i dla każdej zmiany
  — w nowszej wersji fragmenty, których zakres stron obejmuje `change.page` i których treść zawiera
  `change.after` (dla jednostek „§ N …”/„Art. N …” dodatkowo `citation == "§ N"`); co najmniej jeden
  taki fragment MUSI istnieć, a w poprzedniej wersji MUSI istnieć fragment o tym samym `unitKey`, którego
  treść zawiera `change.before`. Działa jednolicie dla „§ 11 ust. 3”, „poz. 75”, „krok 10.11”,
  „sekcja I”.
- Parser: test `TableRow.Page` dla tabeli przez dwie strony; pełny zestaw testów parsera z
  `LEGALAGENT_PRIVATE_CORPUS` przed commitem (pliki wzorcowe bez zmian).

## R12. Wydajność (SC-045)

**Decyzja**: brak optymalizacji z góry. Pomiar: czas `ChunkAsync` na największym dokumencie korpusu
z gotowego wyniku (test kategorii `Performance`, próg 1 s) i czas `refresh` przed i po (ręcznie,
zapis w handoffie).
