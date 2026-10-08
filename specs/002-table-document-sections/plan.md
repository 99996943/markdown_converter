# Implementation Plan: Dokumenty zbudowane jako jedna wielostronicowa tabela dwukolumnowa (tabela-dokument)

**Branch**: `002-table-document-sections` (do utworzenia z `001-legal-pdf-parser` lub `main` po scaleniu 001 — decyzja właściciela; dziś praca na `001-legal-pdf-parser`) | **Date**: 2026-10-08 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/002-table-document-sections/spec.md`

## Summary

Regulaminy promocji mBanku (wzorzec `mbank-reg3.pdf`) to okładka + jedna tabela z pełną siatką linii na
str. 2–12: wąska lewa kolumna z nazwą sekcji, szeroka prawa z treścią (listy „•”/„o”, pogrubione
śródtytuły, linki). Dziś tabela rozpada się na GFM, luźne akapity, fałszywe kolumny i tryb awaryjny,
a nagłówkami zostają śródtytuły zamiast nazw sekcji.

Podejście (research R1): nowy etap potoku **`TableDocumentStage` (Order 560)**, wzorowany na schematach
kroków FR-067. Rozpoznaje ramkę strony z linii siatki (3 pionowe linie; granice wierszy tylko z poziomych
linii przez całą szerokość — podkreślenia linków ignorowane), łączy strony w region, sprawdza kryteria
FR-080 (2 kolumny, lewa ≤ 35%, ≥ 50% stron, długie komórki z listami/przejściami przez stronę).
Linie dzieli na granicy kolumn; lewe komórki stają się nagłówkami `##` (`SectionKind.TableDocumentSection`),
wiersz nazw kolumn artefaktem, a prawe komórki zostają zwykłym tekstem jednej kolumny, przestawionym
w kolejność „nazwa → treść” — dalej przechodzą normalne wykrywanie list, nagłówków i akapitów, więc
treść łączy się przez wiersze i strony. Uzupełnienia: brak nagłówków typograficznych od początku
tabeli-dokumentu (FR-087), podpis grafiki (FR-088, nowe `LayoutPage.ImageAreas`), „Obowiązuje od …” jako
akapit (FR-093), koniec akapitu „zmieściłoby się” i granica pogrubienia w prawej kolumnie (FR-085/086),
punktor „o” w innej czcionce (nowe `LayoutGlyph.FontName`), łącznik w adresie (FR-094). Zmiany
publicznego API są addytywne (kontrakt 1.1.0).

## Technical Context

**Language/Version**: C# 13 / .NET 9 (`net9.0`), SDK przypięty w `global.json` — bez zmian (001 R1)

**Primary Dependencies**: bez zmian — `PdfPig` 0.1.16 (już dostarcza `Page.GetImages()` z `Bounds` i
`Letter.FontName`), `Microsoft.Extensions.*` 9.0.20. Dane testowe: dodatkowo czcionka **Noto Sans Mono**
(OFL, plik `.ttf` w `tests/.../Fixtures/Fonts`, nie pakiet)

**Storage**: N/A

**Testing**: xUnit v3; syntetyczne PDF (`SyntheticPdfBuilder`, `BankingCorpusGenerator`), golden files,
metryki SC, testy etapów (`StageHarness`, `LayoutFactory`), opcjonalny korpus prywatny
(`LEGALAGENT_PRIVATE_CORPUS`)

**Target Platform**: Linux (CI ubuntu-latest) i Windows; wynik niezależny od zainstalowanych czcionek (FR-011a)

**Project Type**: biblioteka klas + cienka aplikacja konsolowa (bez zmian w CLI poza nowymi opcjami
dostępnymi automatycznie przez zmienne środowiskowe)

**Performance Goals**: bez regresji SC-007 (100 stron < 10 s); nowy etap liniowy względem liczby linii
i odcinków siatki na stronie

**Constraints**: wynik zawiera wyłącznie tekst PDF (FR-091); deterministyczny bajt-w-bajt; brak reguł
wydawcy (FR-007 — rozpoznanie wyłącznie z geometrii i typografii, nie z tekstu „Definicje”); golden
`Corpus/acts` i `Corpus/banking` bez zmian; prywatne regulaminy bez zmian poza FR-093

**Scale/Scope**: 1 nowy etap (~350 linii), zmiany w 8 istniejących klasach, 15 wymagań
(FR-080 – FR-094), 8 kryteriów (SC-010 – SC-017), 2 nowe dokumenty syntetyczne

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Zasada / ograniczenie | Ocena | Jak spełnione |
|-----------------------|-------|---------------|
| I. Test-First / TDD | ✅ | Każde FR ma test czerwony przed kodem: testy etapu na `LayoutFactory`, dokument syntetyczny + golden przed implementacją, osobne commity red/green (uzgodniony sposób pracy z 001). Testy offline; prawdziwy PDF banku tylko w teście opcjonalnym. |
| II. Wierność źródłu | ✅ | FR-091: brak dopisanego tekstu (nazwa sekcji = oryginalna lewa komórka; puste komórki bez nazw); jedyny pomijany tekst — wiersz nazw kolumn, jawnie w raporcie (`HeaderRowText`, `DroppedHeaderRows`); FR-094 chroni adresy przed utratą łącznika. |
| III. Powtarzalność | ✅ | Deterministyczne sortowania (strony, Y, X), kultura niezmienna (regex FR-093 `CultureInvariant`), test determinizmu + CI Linux (SC-017). |
| IV. Odporność na błędy | ✅ | Brak nowych wywołań zewnętrznych; region niespełniający kryteriów wraca do dotychczasowej ścieżki (bez wyjątków). |
| V. Bezpieczeństwo i konfiguracja | ✅ | Prawdziwe regulaminy tylko w `Corpus/private` (`.gitignore`), punkt odniesienia `baseline/` też poza git; progi w opcjach. |
| VI. Prostota / zależności | ✅ | Zero nowych pakietów; nowy etap zamiast rozbudowy 737-liniowego `TableDetectionStage`; czcionka testowa OFL jako plik danych. |
| VII. Dokumentacja | ✅ | README: opis tabeli-dokumentu, nowe opcje, nowa wartość `SectionKind`, wskazówka dla chunkera (sekcje `##`). XML-doc nowych typów. |
| Biblioteka ogólna, bez reguł wydawcy | ✅ | Rozpoznanie z siatki/typografii; „Obowiązuje od” to ogólny wzorzec polskich regulaminów (jak FR-023 dla numerów stron), nie nazwa banku. |
| API jawne, SemVer | ✅ | Zmiany addytywne → kontrakt 1.1.0 ([contracts/public-api.md](./contracts/public-api.md)); nowa wartość enum opisana dla klientów. |
| `.slnx`, Linux, osobny projekt testowy | ✅ | Bez zmian struktury solucji. |
| Format OKF | N/A | Nie dotyczy. |

**Wynik bramki (przed Phase 0)**: PASS.

**Re-check po Phase 1**: PASS — projekt danych dodaje wyłącznie pola/wartości (bez zmian łamiących),
bez nowych zależności i projektów. Dwa świadome rozszerzenia zakresu względem pierwotnego opisu
wynikające z pomiarów (R9, R11) zostały wpisane do spec (FR-085, FR-088, FR-094) przed planem zadań.

## Project Structure

### Documentation (this feature)

```text
specs/002-table-document-sections/
├── plan.md              # Ten plik
├── research.md          # Phase 0: pomiary mbank-reg3 + decyzje R1–R14
├── data-model.md        # Phase 1: zmiany modelu publicznego, opcji, modelu układu, kolejności etapów
├── quickstart.md        # Phase 1: walidacja (testy, mbank-reg3, brak regresji, wyłączenie)
├── contracts/
│   ├── public-api.md    # delta API 1.0.0 → 1.1.0
│   └── markdown-output.md # nowe elementy wyniku i niezmienniki 7–9
├── checklists/requirements.md
└── tasks.md             # Phase 2 (/speckit-tasks)
```

### Source Code (repository root)

```text
src/LegalAgent.PdfParser/
├── Model/
│   ├── Section.cs                     # SectionKind.TableDocumentSection
│   └── ConversionReport.cs            # TableDocuments + TableDocumentSummary
├── Options/
│   ├── PdfParserOptions.cs            # TableOptions.DetectTableDocuments + progi; HeadingOptions.DetectImageCaptions, ValidityLineAsParagraph
│   └── PdfParserOptionsValidator.cs   # zakresy nowych pól
├── Layout/
│   ├── LayoutGlyph.cs                 # FontName (opcjonalny)
│   ├── LayoutPage.cs                  # ImageAreas
│   └── LayoutAnnotations.cs           # TableDocumentIndex
├── Pipeline/
│   ├── StageOrder.cs                  # TableDocument = 560
│   ├── BuiltInStages.cs               # rejestracja nowego etapu
│   ├── PipelineContext.cs             # TableDocuments (regiony, internal)
│   └── ReportBuilder.cs               # AddTableDocument(...)
├── Stages/
│   ├── PageExtractionStage.cs         # FontName, ImageAreas
│   ├── TableDocumentStage.cs          # NOWY: ramka, region, kryteria, wiersz nazw, rozcinanie, nagłówki sekcji, kolejność
│   ├── TableDetectionStage.cs         # pomija linie z tabledoc.index
│   ├── ReadingOrderStage.cs           # pomija linie z tabledoc.index
│   ├── ListDetectionStage.cs          # punktor „o” w innej czcionce
│   ├── HeadingDetectionStage.cs       # FR-087, FR-088, FR-093
│   └── BlockAssemblyStage.cs          # koniec akapitu „zmieściłoby się”, granica pogrubienia (linie tabeli-dokumentu)
└── Text/Hyphenation.cs                # FR-094

tests/LegalAgent.PdfParser.Tests/
├── Fixtures/
│   ├── Fonts/NotoSansMono-Regular.ttf # NOWY (OFL)
│   ├── SyntheticPdfBuilder.cs         # wybór kroju mono
│   └── BankingCorpusGenerator.cs      # regulamin-promocji-tabela (+ Truth), dokument negatywny z tabelą definicji
├── Unit/Stages/TableDocumentStageTests.cs   # NOWY
├── Unit/Stages/…                      # HeadingDetection, BlockAssembly, ListDetection — nowe przypadki
├── Unit/Text/HyphenationTests.cs      # FR-094
├── Unit/Options/…                     # nowe opcje i walidator
├── Integration/TableDocumentsIntegrationTests.cs # NOWY: US1–US4 end-to-end na syntetycznych PDF
└── Corpus/
    ├── banking/regulamin-promocji-tabela.expected.md   # NOWY golden
    ├── banking/regulamin-z-tabela-definicji.expected.md # NOWY golden (negatywny: GFM)
    ├── QualityMetricsTests.cs         # SC-010 – SC-015
    └── PrivateCorpusTests.cs          # mbank-reg3 + porównanie z baseline (opcjonalne)
```

**Structure Decision**: bez zmian struktury solucji (konstytucja: biblioteka, CLI, testy w `LegalAgent.slnx`).
Logika tabeli-dokumentu w jednym nowym etapie (testowalnym osobno); pozostałe etapy dostają małe,
lokalne warunki oparte na adnotacji `tabledoc.index`, jak przy FR-067.

## Kolejność realizacji (wskazówka dla /speckit-tasks)

1. **Przygotowanie**: punkt odniesienia `Corpus/private/baseline/` (quickstart §0); Noto Sans Mono +
   wybór kroju w `SyntheticPdfBuilder`; szkielet typów (opcje, `SectionKind`, raport, adnotacja,
   `StageOrder`, `FontName`, `ImageAreas`) — bez zmiany zachowania.
2. **Dokument syntetyczny i golden (red)**: `regulamin-promocji-tabela` + `Truth` + oczekiwany Markdown
   spisany ręcznie wg spec (nie z wyniku), dokument negatywny z tabelą definicji.
3. **US1 + US2 (P1)**: `TableDocumentStage` — ramka i granice wierszy (R2), region i kryteria (R3),
   wiersz nazw (R4), rozcinanie (R5), nazwy sekcji (R6), kolejność i kolumna (R7); pomijanie w
   `TableDetection`/`ReadingOrder`; punktor „o” (R10); akapity R9; FR-094 (R11); raport (R12).
4. **US3 (P2)**: nagłówki — FR-087/086, FR-088 (`ImageAreas`), FR-093.
5. **US4 (P1, ciągle)**: po każdym kroku golden `acts`/`banking` bez zmian; na końcu porównanie prywatnych
   wyników z `baseline/` (tylko FR-093) i weryfikacja ręczna `mbank-reg3` (quickstart §2).
6. **Polish**: metryki SC-010 – SC-015, niezmienniki 7–9, `PrivateCorpusTests`, README, kontrakt
   publiczny 1.1.0 w XML-doc, CI Linux (SC-017), notatki przekazania w tym planie.

## Stan prac i przekazanie

**2026-10-08 — T001–T012 (Setup + Foundational) zrobione** na gałęzi `002-table-document-sections` (utworzonej z
`001-legal-pdf-parser`; nie wypchnięta). Pełny zestaw: 829 testów, 0 błędów, 1 pominięty (korpus prywatny bez zmiennej).
Wynik biblioteki bez zmian — `TableDocumentStage` jeszcze niezarejestrowany (rejestracja w T023).

- Punkt odniesienia: `tests/.../Corpus/private/baseline/*.md` + `*.report.json` (poza git) — identyczne z dotychczasowymi `*.md`.
- `SyntheticPdfBuilder`: krój mono (Noto Sans Mono, OFL) i `TextWidth(...)` do łamania tekstu na zmierzonych szerokościach.
- `BankingCorpusGenerator`: `regulamin-promocji-tabela` i `regulamin-z-tabela-definicji` (dostęp przez `Pdf(name)`/`Truth(name)`,
  jeszcze nie w `Names` — golden w T035). Obecna biblioteka daje na dokumencie syntetycznym te same błędy co na `mbank-reg3`
  (fałszywe kolumny, rozbita nazwa „Warunki/zasady … promocji”, `### bank.example`, `### MOJE OŚWIADCZENIA`).
- `TableDocumentStage` rozpoznaje region (R2, R3): na `mbank-reg3` str. 2–12; na `mbank-regulamin-pdp`, `mbank-reg2`,
  dokumencie z tabelą definicji i obwieszczeniu MSZ — brak regionu.
- Pomocnik testów `Fixtures/TableSheet.cs` buduje strony tabeli-dokumentu bez PDF (linie siatki w dwóch kawałkach, wiersz
  nazw kolumn, podkreślenia linków).

**2026-10-08 — US1 + US2 (T013–T025) zrobione.** `TableDocumentStage` zarejestrowany (Order 560): wiersz nazw kolumn →
artefakt, linie rozcinane na granicy kolumn, nazwy sekcji → `##` (`SectionKind.TableDocumentSection`), nazwa przerwana
stroną → jeden nagłówek, kolejność „nazwa → treść” wiersz po wierszu, `column.left/right` regionu, raport `TableDocuments`.
`TableDetection`/`ReadingOrder` pomijają linie z `tabledoc.index`. R9 w `BlockAssemblyStage` (tylko linie tabeli-dokumentu):
koniec akapitu, gdy następna linia zaczyna się **wielką literą** i jej pierwsze słowo (jednoliterowe razem z następnym)
zmieściłoby się do `column.right`; zmiana „cała linia pogrubiona”; wcięcie liczone od pozycji pióra pierwszej litery
(obrys „J” wystaje 1,6 pt w lewo). Punktor „o” po rodzinie czcionki (`LayoutGlyph.FontName`, `Text/FontFamily.cs`),
FR-094 (łącznik w adresie, także przed cyfrą i w adresie na 3 liniach — `Paragraph.LastWord`).

Weryfikacja `mbank-reg3` (T024/T025): region str. 2–12, 9 sekcji `##`, 8 wierszy nazw kolumn pominiętych, 0 tabel GFM,
0 awaryjnych, brak TBL001; definicje „Ważne pojęcia” jako osobne akapity; „o” zagnieżdżone; akapity i pozycje list przez
granice stron z `<!-- page: N -->`. Błędy znalezione na prawdziwym PDF i poprawione test-first: ręczne złamania linii w
środku zdania (FR-085 doprecyzowany: reguła tylko przed wielką literą), data „01.01.2026 r.” na początku linii brana za
oznaczenie konspektu (wzorzec konspektu: składowe 1–3 cyfr), adres URL złamany przed cyfrą i na 3 liniach. Zostaje do US3:
`## Obowiązuje od …`, `## mBank.pl`, `###` śródtytuły (`Korzyści obowiązujące…`, `Dodatkowo:`), `### MOJE OŚWIADCZENIA`.

Porównanie z `baseline/`: `mbank-regulamin-pdp`, `mbank-reg1` — bez zmian; `mbank-reg2` — jedyna różnica to poprawka daty
(„…na dzień 01.01.2025 r. wynosi…” w jednym akapicie zamiast fałszywej pozycji listy) — uwzględnić w T033.

**2026-10-08 — US3 (T026–T031) i T034 zrobione.** `LayoutPage.ImageAreas` (PdfPig `BoundingBox`, Y w dół).
`HeadingDetectionStage`: flaga `Plain` (nie kandydat, nie wyśrodkowany, nie dołączana do bloku tytułu ani do nagłówka
wieloliniowego) dla linii od górnej krawędzi pierwszej tabeli-dokumentu (FR-087), podpisu obrazu (FR-088: na obrazie
albo górna krawędź ≤ 3 wysokości linii pod nim, w szerokości obrazu ±10%) i linii `^obowiązuje\s+od\b` na pierwszej
stronie (FR-093). `mbank-reg3`: nagłówki = tytuł + 9 nazw sekcji (SC-012); „Obowiązuje od …” akapit, „**mBank.pl**”,
śródtytuły i „**MOJE OŚWIADCZENIA**” jako pogrubione akapity.

**Do decyzji właściciela (blokuje T033)**: w `mbank-regulamin-pdp` i `mbank-reg1` poza linią FR-093 zmienia się poziom
47 nagłówków (`###` → `##`) — linia „obowiązuje od” zajmowała dotąd klasę rozmiaru poziomu 2, po jej usunięciu nagłówki
sekcji przesuwają się pod tytuł. Hierarchia jest teraz bez luki (`#` → `##`), ale to więcej niż „jedyna zmiana” z SC-016.
`mbank-reg2`: to samo + poprawka daty (T024). T034 (`DetectTableDocuments=false` dla `mbank-reg3`): różnice względem
`baseline/` wyłącznie z FR-088, FR-093 i tego samego przesunięcia poziomów; tabele i TBL001 jak w 001.

**Test wydajności**: `PerformanceTests.HundredAndFourteenPageAct_ConvertsInUnderTenSeconds` pada lokalnie w pełnym
przebiegu (10,9–16 s przy równoległych testach), osobno 3,6–3,9 s — tyle samo co przed spec 002 (zmierzone na 74843fd).
CI uruchamia kategorię `Performance` osobno (`ci.yml`), więc lokalnie: `dotnet test … --filter "Category!=Performance"` +
osobno `--filter "Category=Performance"`.

**2026-10-08 — US4 i Polish (T032–T039) zrobione; T040 czeka na CI.** Golden `banking/regulamin-promocji-tabela` i
`banking/regulamin-z-tabela-definicji` spisane ręcznie z tekstu generatora i spec (jedyna różnica przy pierwszym
porównaniu: znacznik strony tabeli przechodzącej przez stronę stoi po tabeli — konwencja 001) i dodane do `Names`.
Metryki SC-010 – SC-015 (`QualityMetricsTests`) i niezmienniki 7–9 (`MarkdownInvariantsTests`) zielone. Korpus prywatny:
`*.expected.md` dla `mbank-reg3` (po przeglądzie i kontroli kompletności słów względem surowego tekstu PdfPig) oraz
`mbank-regulamin-pdp`, `mbank-reg1`, `mbank-reg2` (baseline + FR-093 + przesunięcie poziomów — decyzja właściciela,
SC-016 doprecyzowane — + poprawka daty w `reg2`); `PrivateCorpusTests` z `LEGALAGENT_PRIVATE_CORPUS` zielony i sprawdza
brak wierszy tabel w dokumentach z tabelą-dokumentem. README, XML-doc, odnośnik do kontraktu 1.1.0, wpis w REVIEW.md.

Stan testów (jak w CI): `--filter "Category!=Performance"` 910 zielonych + 1 pominięty, `Category=Performance` 2/2.
**Do zrobienia (T040)**: wypchnąć gałąź `002-table-document-sections` i potwierdzić zielone CI na Ubuntu (SC-017);
gałąź nie była jeszcze wypychana.

**Uwaga do pracy z gitem**: w katalogu głównym leżą nieśledzone pliki właściciela (`Akt prawny - …`, `Regulamin … mBanku …`
— PDF banku); dodawać do commitów wyłącznie konkretne ścieżki, nigdy `git add -A`.

## Complexity Tracking

Brak naruszeń konstytucji wymagających uzasadnienia.
