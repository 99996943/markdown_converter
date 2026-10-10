# Research: Konwersja i generowanie FAQ (spec 006)

Decyzje techniczne dla etapów 2–3 aplikacji `mBank.FaqGenerator` i skryptu zasobu Azure. Każdy punkt:
decyzja, uzasadnienie, odrzucone alternatywy.

## R1. Model GPT-4o-mini w Azure — status w październiku 2026

**Stan (Microsoft Learn, „Model retirement schedule”, aktualizacja 2026-09-23)**: `gpt-4o-mini` w wersji
`2024-07-18` ma status **Deprecated**, a wycofanie zaplanowano na **2027-04-14**. Nie wskazano następcy. Status
„Deprecated” oznacza, że model działa dla istniejących klientów. Subskrypcja, w której ten model **nigdy nie był
wdrożony**, nie może utworzyć nowego wdrożenia. GA i dłuższą datę mają m.in. `gpt-5.4-mini` (2026-03-17, wycofanie
2027-09-21) i `gpt-5-mini` (wycofanie 2027-02-09).

**Decyzja**: domyślny model to `gpt-4o-mini` `2024-07-18`, zgodnie z decyzją właściciela (Clarifications).
Model, wersja i nazwa wdrożenia są parametrami skryptu i konfiguracji. Skrypt najpierw sprawdza, czy model można
wdrożyć w wybranym regionie (`az cognitiveservices model list`). Jeśli nie, kończy się kodem ≠ 0 z podpowiedzią
`--model gpt-5.4-mini --model-version 2026-03-17`. Aplikacja nie zakłada konkretnego modelu: parametr
`Temperature` jest opcjonalny (R6), bo modele z rodziny GPT-5 nie przyjmują temperatury.

**Alternatywy**: zmiana domyślnego modelu na `gpt-5.4-mini` byłaby sprzeczna z decyzją właściciela. Odnotowano
ją w ryzykach planu. Właściciel może ją podjąć bez zmiany kodu.

## R2. Klient modelu: Semantic Kernel

**Decyzja**:
- Biblioteka `LegalAgent.Faq` zależy wyłącznie od **`Microsoft.SemanticKernel.Abstractions` 1.80.1**
  (`IChatCompletionService`, `ChatHistory`, `PromptExecutionSettings`, `HttpOperationException`).
- Aplikacja dodaje **`Microsoft.SemanticKernel.Connectors.AzureOpenAI` 1.80.1** i tworzy
  `AzureOpenAIChatCompletionService` z gotowego `AzureOpenAIClient`.

Wersja 1.80.1 pochodzi z 2026-09-03 (ma ponad 2 tygodnie). 1.81.0 z 2026-10-06 jest zbyt świeża. Obie wersje są
przypięte centralnie w `Directory.Packages.props`. `Azure.AI.OpenAI` przychodzi przechodnio i nie jest przypinany
osobno.

**Uzasadnienie**:
- Semantic Kernel to wymaganie właściciela.
- Rozdział abstrakcji i konektora sprawia, że biblioteka nie zna Azure, a testy biblioteki podstawiają własne
  `IChatCompletionService`.
- Konektor przyjmuje `AzureOpenAIClient` z opcjami transportu, więc aplikacja kontroluje limit czasu, ponowienia
  i `HttpClient` (R7). Testy aplikacji mogą też przepuścić prawdziwy konektor przez atrapę `HttpMessageHandler`.

**Alternatywy**: samo `Azure.AI.OpenAI` (mniej zależności) byłoby sprzeczne z opisem. Pełny pakiet
`Microsoft.SemanticKernel` (Kernel, wtyczki) jest niepotrzebny (YAGNI, zasada VI).

## R3. Dwa kroki generowania i rozmiar wejścia

**Decyzja** (FR-421):
1. **Kandydaci**: osobne zapytanie dla każdego z 5 dokumentów, wykonywane **po kolei**. Wejście to pełny
   Markdown dokumentu (tak jak zapisany na dysku, ze znacznikami stron), oznaczenie `D1`…`D5`, nazwa i adres.
   Wynik to do `CandidatesPerDocument` (domyślnie 10) par z jednostką.
2. **Wybór**: jedno zapytanie z kandydatami wszystkich dokumentów (identyfikatory `D2-K3`). Wynik to dokładnie
   10 pozycji, każda ze wskazaniem kandydatów (`basedOn`) i źródeł.

Rozmiar szacowany jest jako `ceil(liczba znaków / 3)` tokenów. To szacunek ostrożny dla polskiego tekstu:
tokenizer o200k daje ok. 3,5–4 znaku na token. Limit `MaxDocumentTokens` wynosi domyślnie 100 000 przy 128 tys.
tokenów kontekstu GPT-4o-mini, z zapasem na polecenie i odpowiedź. Odpowiada to ok. 300 tys. znaków, a prawdziwe
regulaminy mają 140–155 tys. Sprawdzenie odbywa się **przed pytaniem o klucz**: `FaqGenerator.CheckInput`.

**Uzasadnienie**:
- Kolejne, a nie równoległe zapytania: 5 × ~50 tys. tokenów w jednej minucie przekroczyłoby typowy limit TPM
  wdrożenia, a aplikacja nie ponawia zapytań (FR-424). Koszt to dłuższy czas (kilka minut), co jest akceptowalne
  przy jednym uruchomieniu.
- Szacunek zamiast tokenizera: unika kolejnej zależności (`Microsoft.ML.Tokenizers`) i słownika modelu. Błąd
  szacunku jest po bezpiecznej stronie, a przekroczenie i tak zgłosi usługa (`context_length_exceeded` → kod 6).

**Alternatywy**:
- Podział dokumentu na fragmenty (chunker ze spec 004): więcej zapytań i trudniejsze wskazanie źródła. Niepotrzebny,
  dopóki dokument mieści się w kontekście.
- Przycinanie treści: wykluczone przez FR-421.

## R4. Ustrukturyzowana odpowiedź modelu

**Decyzja**: oba kroki używają **structured outputs** (`response_format` = `json_schema`, `strict: true`) ze
schematami z `contracts/model-exchange.md`. Biblioteka publikuje schematy jako stałe (`FaqSchemas`). Aplikacja
wkłada je do `AzureOpenAIPromptExecutionSettings.ResponseFormat` przez
`ChatResponseFormat.CreateJsonSchemaFormat`. Biblioteka parsuje JSON przez `System.Text.Json` i **zawsze** go
sprawdza (R5). Na schemat po stronie usługi nie polega.

**Uzasadnienie**:
- Structured outputs obsługują `gpt-4o-mini 2024-07-18` i modele GPT-5.
- Odpowiedź w JSON-ie usuwa niejednoznaczność „tekstu przed i po FAQ” (przypadek brzegowy, FR-432). Markdown
  renderuje aplikacja, nie model.

**Alternatywy**: Markdown od modelu parsowany wyrażeniami regularnymi jest kruchy. Tryb `json_object` bez schematu
pozwala na dowolne pola.

## R5. Sprawdzanie odpowiedzi i jednostek

**Decyzja** (FR-422, FR-430). W `FaqResponseValidator` oba kroki sprawdzają:
- JSON zgodny z kształtem;
- pytania i odpowiedzi niepuste po przycięciu;
- brak powtórzonych pytań (porównanie po normalizacji: małe litery, zwinięte białe znaki, bez końcowego `?`);
- dokument źródła należy do zbioru wejściowego;
- jednostka (jeśli podana) istnieje w dokumencie.

Krok kandydatów dodatkowo: 1…`CandidatesPerDocument` par, a źródło to ten sam dokument. Odpowiedź z większą liczbą
par jest odrzucana, nie przycinana.

Krok wyboru dodatkowo:
- dokładnie 10 pozycji;
- każde `basedOn` wskazuje istniejącego kandydata;
- każdy dokument źródła jest dokumentem któregoś z kandydatów `basedOn`.

**Dopasowanie jednostki**. Normalizacja: NBSP → spacja, zwinięcie białych znaków, usunięcie końcowej kropki,
porównanie bez rozróżniania wielkości liter (kultura niezmienna). Zbiór jednostek dokumentu to znormalizowane
`Section.Designation` (np. „§ 12”, „Art. 5”, „Rozdział 3”) i `Section.HeadingText` wszystkich sekcji
(rekurencyjnie). Wskazana jednostka pasuje, gdy po normalizacji jest równa elementowi zbioru albo zaczyna się od
oznaczenia (`Designation`), po którym następuje koniec, spacja lub przecinek. Przykład: „§ 12 ust. 3” → „§ 12”.

**Uzasadnienie**: sprawdzalna w testach namiastka zasady II. Odrzuca wymyślone dokumenty i paragrafy, nie oceniając
treści merytorycznie (SC-074 — człowiek).

**Alternatywy**: sprawdzanie, czy cytat występuje w tekście, wymagałoby od modelu dosłownych cytatów. To większe
odpowiedzi i więcej fałszywych odrzuceń.

## R6. Losowość i limity odpowiedzi

**Decyzja**:
- `Temperature` jest opcjonalne, domyślnie `0`. Wartość `null` oznacza, że parametr nie jest wysyłany (dla modeli
  GPT-5).
- `Seed` domyślnie `42` (wysyłany, gdy ustawiony).
- `MaxOutputTokens` domyślnie 4096 na zapytanie.

Wszystko w sekcji `AzureOpenAI` konfiguracji. Pełnego determinizmu nie ma (zasada III — Complexity Tracking).

## R7. Transport, limit czasu, ponowienia i mapowanie błędów

**Decyzja**:
- **Klient:** `AzureOpenAIClient(endpoint, new ApiKeyCredential(key), options)`, gdzie `options` to
  `AzureOpenAIClientOptions`:
  - `NetworkTimeout = AzureOpenAI:TimeoutSeconds` (domyślnie 300 s na zapytanie);
  - `RetryPolicy = new ClientRetryPolicy(maxRetries: 0)`: brak ponowień (FR-424); kod sprawdzi w implementacji
    nazwę typu z `System.ClientModel` w wersji przypiętej przez konektor;
  - `Transport = new HttpClientPipelineTransport(httpClient)`: w testach `HttpClient` z atrapą handlera.
- **Mapowanie wyjątków na `FaqServiceErrorKind`:** robi je biblioteka, bo wyjątki pochodzą z abstrakcji SK.

| Źródło | Rodzaj |
|--------|--------|
| `HttpOperationException` 401/403 | `Authentication` |
| `HttpOperationException` 404 | `DeploymentNotFound` |
| `HttpOperationException` 429 | `RateLimited` |
| `HttpOperationException` 400 z `content_filter` w treści | `ContentFiltered` |
| `HttpOperationException` 400 z `context_length_exceeded` | `InputTooLong` |
| `HttpOperationException` 5xx | `ServiceUnavailable` |
| `TaskCanceledException` lub `TimeoutException` bez anulowania przez użytkownika | `Timeout` |
| `HttpRequestException` | `Network` |
| inne | `Other` |

Anulowanie przez użytkownika przechodzi dalej jako `OperationCanceledException` (kod 130).

**Uzasadnienie**: jeden jawny limit czasu na zapytanie (zasada IV) i przewidywalne zachowanie przy 429. Testy
offline: atrapa `IChatCompletionService` rzuca `HttpOperationException` (biblioteka), atrapa handlera zwraca 401
(aplikacja przez prawdziwy konektor).

## R8. Klucz API: wczytywanie, gwiazdki, przekierowane wejście, Ctrl+C

**Decyzja** (FR-410–413). W aplikacji `KeyPrompt` korzysta z interfejsu `IKeyInput`:
- `IsInputRedirected`;
- `ReadKey()` (bez echa);
- `ReadLine()` (z tego samego `TextReader`, co pytania o adresy).

Implementacja produkcyjna to `ConsoleKeyInput`: `Console.IsInputRedirected`, `Console.ReadKey(intercept: true)`,
`Console.In`. Testy podają skryptowane klawisze.

- **Konsola:** zwykły znak → dopisz i wypisz `*`. Backspace → usuń ostatni znak i wypisz `"\b \b"`. Enter → koniec.
  Inne klawisze sterujące są ignorowane. Wklejenie z terminala przychodzi jako ciąg zdarzeń klawiszy (Windows
  Terminal, terminale linuksowe), więc daje gwiazdkę na znak bez osobnej obsługi. Pusty klucz po przycięciu →
  komunikat i ponowne pytanie.
- **Ctrl+C:** na czas pytania `Console.TreatControlCAsInput = true` (przywracane w `finally`). Ctrl+C przychodzi
  jako klawisz, a `KeyPrompt` zgłasza `OperationCanceledException` (kod 130). Powód: `ReadKey` blokuje się i nie
  reaguje na token anulowania, a obsługa `CancelKeyPress` z `e.Cancel = true` zostawiłaby program zawieszony.
- **Wejście przekierowane:** `ReadLine()`. Brak wiersza lub pusty wiersz → kod 2 z podpowiedzią
  `… | mBank.FaqGenerator --url …`. Przy wejściu przekierowanym nie są wypisywane ani pytanie, ani gwiazdki.
- **Brak trwałości:** klucz jest trzymany w `string` i trafia tylko do `ApiKeyCredential`. Nie jest logowany,
  nie trafia do `IConfiguration` ani do `Environment`. Wszystkie komunikaty błędów z etapu FAQ przechodzą przez
  `SecretRedactor.Redact(message, key)`: zamiana wystąpień klucza na `***`.

**Alternatywy**: `SecureString` jest przestarzały na .NET i nie chroni na Linuksie. Odczyt bez echa przez
`stty -echo` jest specyficzny dla Uniksa.

## R9. Konwersja w bibliotece

**Decyzja** (FR-400–403): klasa `DocumentSetConverter` w bibliotece (`LegalAgent.Faq.Conversion`):
- **Parser:** wstrzyknięty `IPdfMarkdownConverter`; aplikacja tworzy go przez `AddLegalAgentPdfParser()` z
  domyślnymi opcjami, tak jak `legalagent-pdf convert` bez opcji. Wejście: lista `PdfSource(Index, Address, PdfPath)`.
- **Kolejność:** pliki konwertowane po kolei w kolejności `Index`. `SourceId` to nazwa pliku.
- **Zapis:** `<nazwa>.md` atomowo (`.tmp` + `File.Move(overwrite)`), jak `WriteAtomicAsync` w CLI parsera.
- **Błąd jednego pliku:** `PdfParserException`, Markdown bez tekstu poza znacznikami stron albo `IsComplete == false`
  są zapisywane jako błąd. Kolejne pliki są konwertowane dalej. Wynik to `ConversionRun` z listą `ConvertedDocument`
  i błędami.
- **Sprzątanie:** po sukcesie wszystkich plików usuwane są `*.md` spoza bieżącego zestawu (FR-403).

`DocumentSetConverter` nie pisze na konsolę. Postęp zgłasza przez `IProgress<ConversionEvent>`. Logika jest w
`LegalAgent.Faq.Conversion` (konstytucja, „Architektura”: cała logika w bibliotece; wynik /speckit-analyze C3).
Biblioteka nie zna `LegalAgent.Downloads`: aplikacja mapuje `DownloadResult` na `PdfSource`. Jednostki zbiera
`UnitExtractor.FromDocument(LegalDocument)`.

**Alternatywy**:
- logika w aplikacji (pierwsza wersja planu): sprzeczna z wymogiem cienkiej warstwy;
- osobna biblioteka „pipeline”: kolejny projekt bez zysku (zasada VI);
- metody w `LegalAgent.PdfParser`: parser jest strumieniowy, bez operacji na plikach.

## R10. Plik FAQ i OKF

**Decyzja**: `FAQ_mBank.md` to jeden dokument OKF (front matter YAML) w osobnym katalogu OKF `faq/` (domyślnie;
`Faq:OutputDirectory`, `--faq-output`). Katalog nie jest ignorowany przez git. Format jest opisany w
`contracts/faq-file.md`. Tekst YAML zapisuje ręczny, mały emiter w `FaqMarkdownRenderer`: wartości w cudzysłowach
z ucieczką `\"` i `\\`, adresy jako lista. Biblioteka nie dostaje zależności od YamlDotNet. Pola: `type: faq`,
`title`, `description`, `resource` (lista 5 adresów w kolejności dokumentów), `timestamp` (UTC, ISO 8601, z
`TimeProvider`), `model`, `deployment`. Dodatkowe pola są dozwolone w OKF (repozytorium `knowledge-catalog`:
„arbitrary extra frontmatter keys”).

Repozytorium `knowledge-catalog/okf` jest dziś zamrożoną kopią, a kanoniczna specyfikacja (v0.2) przeszła do
`GoogleCloudPlatform/open-knowledge-format`. Konstytucja wskazuje v0.1 i pola `type`, `title`, `description`,
`resource`, `timestamp`. Plan się ich trzyma.

**Alternatywy**: katalog OKF z plikiem na każde pytanie byłby sprzeczny z opisem („w pliku FAQ_mBank.md”).

## R11. Skrypt zasobu Azure

**Decyzja**: skrypt `scripts/azure/create-openai.sh` (Bash, `set -euo pipefail`, Azure CLI):
- **Parametry:**
  - `--resource-group` (domyślnie `rg-faqgen`);
  - `--location` (`swedencentral`);
  - `--name` (domyślnie `faqgen-<8 znaków skrótu SHA-256 z identyfikatora subskrypcji>`, deterministycznie i
    globalnie unikalnie dla poddomeny);
  - `--deployment` (`gpt-4o-mini`), `--model` (`gpt-4o-mini`), `--model-version` (`2024-07-18`);
  - `--sku` (`GlobalStandard`), `--capacity` (`200` = 200 tys. TPM).
- **Kroki:**
  1. Sprawdza, czy jest `az` i aktywne logowanie (`az account show`).
  2. Sprawdza, czy model jest dostępny w regionie (`az cognitiveservices model list`).
  3. Tworzy grupę (`az group create` jest idempotentne).
  4. Konto: `account show` || `account create --kind OpenAI --sku S0 --custom-domain <name> --yes`.
  5. Wdrożenie: `deployment show` || `deployment create`.
  6. Wypisuje endpoint i nazwę wdrożenia jako fragment `appsettings.Local.json` oraz zmienne `FAQGEN__…`.
- **Klucz:** skrypt nigdy nie wywołuje `keys list`. Wypisuje tylko polecenie, którym użytkownik może przekazać
  klucz potokiem prosto do aplikacji (quickstart).
- **Usuwanie:** opisane w README (`az group delete --name rg-faqgen`).

**Test**: `tests/mBank.FaqGenerator.Tests/AzureScriptTests.cs` uruchamia skrypt przez `bash` z atrapą `az` na
`PATH`. Atrapa to skrypt Bash, który zapisuje wywołania do pliku i zwraca zaprogramowane odpowiedzi. Sprawdzane są:
idempotencja (drugie uruchomienie bez `create`), brak `keys list` w wywołaniach, kody przy braku logowania i
niedostępnym modelu, format wyjścia. Bez `bash` (Windows bez Git Bash) test jest pomijany (`Assert.SkipUnless`).
Na CI (Linux) zawsze się wykonuje.

**Alternatywy**:
- PowerShell i Bicep: Bicep nie daje warunków wstępnych ani czytelnego wyjścia z instrukcją, a PowerShell nie
  jest domyślny na Linuksie.
- Terraform: dodatkowe narzędzie i stan.

## R12. Kody wyjścia

**Decyzja** (FR-451, rozszerzenie kontraktu ze spec 005):

| Kod | Znaczenie |
|-----|-----------|
| 0 | pobrano 5/5, przekonwertowano 5/5, zapisano `FAQ_mBank.md` |
| 1 | błąd nieoczekiwany |
| 2 | błędne argumenty lub konfiguracja (także brak `AzureOpenAI:Endpoint`/`Deployment`), błędne adresy, brak wejścia dla adresów lub klucza |
| 3 | nie wszystkie pliki pobrane |
| 4 | błąd zapisu: katalog pobrań, plik Markdown, plik FAQ |
| 5 | błąd konwersji co najmniej jednego pliku |
| 6 | etap FAQ: błąd usługi modelu (uwierzytelnienie, wdrożenie, limit, filtr, czas, sieć) albo dokument za długi dla modelu |
| 7 | etap FAQ: odpowiedź modelu odrzucona przy sprawdzaniu |
| 130 | przerwano (Ctrl+C) |

**Uzasadnienie**: kody 0–4 i 130 zostają bez zmian, więc wywołujący spec 005 nie są łamani (poza tym, że 0
wymaga teraz FAQ). Kody 5–7 rozróżniają etap porażki.

## R13. Testowalność `Program.RunAsync`

**Decyzja**: `RunAsync` przyjmuje obiekt `AppHost`, który zastępuje parametr `handler`. `AppHost` to rekord z
wartościami domyślnymi dla produkcji:
- `DownloadHandler`;
- `ModelHandler`: `HttpMessageHandler` dla konektora;
- `ChatFactory`: `Func<string apiKey, IChatCompletionService>?`; gdy ustawiona, zastępuje konektor;
- `KeyInput`;
- `TimeProvider`.

`AppHarness` w testach ustawia atrapy. Testy spec 005 dostają w swoim `appsettings.json` sekcję `AzureOpenAI` i
atrapę modelu, bo bez nich `RunAsync` kończy się kodem 2 (FR-420: konfiguracja sprawdzana przed pobieraniem).

**Alternatywy**: kontener DI w aplikacji jest niepotrzebny dla kilku zależności (zasada VI).
