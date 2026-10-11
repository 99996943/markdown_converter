# Research: Poprawki parsera po przeglądzie liczb (spec 009)

Sondy w scratchpadzie sesji (pliki `probe.cs`, `scan/scan.cs`, `sup/sup.cs`, uruchamiane `dotnet run plik.cs` z
`#:package PdfPig@0.1.16`) czytają warstwę tekstową PDF bezpośrednio przez PdfPig, niezależnie od parsera.

## R1 — Indeks górny po „art.” (US1)

**Stan kodu.** `TextNormalizationStage` zamienia cyfrę na znak indeksu górnego, gdy glif jest jedną cyfrą,
`PointSize ≤ 0,8 × rozmiar poprzedniego pełnego glifu`, linia bazowa podniesiona o `≥ 0,15 × rozmiar`, a ostatnie
24 glify pasują do `LegalNumberTail` = `(?:Art\.|§)\s*\d+[a-z]*[⁰¹²³⁴-⁹]*$`. Wzorzec rozróżnia wielkość liter, więc
„art. 66” + mała „1” zostaje cyfrą „1” doklejoną do numeru. Jeśli na stronie jest przypis o tym samym numerze,
`FootnoteDetectionStage` (500) bierze cyfrę za odnośnik (`MarkerLabel` = `^\d{1,3}\)?$`) i powstaje fałszywe `[^n]`.
Znak indeksu (`¹`, kategoria Unicode `No`) nie pasuje do `\d`, więc po zamianie w etapie 200 przypisy go nie widzą.

**Skan wszystkich PDF-ów** (`Corpus/acts`, `Corpus/banking`, prywatny korpus, `corpus/`), małe podniesione cyfry
po oznaczeniu:

| Zbiór | „art.” (dziś błąd) | „Art.” / „§” (dziś poprawne) |
|---|---|---|
| prywatny corp-5 (s. 8) | 2 („art. 66¹”, „art. 68²”) | — |
| prywatny corp-1 (s. 24) | — | 1 („§ 7¹”) |
| `Corpus/acts` | prawa-konsumenta 4, kredyt-konsumencki 1, prawo-bankowe 7, usługi-płatnicze 3 (razem 15) | kredyt-konsumencki 3 × „§ 2¹”, prawo-bankowe 1 |
| `corpus/akty` | ochrona-danych 8, prawa-konsumenta 4, kredyt-konsumencki 1, prawo-bankowe 7, usługi-płatnicze 3, aml 3, kredyt-hipoteczny 7 (razem 33) | ochrona-danych 2 × „Art. 22²”, kredyt-hipoteczny 2 × „§” |
| `Corpus/banking`, pozostałe dokumenty prywatne, syntetyczne regulaminy/taryfy/procedury | 0 | 0 |

Przykład z goldenu `Corpus/acts/dz-u-2020-287-prawa-konsumenta.expected.md`, wiersz 28: „Przepisów art. 3855, art.
5564, art. 556[^2] i art. 576[^2]”; w PDF „art. 385⁵, art. 556⁴, art. 556⁵ i art. 576⁵”. Dwie ostatnie cyfry stały się
odnośnikami do przypisu 2 („Ustawa została ogłoszona…”), a przypisy są numerowane od nowa, stąd `[^2]` zamiast 5.
Wielocyfrowe indeksy („art. 479¹¹⁵”, „art. 166¹¹”) to kolejne małe cyfry: każda kolejna przechodzi wzorzec dzięki
`[⁰¹²³⁴-⁹]*` na końcu.

**Decision.** Wzorzec przyjmuje „art.” i „Art.” (`(?<!\p{L})[Aa]rt\.`), z warunkiem braku litery przed „a”, żeby
„kart. 5” czy „Start.” nie łapały. Kryteria rozmiaru i podniesienia bez zmian. Zgoda właściciela (Clarifications,
sesja planu) obejmuje różnice w `Corpus/acts`, goldenie fragmentów `dz-u-2019-1781-ochrona-danych` i `corpus/akty`,
jeśli są wyłącznie tą zamianą, zniknięciem fałszywych `[^n]` i przenumerowaniem przypisów.

**Rationale.** Najmniejsza zmiana, która usuwa przyczynę; skan pokazuje, że kryterium geometryczne już dziś trafia
w prawdziwe indeksy (wszystkie 48 + 2 miejsca to istniejące artykuły z indeksem). Odnośniki przypisów po słowach (nie
po „art. N”) nie przechodzą wzorca, więc się nie zmieniają.

**Alternatives considered.** Wzorzec bez rozróżniania wielkości liter (`RegexOptions.IgnoreCase`) — łapałby też
„ART.” i „§” bez zmian, ale również „kArt.” bez lookbehindu; jawne `[Aa]` jest czytelniejsze. Rozszerzenie na „ust.”,
„pkt” — skan nie znalazł indeksów po tych oznaczeniach; YAGNI, a miara liczb (R2) wykaże przyszłe przypadki.

## R2 — Miara liczb (US2)

**Decision.** Nowy pomocnik testów `NumberCheck` (w `tests/LegalAgent.PdfParser.Tests/Fixtures/`, wywoływany z
`LayoutMetrics` i z raportu `PrivateCorpusTests`):

- **Strona PDF** — czyta litery PdfPig bezpośrednio (nie z parsera, żeby nie powielać jego błędów). Na każdej
  stronie litery w kolejności strumienia treści dzieli na tokeny: nowy token przy białym znaku, przerwie > 0,15 em,
  zmianie linii bazowej > 0,2 × rozmiar albo przejściu do/od cyfry indeksu. Cyfra indeksu = ta sama reguła co w
  parserze (≤ 0,8 rozmiaru poprzedniej pełnej litery, podniesiona ≥ 0,15 rozmiaru); cyfry indeksu nie są liczbami
  (po stronie Markdownu odpowiadają im znaki `¹` albo odnośniki `[^n]`). Liczby to dopasowania `[0-9]+(?:[.,][0-9]+)*`
  w tokenach. Wiersze surowe (litery jednej linii bazowej) w postaci `LineFingerprint` równej wzorcowi z
  `Report.RemovedArtifacts` na stronie z `Pages` są pomijane (numery stron „N/M”, stopki powtarzalne).
- **Strona Markdown** — usuwa komentarze `<!-- … -->`, odnośniki `[^n]` i etykiety definicji `[^n]:`, ukośniki
  ucieczki; liczby tym samym wyrażeniem. Znaki indeksu górnego nie są cyframi `[0-9]`.
- **Wynik** — `NumberDiff(Excess, Missing)`: wielozbiory (wartość → krotność) różnic Markdown − PDF i PDF − Markdown.
- **Bramka** — `PrivateCorpusTests.RegulationNumbers_HaveNoExcess` czyta `number-check.txt` (nazwa dokumentu w
  wierszu, `#` komentarz) i pada, gdy dokument z listy ma `Excess` niepuste; komunikat: dokument i liczby. Brakujące
  są tylko raportowane (definicje przypisów „1)” na dole strony, rozbite wiersze — szum, nie zmiana sensu).

**Rationale.** Wielozbiór jest odporny na kolejność czytania i podział wierszy; spacja jako separator tysięcy
(„170 103 364”) daje te same trzy tokeny po obu stronach. Liczby sklejone („661” z „66” + „¹”, „150.000” z „¹50.000”)
są nadmiarowe, bo po stronie PDF cyfra indeksu jest oddzielona. Przeplecione litery stopki corp-2 dają nadmiarowe
„0”, „1”, „2” (data rozbita literami słów) — bramka wykryje także US3.

**Weryfikacja na znanych przypadkach** (zadanie implementacyjne, przed bramką): corp-5 → nadmiarowe 661, 682;
firm-1 → 150.000, 230.000, 320.000, 450.000; corp-2 → cyfry z przeplecionej daty. Jeśli którykolwiek regulamin z
listy da fałszywy alarm, reguła tokenizacji jest poprawiana (nie lista), a przypadek opisany w handoffie.

**Alternatives considered.** Liczby z modelu parsera (`page.Glyphs`) — wspólne źródło z parserem ukryłoby błędy
składania wierszy; `page.GetWords()` PdfPig — skleja indeks górny z numerem („661”), czyli dokładnie to, co miara ma
wykrywać. Porównanie zbiorów (bez krotności) — nie wykryłoby zdublowanej ani zgubionej kwoty, która występuje też
gdzie indziej.

## R3 — Przeplecione wiersze stopki (US3)

**Geometria** (corp-2 s. 8, corp-6 s. 3; ten sam szablon w corp-1 s. 49, corp-3 s. 23, corp-5 s. 27): stopka to
cztery wiersze Montserrat 6,7 pt od x = 175,5, linie bazowe 61,0 / 54,0 / 47,0 / 40,0 (interlinia 7 pt); logo
„mBank.pl” mBank-Bold 22,7 pt od x = 39,7, linia bazowa 39,7. W strumieniu treści: stopka wiersz po wierszu, logo
na końcu.

**Mechanizm** (`LineAssemblyStage.GroupIntoLines`, `LineBuilder.Accepts`): glify są przetwarzane od największego,
więc logo zakłada wiersz pierwsze. Glif mniejszy niż `MarkerSizeRatio` (0,7) × rozmiar zarodka wiersza jest
„znacznikiem” (indeks, odnośnik) i dołącza przez samo pionowe nakładanie, bez limitu odległości. Pierwsza litera
wiersza 2 stopki nie ma jeszcze własnego wiersza → trafia do wiersza logo; tak samo kolejne litery aż do pierwszej
spacji, która (pole zerowej wysokości) zakłada własny wiersz na linii bazowej 54, i reszta wiersza wybiera już ten
wiersz (mniejsza różnica linii bazowych). Wiersz 4 (linia bazowa 40,0 ≈ 39,7) dołącza w całości przez tolerancję
linii bazowej. Efekt: pierwsze słowa wierszy 2–4 („Gospodarczy”, „podatkowej”, „01.01.2022”) sortowane po x w
jednym wierszu z logo. `StepSequenceStage` ma już łatkę na ten sam mechanizm przy strzałkach-glifach (T040a,
`RejoinGapFactor`: dołącza resztę wiersza odciągniętą przez wysoki glif).

**Skan 21 dokumentów prywatnych** (napis ≥ 1/0,7 większy z ≥ 4 drobnymi literami na ≥ 2 liniach bazowych w zasięgu):
stopka z logo — corp-1, corp-2, corp-3, corp-5, corp-6; glify strzałek 16–18 pt przy tekście 8 pt — firm-2 (2),
firm-5 (3), firm-6 (3), obsłużone przez T040a; ind-*, reg*, regulamin-pdp, firm-1, firm-3, firm-4, corp-4 — brak.

**Decision.** W `LineAssemblyStage` przed grupowaniem wyznaczyć „wiersze tekstu własnego”: glify (bez białych
znaków) jednego rozmiaru na wspólnej linii bazowej (tolerancja jak `BaselineToleranceRatio`), połączone przerwami
≤ `NearGlyphEm` × własny rozmiar, w ciągu co najmniej 4 glifów. Glif należący do takiego ciągu, mniejszy niż
`MarkerSizeRatio` × zarodek i odległy od glifów wiersza o więcej niż `NearGlyphEm` × mniejszy rozmiar (dla stopki:
35 pt > 3 × 6,7 pt), NIE dołącza do wiersza zarodka — ani przez pionowe nakładanie, ani przez tolerancję linii
bazowej. Znaczniki tuż przy słowie (odległość ~0) i ciągi 1–3 glifów — bez zmian.
Oczekiwany wynik stopki: cztery ciągłe wiersze w kolejności PDF i osobny fragment `**mBank.pl**` (między wierszami
1 i 2 według `Box.Top`, co jest dopuszczalne — logo nie jest częścią żadnego wiersza).

**Rationale.** Usuwa przyczynę (a nie objaw w późniejszym etapie); próg 4 glifów oddziela tekst od znaczników
(indeksy w korpusie mają 1–3 cyfry), a warunek odległości chroni dłuższe odnośniki tuż przy słowie („***)”, „123)”)
— oba przypadki sprawdzane testem. Ten sam warunek naprawia odciąganie przez
strzałki, więc T040a staje się nieaktywne, ale zostaje (bez ryzyka).

**Ryzyko i kryterium wycofania (FR-661).** Rdzeń składania wierszy dotyka każdego dokumentu. Po zmianie: pełny
zestaw testów; różnice dopuszczalne tylko w stopkach pięciu dokumentów corp i (bez pogorszenia miar) w schematach
firm-2, firm-5, firm-6. Każda inna różnica goldenu, rozbity wiersz z indeksem/odnośnikiem albo spadek miar
`CorpusFull` → wycofanie zmiany i wpis w „Znanych ograniczeniach”.

**Alternatives considered.** (a) Łatka w późniejszym etapie (jak T040a) — rozplatanie przeplecionych liter po
złożeniu słów jest trudne (litery już w słowach); (b) kolejność przetwarzania od najmniejszych glifów — zmienia
zachowanie wszystkich nagłówków i znaczników, ryzyko szerokie; (c) wykluczanie logo jako artefaktu — logo to tekst
PDF, a usuwanie tekstu łamie FR-670.

## R4 — Wersje, korpus i kolejność

- Kontrakt Markdown 1.3.0 bez zmian (znak indeksu górnego już jest w kontrakcie dla „Art.”/„§”). Wersja projektów
  1.2.0 → 1.2.1 (PATCH), `refresh` przepisuje manifest i `corpus/akty/*.md`, `*.chunks.jsonl` (wąska zgoda), `verify`
  czysty. Golden fragmentów `tests/LegalAgent.Chunking.Tests/Golden/dz-u-2019-1781-ochrona-danych.chunks.jsonl`
  odświeżony `UPDATE_GOLDEN=1` (wąska zgoda).
- Punkt odniesienia: kopia prywatnych goldenów do `Corpus/private/baseline-009/` (poza git) przed pierwszą zmianą
  parsera; porównanie zbiorów słów i miara liczb przed/po.
- Kolejność: US2 (miara + bramka; bramka czerwona na prywatnym korpusie przez corp-5 i corp-2) → US1 → US3 →
  zamknięcie. Bramka staje się zielona po US1 i US3. Jeśli US3 zostanie wycofane (FR-661), znane przeplecenie corp-2
  jest wpisane jako dopuszczony wyjątek w `number-check.txt` (kontrakt `contracts/number-check.md`), z odwołaniem do
  „Znanych ograniczeń”.
