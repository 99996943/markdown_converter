---

description: "Task list for spec 006 — konwersja i generowanie FAQ (mBank.FaqGenerator)"
---

# Tasks: Konwersja i generowanie FAQ — aplikacja mBank.FaqGenerator

**Input**: Design documents from `specs/006-faq-generation/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: OBOWIĄZKOWE (konstytucja, zasada I: TDD, NON-NEGOTIABLE).
- Zadanie testowe poprzedza implementację i MUSI najpierw padać **na asercji**, nie na kompilacji. W razie
  potrzeby szkielet typów rzuca `NotImplementedException`.
- Osobne commity: najpierw `test: … (red)`, potem `feat:`/`fix: …` (green).
- Testy działają offline i deterministycznie:
  - atrapa `IChatCompletionService` (`FakeChatCompletionService`);
  - atrapa HTTP (`FakeHttpHandler`) dla pobierania i konektora Azure;
  - atrapa klawiatury (`FakeKeyInput`) i czasu (`FakeTimeProvider`);
  - atrapa `az` dla skryptu;
  - żadnych połączeń z Azure ani z `mbank.pl`.
- Test, który przechodzi od razu (charakteryzacja), jest commitowany jako `test:` z adnotacją w opisie commita.

**Organization**: zadania pogrupowane według historyjek ze spec.md:

| Historyjka | Priorytet | Temat |
|------------|-----------|-------|
| US1 | P1 | pełny przebieg: pobranie, konwersja, FAQ |
| US2 | P1 | klucz API tylko w pamięci |
| US3 | P2 | błędy konwersji i usługi modelu |
| US4 | P2 | skrypt zasobu Azure |

Kolejność faz: US1 → US2 → US3 → US4.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: można wykonać równolegle (inne pliki, brak zależności od niezakończonych zadań).
- **[Story]**: historyjka (US1…US4).

## Path Conventions

- **Ścieżki:**

  | Skrót | Ścieżka |
  |-------|---------|
  | `faq-lib/` | `src/LegalAgent.Faq/` (biblioteka) |
  | `faqtests/` | `tests/LegalAgent.Faq.Tests/` |
  | `app/` | `src/mBank.FaqGenerator/` (aplikacja) |
  | `apptests/` | `tests/mBank.FaqGenerator.Tests/` |
  | `script` | `scripts/azure/create-openai.sh` |

- **Konwencje jak w 001–005:**
  - `CultureInfo.InvariantCulture`; komunikaty i liczby dla użytkownika jawnie w `pl-PL`;
  - `StringComparison.Ordinal*`, LF, UTF-8 bez BOM;
  - XML-doc typów publicznych, `TreatWarningsAsErrors`;
  - analizatory CA1304/CA1305/CA1307/CA1309/CA1310 jako błędy;
  - kod i komentarze po angielsku, komunikaty dla użytkownika po polsku.
- **Biblioteka `faq-lib/`** nie zawiera:
  - konsoli, plików, zmiennych środowiskowych, konfiguracji ani mutowalnego stanu statycznego;
  - słów „mBank” i „Azure” ani nazwy `FAQ_mBank.md` (plan, Constitution Check);
  - typów konektora Azure; tylko `Microsoft.SemanticKernel.Abstractions`.
- **Odniesienia:** „R*n*” to decyzje w research.md; formaty są w contracts/*.md; pola i reguły w data-model.md.
- **Testy aplikacji** wołają `Program.RunAsync(args, stdin, stdout, stderr, environment, configDirectory, host, ct)`
  przez `AppHarness`. Każdy test zapisuje własny `appsettings.json` w katalogu tymczasowym.
- **Przed commitem:**
  - `git branch --show-current` = `006-faq-generation`; jeśli bieżąca gałąź to `005-regulation-download`,
    utwórz `006-faq-generation` od niej;
  - nigdy nie commituj na `main`;
  - w `git add` tylko jawne ścieżki, bo w katalogu głównym leżą prywatne pliki właściciela.

---

## Phase 1: Setup

- [X] T001 Projekty biblioteki i jej testów:
  - utwórz `faq-lib/LegalAgent.Faq.csproj`:
    - class library, `GenerateDocumentationFile`, `RootNamespace` `LegalAgent.Faq`, `Version` 1.0.0;
    - `InternalsVisibleTo` `LegalAgent.Faq.Tests`;
    - pakiet `Microsoft.SemanticKernel.Abstractions`; referencja projektu do `src/LegalAgent.PdfParser`;
  - utwórz `faqtests/LegalAgent.Faq.Tests.csproj` na wzór `tests/LegalAgent.Downloads.Tests/LegalAgent.Downloads.Tests.csproj`:
    - Exe, `IsPackable` false, xunit.v3, xunit.runner.visualstudio, Microsoft.NET.Test.Sdk;
    - `NoWarn CA1707`, `Using Xunit`, referencje do `LegalAgent.Faq`, `src/LegalAgent.Corpus` (dla
      `LegalAgent.Corpus.Pdf.SyntheticPdfBuilder`) i pakiet `Microsoft.Extensions.DependencyInjection` (parser w testach);
  - dodaj `<PackageVersion Include="Microsoft.SemanticKernel.Abstractions" Version="1.80.1" />` i
    `<PackageVersion Include="Microsoft.SemanticKernel.Connectors.AzureOpenAI" Version="1.80.1" />` do
    `Directory.Packages.props`;
  - dodaj oba projekty do `LegalAgent.slnx` (foldery `/src/`, `/tests/`);
  - `dotnet build LegalAgent.slnx -c Release` zielony (SK może wymagać `NoWarn` dla ostrzeżeń
    eksperymentalnych `SKEXP*`; dodaj tylko konkretne kody, z komentarzem);
  - treść commita zawiera uzasadnienie nowych zależności (research R2, zasada VI).
- [X] T002 Rozszerz `app/mBank.FaqGenerator.csproj`:
  - referencje do `src/LegalAgent.PdfParser` i `src/LegalAgent.Faq`;
  - pakiety `Microsoft.SemanticKernel.Connectors.AzureOpenAI` i `Microsoft.Extensions.DependencyInjection`;
  - `<None Update="appsettings.Local.json" CopyToOutputDirectory="PreserveNewest" Condition="Exists('appsettings.Local.json')" />`;
  - w `apptests/mBank.FaqGenerator.Tests.csproj` referencje do `src/LegalAgent.Faq` i `src/LegalAgent.Corpus`
    (dla `TestPdfs`) oraz
    `<Compile Include="..\LegalAgent.Faq.Tests\Fakes\*.cs" LinkBase="Fakes" />`;
  - build zielony.
- [X] T003 [P] W `app/appsettings.json` dodaj sekcje dokładnie jak w contracts/cli.md:
  - `AzureOpenAI`: `Endpoint: ""`, `Deployment: "gpt-4o-mini"`, `Model: "gpt-4o-mini"`, `TimeoutSeconds: 300`,
    `Temperature: 0`, `Seed: 42`, `MaxOutputTokens: 4096`;
  - `Faq`: `OutputDirectory: "faq"`, `CandidatesPerDocument: 10`, `MaxDocumentTokens: 100000`;
  - katalogu `faq/` NIE dodawaj do `.gitignore` (rezultat do oddania; spec, „Prywatność”);
  - w `.gitattributes` dodaj `*.sh text eol=lf`.
- [X] T004 [P] Szkielet publicznych typów `faq-lib/` z contracts/library-api.md:
  - `FaqGenerator.cs` (konstruktor, `CheckInput`, `GenerateAsync`), `FaqGeneratorOptions.cs`, `FaqSchemas.cs`,
    `FaqMarkdownRenderer.cs`, `UnitMatcher.cs`;
  - modele w `Model/`: `FaqDocumentInput`, `FaqSourceDocument`, `FaqSource`, `FaqCandidate`, `FaqItem`,
    `FaqResult`, `FaqUsage`, `FaqEvent`, `FaqEventKind`, `FaqStep`, `FaqFileHeader`, `FaqInputEstimate`;
  - wyjątki w `Exceptions/`: `FaqInputTooLongException`, `FaqServiceException` + `FaqServiceErrorKind`,
    `FaqResponseException`;
  - konwersja w `Conversion/`: `DocumentSetConverter.cs`, `UnitExtractor.cs` i modele `Conversion/Model/`
    (`PdfSource`, `ConvertedDocument`, `ConversionFailure`, `ConversionRun`, `ConversionEvent`,
    `ConversionEventKind`) według contracts/library-api.md, „Konwersja”;
  - metody rzucają `NotImplementedException`; XML-doc.
- [X] T005 [P] Atrapa modelu w `faqtests/Fakes/FakeChatCompletionService.cs`, implementująca `IChatCompletionService`:
  - kolejka skryptowanych kroków: odpowiedź tekstowa z opcjonalnymi metadanymi `Usage` albo wyjątek do rzucenia;
  - rejestr wywołań (kopie `ChatHistory`, `PromptExecutionSettings`);
  - opcjonalne oczekiwanie na token (do testów anulowania);
  - metoda strumieniowa rzuca `NotSupportedException`;
  - plus `faqtests/Fakes/FaqJson.cs`: pomocnicy budujący poprawne JSON-y kandydatów i wyboru (data-model.md,
    „Reguły sprawdzania”).
- [X] T006 Infrastruktura testów aplikacji (refaktor bez zmiany zachowania; wszystkie istniejące testy
  `apptests/` zielone):
  - wprowadź `app/AppHost.cs`, rekord z polami `DownloadHandler`, `ModelHandler`, `ChatFactory`
    (`Func<string, IChatCompletionService>?`), `KeyInput` (`IKeyInput`), `TimeProvider`;
  - zmień `Program.RunAsync`: parametr `HttpMessageHandler? handler` zastąp przez `AppHost? host`;
  - utwórz `app/IKeyInput.cs`: `bool IsInputRedirected`, `ConsoleKeyInfo ReadKey()`, `string? ReadLine()`,
    `IDisposable CaptureControlC()`;
  - rozszerz `apptests/Fakes/AppHarness.cs`:
    - `FakeKeyInput` (skryptowane klawisze lub wiersze, rejestr odczytów, flaga przekierowania);
    - `FakeTimeProvider` ze stałą chwilą `2026-10-09T12:00:00Z`;
    - `FakeChatCompletionService` jako `ChatFactory` (rejestr przekazanego klucza);
    - `WriteSettings` zapisuje też sekcje `AzureOpenAI` (`Endpoint: "https://faq.example.test/"`) i `Faq`
      (`OutputDirectory` w katalogu tymczasowym);
  - dodaj `faqtests/Fakes/TestPdfs.cs` (linkowany do `apptests` przez wzorzec `Fakes\*.cs` z T002): syntetyczne
    PDF-y z `SyntheticPdfBuilder`:
    - regulamin z nagłówkami „§ 1.”, „§ 2.”, „Rozdział 1. …” i treścią;
    - regulamin, z którego parser zgłasza ostrzeżenie `TBL001` (tabela w formacie zastępczym); jeśli nie da się go
      wywołać syntetycznie, testy używają atrapy `IPdfMarkdownConverter` z ostrzeżeniem w raporcie;
    - PDF bez tekstu;
    - uszkodzony PDF zaczynający się od `%PDF-`.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: reguły biblioteki (jednostki, rozmiar, parsowanie, walidacja, renderowanie) i konfiguracja aplikacji,
wspólne dla wszystkich historyjek.

- [X] T007 [P] Test (red) w `faqtests/UnitMatcherTests.cs`. Przypadki z data-model.md („Dopasowanie jednostki”):
  - „§ 12 ust. 3” pasuje do „§ 12.”;
  - „Art. 5a” NIE pasuje do „Art. 5”;
  - „Rozdział 2” pasuje do zbioru z „Rozdział 2” i „Rozdział 2. Otwarcie rachunku”;
  - NBSP i wielokrotne spacje są normalizowane;
  - wielkość liter bez znaczenia;
  - „§ 1,” pasuje do „§ 1”;
  - pusta lista jednostek nie pasuje do niczego;
  - „§ 120” NIE pasuje do „§ 12”.
- [X] T008 Implementacja `faq-lib/UnitMatcher.cs`:
  - normalizacja: NBSP → spacja, zwinięcie białych znaków, przycięcie, bez końcowej kropki, `ToLowerInvariant`;
  - dopasowanie: równość albo prefiks, po którym jest koniec, spacja lub `,`.

  T007 zielony.
- [X] T009 [P] Test (red) w `faqtests/CheckInputTests.cs`:
  - walidacja `FaqGeneratorOptions`: `CandidatesPerDocument` „1–30”, `ItemCount` „≥ 1”, `MaxDocumentTokens`
    „> 0”, `CharactersPerToken` „> 0” → `ArgumentException` w konstruktorze `FaqGenerator`;
  - `CheckInput` zwraca `FaqInputEstimate` z `EstimatedTokens = ceil(znaki / CharactersPerToken)`;
  - dokument powyżej `MaxDocumentTokens` → `FaqInputTooLongException` z `DocumentName`, `Characters`,
    `EstimatedTokens`, `Limit` (pierwszy za długi w kolejności listy);
  - pusta lista dokumentów, pusty `Markdown`, pusta `Name` lub względny `Resource` → `ArgumentException`.
- [X] T010 Implementacja `faq-lib/TokenEstimator.cs`, walidacji w `faq-lib/FaqGeneratorOptions.cs` i
  `FaqGenerator.CheckInput`. T009 zielony.
- [X] T011 [P] Test (red) w `faqtests/ResponseParserTests.cs`:
  - poprawne JSON-y obu kroków (contracts/model-exchange.md) dają kandydatów z identyfikatorami `D2-K1`…
    w kolejności odpowiedzi oraz pozycje wyboru;
  - JSON niepoprawny, brak wymaganego pola, zły typ albo tekst przed lub po JSON-ie → problem
    „odpowiedź nie jest poprawnym JSON-em zgodnym ze schematem” (bez wyjątku nieobsłużonego);
  - `unit` pusty lub z samych spacji → `null`.
- [X] T012 Implementacja `faq-lib/FaqResponseParser.cs` (`System.Text.Json`, bez refleksji dynamicznej; typy DTO
  wewnętrzne). T011 zielony.
- [X] T013 [P] Test (red) w `faqtests/ValidatorCandidatesTests.cs`. Reguły kandydatów z data-model.md:
  - „1 ≤ liczba ≤ `CandidatesPerDocument`” (0 i N+1 odrzucone, bez przycinania);
  - pytanie lub odpowiedź pusta po przycięciu;
  - powtórzone pytanie (różnice wielkości liter, spacji, końcowego `?`);
  - `unit` niepasujący do `Units` dokumentu;
  - wszystkie problemy zebrane w `FaqResponseException.Problems` z numerem pozycji, `Step = Candidates`,
    `DocumentId`.
- [X] T014 Implementacja `faq-lib/FaqResponseValidator.cs` (część kandydatów). T013 zielony.
- [X] T015 [P] Test (red) w `faqtests/ValidatorSelectionTests.cs`. Reguły wyboru z data-model.md:
  - „dokładnie `ItemCount` pozycji” (9 i 11 odrzucone);
  - puste pola, puste `basedOn` lub `sources`;
  - powtórzone pytania;
  - `basedOn` z nieistniejącym kandydatem;
  - `documentId` spoza `D1…Dn`;
  - dokument źródła, który nie jest dokumentem żadnego z kandydatów `basedOn`;
  - `unit` niepasujący do `Units` dokumentu;
  - poprawna odpowiedź daje `FaqItem` z `Number` 1…10 w kolejności modelu.
- [X] T016 Implementacja części wyboru w `faq-lib/FaqResponseValidator.cs`. T015 zielony.
- [X] T017 [P] Test (red) w `faqtests/RendererTests.cs`, porównanie z golden
  `faqtests/Golden/faq.expected.md` (dodaj `tests/**/Golden/*.md text eol=lf` do `.gitattributes`, jeśli
  wzorzec `*.expected.md` go nie obejmuje). Sprawdza:
  - front matter w kolejności `type`, `title`, `description`, `resource` (lista), `timestamp`
    (`yyyy-MM-ddTHH:mm:ssZ`), `model`, `deployment`;
  - ucieczkę `\"`/`\\` i zamianę nowych wierszy na spację w wartościach;
  - 10 sekcji `## <pytanie>`, bez nagłówka `#` i bez numeracji;
  - wiodące `#` w pytaniu poprzedzone `\`;
  - wiersz `Źródło:` / `Źródła:` z linkami `[Name](Resource)` i `, <jednostka>`, oddzielone `; `;
  - jeden pusty wiersz między blokami, brak spacji na końcach, jedno końcowe `\n`;
  - przy `UPDATE_GOLDEN=1` test przepisuje golden, przy niezgodności zapisuje `*.actual.md`
    (wzorzec jak `tests/LegalAgent.PdfParser.Tests/Fixtures/GoldenFile.cs`).
- [X] T018 Implementacja `faq-lib/FaqMarkdownRenderer.cs` (ręczny emiter YAML, research R10). T017 zielony.
- [X] T019 [P] Test (red) w `apptests/ConfigurationTests.cs`:
  - brak `AzureOpenAI:Endpoint`, `Endpoint` względny lub `http`, pusty `Deployment` → kod 2 z nazwą pola
    (contracts/cli.md: „Błąd konfiguracji: brak AzureOpenAI:Endpoint …”);
  - `FakeHttpHandler` nie dostał żadnego żądania, a pytania o adresy się nie pojawiły;
  - `TimeoutSeconds` ≤ 0, `Temperature` poza 0–2, `MaxOutputTokens` ≤ 0, `CandidatesPerDocument` poza 1–30,
    `MaxDocumentTokens` ≤ 0 → kod 2;
  - `FAQGEN__AzureOpenAI__ApiKey=sekret-123` → kod 2, komunikat o podawaniu klucza w konsoli lub potokiem, bez
    „sekret-123” w stdout/stderr;
  - pusta wartość `FAQGEN__AzureOpenAI__Temperature=` → `null`;
  - `--help` działa bez konfiguracji AzureOpenAI (kod 0).
- [X] T020 Implementacja `AzureOpenAiSettings`, `FaqSettings`, wiązania i walidacji w `app/AppSettings.cs` oraz
  sprawdzenia w `app/Program.cs` przed zbieraniem adresów. T019 zielony.

**Checkpoint**: reguły biblioteki gotowe, konfiguracja sprawdzana. Historyjki mogą się zaczynać.

---

## Phase 3: User Story 1 — Pełny przebieg: pobranie, konwersja, FAQ (Priority: P1) 🎯 MVP

**Goal**: po pobraniu 5 plików aplikacja konwertuje je do Markdown, generuje FAQ w dwóch krokach i zapisuje
`FAQ_mBank.md`. Kod 0.

**Independent Test**: `RunAsync` z 5 adresami (`FakeHttpHandler` serwuje syntetyczne PDF-y), `FakeKeyInput` z
kluczem i atrapą modelu z poprawnymi odpowiedziami daje 5 plików `.md`, `FAQ_mBank.md` zgodny z
contracts/faq-file.md i kod 0.

- [X] T021 [P] [US1] Test (red) w `faqtests/FaqGeneratorTests.cs`:
  - kolejno N zapytań kandydatów (D1…Dn), potem jedno zapytanie wyboru, bez równoległości (atrapa rejestruje,
    że drugie wywołanie zaczęło się po zakończeniu pierwszego);
  - komunikat użytkownika kroku kandydatów zawiera `Dokument D2: <Name>`, `Źródło: <Resource>` i pełny Markdown;
  - komunikat kroku wyboru zawiera listę dokumentów i kandydatów `[D1-K1] (D1, § 3) Pytanie: … | Odpowiedź: …`
    bez pełnej treści dokumentów;
  - fabryka ustawień wywołana z `(Candidates, FaqSchemas.Candidates)` i `(Selection, FaqSchemas.Selection)`, a
    zwrócone ustawienia przekazane do usługi;
  - `FaqResult`: 10 pozycji, `Documents` z `D1…Dn` w kolejności wejścia, `Candidates` ze wszystkich dokumentów;
  - zdarzenia `IProgress<FaqEvent>` w kolejności `CandidatesStarted`/`CandidatesFinished` ×N, `SelectionStarted`,
    `SelectionFinished`, ze znakami i szacunkiem tokenów;
  - `GenerateAsync` wywołuje `CheckInput` przed pierwszym zapytaniem;
  - anulowanie tokenem w trakcie zapytania → `OperationCanceledException`.
- [X] T022 [US1] Implementacja `faq-lib/FaqGenerator.cs` (`GenerateAsync`: budowa `ChatHistory`, wywołania
  `GetChatMessageContentsAsync`, parser, walidator, wynik, postęp). T021 zielony.
- [X] T023 [P] [US1] Test (red) w `faqtests/PromptTests.cs`. Wymagane elementy poleceń z contracts/model-exchange.md:
  - kandydaci:
    - zakaz informacji spoza dokumentu;
    - fraza „Dokument nie rozstrzyga”;
    - „po polsku”;
    - „co najwyżej {N}” z wartością `CandidatesPerDocument`;
    - oznaczenie jednostki „dokładnie tak, jak w nagłówku dokumentu”;
  - wybór:
    - „dokładnie {ItemCount}”;
    - zakaz faktów spoza kandydatów;
    - wymóg `basedOn` i `sources`;
    - „po polsku”.
- [X] T024 [US1] Implementacja `faq-lib/FaqPrompts.cs`. T023 zielony.
- [X] T025 [P] [US1] Test (red) w `faqtests/UsageTests.cs`:
  - metadane `Usage` z `InputTokenCount`/`OutputTokenCount` albo `PromptTokens`/`CompletionTokens` są sumowane w
    `FaqResult.Usage` i przekazywane w zdarzeniach `*Finished`;
  - brak lub nieznany kształt metadanych → `null`, bez błędu.
- [X] T026 [US1] Implementacja `faq-lib/UsageReader.cs` (refleksja po nazwach właściwości obiektu metadanych,
  bez zależności od typów OpenAI) i podłączenie w `FaqGenerator`. T025 zielony.
- [X] T027 [P] [US1] Test (red) w `faqtests/Conversion/DocumentSetConverterTests.cs`, na `DocumentSetConverter` z
  prawdziwym parserem (`AddLegalAgentPdfParser()`):
  - dla 5 `PdfSource` z syntetycznymi PDF-ami w katalogu tymczasowym powstają `<nazwa>.md` obok PDF-ów;
  - treść równa `PdfConversionResult.Markdown` z domyślnymi opcjami parsera (porównanie z bezpośrednim
    wywołaniem `IPdfMarkdownConverter`);
  - `ConvertedDocument` ma `Index`, `Address`, `PdfFileName`, `MarkdownFileName`, `Title`, `PageCount`,
    `Warnings` (dla PDF-u z ostrzeżeniem: `TBL001`) i `Units`;
  - `UnitExtractor.FromDocument` zwraca „§ 1”, „§ 1.” lub `HeadingText` sekcji i „Rozdział 1”, bez duplikatów, w
    kolejności dokumentu (także sekcje zagnieżdżone);
  - zdarzenia `Started`/`Converted` w kolejności indeksów;
  - istniejący `<nazwa>.md` jest nadpisywany, a po zapisie nie ma plików `*.tmp`.
- [X] T028 [US1] Implementacja `faq-lib/Conversion/DocumentSetConverter.cs` i `faq-lib/Conversion/UnitExtractor.cs`
  (research R9: wstrzyknięty `IPdfMarkdownConverter`, konwersja kolejno, zapis atomowy `.tmp` +
  `File.Move(overwrite: true)`, jednostki rekurencyjnie z `LegalDocument.Sections`). T027 zielony.
- [X] T029 [P] [US1] Test (red) w `apptests/ModelTransportTests.cs`. Prawdziwy konektor przez
  `ChatServiceFactory` z `AppHost.ModelHandler` = `FakeHttpHandler` zwracającym odpowiedź chat completions
  (JSON w `choices[0].message.content`, `usage`):
  - żądanie idzie na `{Endpoint}openai/deployments/{Deployment}/chat/completions`;
  - nagłówek `api-key` ma wartość podanego klucza;
  - treść żądania ma `response_format.type = "json_schema"` ze `strict: true` i schematem z `FaqSchemas`,
    `temperature: 0`, `seed: 42` i limit tokenów 4096;
  - przy `Temperature: null` pole `temperature` nie jest wysyłane;
  - odpowiedź trafia do `FaqGenerator` jako tekst, a zużycie jako metadane.
- [X] T030 [US1] Implementacja `app/ChatServiceFactory.cs` (research R7: `AzureOpenAIClient` z `ApiKeyCredential`,
  `AzureOpenAIClientOptions` z `NetworkTimeout`, `RetryPolicy` bez ponowień, `Transport` na `HttpClient` z
  `ModelHandler`; `AzureOpenAIChatCompletionService`; fabryka `PromptExecutionSettings` z
  `ChatResponseFormat.CreateJsonSchemaFormat(..., jsonSchemaIsStrict: true)`). T029 zielony.
- [X] T031 [US1] Test (red) w `apptests/FaqFlowTests.cs`, pełny przebieg `RunAsync` (z dawnym T033):
  - 5 adresów `--url`, PDF-y z `TestPdfs`, `FakeKeyInput` (konsola, klucz „test-key”), atrapa modelu z 5
    odpowiedziami kandydatów i 1 wyboru;
  - wynik: kod 0, 5 plików `.md` w katalogu pobrań, `FAQ_mBank.md` w `Faq:OutputDirectory` równy tekstowi z
    `FaqMarkdownRenderer` dla `FakeTimeProvider`;
  - nagłówek FAQ: tytuł „FAQ — regulaminy mBanku”, opis „10 najważniejszych pytań i odpowiedzi na podstawie 5
    regulaminów mBanku.”, `resource` = 5 adresów, `model`/`deployment` z konfiguracji;
  - stdout zawiera „Konwersja do Markdown:”, wiersze `[1/5] <plik>.pdf → <plik>.md (N stron)`, dla PDF-u z
    ostrzeżeniem `[k/5] … (N stron, 1 ostrzeżenie)` i `      ostrzeżenie TBL001 (strona n): …`, „Przekonwertowano
    5 z 5 plików.”, „Generowanie FAQ (…)”, wiersze `[D1] …`, `[wybór] …` i „Zapisano FAQ: …” z liczbami w `pl-PL`
    (contracts/cli.md); ostrzeżenia parsera nie blokują FAQ (kod 0; FR-402, przypadek brzegowy ze spec);
  - bez `Faq:OutputDirectory` w konfiguracji testu FAQ trafia do `faq/` względem katalogu roboczego (test podaje
    względną ścieżkę `faq` rozwiązywaną od katalogu tymczasowego przez parametr katalogu roboczego w `AppHost`
    albo sprawdza wartość domyślną `FaqSettings`);
  - `--faq-output <katalog>` zmienia katalog FAQ, a brakujący katalog jest tworzony;
  - atrapa modelu dostała klucz „test-key”;
  - dostosuj istniejące testy spec 005 w `apptests/`: scenariusze oczekujące kodu 0 po pobraniu serwują PDF-y z
    `TestPdfs` i mają atrapę modelu z poprawnymi odpowiedziami; scenariusze kodów 2/3/4/130 bez zmian (opisz w
    commicie).
- [X] T032 [US1] Implementacja `app/FaqStage.cs`:
  - `CheckInput` → klucz → `GenerateAsync` → `Render` → zapis;
  - stałe aplikacji: nazwa `FAQ_mBank.md`, tytuł, opis;
  - podłączenie etapów w `app/Program.cs` po udanym pobraniu: mapowanie `DownloadRun.Results` → `PdfSource`,
    `DocumentSetConverter` z parserem z `AddLegalAgentPdfParser()`, `ConvertedDocument` → `FaqDocumentInput`;
  - `--faq-output` w `app/AppArguments.cs`;
  - postęp i podsumowanie w `app/ConsoleReport.cs`.

  Na tym etapie klucz jest czytany najprostszą wersją `KeyPrompt` (pełna obsługa w US2). T031 i cały zestaw
  `apptests/` zielone.

T033 — połączone z T031/T032 (/speckit-analyze I1: zestaw testów nie może być czerwony między commitami).
- [X] T034 [P] [US1] Test (red) w `apptests/FaqWriteTests.cs`:
  - istniejący `FAQ_mBank.md` zostaje zastąpiony w całości;
  - po zapisie nie ma `FAQ_mBank.md.tmp`;
  - nieudane przeniesienie `.tmp` → `FAQ_mBank.md` (Windows: docelowy plik otwarty z `FileShare.None`; Linux: w
    miejscu pliku docelowego leży katalog `FAQ_mBank.md`) → kod 4, stary plik (Windows) ma niezmienioną treść,
    `FAQ_mBank.md.tmp` usunięty (FR-433).
- [X] T035 [US1] Implementacja zapisu atomowego w `app/FaqStage.cs` (usuwanie `.tmp` w `finally`). T034 zielony.
- [X] T036 [P] [US1] Test (red) w `faqtests/Conversion/DocumentSetConverterTests.cs` (FR-403) i asercja podsumowania
  w `apptests/FaqFlowTests.cs`:
  - po udanej konwersji 5/5 z katalogu pobrań znikają `*.md` spoza bieżącego zestawu, a nazwy usuniętych plików są
    w `ConversionRun.RemovedMarkdownFiles` i w podsumowaniu;
  - przy błędzie konwersji żaden `.md` nie jest usuwany;
  - `manifest.json` i podkatalogi nietknięte.
- [X] T037 [US1] Implementacja sprzątania `*.md` w `faq-lib/Conversion/DocumentSetConverter.cs` i wypisania w
  `app/ConsoleReport.cs`.
  T036 zielony.
- [X] T038 [P] [US1] Test (red) w `apptests/HelpTests.cs`: `--help` zawiera `--faq-output`, sekcje `AzureOpenAI`
  i `Faq`, sposób podania klucza (konsola lub potok, nigdy opcja ani zmienna) i kody 0–7/130 z contracts/cli.md.
- [X] T039 [US1] Aktualizacja `AppArguments.Usage` w `app/AppArguments.cs`. T038 zielony.

**Checkpoint**: MVP. Pełny przebieg z atrapą modelu daje `FAQ_mBank.md` i kod 0.

---

## Phase 4: User Story 2 — Klucz API nigdy nie opuszcza pamięci procesu (Priority: P1)

**Goal**: klucz wpisywany z gwiazdkami albo przekazany potokiem. Nie trafia do plików, środowiska ani komunikatów.

**Independent Test**: uruchomienia z charakterystycznym kluczem (sukces, 401, odrzucona odpowiedź, przerwanie):
klucza nie ma w stdout, stderr, plikach pod katalogiem testu ani w środowisku.

- [X] T040 [P] [US2] Test (red) w `apptests/KeyPromptTests.cs`, konsola (`FakeKeyInput.IsInputRedirected = false`):
  - prompt „Klucz API Azure OpenAI: ”;
  - każdy znak (także sekwencja „wklejona” jako ciąg klawiszy) wypisuje dokładnie jedną `*`, a w stdout nie ma
    żadnego znaku klucza;
  - Backspace usuwa ostatni znak i wypisuje `"\b \b"`; Backspace na pustym kluczu nic nie wypisuje;
  - Enter kończy wpisywanie; białe znaki na brzegach są przycinane;
  - pusty klucz → „Klucz nie może być pusty.” i ponowne pytanie;
  - strzałki, Tab i Escape są ignorowane;
  - Ctrl+C (klawisz `C` z `ConsoleModifiers.Control`) → `OperationCanceledException`;
  - `CaptureControlC()` jest wywołane i zwolnione także przy wyjątku.
- [X] T041 [US2] Implementacja `app/KeyPrompt.cs` i `app/ConsoleKeyInput.cs` (research R8: `Console.ReadKey(intercept: true)`,
  `Console.TreatControlCAsInput` ustawiane w `CaptureControlC` i przywracane, `Console.IsInputRedirected`,
  `ReadLine` przez ten sam `TextReader` co adresy). T040 zielony.
- [X] T042 [P] [US2] Test (red) w `apptests/KeyPromptTests.cs`, wejście przekierowane:
  - z `--url` klucz to pierwszy wiersz stdin;
  - przy adresach czytanych ze stdin klucz to szósty wiersz;
  - brak promptu i gwiazdek w stdout;
  - brak wiersza albo pusty wiersz → kod 2 i komunikat z contracts/cli.md („Brak klucza API: wejście jest
    przekierowane…”), bez zapytań do modelu;
  - Ctrl+C podczas pytania w konsoli → kod 130, „Przerwano.” na stderr, brak `FAQ_mBank.md`.
- [X] T043 [US2] Obsługa przekierowanego wejścia i kodów w `app/KeyPrompt.cs` i `app/FaqStage.cs`. T042 zielony.
- [X] T044 [P] [US2] Test (red) w `apptests/KeyOrderTests.cs` (FR-413):
  - `FakeKeyInput` nie jest odczytywany, gdy pobieranie kończy się kodem 3, konwersja kodem 5 albo `CheckInput`
    zgłasza za długi dokument (kod 6, `Faq:MaxDocumentTokens` = 10);
  - przy sukcesie klucz jest czytany dokładnie raz, po komunikacie „Przekonwertowano 5 z 5 plików.”.
- [X] T045 [US2] Kolejność etapów w `app/Program.cs` i `app/FaqStage.cs` (`CheckInput` przed `KeyPrompt`). T044 zielony.
- [X] T046 [P] [US2] Test (red) w `apptests/SecretRedactorTests.cs`:
  - wszystkie wystąpienia klucza w tekście zamieniane na `***`;
  - klucz pusty lub `null` → tekst bez zmian;
  - klucz z wyrażeniem regularnym nie psuje zamiany (zamiana zwykła, `StringComparison.Ordinal`).
- [X] T047 [US2] Implementacja `app/SecretRedactor.cs` i użycie go dla każdego komunikatu z etapu FAQ w
  `app/FaqStage.cs` oraz w ogólnym `catch` w `app/Program.cs`. T046 zielony.
- [X] T048 [US2] Test (red lub charakteryzacja) w `apptests/SecretSafetyTests.cs`. Klucz „KLUCZ-7f3a9c-TEST”,
  scenariusze:
  - sukces;
  - 401 z treścią odpowiedzi zawierającą ten klucz (przez `ModelTransportTests`/`ChatServiceFactory` i
    `FakeHttpHandler`);
  - odpowiedź modelu odrzucona;
  - Ctrl+C w trakcie zapytania.

  Asercje:
  - klucz nie występuje w stdout ani stderr;
  - klucza nie ma w żadnym pliku pod `harness.Root` (rekurencyjnie, bajtowo w UTF-8);
  - klucza nie ma w słowniku `environment` ani w `Environment.GetEnvironmentVariables()` procesu testowego
    (porównanie przed i po);
  - liczba gwiazdek w trybie konsoli jest równa długości klucza (świadomy kompromis z Assumptions).
- [X] T049 [US2] Poprawki ujawnione przez T048, jeśli są. Jeśli nie ma, T048 jest commitem charakteryzacji z
  adnotacją. T048 zielony.

**Checkpoint**: US1 i US2 kompletne. Klucz obsługiwany zgodnie z FR-410–413.

---

## Phase 5: User Story 3 — Błędy konwersji i usługi modelu są obsłużone (Priority: P2)

**Goal**: każdy błąd konwersji, usługi lub odpowiedzi kończy się czytelnym komunikatem, właściwym kodem i bez
uszkodzenia poprzedniego `FAQ_mBank.md`.

**Independent Test**: atrapy zwracające przekroczenie czasu, 429, odpowiedź z 7 pytaniami i powołanie na
nieistniejący dokument. Każde uruchomienie daje komunikat, kod ≠ 0 i nietknięty poprzedni FAQ.

- [X] T050 [P] [US3] Test (red) w `faqtests/Conversion/DocumentSetConverterTests.cs` i `apptests/FaqFlowTests.cs`:
  - uszkodzony PDF (sygnatura `%PDF-`, ale nieczytelna struktura) i PDF bez tekstu wśród 5 → pozostałe pliki
    konwertowane, `ConversionFailure` z nazwą i przyczyną;
  - stderr „Błąd konwersji <plik>: …”, podsumowanie „Przekonwertowano 3 z 5 plików.”, kod 5;
  - brak pytania o klucz i brak zapytań do modelu.
- [X] T051 [US3] Obsługa `PdfParserException`, `IsComplete == false` i Markdown bez tekstu poza
  `<!-- page: N -->` w `faq-lib/Conversion/DocumentSetConverter.cs`; kod 5 w `app/Program.cs`. T050 zielony.
- [X] T052 [P] [US3] Test (red) w `faqtests/ServiceErrorMapperTests.cs`. `HttpOperationException` z kodami:
  - 401/403 → `Authentication`, 404 → `DeploymentNotFound`, 429 → `RateLimited`;
  - 400 z `content_filter` → `ContentFiltered`, 400 z `context_length_exceeded` → `InputTooLong`;
  - 500/503 → `ServiceUnavailable`;
  - `TaskCanceledException`/`TimeoutException` przy nieanulowanym tokenie → `Timeout`;
  - `HttpRequestException` → `Network`, inne → `Other`;
  - anulowany token → `OperationCanceledException` przechodzi dalej;
  - `StatusCode`, `Step`, `DocumentId`, komunikat po polsku ze skrótem odpowiedzi ≤ 300 znaków.
- [X] T053 [US3] Implementacja `faq-lib/ServiceErrorMapper.cs` i użycie w `FaqGenerator`. T052 zielony.
- [X] T054 [P] [US3] Test (red) w `faqtests/FaqGeneratorErrorTests.cs`:
  - błąd usługi przy D3 → `FaqServiceException` z `DocumentId = "D3"`, bez zapytań D4, D5 i wyboru;
  - zła odpowiedź kandydatów lub wyboru → `FaqResponseException` z listą problemów, bez kolejnych zapytań;
  - brak automatycznych ponowień (atrapa dostała dokładnie tyle wywołań, ile kroków do błędu).
- [X] T055 [US3] Poprawki przepływu błędów w `faq-lib/FaqGenerator.cs`. T054 zielony.
- [X] T056 [P] [US3] Test (red) w `apptests/FaqErrorFlowTests.cs`, kody i komunikaty z tabeli contracts/cli.md:
  - kod 6 dla `Authentication` (401), `DeploymentNotFound` (z nazwą wdrożenia i endpointem), `RateLimited`
    (z „D3”), `ContentFiltered`, `Timeout` (z liczbą sekund i krokiem), `Network`;
  - kod 6 dla dokumentu za długiego, z nazwą, znakami, szacunkiem i limitem w `pl-PL`;
  - kod 7 z „Odpowiedź modelu odrzucona (krok wyboru):” i problemami w wierszach `  - …`;
  - kod 4, gdy `Faq:OutputDirectory` wskazuje istniejący plik (komunikat „Nie można zapisać FAQ_mBank.md w …”);
  - w każdym przypadku wcześniej zapisany `FAQ_mBank.md` ma niezmienioną treść, a `FAQ_mBank.md.tmp` nie istnieje.
- [X] T057 [US3] Obsługa wyjątków biblioteki, komunikatów i kodów 4/6/7 w `app/FaqStage.cs`, `app/Program.cs` i
  `app/ConsoleReport.cs`. T056 zielony.
- [X] T058 [P] [US3] Test (red) w `apptests/ModelTransportTests.cs`:
  - 429 i 503 z atrapy HTTP → dokładnie jedno żądanie (brak ponowień SDK), kod 6;
  - `AzureOpenAI:TimeoutSeconds` = 0.5 i atrapa opóźniająca odpowiedź o 5 s → kod 6 z „Brak odpowiedzi usługi
    w ciągu 0,5 s” (format liczby `pl-PL`);
  - błąd DNS (`HttpRequestException`) → kod 6 „Błąd połączenia z usługą Azure OpenAI: …”.
- [X] T059 [US3] Poprawki konfiguracji klienta w `app/ChatServiceFactory.cs` (`ClientRetryPolicy(0)`,
  `NetworkTimeout`). T058 zielony.
- [X] T060 [P] [US3] Test (red) w `apptests/FaqErrorFlowTests.cs`: anulowanie tokenem `RunAsync` w trakcie zapytania
  kandydatów lub wyboru (atrapa czeka na token) → kod 130, „Przerwano.”, brak `FAQ_mBank.md` i `.tmp`, poprzedni
  FAQ nietknięty.
- [X] T061 [US3] Poprawki anulowania w `app/FaqStage.cs`, jeśli potrzebne (inaczej T060 jako charakteryzacja).
  T060 zielony.

**Checkpoint**: wszystkie scenariusze błędów z US3 i SC-073 pokryte.

---

## Phase 6: User Story 4 — Utworzenie zasobu w Azure jednym skryptem (Priority: P2)

**Goal**: `scripts/azure/create-openai.sh` idempotentnie tworzy grupę, zasób i wdrożenie, wypisuje konfigurację,
nie dotyka klucza.

**Independent Test**: test z atrapą `az` na `PATH` sprawdza tworzenie, drugie uruchomienie bez `create`, brak
`keys list` i kody błędów.

- [X] T062 [US4] Test (red) w `apptests/AzureScriptTests.cs`. Uruchamia `bash scripts/azure/create-openai.sh` z
  katalogu repozytorium (ścieżka przez `AppContext.BaseDirectory` w górę do `LegalAgent.slnx`). Atrapa `az` to
  skrypt Bash w katalogu tymczasowym na początku `PATH`: zapisuje argumenty do pliku logu i zwraca odpowiedzi
  sterowane zmiennymi `FAKE_AZ_*`. Przypadki z contracts/azure-script.md:
  - brak logowania → kod 3, „az login”;
  - brak `az` na `PATH` → kod 3;
  - model niedostępny w `model list` → kod 4 z podpowiedzią `--model gpt-5.4-mini --model-version 2026-03-17`,
    bez żadnego `create`;
  - pierwsze uruchomienie → `group create`, `account create --kind OpenAI --sku S0 --custom-domain faqgen-<8 hex>`
    i `deployment create` z `--model-name gpt-4o-mini --model-version 2024-07-18 --sku-name GlobalStandard
    --sku-capacity 200`; kod 0; wyjście z fragmentem `appsettings.Local.json`, zmiennymi `FAQGEN__AzureOpenAI__…`
    i poleceniem `az group delete --name rg-faqgen --yes`;
  - drugie uruchomienie (atrapa: zasób i wdrożenie istnieją) → kod 0, bez `account create` i `deployment create`;
  - log wywołań nigdy nie zawiera `keys`;
  - nieznany parametr → kod 2;
  - błąd `az` w `account create` → kod 5.

  Bez `bash` w `PATH` test jest pomijany: `Assert.SkipUnless(…, "bash not available")`.
- [X] T063 [US4] Implementacja `scripts/azure/create-openai.sh` według contracts/azure-script.md:
  `#!/usr/bin/env bash`, `set -euo pipefail`, `--help`, parametry z wartościami domyślnymi, nazwa domyślna z
  `az account show --query id -o tsv | sha256sum | cut -c1-8`, z możliwością użycia `shasum -a 256` na macOS.
  Plik wykonywalny w git: `git update-index --chmod=+x scripts/azure/create-openai.sh`. T062 zielony.

**Checkpoint**: wszystkie historyjki kompletne.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T064 [P] README: nowa sekcja „Generowanie FAQ” po „Pobieraniu regulaminów” (zasada VII, FR-444):
  - utworzenie zasobu skryptem; ostrzeżenie o statusie Deprecated `gpt-4o-mini` i jak wybrać inny model (R1);
  - konfiguracja `AzureOpenAI`/`Faq` (`appsettings.Local.json`, zmienne);
  - klucz: konsola z gwiazdkami albo potok, nigdy plik ani zmienna;
  - przebieg i pliki wynikowe; kody 0–7/130;
  - usunięcie zasobów (`az group delete`);
  - uruchamianie nowych testów.
- [X] T065 [P] `CLAUDE.md`, sekcja „What this is” i „Commands”:
  - `LegalAgent.Faq` (dwa kroki, walidacja, renderer OKF, zależność tylko od SK Abstractions);
  - etapy aplikacji;
  - skrypt Azure;
  - atrapy w testach (`FakeChatCompletionService`, `FakeKeyInput`, atrapa `az`);
  - klucz nigdy w konfiguracji.
- [X] T066 Walidacja:
  - `dotnet build LegalAgent.slnx -c Release` bez ostrzeżeń;
  - `dotnet test LegalAgent.slnx --filter "Category!=Performance"` zielone;
  - `LEGALAGENT_PRIVATE_CORPUS=tests/LegalAgent.PdfParser.Tests/Corpus/private` — pełny zestaw parsera
    zielony (parser bez zmian, kontrola regresji);
  - quickstart 1–3 na zbudowanej aplikacji;
  - liczby testów zapisz do handoffu.
- [ ] T067 Weryfikacja ręczna z właścicielem (quickstart 4–9, SC-070, SC-074, SC-075):
  - skrypt na subskrypcji właściciela; zanotuj, czy `gpt-4o-mini` dało się wdrożyć;
  - pełny przebieg na 5 prawdziwych regulaminach; gwiazdki przy wklejaniu na Windows;
  - klucz potokiem;
  - ocena 10 odpowiedzi przez właściciela;
  - `az group delete`.

  Wymaga klucza i adresów od właściciela. Jeśli niedostępne, zapisz jako otwarte w handoffie.
- [X] T067a Pierwszy prawdziwy przebieg (gpt-4.1-mini) odrzucił D1: model podał „§ 6”, „§ 15”…, a regulaminy mBanku
  nie mają § ani Art. — nagłówki to „6. Jakie informacje musisz podać…”. Poprawka:
  - komunikat użytkownika kroku kandydatów kończy się listą „Jednostki dokumentu Dn” (pozycje `Units`) albo
    „Jednostki dokumentu Dn: brak” z prośbą o pusty tekst; komunikat systemowy każe przepisać jednostkę z tej listy
    (`tests/LegalAgent.Faq.Tests/PromptTests.cs`);
  - `UnitMatcher`: numer nagłówka numerowanego („6.” w „6. Jakie …”) też jest oznaczeniem jednostki, więc „6”,
    „6.” i „6 ust. 2” pasują, a „§ 6”, „16”, „2” do „2.1. …” nie (`UnitMatcherTests`);
  - aktualizacja `contracts/model-exchange.md` i `data-model.md` („Dopasowanie jednostki”).
- [X] T067b Drugi prawdziwy przebieg przeszedł kandydatów, ale krok wyboru odrzucono: model przeredagował jednostkę
  źródła („3. Co powinna zawierać reklamacja?” zamiast „3. Jak możesz złożyć reklamację?”). Poprawka (decyzja
  właściciela): model w kroku wyboru nie podaje źródeł — schemat `Selection` to `question`, `answer`, `basedOn`;
  źródła pozycji liczy kod z kandydatów `basedOn` (dokument + jednostka już sprawdzona w kroku kandydatów), w
  kolejności `basedOn`, bez powtórzeń, bez źródła bez jednostki, gdy ten sam dokument ma źródło z jednostką.
  Testy: `ValidatorSelectionTests`, `ResponseParserTests`, `PromptTests`, `FaqGeneratorErrorTests`, `FaqFlowTests`.

Ocena trzeciego przebiegu (FAQ 10/10 zapisane): 8 pozycji poprawnych, pozycja 4 łączy dwa tematy, pozycja 5
zniekształca warunki przy łączeniu kandydatów; przegląd Markdownów pokazał błędy parsera. Decyzje właściciela:

- [X] T067c Komunikat systemowy kroku wyboru: jedno pytanie = jedna sprawa; łączyć tylko kandydatów o tę samą
  sprawę, nie łączyć różnych tematów w jedno pytanie (`PromptTests`).
- [X] T067d Ugruntowanie kandydatów (cytat + liczby):
  - schemat `Candidates` dostaje wymagane pole `quote` — dosłowny fragment dokumentu potwierdzający odpowiedź;
  - tekst jednostki = sekcja Markdown od nagłówka pasującego do `unit` (`UnitMatcher`) do następnego nagłówka tego
    samego lub wyższego poziomu; bez jednostki albo bez pasującego nagłówka — cały dokument;
  - porównanie po normalizacji: tylko litery i cyfry, małe litery, reszta jako pojedyncza spacja (znaczniki
    Markdown, etykiety list, cudzysłowy i komentarze stron nie przeszkadzają); cytat ma co najmniej 3 słowa;
  - kandydat, którego cytatu nie ma w tekście jednostki albo którego odpowiedź zawiera liczbę (ciąg cyfr) spoza
    tego tekstu, **odpada** z ostrzeżeniem w konsoli (decyzja właściciela); odpowiedź jest odrzucana (kod 7)
    dopiero, gdy z dokumentu nie zostanie żaden kandydat;
  - krok wyboru: liczba w odpowiedzi pozycji, której nie ma w odpowiedziach ani cytatach kandydatów `basedOn`,
    odrzuca odpowiedź (kod 7).
- [X] T067e Parser: tytuł z kilku linii pierwszej strony („**Regulamin usług płatniczych dla osób fizycznych**” +
  trzy osobne `# …`) to jeden nagłówek `#` (replika strony w teście).
- [X] T067f Parser: dwuwierszowy pogrubiony nagłówek rozdziału („**14. Jak będziemy Cię obsługiwać, gdy władze
  ogłoszą stan nadzwyczajny,**” + „**stan zagrożenia epidemicznego lub stan epidemii?**”) to jeden nagłówek na
  poziomie pozostałych rozdziałów; dziś jego treść trafia do rozdziału 13 (regulamin obsługi klientów: rozdziały 14
  i 18; karty kredytowe dla firm: część II, rozdział 3).
- [X] T067g Parser: rozdziały nie mogą być podrozdziałami spisu treści (regulamin reklamacji: `## Spis treści`,
  rozdziały `###`).

  Wykonanie: T067e — obraz niższy od linii (pasek ozdobny 9 pt) nie ma podpisów, więc tytuł pod nim zostaje
  tytułem; T067f — pogrubiony/powiększony wiersz zakończony przecinkiem jest nagłówkiem tylko, gdy dołączony
  wiersz go domyka (rozdz. 14), a wiersz z wyrazem na krawędzi ramki schematu kroków nie jest wierszem nazw kolumn
  (rozdz. 18); T067g — nagłówek „Spis treści”/„Spis rzeczy” nigdy nie jest rodzicem. Goldeny, prywatny korpus
  (956/956) i `corpus/` bez zmian. **Otwarte:** karty kredytowe dla firm, część II, rozdział 3 — nagłówek wchodzi
  do tabeli (TableDetection, ramki „etap”), inna przyczyna; nagłówek schematu kroków w cieniowanej ramce w
  rozdziale 18 nadal jako pogrubione tytuły kroków.

- [X] T067h Czwarty przebieg: FAQ zapisane, ale odpadło 27 z 45 kandydatów (D2: został 1). Część to prawdziwe
  halucynacje (D2-K3: wiek „13”, „18” spoza rozdziału 5), reszta to:
  - zakres jednostki za wąski: „## Dodatkowe wyjaśnienia” na poziomie rozdziałów ucinał rozdział 4 regulaminu
    reklamacji — rozdział z numerem/oznaczeniem obejmuje następujące po nim nagłówki bez numeru, aż do nagłówka
    z numerem/oznaczeniem tego samego lub wyższego poziomu;
  - cytaty niedosłowne (wielokropek, pojedyncze słowa) — cytat dzielony na fragmenty po „…”/„...”, przyjęty, gdy
    ≥ 80% trójek kolejnych słów fragmentów (co najmniej 3 słowa) występuje w tekście jednostki;
  - ostrzeżenie pokazuje początek cytatu, jeden kandydat = jedna linia („kandydat Dn-Kk: powód; powód”), liczby
    zebrane („liczby „13”, „18” nie występują …”); polecenie: jeden ciągły fragment bez wielokropków.

  T067e–T067g zmieniają parser — wbrew pierwotnej uwadze „Ta funkcjonalność nie zmienia parsera”. Goldeny
  parsera mogą się zmienić tylko za zgodą właściciela (FR-163); przed commitem pełny zestaw parsera z
  `LEGALAGENT_PRIVATE_CORPUS`, potem `refresh` korpusu i `verify`.
- [X] T068 Handoff „Stan prac i przekazanie” na końcu `specs/006-faq-generation/plan.md`:
  - zrobione zadania, walidacja, odstępstwa od planu, decyzje w trakcie, otwarte punkty;
  - w tym: odsetek odrzuceń walidacji jednostek na prawdziwych danych, 429, model;
  - aktualizacja pamięci projektu.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: T001 → T002 → T006. T003, T004, T005 równolegle po T001.
- **Foundational (Phase 2)**: po Phase 1. Pary red/green: T007→T008, T009→T010, T011→T012, T013→T014,
  T015→T016, T017→T018, T019→T020. Pary są niezależne od siebie, z wyjątkiem T014/T016 (wymagają T008, T012).
- **US1 (Phase 3)**: po Phase 2.
  - T021→T022 wymaga T010, T012, T014, T016;
  - T023→T024 i T025→T026 po T022;
  - T027→T028 niezależne od biblioteki FAQ;
  - T029→T030 po T004;
  - T031→T032 wymaga T022, T024, T028, T030, T020;
  - T034→T035, T036→T037, T038→T039 po T032.
- **US2 (Phase 4)**: po US1 (T032 tworzy `FaqStage`).
  - T040→T041 → T042→T043;
  - T044→T045 po T043;
  - T046→T047;
  - T048→T049 na końcu.
- **US3 (Phase 5)**: po US1. Może iść równolegle z US2, ale oba zmieniają `FaqStage.cs`/`Program.cs`, więc
  zalecana kolejność to US2 → US3.
  - T052→T053 → T054→T055 (biblioteka);
  - T050→T051, T056→T057, T058→T059, T060→T061 (aplikacja).
- **US4 (Phase 6)**: niezależna od kodu C#; możliwa w dowolnym momencie po T002 (projekt testów aplikacji).
- **Polish (Phase 7)**: po wszystkich historyjkach.

### User Story Dependencies

- US1: fundament dla US2 i US3 (etap FAQ w aplikacji).
- US2: wymaga US1.
- US3: wymaga US1; zalecane po US2.
- US4: niezależna.

### Within Each User Story

- Test (red, osobny commit) → implementacja (green, osobny commit).
- Biblioteka przed aplikacją.
- Charakteryzacje (dostosowanie testów spec 005 w T031, T048, T061) opisane w commicie.

### Parallel Opportunities

- Setup: T003, T004, T005.
- Foundational: testy T007, T009, T011, T013, T015, T017, T019 (różne pliki), implementacje po kolei.
- US1: testy T021, T027, T029 równolegle; T023, T025 po T022.
- US3: testy biblioteki T052, T054 równolegle z testami aplikacji T050, T056, T058.
- US4 równolegle z dowolną fazą po Setup.
- Polish: T064, T065.

---

## Parallel Example: User Story 1

```text
Task: "T021 [US1] Test kroków generatora w faqtests/FaqGeneratorTests.cs"
Task: "T027 [US1] Test konwersji w faqtests/Conversion/DocumentSetConverterTests.cs"
Task: "T029 [US1] Test transportu konektora w apptests/ModelTransportTests.cs"
```

## Parallel Example: User Story 3

```text
Task: "T052 [US3] Test mapowania błędów w faqtests/ServiceErrorMapperTests.cs"
Task: "T050 [US3] Test błędów konwersji w faqtests/Conversion/DocumentSetConverterTests.cs"
Task: "T058 [US3] Test braku ponowień i limitu czasu w apptests/ModelTransportTests.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1)

1. Phase 1 + Phase 2.
2. Phase 3 (US1) → STOP i walidacja: `RunAsync` z atrapami daje `FAQ_mBank.md` i kod 0.
3. Wczesna próba ręczna: skrypt Azure (T062–T063 można przyspieszyć) i jedno zapytanie do prawdziwego wdrożenia,
   żeby sprawdzić dostępność `gpt-4o-mini` (ryzyko R1).

### Incremental Delivery

1. US1: MVP (konwersja, FAQ, plik).
2. US2: bezpieczeństwo klucza (wymaganie z opisu właściciela).
3. US3: odporność na błędy (zasada IV).
4. US4: skrypt zasobu.
5. Polish: README, CLAUDE.md, weryfikacja ręczna, handoff.

---

## Notes

- Żadne zadanie testowe nie łączy się z Azure ani z `mbank.pl`.
- Klucze w testach to oczywiste ciągi testowe. Prawdziwy klucz nigdy nie trafia do repozytorium, testów, logów ani
  commitów.
- Goldeny parsera (`tests/LegalAgent.PdfParser.Tests/Corpus/**/*.expected.md`) nie mogą się zmienić (FR-163).
  Ta funkcjonalność nie zmienia parsera.
- Nowe poprawki w trakcie implementacji dopisuj jako T0xxa (np. T057a) i do handoffu.
