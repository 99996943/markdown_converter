# Research: LegalAgent.PdfParser

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Date**: 2026-10-07

Każda pozycja: **Decyzja** / **Uzasadnienie** / **Rozważone alternatywy**.

---

## R1. Platforma docelowa i SDK

- **Decyzja**: Wszystkie projekty celują w `net9.0` (wymóg konstytucji). Budowanie SDK 10.0.x
  (najnowsze zainstalowane: 10.0.301) — SDK 10 kompiluje projekty `net9.0`. Do uruchamiania testów
  wymagany jest runtime **.NET 9.0 GA** (na maszynie deweloperskiej jest tylko 9.0.0-preview.4 —
  trzeba doinstalować `Microsoft.NETCore.App 9.0.x`). `global.json` przypina SDK z
  `rollForward: latestFeature`.
- **Uzasadnienie**: Konstytucja: „C# na .NET 9”. SDK 10 jest wstecznie zgodne.
- **Ryzyko (do decyzji właściciela projektu)**: .NET 9 to wydanie STS ze wsparciem do
  **10 listopada 2026** (ok. miesiąc od dziś). Po tej dacie brak poprawek bezpieczeństwa. Zalecana
  poprawka konstytucji (MINOR/MAJOR wg governance) na .NET 10 LTS; zmiana sprowadza się do
  `TargetFramework` i wersji pakietów `Microsoft.Extensions.*` 10.0.x — plan jest na to gotowy
  (wersje w jednym pliku `Directory.Packages.props`).
- **Alternatywy**: multi-targeting `net9.0;net10.0` — odrzucone bez zmiany konstytucji (podwaja
  macierz testów, YAGNI).

## R2. Biblioteka PDF

- **Decyzja**: `PdfPig` (pakiet NuGet `PdfPig`, przestrzeń nazw `UglyToad.PdfPig`) **0.1.16**
  (najnowsza stabilna; nowsze to wersje alpha 0.1.17). Licencja Apache-2.0, czysty kod zarządzany,
  brak zależności natywnych, target `net9.0` w paczce.
- **Wykorzystywane możliwości**: `PdfDocument.Open(byte[], ParsingOptions)`; `Page.Letters`
  (tekst, `GlyphRectangle`, `StartBaseLine`, `PointSize`, `FontName`, `Font.IsBold/IsItalic`,
  `TextOrientation`, `RenderingMode`, `Color`); `Page.Width/Height/Rotation`; ścieżki graficzne
  strony (linie siatki tabel); `Page.GetImages()` (wykrywanie skanów); wyjątki szyfrowania.
- **Rozmiar czcionki**: używamy `PointSize` (efektywny rozmiar po macierzy transformacji), a nie
  `FontSize` — w PDF z ISAP rozmiar często ustawiony jest jako 1 pt i skalowany macierzą.
- **Uzasadnienie**: wymóg zleceniodawcy; jedyna dojrzała, darmowa, wieloplatformowa biblioteka .NET
  dająca dostęp do pozycji i czcionek pojedynczych liter.
- **Alternatywy**: iText (AGPL — odrzucone licencyjnie), Docnet/PDFium (natywne binaria — sprzeczne
  z „minimalne zależności” i przenośnością), wersja alpha 0.1.17 (niestabilna).

## R3. Ekstrakcja słów i linii — własna czy z PdfPig

- **Decyzja**: Własne składanie słów i linii z `Letters` (FR-011, FR-030). Z modułu analizy
  układu PdfPig (`DocumentLayoutAnalysis`) **nie** korzystamy w potoku głównym.
- **Uzasadnienie**: Potrzebny jest styl każdej litery (pogrubienie fragmentu → `**…**`), kontrola
  progów (konfigurowalne, deterministyczne) oraz zachowanie dużych odstępów poziomych jako granic
  komórek tabel. `NearestNeighbourWordExtractor`/`DocstrumBoundingBoxes` łączą je w bloki, co
  „rozsypuje” tabele opłat; `RecursiveXYCut` dzieli wiersze tabel na kolumny-bloki.
- **Alternatywy**: `ContentOrderTextExtractor` (kolejność strumienia treści — w PDF z generatorów
  bankowych bywa losowa) — odrzucone.

## R4. Wejście strumieniowe i limity

- **Decyzja**: Strumień czytany od bieżącej pozycji do końca przez `CopyToAsync` do bufora z
  licznikiem; przekroczenie `MaxInputBytes` przerywa kopiowanie (nadmiar nie jest wczytywany).
  Dokument otwierany z `byte[]`. Strumień nie jest zamykany. Limit stron sprawdzany zaraz po
  otwarciu (`NumberOfPages`). Limit czasu: `CancellationTokenSource.CreateLinkedTokenSource` z
  `CancelAfter(MaxDuration)`; rozróżnienie: anulowanie przez wywołującego →
  `OperationCanceledException`, upływ limitu → `PdfLimitExceededException(Duration)`.
- **Uzasadnienie**: PdfPig wymaga strumienia przewijalnego; bufor ujednolica obsługę strumieni
  sieciowych (edge case) i daje egzekwowalny limit. Async dotyczy I/O; przetwarzanie stron jest
  CPU-bound i odbywa się na wątku wywołującego z kontrolą anulowania przed każdą stroną
  (biblioteka nie robi `Task.Run` — decyzję o odciążeniu wątku zostawia aplikacji).
- **Alternatywy**: `PdfDocument.Open(Stream)` bezpośrednio — odrzucone (nieprzewijalne strumienie,
  brak limitu rozmiaru); `Task.Run` wewnątrz biblioteki — antywzorzec w bibliotekach.

## R5. Architektura potoku i DI

- **Decyzja**: Fasada `IPdfMarkdownConverter` + uporządkowana lista etapów
  `IPipelineStage { int Order; void Execute(PipelineContext ctx); }` rejestrowanych w DI jako
  `IEnumerable<IPipelineStage>`, sortowanych po `Order` (remis → nazwa typu, dla determinizmu).
  Rejestracja: `services.AddLegalAgentPdfParser(o => …)`; własny etap: `AddPdfParserStage<T>()`;
  podmiana: `ReplacePdfParserStage<TOld, TNew>()`. Wbudowane etapy mają stałe `Order` co 100
  (miejsce na etapy użytkownika). Wszystkie usługi bezstanowe → `Singleton`; stan wyłącznie w
  `PipelineContext` tworzonym na konwersję (bezpieczne współbieżnie).
- **Opcje**: `PdfParserOptions` (grupy: `Limits`, `Normalization`, `Artifacts`, `Layout`,
  `Headings`, `Lists`, `Tables`, `Rendering`, `AllowPartialResult`) przez wzorzec Options;
  walidacja `IValidateOptions<PdfParserOptions>` (bez DataAnnotations). Opcjonalne nadpisanie
  opcji per wywołanie w `PdfConversionRequest`.
- **Uzasadnienie**: FR-004–FR-006, US5; mała, standardowa powierzchnia DI.
- **Alternatywy**: MediatR / pipeline behaviors (zbędna zależność), osobne interfejsy per etap
  (utrudnia wstawianie własnych etapów między wbudowane).

## R6. Wykrywanie stylu: pogrubienie, kursywa, tekst niewidoczny/obrócony

- **Decyzja**: Pogrubienie = `Font.IsBold` **lub** nazwa czcionki (po usunięciu prefiksu podzbioru
  `ABCDEF+`) zawiera `Bold|Black|Heavy|Semibold|Demi|,B|-B$` **lub** tryb renderowania
  `FillThenStroke` (sztuczne pogrubienie). Kursywa analogicznie (`Italic|Oblique|It$`, `IsItalic`).
  Pomijane: `TextOrientation != Horizontal` (FR-013), tryb `Neither`/clip-only (niewidoczny),
  kolor wypełnienia biały na stronie bez tła, glify poza `CropBox`.
- **Uzasadnienie**: Generatory PDF (Word, LaTeX, systemy bankowe) sygnalizują pogrubienie
  różnymi drogami; flagi deskryptora czcionki bywają nieustawione.
- **Alternatywy**: analiza grubości kreski glifów — zbyt kosztowna, niedeterministyczna względem
  rasteryzacji.

## R7. Normalizacja Unicode i zgodność z Linuxem

- **Decyzja**: Własna tabela ligatur (ﬀ, ﬁ, ﬂ, ﬃ, ﬄ, ﬅ, ﬆ) + mapowanie spacji specjalnych →
  potem `string.Normalize(NormalizationForm.FormC)`. **Nie** używamy NFKC (zamieniłby „¹” na „1”,
  niszcząc „§ 5¹” i odnośniki przypisów). Porównania bez wielkości liter:
  `ToLowerInvariant` / `StringComparison.OrdinalIgnoreCase`; formatowanie liczb: `CultureInfo.InvariantCulture`.
- **Linux**: .NET używa systemowej biblioteki ICU. Biblioteka nie ustawia
  `InvariantGlobalization`; README dokumentuje wymóg ICU (np. `icu-libs` na Alpine), a test
  jednostkowy weryfikuje normalizację „a + ̨ → ą” — uruchamiany w CI na Ubuntu.
- **Alternatywy**: NFKC (odrzucone j.w.).

## R8. Usuwanie artefaktów stron

- **Decyzja**: Algorytm dwufazowy:
  1. Strefy marginesu per strona (8% wysokości, dla stron obróconych — po normalizacji orientacji).
  2. Odcisk linii: lower-invariant, `\d+` → `#`, zredukowane białe znaki, usunięte znaki
     interpunkcyjne na krańcach. Grupowanie odcisków po (strefa, parzystość strony, kubełek pozycji Y
     ±2% wysokości). Podobieństwo: znormalizowana odległość Levenshteina ≥ 0,85 (własna
     implementacja, O(n·m) na krótkich liniach). Próg: ≥ 50% stron danej parzystości i ≥ 3 strony
     łącznie; dodatkowo sprawdzenie bez rozbicia na parzystość (dokumenty jednostronne).
  3. Numery stron (FR-023): zestaw wyrażeń regularnych + kontrola zgodności z numerem fizycznym
     (stałe przesunięcie wyznaczane jako moda różnic na całym dokumencie).
- **Uzasadnienie**: Odcisk z cyframi zastąpionymi `#` łączy „Dziennik Ustaw – 3 – Poz. 1234” na
  wszystkich stronach; tolerancja 85% pokrywa zmieniające się nazwy rozdziałów w żywej paginie;
  ograniczenie do stref marginesu chroni treść (FR-024).
- **Alternatywy**: usuwanie „pierwszej i ostatniej linii każdej strony” — niszczy treść; analiza
  znaczników `/Artifact` z oznakowanego PDF (Tagged PDF) — PDF z ISAP/banków rzadko są oznakowane;
  może być dodana później jako dodatkowy sygnał.

## R9. Kolejność czytania i kolumny

- **Decyzja**: Po złożeniu linii: wykrycie „rynny” (pionowy pas pustej przestrzeni ≥ 2% szerokości
  strony, przecinający ≥ 60% wysokości regionu tekstu) **tylko** gdy linie po obu stronach są
  długie (średnio ≥ 30% szerokości strony) — to odróżnia dwie kolumny tekstu od tabeli. Regiony
  tabel (R10) są wyznaczane wcześniej i wyłączane z podziału na kolumny.
- **Alternatywy**: `RecursiveXYCut` — tnie tabele (R3).

## R10. Tabele

- **Decyzja**: Detekcja na liniach wizualnych z „komórkami” (segmenty rozdzielone odstępem
  > 2× średniej szerokości spacji). Okno ≥ 3 kolejnych linii z ≥ 2 segmentami → klasteryzacja
  krawędzi lewych segmentów (tolerancja 3% szerokości) → pasy kolumn. Linie siatki ze ścieżek
  graficznych (poziome/pionowe odcinki) wzmacniają granice wierszy/kolumn. Komórki wieloliniowe
  (FR-062): linia z segmentami tylko w części kolumn i odstępem ≤ 1,2× typowego → doklejana do
  poprzedniego wiersza. Kontynuacja na kolejnej stronie: zgodność pasów kolumn (±3%) i pierwszej
  linii po artefaktach; identyczny (po normalizacji) wiersz nagłówka jest pomijany. Rendering:
  tabela GFM; fallback „ | ” + ostrzeżenie + `Table.IsFallback = true`.
- **Uzasadnienie**: Tabele opłat bankowych zwykle mają siatkę lub stałe wyrównanie; łączenie po osi Y
  (FR-030) zapewnia minimum jakości nawet przy fallbacku (kwota w linii z nazwą usługi — SC-005).
- **Alternatywy**: Tabula/Camelot (Java/Python — inna platforma), ML (sprzeczne z prostotą i
  determinizmem).

## R11. Nagłówki i jednostki redakcyjne

- **Decyzja**: Styl podstawowy = moda (rozmiar zaokrąglony do 0,5 pt, pogrubienie) ważona liczbą
  znaków. Kandydaci typograficzni wg FR-041; klasy rozmiarów → poziomy (FR-042). Wzorce prawne
  (wyrażenia regularne, wielkość liter wg wariantu): `^(KSIĘGA|Księga)\s+[IVXLC\w]+`,
  `^(CZĘŚĆ|Część)\s+…`, `^(DZIAŁ|Dział)\s+[IVXLC]+[a-z]?`, `^Rozdział\s+\d+[a-z]?`,
  `^Oddział\s+\d+[a-z]?`, `^Art\.\s*\d+[a-z]*[¹²³⁴⁵⁶⁷⁸⁹⁰]*\.`, `^§\s*\d+[a-z]*[¹-⁹]*\.`.
  Poziomy jednostek wg FR-043 (tylko typy występujące w dokumencie; Art./§ = najgłębsza
  jednostka + 1). Wyłączenie z wykrywania: linie wewnątrz tabel, przypisów oraz odwołania typu
  „art. 5” (mała litera, w środku zdania).
- **Uwaga**: W tekstach ujednoliconych ISAP indeksy („Art. 12¹”) są zwykle mniejszą czcionką
  podniesioną — składanie linii (FR-030) przypisuje je do linii, a normalizacja zapisuje jako znaki
  indeksu górnego Unicode, by wzorzec działał.

## R12. Listy

- **Decyzja**: Klasyfikacja oznaczeń: punktory (zbiór znaków + znaki z czcionek Symbol/Wingdings
  mapowane na `•`), `\d+\)`, `\d+[a-z]?\)`, `[a-z]{1,2}\)`, `\d+\.` (warunkowo — FR-051),
  rzymskie, `\d+(\.\d+)+\.?`, tiret `[–—-]`. Poziom: stos wcięć (X oznaczenia) z tolerancją 1,5 pt
  + priorytet hierarchii prawnej (ust. → pkt → lit. → tiret). Kontynuacja: X linii ≈ X tekstu
  pozycji (±2 pt). Rendering: lista nieuporządkowana CommonMark `- ` z oznaczeniem dosłownym i
  ucieczką (`- 1\) treść`, `- a\) treść`), wcięcie 2 spacje na poziom — chroni oryginalne
  oznaczenia (FR-052) i zapobiega przenumerowaniu przez renderery.
- **Alternatywy**: listy uporządkowane Markdown `1.` — tracą „1)”, „a)”, przenumerowują.

## R13. Determinizm

- **Decyzja**: brak równoległości w potoku; wszystkie kolekcje wynikowe sortowane jawnie (Y, X,
  potem indeks litery); brak `Dictionary` iterowanych bez sortowania; arytmetyka `double` z
  zaokrąglaniem progów do 0,01 pt przed porównaniami; LF jako separator linii; brak dat/czasu w
  wyniku (czas trwania tylko w raporcie, poza porównaniami golden — `Report.Elapsed` wykluczony z
  testów determinizmu).
- **Uzasadnienie**: Zasada III konstytucji, FR-008, SC-006.

## R14. Testy i korpus

- **Decyzja**:
  - Framework: **xUnit v3** (`xunit.v3`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`;
    wersje przypięte w `Directory.Packages.props`, najnowsze stabilne w chwili implementacji).
    Asercje: wbudowane xUnit (bez FluentAssertions — zmiana licencji v8, zbędna zależność).
  - **Syntetyczne PDF** budowane w testach przez `PdfDocumentBuilder` z PdfPig (już jest zależnością)
    z czcionką TrueType **Noto Sans** Regular/Bold/Italic (licencja OFL, w `tests/…/Fonts/`) —
    czcionki standardowe PDF nie obsługują polskich znaków. Pozwala to precyzyjnie testować każdą
    heurystykę (pozycje, rozmiary, pogrubienie) offline i deterministycznie (TDD per FR).
  - **Korpus publiczny** (`tests/…/Corpus/acts/`): ≥ 4 akty prawne pobrane jednorazowo z ISAP i
    zapisane w repozytorium (art. 4 pr. aut.) + pliki `*.expected.md` (przegląd ręczny).
  - **Korpus bankowy**: syntetyczne regulaminy/taryfy odtwarzające układ (stopki rejestrowe,
    tabele wielostronicowe, dwie kolumny) w `Corpus/banking/`. Prawdziwe regulaminy mogą być
    użyte lokalnie przez katalog wskazany zmienną `LEGALAGENT_PRIVATE_CORPUS` (testy pomijane, gdy
    brak — katalog w `.gitignore`).
  - **Golden files**: własny pomocnik porównujący Markdown z `*.expected.md`; przy różnicy zapisuje
    `*.actual.md` obok (ignorowany przez git); aktualizacja wzorców tylko jawnie
    (`UPDATE_GOLDEN=1`). Bez biblioteki Verify (YAGNI).
  - **Metryki SC**: test korpusowy liczy recall/precision nagłówków, poprawność list, % kwot w
    wierszu z nazwą usługi, kompletność słów — na podstawie porównania modelu z oczekiwanym
    Markdown; progi = kryteria SC-001…SC-005.
- **Alternatywy**: Verify (dodatkowa zależność), prawdziwe PDF mBanku w repo (niepewna licencja).

## R15. Aplikacja wykonawcza

- **Decyzja**: Minimalna konsola `LegalAgent.PdfParser.Cli`: `convert <wejście.pdf> [-o wyjście.md]
  [--report raport.json] [--no-page-markers] [--allow-partial]`; ręczne parsowanie argumentów
  (bez System.CommandLine), `System.Text.Json` do raportu, kody wyjścia wg kontraktu.
- **Uzasadnienie**: Konstytucja wymaga osobnej aplikacji w solucji; cienka warstwa służy też do
  walidacji z quickstart. Pobieranie z sieci i lista źródeł — poza zakresem tej funkcjonalności.
- **Alternatywy**: `System.CommandLine` — dodatkowa zależność zbędna dla jednej komendy i
  czterech opcji (zasada VI).

## R16. CI na Linuxie

- **Decyzja**: Workflow GitHub Actions `ubuntu-latest`: `actions/setup-dotnet` (9.0.x runtime +
  SDK z `global.json`), `dotnet build LegalAgent.slnx -c Release`, `dotnet test LegalAgent.slnx`.
- **Uzasadnienie**: Konstytucja (Linux), SC-006, weryfikacja ICU (R7).
