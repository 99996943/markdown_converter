# Research: syntetyczny korpus banku (spec 003)

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Date**: 2026-10-08

Technologia biblioteki i jej potoku jest bez zmian względem `specs/001-legal-pdf-parser/research.md` i
`specs/002-table-document-sections/research.md`. Ten dokument zapisuje decyzje generatora korpusu oraz
pomiary wykonane jednorazowymi programami poza repozytorium (PdfPig 0.1.16, czcionki Noto z
`tests/LegalAgent.PdfParser.Tests/Fixtures/Fonts`).

## Pomiary

| Pomiar | Wynik | Wniosek |
|--------|-------|---------|
| Dwa złożenia tego samego dokumentu `PdfDocumentBuilder` (2 i 25 stron, Noto Sans) | Pliki różnią się **tylko** wartością `/ID` w trailerze (losowy GUID); brak `/CreationDate`, `/ModDate`; `/Producer` stały | R4: deterministyczne `/ID` w post-processingu |
| Rozmiar PDF: 25 stron × 50 linii tekstu z polskimi znakami, jedna czcionka | ≈ 32 KB (1 strona ≈ 11 KB) — czcionka osadzana jako podzbiór i kompresowana | R9: rozmiar korpusu (≈ 75 dokumentów + wersje) rzędu kilku–kilkunastu MB; bez dodatkowych zabiegów |
| Pokrycie znaków Noto Sans Regular/Bold | Jest: • – — § € №; **brak**: ☐ □ ☑ ✓ ✔ → ▪ | R7: pola wyboru list kontrolnych rysowane wektorowo; strzałki schematów jako obrazy/wektor |
| Pokrycie Noto Sans Mono | Jest dodatkowo: □ → ▪ | Krój mono dostępny dla oznaczeń „□” tam, gdzie znak ma być tekstem (wariant układu) |
| Oznaczenia list w bibliotece (`ListLabelPatterns`) | Obsługiwane „N.N.” / „N.N.N.” (`OutlineLabel`), „N)”, „N.”, „a)”, rzymskie, punktory | Kroki procedur „4.1.”, „4.1.1.” mieszczą się w FR-050; ryzyko dotyczy raczej poziomów i odróżnienia od nagłówków (R11) |
| Escapowanie Markdown (`MarkdownEscaper`) | Znaki `\ * _ [ ] < > \``, `|` w tabelach, oznaczenia list na początku linii | Zatrucia ze znakami `#`, `>` na początku linii akapitu wymagają testu (R11) |

## R1. Umiejscowienie generatora w solucji

**Decision**: dwa nowe projekty w `src/` i jeden w `tests/`:
- `src/LegalAgent.Corpus` — biblioteka klas z całą logiką generatora (wczytanie treści, planowanie
  korpusu, składanie treści, skład stron, prawda referencyjna, manifest, zapis plików, konwersja
  biblioteką) oraz przeniesionym z testów `SyntheticPdfBuilder` (publiczny) i czcionkami Noto jako
  zasobami osadzonymi;
- `src/LegalAgent.Corpus.Cli` — cienka aplikacja konsolowa (`generate`, `refresh`, `verify`);
- `tests/LegalAgent.Corpus.Tests` — testy generatora i testy korpusu (próbka / pełny).

`tests/LegalAgent.PdfParser.Tests` odwołuje się do `LegalAgent.Corpus` zamiast mieć własną kopię
konstruktora PDF; `BankingCorpusGenerator` i jego goldeny zostają w testach parsera bez zmian treści.

**Rationale**: konstytucja wymaga biblioteki z logiką + cienkiej aplikacji + osobnego projektu
testowego dla biblioteki. Generator nie może żyć w projekcie testowym (musi być uruchamiany jednym
poleceniem), a konstruktor PDF jest potrzebny w obu miejscach — duplikacja kodu jest gorsza niż
przeniesienie. Biblioteka parsera nie zależy od generatora (zależność tylko w drugą stronę).

**Alternatives considered**: (a) generator jako dodatkowe polecenie istniejącego CLI parsera — miesza
odpowiedzialności, CLI parsera musiałoby zależeć od kodu testowego; (b) osobny projekt tylko na
`SyntheticPdfBuilder` — czwarty nowy projekt bez samodzielnej wartości; (c) generator w projekcie
testowym uruchamiany testem z flagą — nie spełnia „jednego polecenia” i kontraktu parametrów.

## R2. Format plików źródłowych treści

**Decision**: YAML (pakiet `YamlDotNet`, wersja przypięta w `Directory.Packages.props`), pliki w
`corpus/zrodla/`: `typy.yaml` (typy dokumentów: katalog, prefiks, elementy obowiązkowe), `fakty.yaml` (fakty banku — stawki, terminy, jednostki, dane fikcyjne),
`szablony/*.yaml` (szablony typów), `bloki/<typ>/*.yaml` (bloki treści), `zatrucia/*.yaml` (wzorce
zatruć), `zabronione.yaml` (lista nazw zabronionych), `akty.yaml` (metadane aktów). Składnia wariantów
i parametrów — [contracts/content-format.md](./contracts/content-format.md).

**Rationale**: treść to setki stron polskiej prozy; YAML z blokami `|` pozwala pisać akapity bez
escapowania, czytelnie w diffie; struktura (typ elementu, oznaczenie, poziom) jest jawna. JSON
(wbudowany) wymagałby escapowania cudzysłowów i jednej linii na akapit — nieczytelny przy tej skali;
własny format tekstowy oznaczałby parser i gramatykę do utrzymania (więcej kodu niż zależność).

**Alternatives considered**: JSON (System.Text.Json) — odrzucony jw.; Markdown z front matter —
wymaga własnego parsera struktury list/tabel i myli się z Markdown wyjściowym; kod C# z treścią —
łamie FR-102 (treść oddzielona od kodu).

## R3. Losowość i niezależność dokumentów

**Decision**: własny generator liczb pseudolosowych (SplitMix64, ~20 linii) zamiast `System.Random`.
Strumień losowy każdej decyzji wyprowadzany z `(ziarno, cel, identyfikator)` przez stabilny skrót
(FNV-1a 64 po UTF-8), np. `(seed, "przydział-bloków", "REG")`, `(seed, "dokument", "REG-03")`. Pule
bloków niewspólnych są **dzielone z góry** między dokumenty danego typu i tematu (deterministyczne
tasowanie Fisher–Yates), więc każdy dokument składa się niezależnie od innych.

**Rationale**: algorytm `System.Random(seed)` nie ma gwarancji stabilności między wersjami .NET;
FR-101 wymaga identycznych bajtów na każdej platformie. Podział pul z góry realizuje FR-103a bez
sekwencyjnej zależności, pozwala generować pojedynczy dokument (próbka testów, `verify`) i równolegle.

**Alternatives considered**: `System.Random` — ryzyko zmiany wyniku po aktualizacji SDK;
sekwencyjne „zużywanie” bloków — dokument N zależy od N−1, próbka testów musiałaby budować cały korpus.

## R4. Deterministyczny PDF

**Decision**: po `PdfDocumentBuilder.Build()` wartość `/ID [ <a><b> ]` w trailerze jest zastępowana
(ta sama długość, 2 × 32 cyfry szesnastkowe) skrótem SHA-256 treści pliku z wyzerowanym `/ID` —
funkcja `PdfIdNormalizer` w `LegalAgent.Corpus`, używana przez `SyntheticPdfBuilder.Build()`. Daty w
dokumentach pochodzą wyłącznie z parametrów przebiegu i faktów; generator nie czyta zegara. Formatowanie
liczb i dat przez `CultureInfo.InvariantCulture` + jawny polski format („15,00 zł”, „1 stycznia 2027 r.”).

**Rationale**: pomiar wykazał, że `/ID` to jedyne źródło niedeterminizmu; podmiana w miejscu nie
zmienia offsetów tabeli xref. Ten sam skrót daje ten sam `/ID` na każdej platformie.

**Alternatives considered**: usunięcie `/ID` — dopuszczalne w PDF 1.x, ale zmienia offsety (trzeba by
przeliczać xref); fork PdfPig — nieproporcjonalne.

## R5. Fakty banku jako jedno źródło wartości

**Decision**: wszystkie wartości merytoryczne (opłaty, oprocentowania, limity, terminy reklamacji,
nazwy jednostek organizacyjnych, adresy, numery infolinii) są **faktami** w `fakty.yaml` z
identyfikatorami (`oplata.karta.wydanie-duplikatu`), opcjonalnie z historią wartości w czasie. Bloki
odwołują się do faktów (`{{fakt:oplata.karta.wydanie-duplikatu}}`). Regulamin i taryfa używające tego
samego faktu są spójne z konstrukcji; **wersja** dokumentu to zmiana wartości faktu (lub wariantu
bloku) od daty; **sprzeczność** to nadpisanie faktu w jednym dokumencie pary; **fałszywa stawka**
(zatrucie) to nadpisanie faktu w dokumencie zatrutym. Manifest odczytuje wartości przed/po wprost z
tych nadpisań.

**Rationale**: realizuje FR-114 (spójność), FR-120–FR-122 i FR-141 (manifest wskazuje jednostkę i
obie wartości) bez ręcznego pilnowania; prawda referencyjna wersji i sprzeczności powstaje
automatycznie.

**Alternatives considered**: wartości wpisane w bloki — sprzeczności przypadkowe, manifest ręczny.

## R6. Skład stron i trafienie w zakres stron

**Decision**: `Typesetter` w `LegalAgent.Corpus` — uogólnienie klasy `Flow` z `BankingCorpusGenerator`:
składa abstrakcyjne drzewo dokumentu (okładka, metryczka, nagłówki, akapity z łamaniem wierszy po
szerokości `TextWidth`, listy wielopoziomowe, tabele z siatką/bez z powtarzanym nagłówkiem, przypisy u
dołu strony, dwie kolumny, tabela-dokument FR-080, schemat kroków FR-067, listy kontrolne) według
**stylu układu** (marginesy, kroje, rozmiary, nagłówek/stopka strony, „Strona n z N” — dwa przebiegi).
Dobór objętości: kompozytor dodaje bloki opcjonalne z puli dokumentu do osiągnięcia minimum stron,
skład liczy strony, a gdy przekroczono maksimum — usuwa ostatni blok opcjonalny; najwyżej 8 iteracji,
potem błąd (FR-103). Docelowa liczba stron dokumentu losowana z zakresu (ziarno dokumentu).

**Rationale**: `Flow` już rozwiązuje łamanie wierszy, listy, przypisy i siatkę dla goldenów parsera;
uogólnienie zamiast nowego silnika. Liczenie stron po składzie jest jedyną pewną miarą.

**Alternatives considered**: szacowanie stron z liczby słów — niedokładne przy tabelach; zewnętrzny
silnik składu (np. QuestPDF) — nowa, duża zależność i licencja, a spec wymaga istniejącego konstruktora.

## R7. Elementy układu bez glifów w czcionkach

**Decision**: pole wyboru listy kontrolnej — pusty prostokąt rysowany wektorowo (nie tekst) przed
tekstem pozycji albo w kolumnie „Wykonano” tabeli z siatką; strzałki schematu kroków — obraz (`Image`),
jak w FR-067. W wariancie „oznaczenie tekstowe” lista kontrolna używa „□” krojem mono. Prawda
referencyjna nie zawiera słów, których nie ma w PDF jako tekst (pole wektorowe nie jest słowem).

**Rationale**: Noto Sans nie ma ☐/✓/→ (pomiar); znak spoza czcionki dałby pusty glif lub błąd.

**Alternatives considered**: dodanie czcionki z symbolami (np. Noto Sans Symbols 2) — nowy plik, a
biblioteka i tak ma rozpoznawać listy kontrolne po tekście pozycji; zbędne.

## R8. Zatrucia

**Decision**: wzorzec zatrucia (`zatrucia/*.yaml`) ma rodzaj (5 rodzajów FR-130), dozwolone miejsca
(akapit, przypis, komórka tabeli, metryczka, okładka, osobny akapit/ramka), cel (dla `polecenia-dla-ai`,
lista FR-132a) i warianty tekstu z parametrami (fikcyjne adresy z domeny `przyklad.invalid`/`example.com`,
numery z zakresów niemożliwych). Dokument zatruty = kopia planu dokumentu podrabianego (ten sam szablon,
styl układu, pule bloków) + operacje zatrucia: wstawienie elementu (polecenie), nadpisanie faktu
(fałszywa stawka / sprzeczność), zmiana metryczki (podszywanie, fałszywe zatwierdzenie), zmiana dat
okładki (nieaktualny jako obowiązujący). Kompozytor oznacza wstawione elementy, a `Typesetter` zapisuje
stronę, na której wylądowały → miejsce zatrucia w manifeście (FR-142) bez ręcznej pracy. Styl tekstu
zatrucia = styl elementu, w który jest wstawiony (FR-131), poza wariantem „jawny/ramka”.

**Rationale**: zatrucia powstają tym samym torem co zwykła treść, więc wyglądają jak zwykły dokument;
manifest jest prawdą referencyjną wyliczoną, nie wpisaną.

**Alternatives considered**: ręcznie przygotowane PDF zatrute — sprzeczne z FR-134 i powtarzalnością.

## R9. Rozmiar korpusu w repozytorium

**Decision**: PDF-y commitowane wprost (bez Git LFS); czcionki osadzane jako podzbiór przez PdfPig.
Szacunek: ~45 dokumentów bazowych (z wersjami) + 30 zatrutych × ≈ 30–60 KB PDF + ≈ 60 KB Markdown ≈
10 MB. Prawda referencyjna **nie** jest commitowana — testy i `verify` odtwarzają ją w pamięci; opcja
`--truth <katalog>` zapisuje ją do diagnostyki.

**Rationale**: rozmiar akceptowalny bez LFS; commitowanie prawdy dublowałoby informację wyliczalną
z treści i zwiększało diff.

## R10. Testy: próbka i pełny korpus

**Decision**: projekt `tests/LegalAgent.Corpus.Tests`:
- testy jednostkowe generatora (PRNG, warianty, fakty, planowanie, skład, manifest, zapis atomowy);
- **testy próbki** (zawsze): wybór automatyczny z zapisanego przebiegu `corpus/przebieg.json` —
  pierwszy dokument dla każdej pary (typ, styl układu) i dla każdej pary (typ, rodzaj zatrucia), oraz
  wszystkie wersje pierwszego wersjonowanego dokumentu; dla nich: PDF z pamięci == plik w `corpus/`, Markdown z
  biblioteki == plik w `corpus/`, metryki SC-022 – SC-026 względem prawdy z pamięci;
- **testy pełne** (`[Trait("Category","CorpusFull")]`) — to samo dla wszystkich dokumentów + SC-020,
  SC-021 (bez czasu), SC-027, SC-028, SC-031; pomijane lokalnie (Skip), gdy zmienna
  `LEGALAGENT_CORPUS_FULL` nie jest ustawiona; CI ustawia ją w kroku testów.

Testy SC-029 (brak regresji goldenów) to istniejące `GoldenTests` parsera — bez zmian.

**Rationale**: realizuje FR-164/FR-165 i decyzję z clarify (próbka lokalnie, pełny w CI).

## R11. Spodziewane poprawki biblioteki (test-first)

Pomiar na pełnym korpusie wskaże faktyczne braki; z analizy układów wynikają kandydaci (każdy jako
czerwony test na minimalnym syntetycznym PDF w `tests/LegalAgent.PdfParser.Tests` przed poprawką):

| Układ | Ryzyko | Oczekiwany wynik |
|-------|--------|------------------|
| Metryczka procedury (tabela klucz–wartość z siatką na 1. stronie) | Uznana za tabelę-dokument (FR-080) lub rozbita | Tabela GFM 2-kolumnowa; nie tabela-dokument (krótkie komórki, < 50% stron) |
| Kroki „4.1.” / „4.1.1.” po nagłówku „4. Opis postępowania” | Krok pogrubiony uznany za nagłówek; zły poziom zagnieżdżenia | Pozycje listy z oryginalnym oznaczeniem, poziom z hierarchii „N.N.” → „N.N.N.” |
| Lista kontrolna z polem wektorowym | Pole jako komórka tabeli / zgubiona pozycja | Pozycja listy lub wiersz tabeli z tekstem pozycji |
| Taryfa bez siatki przez wiele stron z przypisami „1)” pod tabelą | Przypisy jako lista w tabeli; „(1)” w komórce oderwane | Jedna tabela GFM, przypisy jako akapity/przypisy po tabeli |
| Zatrucie zaczynające się od „#”, „>”, „§ 99.” w środku akapitu | Fałszywy nagłówek / cytat | Tekst dosłowny w akapicie (escape) |
| Dwie kolumny z przypisami i nagłówkami „§ N.” w obu kolumnach | Kolejność czytania | Kolumna lewa, potem prawa; nagłówki w kolejności |

Zmiany, które przestawiają istniejące goldeny, wymagają zgody właściciela (FR-163, SC-029).

## R12. Akty prawne

**Decision**: pobranie jednorazowe oficjalnych tekstów jednolitych z Dziennika Ustaw
(`https://dziennikustaw.gov.pl/D<rok><pozycja 7 cyfr>01.pdf`) poleceniem opisanym w `corpus/README.md` (curl z
`--max-time`, sprawdzenie nagłówka `%PDF`), commit do `corpus/akty/`; 6 aktów z
`tests/.../Corpus/acts` kopiowanych (testy parsera zachowują swoje pliki). Metadane w
`corpus/zrodla/akty.yaml`; generator tworzy z nich wpisy manifestu i `corpus/akty/ZRODLA.md`.
Pozycje zweryfikowane 2026-10-08 w API ELI Sejmu (`api.sejm.gov.pl/eli/acts/DU/...`, brak nowszego
tekstu jednolitego) i pobraniem próbnym (HTTP 200, `application/pdf`, plik zaczyna się od `%PDF-`).
Uwaga: numer pozycji w adresie ma **7 cyfr** (`D` + rok + 7-cyfrowa pozycja + `01.pdf`).

| Akt | Tekst jednolity | Źródło | Strony | Uwagi |
|-----|-----------------|--------|--------|-------|
| Ustawa z 1 marca 2018 r. o przeciwdziałaniu praniu pieniędzy oraz finansowaniu terroryzmu | Dz. U. 2025 poz. 644 (obwieszczenie z 9 maja 2025 r.) | https://dziennikustaw.gov.pl/D2025000064401.pdf | ≈ 68 | późniejsza nowelizacja Dz. U. 2025 poz. 1669 nieuwzględniona — odnotować w `ZRODLA.md` |
| Ustawa z 10 maja 2018 r. o ochronie danych osobowych | Dz. U. 2019 poz. 1781 (obwieszczenie z 30 sierpnia 2019 r.) | https://dziennikustaw.gov.pl/D2019000178101.pdf | ≈ 36 | nowelizacje Dz. U. 2026 poz. 252 i 548 nieuwzględnione — odnotować |
| Ustawa z 5 sierpnia 2015 r. o rozpatrywaniu reklamacji przez podmioty rynku finansowego, o Rzeczniku Finansowym i o Funduszu Edukacji Finansowej | Dz. U. 2026 poz. 823 (obwieszczenie z 12 czerwca 2026 r.) | https://dziennikustaw.gov.pl/D2026000082301.pdf | ≈ 19 | — |
| Ustawa z 23 marca 2017 r. o kredycie hipotecznym oraz o nadzorze nad pośrednikami kredytu hipotecznego i agentami | Dz. U. 2025 poz. 720 (obwieszczenie z 21 maja 2025 r.) | https://dziennikustaw.gov.pl/D2025000072001.pdf | ≈ 48 | dodatkowy akt (FR-150 „co najmniej”) — tematyka kredytów |

Łącznie `corpus/akty/`: 6 + 4 = 10 aktów.

## R13. Konwersja w generatorze

**Decision**: generator wywołuje `PdfMarkdownConverter.CreateDefault()` z ustawieniami domyślnymi
(znaczniki stron włączone) i `SourceId` = ścieżka względna PDF w `corpus/`; wersja biblioteki
(`AssemblyInformationalVersion`) trafia do manifestu (FR-143). Polecenie `refresh` konwertuje istniejące
PDF-y (także akty) bez ponownego składania. Konwersja niekompletna (`IsComplete == false`) = błąd
przebiegu (FR-160, zasada IV).

## R14. Zapis plików i sprzątanie

**Decision**: zapis każdego pliku atomowo (plik tymczasowy w tym samym katalogu + `File.Move`
z nadpisaniem), Markdown/manifest w UTF-8 bez BOM z `\n`. Zarządzane katalogi: `regulaminy/`,
`taryfy/`, `procedury/`, `zatrute/` (rekurencyjnie) i plik `manifest.json`; pliki `*.pdf`/`*.md` w
nich, których nie ma w nowym planie, są usuwane po udanym przebiegu. `akty/`, `zrodla/`, `README.md`,
`przebieg.json` nie są usuwane (FR-108).
