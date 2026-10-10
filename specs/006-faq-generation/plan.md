# Implementation Plan: Konwersja i generowanie FAQ — aplikacja mBank.FaqGenerator

**Branch**: `006-faq-generation` | **Date**: 2026-10-09 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/006-faq-generation/spec.md`

## Summary

Aplikacja `mBank.FaqGenerator` dostaje dwa kolejne etapy po pobraniu.

**Konwersja.** `DocumentSetConverter` z biblioteki `LegalAgent.Faq` konwertuje każdy z 5 PDF-ów wstrzykniętym
`IPdfMarkdownConverter` (domyślne opcje parsera), zapisuje Markdown atomowo obok PDF-a, zbiera jednostki redakcyjne i
sprząta nieaktualne `*.md`. Aplikacja tylko tworzy parser i wypisuje postęp.

**FAQ.** Nowa biblioteka `LegalAgent.Faq` (zależna tylko od `Microsoft.SemanticKernel.Abstractions`):
1. sprawdza rozmiar dokumentów;
2. dla każdego dokumentu po kolei pyta model o pytania kandydujące;
3. jednym zapytaniem wybiera 10 najważniejszych;
4. sprawdza każdą odpowiedź JSON (liczba, puste pola, powtórzenia, istniejące dokumenty, kandydaci i jednostki
   redakcyjne);
5. renderuje `FAQ_mBank.md` w OKF (osobny katalog `faq/`).

Aplikacja tworzy klienta Azure OpenAI (konektor SK 1.80.1, bez ponowień, limit czasu na zapytanie) z kluczem
wpisanym w konsoli z gwiazdkami albo przekazanym potokiem. Klucz nie trafia do konfiguracji, środowiska, plików ani
komunikatów.

Skrypt Bash `scripts/azure/create-openai.sh` idempotentnie tworzy grupę, zasób i wdrożenie modelu i wypisuje
konfigurację. Nowe kody wyjścia: 5, 6, 7.

## Technical Context

**Language/Version**: C# 13 / .NET 9 (SDK z `global.json`); Bash dla skryptu Azure

**Primary Dependencies**:
- **nowe** `Microsoft.SemanticKernel.Abstractions` 1.80.1 (biblioteka) i
  `Microsoft.SemanticKernel.Connectors.AzureOpenAI` 1.80.1 (aplikacja); research R2;
- `LegalAgent.Faq` → `LegalAgent.PdfParser` (referencja projektu, bez nowych pakietów);
- istniejące: `LegalAgent.PdfParser`, `LegalAgent.Downloads`,
  `Microsoft.Extensions.DependencyInjection` 9.0.20 (do `AddLegalAgentPdfParser`, już przypięty),
  `Configuration.Json`/`.Binder`.

**Storage**: pliki: `downloads/*.md` (git-ignored), `faq/FAQ_mBank.md` (katalog OKF, nie ignorowany)

**Testing**: xUnit v3 na Microsoft Testing Platform. Nowy projekt `tests/LegalAgent.Faq.Tests` z atrapą
`IChatCompletionService`. Rozszerzony `tests/mBank.FaqGenerator.Tests`:
- `AppHarness` z atrapami klawiatury, modelu i czasu;
- atrapa HTTP dla konektora;
- syntetyczne PDF-y z `SyntheticPdfBuilder` podawane przez `FakeHttpHandler`;
- test skryptu z atrapą `az`.

**Target Platform**: Linux i Windows (CI na Linuksie)

**Project Type**: biblioteka klas + aplikacja konsolowa + skrypt Bash

**Performance Goals**:
- konwersja 5 regulaminów w czasie porównywalnym z `legalagent-pdf convert` (kilka sekund na plik);
- generowanie: 6 zapytań kolejno, łącznie kilka minut przy prawdziwej usłudze, bez celu liczbowego.

**Constraints**:
- limit 100 000 szacowanych tokenów na dokument;
- 300 s na zapytanie;
- brak ponowień;
- zero sieci w testach;
- klucz wyłącznie w pamięci;
- atomowe zapisy.

**Scale/Scope**: 5 dokumentów po ~150 tys. znaków; 6 zapytań do modelu; 10 pozycji FAQ

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Zasada / ograniczenie | Ocena | Uwagi |
|-----------------------|-------|-------|
| I. TDD | ✅ | red/green na każdą zmianę zachowania; atrapy modelu, HTTP, klawiatury, czasu i `az`; testy offline i deterministyczne |
| II. Wierność źródłu | ✅ | polecenia zakazują informacji spoza dokumentu i wymagają „Dokument nie rozstrzyga…”; walidacja odrzuca nieistniejące dokumenty, kandydatów i jednostki; każda odpowiedź ma źródło z adresem; zgodność merytoryczna — ręcznie (SC-074) |
| III. Powtarzalność | ✅ | jedno polecenie (adresy z `--url`/konfiguracji, klucz potokiem); pobranie, konwersja, walidacja i renderowanie deterministyczne; treść FAQ z modelu i `timestamp` objęte wyjątkiem z konstytucji 1.4.0 (`Temperature` 0, `Seed`) |
| IV. Błędy zewnętrzne | ✅ | limit czasu na zapytanie, mapowanie każdego błędu usługi, kody 5/6/7, atomowe zapisy, poprzedni FAQ nietknięty, niepełny wynik nigdy nie kończy się kodem 0 |
| V. Bezpieczeństwo i konfiguracja | ✅ | brak sekretów w repo; endpoint i wdrożenie w konfiguracji; klucz z konsoli lub przekierowanego wejścia, tylko w pamięci procesu (konstytucja 1.4.0); `AzureOpenAI:ApiKey` w konfiguracji odrzucany |
| VI. Prostota, zależności | ✅ | 2 nowe pakiety (SK Abstractions w bibliotece, konektor w aplikacji), przypięte centralnie, uzasadnione wymaganiem właściciela (R2); bez tokenizera (R3), bez YamlDotNet w bibliotece (R10), bez DI w aplikacji poza parserem |
| VII. Dokumentacja | ✅ | README: zasób Azure (utworzenie, usunięcie), konfiguracja, klucz (konsola, potok), kody, FAQ; `CLAUDE.md` |
| Biblioteka + aplikacja | ✅ | logika konwersji i FAQ w `LegalAgent.Faq` (R9); aplikacja: konfiguracja, klucz, klient Azure, konsola, kody |
| Biblioteka ogólna | ✅ | brak „mBank”, „Azure” i nazwy `FAQ_mBank.md` w `LegalAgent.Faq`; tytuł i opis FAQ podaje aplikacja |
| Biblioteka bez konsoli i stanu globalnego | ✅ | `IChatCompletionService`, opcje i fabryka ustawień wstrzykiwane; postęp przez `IProgress` |
| Linux, brak API Windows | ✅ | `Console.ReadKey`/`TreatControlCAsInput` działają na Linuksie; skrypt Bash; test skryptu na CI |
| Solucja `.slnx` | ✅ | + `src/LegalAgent.Faq`, `tests/LegalAgent.Faq.Tests` |
| Publiczne API udokumentowane | ✅ | `contracts/library-api.md`, komentarze XML, wersja 1.0.0 |
| Format OKF | ✅ | osobny katalog OKF `faq/` z jednym dokumentem z front matter (`type`, `title`, `description`, `resource`, `timestamp`); nazwa pliku z opisu (R10) |

Ponowna ocena po fazie 1, po /speckit-analyze i po poprawce konstytucji 1.4.0 (2026-10-10): bez odstępstw.

## Project Structure

### Documentation (this feature)

```text
specs/006-faq-generation/
├── spec.md
├── plan.md                    # ten plik
├── research.md                # R1–R13
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── cli.md                 # nowe opcje, konfiguracja, klucz, wyjście, kody 0–7/130
│   ├── faq-file.md            # FAQ_mBank.md (OKF)
│   ├── model-exchange.md      # polecenia i schematy JSON obu kroków
│   ├── library-api.md         # publiczne API LegalAgent.Faq
│   └── azure-script.md        # create-openai.sh
├── checklists/requirements.md
└── tasks.md                   # /speckit-tasks
```

### Source Code (repository root)

```text
src/
├── LegalAgent.Faq/                           # NOWA biblioteka (SK Abstractions 1.80.1)
│   ├── LegalAgent.Faq.csproj                 # + ref LegalAgent.PdfParser
│   ├── FaqGenerator.cs                       # CheckInput, GenerateAsync (kandydaci kolejno → wybór)
│   ├── FaqGeneratorOptions.cs                # + walidacja
│   ├── FaqPrompts.cs                         # polecenia PL (model-exchange.md)
│   ├── FaqSchemas.cs                         # schematy JSON
│   ├── FaqResponseParser.cs                  # JSON → kandydaci / pozycje
│   ├── FaqResponseValidator.cs               # reguły R5
│   ├── UnitMatcher.cs                        # dopasowanie jednostek
│   ├── TokenEstimator.cs                     # znaki / CharactersPerToken
│   ├── ServiceErrorMapper.cs                 # wyjątki SK → FaqServiceErrorKind (R7)
│   ├── UsageReader.cs                        # metadane zużycia
│   ├── FaqMarkdownRenderer.cs                # faq-file.md (ręczny emiter YAML)
│   ├── Conversion/DocumentSetConverter.cs    # R9: konwersja kolejno, zapis atomowy, pusty tekst, sprzątanie *.md
│   ├── Conversion/UnitExtractor.cs           # Designation + HeadingText z LegalDocument (rekurencyjnie)
│   ├── Conversion/Model/…                    # PdfSource, ConvertedDocument, ConversionFailure, ConversionRun, ConversionEvent
│   ├── Model/…                               # FaqDocumentInput, FaqSourceDocument, FaqCandidate, FaqItem, FaqSource, FaqResult, FaqUsage, FaqEvent, FaqFileHeader, enums
│   └── Exceptions/…                          # FaqInputTooLongException, FaqServiceException, FaqResponseException
└── mBank.FaqGenerator/
    ├── mBank.FaqGenerator.csproj             # + ref LegalAgent.PdfParser, LegalAgent.Faq; + Connectors.AzureOpenAI, DependencyInjection; appsettings.Local.json kopiowany, jeśli istnieje
    ├── appsettings.json                      # + sekcje AzureOpenAI (Endpoint ""), Faq (OutputDirectory "faq")
    ├── Program.cs                            # RunAsync(args, stdin, stdout, stderr, env, configDirectory, AppHost?, ct); etapy; kody 5/6/7
    ├── AppHost.cs                            # DownloadHandler, ModelHandler, ChatFactory, KeyInput, TimeProvider (R13)
    ├── AppArguments.cs                       # + --faq-output; pomoc
    ├── AppSettings.cs                        # + AzureOpenAiSettings, FaqSettings, walidacja, zakaz ApiKey
    ├── KeyPrompt.cs, IKeyInput.cs, ConsoleKeyInput.cs   # R8
    ├── SecretRedactor.cs                     # usuwanie klucza z komunikatów
    ├── ChatServiceFactory.cs                 # AzureOpenAIClient (bez ponowień, NetworkTimeout, transport) → AzureOpenAIChatCompletionService; ustawienia z schematem
    ├── FaqStage.cs                           # CheckInput → klucz → GenerateAsync → render → zapis atomowy
    └── ConsoleReport.cs                      # + postęp konwersji i FAQ, podsumowanie

scripts/azure/create-openai.sh               # NOWY (contracts/azure-script.md)

tests/
├── LegalAgent.Faq.Tests/                    # NOWY
│   ├── Fakes/FakeChatCompletionService.cs   # kolejka odpowiedzi/wyjątków, zapis otrzymanych ChatHistory i ustawień
│   ├── FaqGeneratorTests.cs                 # kolejność kroków, treść zapytań, wynik, postęp, anulowanie
│   ├── CheckInputTests.cs                   # limit tokenów
│   ├── ValidatorTests.cs                    # wszystkie reguły R5
│   ├── UnitMatcherTests.cs
│   ├── ServiceErrorMapperTests.cs           # 401/404/429/400 filtr/400 kontekst/5xx/czas/sieć
│   ├── PromptTests.cs                       # wymagane elementy poleceń
│   ├── RendererTests.cs + Golden/faq.expected.md
│   ├── Conversion/DocumentSetConverterTests.cs  # syntetyczne PDF-y, .md obok PDF, jednostki, błąd jednego pliku, pusty tekst, sprzątanie
│   └── Fakes/TestPdfs.cs                    # syntetyczne PDF-y (SyntheticPdfBuilder); linkowane do apptests
└── mBank.FaqGenerator.Tests/
    ├── Fakes/AppHarness.cs                  # + FakeKeyInput, atrapa modelu, FakeTimeProvider, AzureOpenAI w appsettings
    ├── KeyPromptTests.cs                    # gwiazdki, Backspace, pusty, Ctrl+C, wejście przekierowane, brak wiersza
    ├── SecretSafetyTests.cs                 # klucz nie w stdout/stderr/plikach/środowisku (sukces, 401, odrzucenie, przerwanie)
    ├── FaqFlowTests.cs                      # pełny przebieg → 0; kody 5/6/7/4; poprzedni FAQ nietknięty; kolejność (klucz po konwersji)
    ├── ModelTransportTests.cs               # prawdziwy konektor + atrapa HTTP: 401 → 6 z komunikatem, brak ponowień, schemat w żądaniu
    ├── ConfigurationTests.cs                # brak Endpoint/Deployment → 2 przed HTTP; ApiKey w konfiguracji → 2
    └── AzureScriptTests.cs                  # atrapa az: tworzenie, idempotencja, brak keys list, kody 3/4

LegalAgent.slnx                              # + 2 projekty
Directory.Packages.props                     # + SK Abstractions i Connectors.AzureOpenAI 1.80.1
README.md, CLAUDE.md                         # sekcje FAQ i Azure
```

**Structure Decision**:
- **Nowa biblioteka `LegalAgent.Faq`.** Wymaga tego konstytucja (logika w bibliotece). Biblioteka jest ogólna: dowolne
  dokumenty Markdown i dowolny `IChatCompletionService`.
- **Konwersja w bibliotece (R9).** `LegalAgent.Faq.Conversion` przyjmuje wstrzyknięty parser i listę `PdfSource`;
  nie zna `LegalAgent.Downloads` — aplikacja mapuje `DownloadResult` na `PdfSource`.
- **Azure tylko w aplikacji.** Klient Azure i klucz należą wyłącznie do aplikacji.

## Kolejność prac (dla /speckit-tasks)

1. **Szkielet:**
   - projekty `LegalAgent.Faq` i `LegalAgent.Faq.Tests`, wpisy w `.slnx` i `Directory.Packages.props`;
   - referencje aplikacji, `appsettings.json` z nowymi sekcjami;
   - `AppHost` i `AppHarness`: istniejące testy spec 005 przechodzą z atrapą modelu.
2. **Biblioteka (test-first):** `UnitMatcher` → `TokenEstimator` i `CheckInput` → parser odpowiedzi → walidator
   (kandydaci, wybór) → `FaqGenerator` (kolejność, polecenia, ustawienia, postęp, anulowanie) → mapowanie błędów →
   zużycie tokenów → renderer i golden.
3. **US1 (konwersja i przebieg):** `DocumentSetConverter` → konfiguracja AzureOpenAI/Faq z walidacją przed pobraniem →
   `FaqStage` z atrapą modelu → zapis atomowy FAQ → postęp i podsumowanie → kod 0.
4. **US2 (klucz):** `KeyPrompt` (gwiazdki, Backspace, pusty, Ctrl+C, wejście przekierowane, brak wiersza) →
   kolejność (klucz po konwersji i `CheckInput`) → `SecretRedactor` → testy bezpieczeństwa klucza → odrzucenie
   `ApiKey` w konfiguracji.
5. **US3 (błędy):** błąd konwersji → 5; dokument za długi → 6 przed kluczem; błędy usługi → 6; odrzucona odpowiedź
   → 7; błąd zapisu → 4; Ctrl+C → 130; poprzedni FAQ nietknięty. `ChatServiceFactory` z atrapą HTTP: brak ponowień,
   limit czasu, schemat w żądaniu.
6. **US4 (skrypt):** test z atrapą `az` (red) → skrypt (green) → idempotencja → brak klucza w wyjściu.
7. **Dokumentacja i weryfikacja:**
   - README i `CLAUDE.md`;
   - ręczne scenariusze z quickstartu 5–9 na subskrypcji właściciela (SC-074);
   - handoff „Stan prac i przekazanie” na końcu tego pliku.

## Ryzyka

- **GPT-4o-mini jest „Deprecated” (R1).** Subskrypcja, w której nigdy go nie wdrożono, nie utworzy wdrożenia.
  Skrypt wykrywa to przed tworzeniem zasobów i podpowiada `gpt-5.4-mini` (`2026-03-17`) z `Temperature: null`.
  Zmiana domyślnego modelu wymaga decyzji właściciela, nie zmiany kodu. Wycofanie: 2027-04-14.
- **Limit TPM i 429.** 5 zapytań po ~50 tys. tokenów kolejno. Skrypt domyślnie ustawia przepustowość 200 tys.
  TPM. Przy 429 kod 6 z podpowiedzią, bez ponowień (FR-424). Jeśli to okaże się uciążliwe, właściciel może
  zdecydować o ponawianiu po `Retry-After` (zmiana spec).
- **Odrzucanie odpowiedzi przez walidację jednostek.** Model może podać jednostkę w innej postaci niż nagłówek
  (np. „ust. 3 § 12”). Ograniczają to polecenie („dokładnie tak, jak w nagłówku”) i luźne dopasowanie prefiksu.
  Jeśli na prawdziwych regulaminach odsetek odrzuceń będzie wysoki, rozważymy pominięcie jednostki zamiast
  odrzucenia — to zmiana spec (FR-430), decyzja właściciela.
- **Wklejanie w konsoli Windows.** Klasyczne `conhost` przy wklejaniu dostarcza zdarzenia klawiszy, podobnie
  Windows Terminal. Weryfikacja ręczna (quickstart 4).
- **`Console.TreatControlCAsInput` przy przekierowanym wyjściu.** Ustawiane tylko w trybie konsoli.
- **FAQ z treścią prawdziwego banku.** Katalog `faq/` nie jest ignorowany przez git (rezultat do oddania); commit
  wyłącznie decyzją właściciela, jawną ścieżką.
- **Rozmiar prawdziwych dokumentów.** 140–155 tys. znaków daje ~47–52 tys. szacowanych tokenów, czyli dużo
  poniżej limitu. Dokument powyżej ~300 tys. znaków zakończy się kodem 6 (bez przycinania, zgodnie ze spec).

## Complexity Tracking

Brak odstępstw (zasady III i V objęte konstytucją 1.4.0).
