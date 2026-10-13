# Implementation Plan: Ostrzeżenia FTN001_OrphanFootnote po spec 010 (parser 1.3.1)

**Branch**: `011-ftn001-false-warnings` (od `main` po scaleniu PR #14; wersja 1.3.0) | **Date**: 2026-10-13 |
**Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/011-ftn001-false-warnings/spec.md`

## Summary

Diagnoza ([research.md](./research.md)) obaliła założenie opisu: 5 nowych ostrzeżeń FTN001 aktu D20081505Lj nie jest
fałszywych. To prawdziwe przypisy, których odnośnik („zm.¹⁾)”, „ogłoszenia¹²⁾,”: podniesiony ciąg i pełnowymiarowa
interpunkcja za nim) parser nigdy nie rozpoznawał. W 1.2.3 maskował je **samoodnośnik**: `FindMarkers` brał
podniesioną etykietę otwierającą linię definicji za odnośnik do tej samej definicji. Spec 010 wyłączyło to tylko pod
kreską przypisów (`BelowFirstRule`), więc na stronach bez kreski samoodnośnik działa dalej (s. 67 aktu: 6 definicji).

Decyzja właściciela (wariant a′):
1. **Koniec samoodnośnika wszędzie** (FR-801): znacznik, który jest podniesioną etykietą otwierającą linię uznaną za
   linię przypisu, nie wiąże definicji (`LinkMarkers`). Zbiór `referenced`, a z nim wykrywanie obszaru i etykiet,
   zostaje bez zmian, więc Markdown się nie zmienia. Sonda na kopii kodu: 0 różnic Markdownu we wszystkich
   dokumentach, FTN001 zmienia się tylko w akcie o służbie cywilnej (5 → 11).
2. **Znane ograniczenia** (FR-802): README opisuje klasy nierozpoznanych odnośników (R2: K1–K3).
3. **Bramka** (FR-810): liczba FTN001 każdego aktu `Corpus/acts` utrwalona w teście, uruchamianym w CI.
4. Wersja 1.3.1 (PATCH), `refresh`/`verify`, `publish/`.

## Technical Context

**Language/Version**: C# 13 / .NET 9 (`net9.0`), SDK z `global.json` — bez zmian

**Primary Dependencies**: bez zmian (`PdfPig` 0.1.16)

**Storage**: N/A (prywatny korpus w `tests/LegalAgent.PdfParser.Tests/Corpus/private`, poza git)

**Testing**: xUnit v3 na MTP; `Unit/Stages/FootnoteDetectionStageTests` (strona bez kreski z `PageSketch`/
`LayoutFactory`, jak `SmallFontBlockAtBottomWithoutRule_IsAFootnote_WhenItsLabelMatchesAReference`), replika
strony aktu w `Integration/` (`SyntheticPdfBuilder`: tekst 12 pt, odnośnik „3)” 8 pt podniesiony z przecinkiem
12 pt za nim, blok definicji 10 pt z etykietami 6,5 pt bez kreski), bramka w `Corpus/GoldenTests` (nowa teoria na
aktach); goldeny `Corpus/acts`, `Corpus/banking`, prywatny korpus, korpus syntetyczny (`refresh`, `verify`)

**Target Platform**: Windows i Linux (CI ubuntu-latest); wynik deterministyczny

**Project Type**: biblioteka klas (`LegalAgent.PdfParser`) + CLI; chunker i aplikacje bez zmian

**Performance Goals**: bez regresji SC-007; bramka dokłada jedną konwersję każdego z 6 aktów (albo korzysta z
konwersji testu goldena, jeśli da się to zrobić bez komplikowania testów)

**Constraints**: Markdown i chunki bez zmian (FR-807); goldeny bez zmian (FR-805); bez nowych reguł wykrywania
(FR-809); reguła z geometrii i roli linii, bez numerów etykiet, stron i tekstu aktu

**Scale/Scope**: jedna zmiana w `FootnoteDetectionStage.Execute`/`LinkMarkers` (filtr znaczników), testy, README
(„Znane ograniczenia”, „Ostrzeżenia i raport”: znaczenie FTN001), CLAUDE.md (dopisek spec 011), wersja; 11 wymagań
FR-800…FR-810, 6 kryteriów SC-120…SC-125

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Zasada / ograniczenie | Ocena | Jak spełnione |
|---|---|---|
| I. Test-First / TDD | ✅ | Test etapu i replika strony bez kreski oraz bramka liczby FTN001 jako osobny commit red (na 1.3.0: brak FTN001 dla definicji z samoodnośnikiem, akt 5 zamiast 11) → poprawka green. Testy offline. |
| II. Wierność źródłu | ✅ | Markdown bez zmian; raport przestaje twierdzić, że definicja ma odnośnik, którego w tekście nie rozpoznano. |
| III. Powtarzalność | ✅ | Filtr deterministyczny; `refresh`/`verify` czyste (zmienia się tylko wersja w manifeście). |
| IV. Odporność na błędy | ✅ | Brak wywołań zewnętrznych; ostrzeżenie nie zmienia kodu wyjścia CLI. |
| V. Bezpieczeństwo i konfiguracja | ✅ | Replika bez treści prywatnego aktu (układ, rozmiary, etykiety); prywatne PDF-y poza git. |
| VI. Prostota | ✅ | Jeden warunek w istniejącej pętli; bez nowych opcji, etapów i zależności. |
| VII. Dokumentacja | ✅ | README parsera (znaczenie FTN001, znane ograniczenia, wersja 1.3.1), CLAUDE.md, handoff. |
| Biblioteka ogólna | ✅ | Reguła z roli linii i położenia znacznika (pierwsze słowo, początek słowa). |
| API jawne, SemVer | ✅ | Publiczne API bez zmian; zmiana tylko raportu → 1.3.1 (PATCH); kontrakt Markdown bez zmian. |

**Wynik bramki (przed Phase 0)**: PASS.

**Re-check po Phase 1**: PASS — sonda (research R3) potwierdza brak różnic Markdownu; jedyna zmiana obserwowalna to
`FootnoteDraft.IsOrphan` i FTN001 ([contracts/conversion-report.md](./contracts/conversion-report.md)).

## Project Structure

### Documentation (this feature)

```text
specs/011-ftn001-false-warnings/
├── plan.md              # ten plik
├── research.md          # R1 przyczyna 5 ostrzeżeń, R2 porównanie 1.2.3/1.3.0 (K1–K3), R3 warianty i sonda
├── data-model.md        # FootnoteDraft.IsOrphan, Marker, reguła samoodnośnika
├── quickstart.md        # weryfikacja: testy, bramka, akt prywatny, korpusy, publish/
├── contracts/
│   └── conversion-report.md   # znaczenie FTN001 od 1.3.1
├── checklists/requirements.md
└── tasks.md             # /speckit-tasks
```

### Source Code (repository root)

```text
src/LegalAgent.PdfParser/
├── Stages/FootnoteDetectionStage.cs     # filtr samoodnośnika przy wiązaniu znaczników
└── README.md                            # FTN001, znane ograniczenia, 1.3.1
Directory.Build.props                    # <Version>1.3.1</Version>

tests/LegalAgent.PdfParser.Tests/
├── Unit/Stages/FootnoteDetectionStageTests.cs        # definicja bez kreski z samoodnośnikiem → sierota
├── Integration/FootnoteWarningsIntegrationTests.cs   # replika s. 67 aktu (SyntheticPdfBuilder)
└── Corpus/GoldenTests.cs                             # bramka: liczba FTN001 każdego aktu

corpus/manifest.json                     # wersja parsera po refresh
CLAUDE.md                                # dopisek spec 011
```

**Structure Decision**: bez nowych projektów; nowy plik testów integracyjnych tylko dla repliki (albo test w
istniejącym `BankTablesIntegrationTests`, jeśli tam są już pomocniki przypisów).

## Kolejność realizacji (wskazówka dla /speckit-tasks)

1. **Setup**: pomiar wyjściowy (FTN001 i hash Markdownu wszystkich dokumentów na 1.3.0, w tym prywatnych i
   `publish/…/downloads`) zapisany w handoffie.
2. **US1**: research.md jest gotowy (R1–R3); zadanie tylko potwierdza go po implementacji (SC-120).
3. **US2 red**: (a) test etapu: strona bez kreski, blok definicji z podniesionymi etykietami „2)”, „3)”, odnośnik
   rozpoznany do „2)”, do „3)” z przecinkiem za nim → oczekiwane FTN001 dla „3)”, brak dla „2)”, tekst definicji
   bez zmian; (b) replika strony w `SyntheticPdfBuilder`, oczekiwany Markdown jak dziś i FTN001 dla „3)”;
   (c) bramka FTN001 na aktach (2, 5, 24, 10, 6, 11). Commit red.
4. **US2 green**: filtr w `FootnoteDetectionStage` (znacznik `WordIndex == 0`, `GlyphStart == 0` na linii z rolą
   `Footnote` nie wiąże definicji). Commit green.
5. **US3**: pełny zestaw testów (bez `Performance`), prywatny korpus (`LEGALAGENT_PRIVATE_CORPUS`, bramka
   `RegulationNumbers_HaveNoExcess`), porównanie FTN001 i Markdownu z pomiarem z kroku 1.
6. **Polish**: wersja 1.3.1, `refresh` + `verify`, README, CLAUDE.md, `publish/` (biblioteka, NuGet 1.3.1, exe
   generatora FAQ, konwersja regulaminów z `publish/`), handoff.

## Stan prac i przekazanie

- 2026-10-13: spec, clarify (2 pytania), plan z diagnozą. Narzędzie diagnostyczne i worktree 1.2.3 (`ffd5f7c`) oraz
  kopia z sondą wariantu a′ były w scratchpadzie sesji (poza repozytorium); sonda: filtr w `LinkMarkers`, 0 różnic
  Markdownu, FTN001 tylko akt o służbie cywilnej 5 → 11.
- Decyzje właściciela: wariant a′ (bez rozpoznawania nowych odnośników, bez zmian goldenów); bramka na akcie
  publicznym.
- 2026-10-13: tasks.md (T001–T017).
- 2026-10-13, `/speckit-implement`:
  - **T001** (pomiar wyjściowy na 1.3.0, `ad0de8f`) i **T011** (po poprawce): tymczasowy test
    `tests/LegalAgent.PdfParser.Tests/Scratch/Ftn001Measure.cs` (niecommitowany, `Scratch/` w `.git/info/exclude`)
    konwertuje każdy dokument potokiem `BuiltInStages` z sondą po etapie przypisów. Liczy FTN001 z raportu,
    samoodnośniki (szkice, których wszystkie powiązane słowa leżą w liniach z rolą `Footnote`) i SHA-256 Markdownu.
    Zamiast CLI użyty ten sam konwerter w teście (ten sam zestaw etapów), bo `Corpus/banking` nie ma PDF-ów na dysku.
    Wynik T001 zgodny z research R2. Jedyny dokument spoza R2: `Corpus/banking/regulamin-rachunku` z 1 FTN001,
    istniejącym od spec 001 (odnośnik „¹” to znak pełnej wielkości), dopisany do R2 (**T003**: brak klasy spoza K1–K3).
  - **T002**: pełny zestaw `Category!=Performance` na 1.3.0: 2231 testów, 0 niepowodzeń, 13 pominiętych; testy
    parsera z `LEGALAGENT_PRIVATE_CORPUS`: zielone (bramka `RegulationNumbers_HaveNoExcess` przechodzi). Pięć starych
    plików `*.actual.md` z 2026-10-12 (kredyt-konsumencki, obwieszczenie-msz, firm-1, firm-2, firm-5; pozostałości
    pracy nad spec 010) przeniesione do scratchpada sesji przed pomiarem; żaden nowy nie powstał.
  - **T004–T007** red `d8a5213`: na 1.3.0 padają test etapu (`[False, False]` zamiast `[False, True]`), raport
    repliki (0 FTN001 zamiast „2)” i „3)”; Markdown repliki zgodny z literałem) i bramka `ustawa-o-sluzbie-cywilnej`
    (5 zamiast 11); reszta zielona (test wydajności `HundredAndFourteenPageAct` padł raz przy równoległym obciążeniu,
    osobno zielony).
  - **T008–T009** green `80d0fb4`: `IsDefinitionLabel` w pętli wiązania. Pełny zestaw: 2240 testów, 0 niepowodzeń,
    13 pominiętych, 0 `*.actual.md`; parser z prywatnym korpusem: 1254 testy, 0 niepowodzeń, 3 pominięte (jak
    przed zmianą).
  - **T010**: jw. — goldeny bez zmian, `RegulationNumbers_HaveNoExcess`, `FootnoteDetectionStageTests` i
    `BankTablesIntegrationTests` bez zmian w kodzie testów.
  - **T011**: Markdown identyczny we wszystkich 40 dokumentach; FTN001 zmienia się tylko w akcie o służbie cywilnej
    (oba pliki: 5 → 11), samoodnośniki 0 wszędzie (SC-121). Akt prywatny przez CLI: FTN001 na s. 13, 67 ×6
    („2)”–„7)”), 68, 70, 71 ×2; `convert` i `chunk` (`--id AKT-2008-1505 --designation "Dz.U. 2008 nr 150 poz. 1505"`)
    bajt w bajt jak `publish/porownanie-1.3.0/akt-D20081505Lj.new.{md,chunks.jsonl}`.

| Grupa | Dokument | FTN001 1.3.0 | Samoodn. 1.3.0 | FTN001 nowy | Samoodn. nowy | SHA-256 Markdownu (1.3.0) | Markdown |
|---|---|---|---|---|---|---|---|
| acts | dz-u-2020-287-prawa-konsumenta | 2 | 0 | 2 | 0 | `1F22973B4749` | = |
| acts | dz-u-2024-1497-kredyt-konsumencki | 5 | 0 | 5 | 0 | `0522FB01D012` | = |
| acts | dz-u-2024-1646-prawo-bankowe | 24 | 0 | 24 | 0 | `1EEFDCB0280D` | = |
| acts | dz-u-2024-30-uslugi-platnicze | 10 | 0 | 10 | 0 | `F0DA9E882747` | = |
| acts | dz-u-2026-1298-obwieszczenie-msz | 6 | 0 | 6 | 0 | `3C33C2F66E6A` | = |
| acts | ustawa-o-sluzbie-cywilnej | 5 | 6 | 11 | 0 | `3623BFA03F09` | = |
| private | mbank-corp-1 | 0 | 0 | 0 | 0 | `2C0044A5891E` | = |
| private | mbank-corp-2 | 0 | 0 | 0 | 0 | `3E654D70B2EB` | = |
| private | mbank-corp-3 | 0 | 0 | 0 | 0 | `5A7AA1E17B99` | = |
| private | mbank-corp-4 | 0 | 0 | 0 | 0 | `2F6B754AC37F` | = |
| private | mbank-corp-5 | 0 | 0 | 0 | 0 | `D42198051077` | = |
| private | mbank-corp-6 | 0 | 0 | 0 | 0 | `675B6A57798F` | = |
| private | mbank-firm-1 | 2 | 0 | 2 | 0 | `A2894A6B320B` | = |
| private | mbank-firm-2 | 0 | 0 | 0 | 0 | `9B7756B53041` | = |
| private | mbank-firm-3 | 1 | 0 | 1 | 0 | `AB033EBC6CE7` | = |
| private | mbank-firm-4 | 0 | 0 | 0 | 0 | `C3F64EC2FE12` | = |
| private | mbank-firm-5 | 0 | 0 | 0 | 0 | `440EE454E131` | = |
| private | mbank-firm-6 | 0 | 0 | 0 | 0 | `BF25CCAFA5CA` | = |
| private | mbank-ind-1 | 0 | 0 | 0 | 0 | `ABB42BD8B4FF` | = |
| private | mbank-ind-2 | 1 | 0 | 1 | 0 | `7ECC3E2273C5` | = |
| private | mbank-ind-3 | 0 | 0 | 0 | 0 | `67CFEC65C578` | = |
| private | mbank-ind-4 | 7 | 0 | 7 | 0 | `0100F00109F5` | = |
| private | mbank-ind-5 | 0 | 0 | 0 | 0 | `6BB4CFE56A3B` | = |
| private | mbank-reg1 | 6 | 0 | 6 | 0 | `4992B9FAECF9` | = |
| private | mbank-reg2 | 0 | 0 | 0 | 0 | `64950E02C8F5` | = |
| private | mbank-reg3 | 0 | 0 | 0 | 0 | `6CC614FB0920` | = |
| private | mbank-regulamin-pdp | 6 | 0 | 6 | 0 | `4992B9FAECF9` | = |
| downloads | regulamin-otwierania-prowadzenia-i-zamyk… | 0 | 0 | 0 | 0 | `2C0044A5891E` | = |
| downloads | regulamin-stosowania-polecenia-zaplaty-0… | 0 | 0 | 0 | 0 | `3E654D70B2EB` | = |
| downloads | regulamin-uslug-gotowkowych-pl… | 0 | 0 | 0 | 0 | `5A7AA1E17B99` | = |
| downloads | regulamin_swiadczenie_klientom_przez_mba… | 0 | 0 | 0 | 0 | `675B6A57798F` | = |
| downloads | zasady_wspolpracy_w_zakresie_transakcji_… | 0 | 0 | 0 | 0 | `D42198051077` | = |
| root | Akt prawny - D20081505Lj | 5 | 6 | 11 | 0 | `3623BFA03F09` | = |
| root | Regulamin podstawowego rachunku płatniczego w ramach bankowości detalicznej mBanku S.A. - | 6 | 0 | 6 | 0 | `4992B9FAECF9` | = |
| banking | regulamin-rachunku | 1 | 0 | 1 | 0 | `D44AB9D89821` | = |
| banking | taryfa-z-siatka | 0 | 0 | 0 | 0 | `EF9912A9F165` | = |
| banking | taryfa-bez-siatki | 0 | 0 | 0 | 0 | `25BE70971694` | = |
| banking | regulamin-dwie-kolumny | 0 | 0 | 0 | 0 | `4A18C0B6C26C` | = |
| banking | regulamin-promocji-tabela | 0 | 0 | 0 | 0 | `BC0481E54E6C` | = |
| banking | regulamin-z-tabela-definicji | 0 | 0 | 0 | 0 | `F859F2A03622` | = |

- 2026-10-13, faza 6:
  - **T012** `6aea9f8`: wersja 1.3.1. `refresh` zmienia tylko `parserVersion`, a `verify` wtedy pada
    (`generatorVersion` w manifeście odtwarzanym w pamięci to też 1.3.1, bo wersję ma cały `Directory.Build.props`).
    Dlatego uruchomiony `generate`: 76 dokumentów, 0 usuniętych plików, `verify` zgodny; w `corpus/` zmienia się tylko
    `parserVersion` i `generatorVersion` w `manifest.json` (jak w spec 010 T035).
  - **T013–T015** `6283931`: README parsera (sekcja 1.3.1, FTN001 w „Ostrzeżenia i raport”, punkt „Nierozpoznane
    odnośniki przypisów” i punkt o punktach listy w przypisie w „Znanych ograniczeniach”), CLAUDE.md („Spec 011
    additions”), artefakty spec 011.
  - **T016**: `publish/` przebudowane z `6283931`: `lib/` (5 bibliotek), `nuget/` 1.3.1 (5 pakietów),
    `LegalAgent.PdfParser/lib`, `LegalAgent.Chunking/lib` (wersja DLL 1.3.1), exe generatora FAQ jak dotąd:
    single-file, win-x64, self-contained, skompresowany (`-p:EnableCompressionInSingleFile=true`, 41 MB);
    `appsettings*.json` bez zmian, `--help` działa. Konwersja 20 regulaminów z
    `publish/mBank.FaqGenerator-win-x64/{corp,firm,ind,downloads}` CLI 1.3.1: 0 błędów, `.md` bajt w bajt jak
    poprzednie. FTN001: firm-3 1, ind-2 1, ind-4 7, pozostałe 0, czyli tyle samo co w T011 (firm-1 nie ma w
    `publish/`). Exe nie uruchamiany (FAQ wymaga Azure).
  - **T017**: sonda `Scratch/Ftn001Measure.cs` usunięta z repozytorium (kopia w scratchpadzie sesji), worktree
    diagnostyczne (`wt123`, `wt130`) usunięte.
  - Otwarte: rozpoznawanie odnośników z pełnowymiarową interpunkcją, list odnośników, odnośników w środku słowa i
    złożonych do sąsiedniej linii (klasy K1/K2) jako kandydat na osobną spec. Zmieni goldeny 6 aktów i reg1, pdp,
    ind-4 i obniży liczby FTN001 w bramce `GoldenTests`. Punkty listy w przypisie (K3) to też kandydat.
  - Gałąź `011-ftn001-false-warnings` niewypchnięta: push po potwierdzeniu właściciela; PR tworzy właściciel.

## Complexity Tracking

Brak naruszeń konstytucji.
