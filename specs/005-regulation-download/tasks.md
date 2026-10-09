---

description: "Task list for spec 005 — pobieranie regulaminów (mBank.FaqGenerator)"
---

# Tasks: Pobieranie regulaminów — aplikacja mBank.FaqGenerator

**Input**: Design documents from `specs/005-regulation-download/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: OBOWIĄZKOWE — konstytucja, zasada I (TDD, NON-NEGOTIABLE). Zadanie testowe poprzedza
implementację i MUSI najpierw padać **na asercji** (nie na kompilacji — w razie potrzeby szkielet typów
z `NotImplementedException`). Osobne commity: `test: … (red)`, potem `feat:`/`fix: …` (green). Testy
offline (atrapa `FakeHttpHandler`, katalogi tymczasowe) i deterministyczne — bez `Task.Delay` jako dowodu
równoległości i bez połączeń sieciowych. Test, który przechodzi od razu (charakteryzacja zachowania już
zapewnionego wcześniejszym zadaniem), jest commitowany jako `test:` z adnotacją w opisie commita.

**Organization**: zadania pogrupowane wg historyjek ze spec.md: US1 (P1) interaktywne podanie adresów i
pobranie, US2 (P1) niedostępne linki nie przerywają pracy, US3 (P2) uruchomienie jednym poleceniem,
powtarzalność i sprzątanie. Kolejność faz: US1 → US2 → US3.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: można wykonać równolegle (inne pliki, brak zależności od niezakończonych zadań)
- **[Story]**: historyjka (US1…US3)

## Path Conventions

- Biblioteka: `src/LegalAgent.Downloads/` (dalej `dl-lib/`), testy: `tests/LegalAgent.Downloads.Tests/`
  (dalej `dltests/`); aplikacja: `src/mBank.FaqGenerator/` (dalej `app/`), testy aplikacji:
  `tests/mBank.FaqGenerator.Tests/` (dalej `apptests/`).
- Konwencje jak w 001–004: `CultureInfo.InvariantCulture` (wyjątek: komunikaty i rozmiary dla
  użytkownika jawnie w `pl-PL` — `CultureInfo.GetCultureInfo("pl-PL")` — także w bibliotece), `StringComparison.Ordinal*`, jawne sortowania, LF, UTF-8 bez BOM, XML-doc publicznych
  typów, `TreatWarningsAsErrors`, analizatory CA1304/CA1305/CA1307/CA1309/CA1310 jako błędy.
- Biblioteka: bez konsoli, zmiennych środowiskowych, konfiguracji, stanu statycznego mutowalnego, bez
  `mbank.pl` i bez liczby 5 (konstytucja; plan → Constitution Check). Kod i komentarze po angielsku,
  komunikaty dla użytkownika po polsku.
- Odniesienia „R*n*” = decyzje w research.md; formaty = contracts/*.md; pola = data-model.md.
- Aplikacja testowana przez `Program.RunAsync(args, stdin, stdout, stderr, environment, configDirectory,
  handler, ct)` — bez prawdziwej konsoli, zmiennych procesu, sieci i bez `appsettings.json` aplikacji
  (każdy test zapisuje własny `appsettings.json` w katalogu tymczasowym).
- Przed commitem: `git branch --show-current` = `005-regulation-download`; tylko jawne ścieżki w `git add`.

---

## Phase 1: Setup

- [X] T001 Utwórz `dl-lib/LegalAgent.Downloads.csproj` (class library, `GenerateDocumentationFile`, `RootNamespace` `LegalAgent.Downloads`, `Version` 1.0.0, `InternalsVisibleTo` `LegalAgent.Downloads.Tests`, bez pakietów) i `dltests/LegalAgent.Downloads.Tests.csproj` (jak `tests/LegalAgent.Chunking.Tests/LegalAgent.Chunking.Tests.csproj`: Exe, `IsPackable` false, xunit.v3, xunit.runner.visualstudio, Microsoft.NET.Test.Sdk, `NoWarn CA1707`, `Using Xunit`, referencja do `LegalAgent.Downloads`); dodaj oba do `LegalAgent.slnx` (foldery `/src/`, `/tests/`)
- [X] T002 Utwórz `app/mBank.FaqGenerator.csproj` (Exe, `AssemblyName` `mBank.FaqGenerator`, `RootNamespace` `MBank.FaqGenerator`, referencja do `LegalAgent.Downloads`, pakiety `Microsoft.Extensions.Configuration.Json`, `.EnvironmentVariables`, `.Binder`, `InternalsVisibleTo` `mBank.FaqGenerator.Tests`, `appsettings.json` z `CopyToOutputDirectory` PreserveNewest) i `apptests/mBank.FaqGenerator.Tests.csproj` (jak T001, referencja do `app/`); dodaj `<PackageVersion Include="Microsoft.Extensions.Configuration.Json" Version="9.0.20" />` do `Directory.Packages.props`; dodaj oba projekty do `LegalAgent.slnx`; `dotnet build LegalAgent.slnx -c Release` zielony; treść commita zawiera uzasadnienie nowej zależności (research R10, zasada VI)
- [X] T003 [P] Dodaj `downloads/` do `.gitignore` (sekcja „mBank.FaqGenerator”) oraz `app/appsettings.json` dokładnie jak w contracts/cli.md (`Urls: []`, `AllowedHosts: ["mbank.pl"]`, `AllowHttp: false`, `OutputDirectory: "downloads"`, `TimeoutSeconds: 60`, `MaxFileSizeMegabytes: 50`, `MaxRedirects: 5`, `UserAgent: "mBank.FaqGenerator/1.0"`)
- [X] T004 [P] Szkielet publicznych typów biblioteki z contracts/library-api.md i data-model.md: `dl-lib/DownloadOptions.cs`, `dl-lib/AddressCheck.cs` (+ `AddressError`), `dl-lib/AddressValidator.cs`, `dl-lib/FileNamePlanner.cs`, `dl-lib/PlannedDownload.cs`, `dl-lib/DocumentDownloader.cs`, `dl-lib/DownloadManifestJson.cs`, `dl-lib/DownloadDirectoryException.cs`, `dl-lib/Model/DownloadResult.cs`, `DownloadError.cs`, `DownloadErrorKind.cs`, `DownloadStatus.cs`, `DownloadRun.cs`, `DownloadEvent.cs`, `DownloadEventKind.cs`; metody z `NotImplementedException`; XML-doc
- [X] T005 [P] Atrapa HTTP w `dltests/Fakes/FakeHttpHandler.cs` (dziedziczy `HttpMessageHandler`): mapa adres → skrypt odpowiedzi (kod, nagłówki `Location`/`Last-Modified`/`Content-Length`, treść bajtowa lub strumień, opóźnienie z `Task.Delay(…, ct)`, rzucany `HttpRequestException` z `HttpRequestError.NameResolutionError`, strumień zrywany po N bajtach), rejestr otrzymanych żądań (adres, `User-Agent`), opcjonalna bariera „wstrzymaj odpowiedzi do nadejścia K żądań” (limit 5 s); `dltests/Fakes/PdfBytes.cs` (minimalne bajty zaczynające się od `%PDF-1.7`, strona HTML); `dltests/Fakes/TempDirectory.cs` (`IDisposable`, katalog w `Path.GetTempPath()`); udostępnij te same pliki w `apptests/` przez `<Compile Include="..\LegalAgent.Downloads.Tests\Fakes\*.cs" LinkBase="Fakes" />`; `apptests/Fakes/AppHarness.cs` (katalog tymczasowy z `appsettings.json` budowanym w teście, domyślnie `AllowedHosts: ["example.test"]`, wywołanie `RunAsync` ze `StringReader`/`StringWriter` i `configDirectory`, zwraca kod, stdout, stderr)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: opcje, lista hostów, sprawdzanie adresów i nazwy plików — wspólne dla wszystkich historyjek.

**⚠️ CRITICAL**: żadna historyjka nie startuje przed końcem tej fazy.

- [X] T006 Test (red) w `dltests/DownloadOptionsTests.cs`: domyślne wartości z data-model.md (`OutputDirectory` „downloads”, `AllowHttp` false, `Timeout` 60 s, `MaxFileSizeBytes` „50 × 1024 × 1024”, `MaxRedirects` 5); walidacja → `ArgumentException` w konstruktorze `DocumentDownloader`: `OutputDirectory` „niepusta”, `AllowedHosts` „co najmniej 1 wpis; wpis to nazwa hosta bez schematu, portu i ścieżki” (`"https://mbank.pl"`, `"mbank.pl:443"`, `"mbank.pl/x"`, `""` odrzucone), `Timeout` „> 0”, `MaxFileSizeBytes` „> 0”, `MaxRedirects` „0–20”
- [X] T007 Implementacja (green) walidacji w `dl-lib/DownloadOptions.cs` (metoda `internal void Validate()`) wywoływanej z konstruktora `dl-lib/DocumentDownloader.cs`
- [X] T008 [P] Test (red) w `dltests/HostAllowListTests.cs` (R3): `mbank.pl` dopuszcza `mbank.pl`, `www.mbank.pl`, `WWW.MBANK.PL`, `a.b.mbank.pl`; odrzuca `mbank.pl.evil.com`, `evilmbank.pl`, `mbank.com`, adres IP; porównanie po `Uri.IdnHost` (host z Unicode porównywany w punycode)
- [X] T009 Implementacja (green) `dl-lib/HostAllowList.cs` (internal; `IsAllowed(Uri)`)
- [X] T010 [P] Test (red) w `dltests/AddressValidatorTests.cs` (FR-302, data-model.md → AddressCheck): `Trim` spacji; `Empty` dla `null`/`""`/`"   "`; `NotAbsolute` dla `"abc"` i `"/pdf/a.pdf"`; `SchemeNotAllowed` dla `ftp://mbank.pl/a.pdf` i `http://…` (bez `AllowHttp`), `http` przyjęty przy `AllowHttp`; `HasUserInfo` dla `https://mbank.pl@evil.com/a.pdf` i `https://u:p@mbank.pl/a.pdf`; `HostNotAllowed` z komunikatem zawierającym host i listę hostów; `Duplicate` dla adresu równego wcześniejszemu (także różniącego się tylko fragmentem `#page=2` i wielkością liter hosta); poprawny adres → `IsValid`, `Address` po `Trim`; `CheckAll` zwraca wyniki w kolejności, duplikat wykrywany względem wcześniejszych pozycji; komunikaty po polsku
- [X] T011 Implementacja (green) `dl-lib/AddressValidator.cs` i `dl-lib/AddressCheck.cs`
- [X] T012 [P] Test (red) w `dltests/FileNamePlannerTests.cs` (R7, FR-321, przypadki brzegowe spec): ostatni segment (`…/regulaminy/reg-konta.pdf` → `reg-konta.pdf`); parametry i fragment pomijane (`a.pdf?v=3#page=2` → `a.pdf`); dekodowanie `%20`, `%C5%82` → spacja, „ł” (NFC); znaki `< > : " / \ | ? *` i sterujące → `-`, ciągi `-` scalane, brzegowe spacje/kropki obcięte; `.PDF` → `.pdf`, brak rozszerzenia → dopisane `.pdf`; `CON.pdf` → `_CON.pdf`; rdzeń > 120 znaków obcięty do 120; ścieżka `/` lub pusta → `regulamin-N.pdf`; kolizja (także różna tylko wielkością liter) → drugi i dalsze `<rdzeń>-N.pdf` z numerem adresu; `Index` 1-based; ten sam wynik przy wielokrotnym wywołaniu
- [X] T013 Implementacja (green) `dl-lib/FileNamePlanner.cs`

**Checkpoint**: biblioteka sprawdza adresy i planuje nazwy — można zacząć historyjki.

---

## Phase 3: User Story 1 — Interaktywne podanie 5 adresów i pobranie regulaminów (Priority: P1) 🎯 MVP

**Goal**: aplikacja pyta po kolei o 5 adresów, pobiera je równolegle do `./downloads`, zapisuje manifest i
podsumowanie, kod 0.

**Independent Test**: `Program.RunAsync` z 5 adresami na `stdin` i atrapą zwracającą PDF → 5 plików o treści
identycznej z atrapą, `manifest.json`, podsumowanie, kod 0.

### Biblioteka

- [X] T014 [P] [US1] Test (red) w `dltests/DocumentDownloaderTests.cs` — sukces: katalog wyjściowy tworzony, gdy nie istnieje; 2 adresy → 2 pliki o nazwach z `FileNamePlanner` i treści bajt w bajt z atrapy; `DownloadResult` `Downloaded` z `SizeBytes`, `Sha256` („64 małe znaki hex”, zgodny z `SHA256.HashData`), `LastModified` z nagłówka (UTC) lub `null`; `Address` = adres podany (nie po przekierowaniu); żądanie z nagłówkiem `User-Agent` z opcji; brak plików `*.part` po zakończeniu; `DownloadRun.Results` w kolejności `Index`, `AllSucceeded` true; `DownloadAllAsync` z pustą listą albo adresem spoza `AllowedHosts` → `ArgumentException`, bez żądań i bez zmian w katalogu
- [X] T015 [US1] Implementacja (green) `dl-lib/SingleDownload.cs` (internal: `ResponseHeadersRead`, kopiowanie buforem 81920 B do `<nazwa>.part` z `IncrementalHash` SHA-256, `File.Move(part, final, overwrite: true)`, `finally` usuwa `.part`) i orkiestracji w `dl-lib/DocumentDownloader.cs` (tworzenie katalogu, plan nazw, wyniki w kolejności)
- [X] T016 [P] [US1] Test (red) w `dltests/DocumentDownloaderTests.cs` — równoległość (FR-310, SC-060, R12): atrapa z barierą „5 żądań” → `DownloadAllAsync` dla 5 adresów kończy się sukcesem (przy pobieraniu sekwencyjnym bariera wygasa po 5 s i test pada); `IProgress<DownloadEvent>` dostaje dla każdego adresu `Started` i `Finished` z wynikiem
- [X] T017 [US1] Implementacja (green) w `dl-lib/DocumentDownloader.cs`: `Task.WhenAll` po wszystkich adresach, zdarzenia postępu
- [X] T018 [P] [US1] Test (red) w `dltests/ManifestTests.cs` (FR-324, contracts/download-manifest.md): `DownloadManifestJson.Serialize` daje dokładnie tekst z kontraktu dla wyniku `downloaded` i `failed` (`schemaVersion` 1, kolejność kluczy, pominięte `null`, `error.kind` w kebab-case, `httpStatus` tylko dla `http-status`, wcięcie 2 spacje, LF, końcowy `\n`, UTF-8 bez BOM, polskie znaki niezakodowane); brak `Detail` w wyniku; `DownloadAllAsync` zapisuje `<katalog>/manifest.json` i zwraca `ManifestPath`; dwa uruchomienia z tą samą atrapą → identyczny manifest bajt w bajt
- [X] T019 [US1] Implementacja (green) `dl-lib/DownloadManifestJson.cs` (`System.Text.Json`, `JavaScriptEncoder.UnsafeRelaxedJsonEscaping` dla polskich znaków, `NewLine = "\n"`) i atomowy zapis manifestu (`manifest.json.part` + `File.Move`) w `dl-lib/DocumentDownloader.cs`

### Aplikacja

- [X] T020 [P] [US1] Test (red) w `apptests/PromptTests.cs` (FR-301, FR-302, scenariusze US1 1–3): `RunAsync([], stdin, …)` z 5 poprawnymi adresami w `stdin` → na stdout pytania „Podaj adres regulaminu 1 z 5: ” … „5 z 5: ”, kod 0; wpis `abc` → komunikat „Niepoprawny adres: …” i ponowne „1 z 5”; pusty wiersz, `ftp://…`, host spoza listy → to samo; duplikat → komunikat o powtórzonym adresie i ten sam numer; spacje wokół adresu pomijane; konfiguracja testu przez `AppHarness` (własny `appsettings.json`, bez `mbank.pl`)
- [X] T021 [US1] Implementacja (green) `app/Program.cs` (`Main` → `RunAsync` z `Console.In/Out/Error`, zmiennymi procesu i `Console.CancelKeyPress`), `app/AppSettings.cs` (warstwy konfiguracji z R10: `appsettings.json` z `configDirectory` — w `Main` `AppContext.BaseDirectory` —, `appsettings.Local.json` opcjonalny, `FAQGEN__` z przekazanego słownika `environment`; mapowanie `TimeoutSeconds`/`MaxFileSizeMegabytes` → `DownloadOptions`), `app/AddressPrompt.cs` (pętla R11 na `TextReader`), stała `RequiredCount = 5`; `HttpClient` na `SocketsHttpHandler { AllowAutoRedirect = false, AutomaticDecompression = All, ConnectTimeout = 15 s }` z `Timeout = InfiniteTimeSpan`, w testach na przekazanym `handler`
- [X] T022 [P] [US1] Test (red) w `apptests/SummaryTests.cs` (FR-330, contracts/cli.md → „Postęp i podsumowanie”): linie postępu `[n/5] pobieranie <adres>` i `[n/5] pobrano <plik> (<rozmiar>)`; podsumowanie „Pobrano 5 z 5 plików do <katalog>:” z pozycjami 1–5 w kolejności numerów („n. <plik> — <rozmiar>”, rozmiary `pl-PL`: „812,4 KB”, „1,2 MB”, „512 B”) i linia „Manifest: <ścieżka>”; pliki w katalogu podanym w `--output`/konfiguracji (katalog tymczasowy testu)
- [X] T023 [US1] Implementacja (green) `app/ConsoleReport.cs` (postęp pod blokadą, podsumowanie, formatowanie rozmiarów) i podłączenie w `app/Program.cs`

**Checkpoint**: MVP — interaktywne pobranie 5 regulaminów działa na atrapie.

---

## Phase 4: User Story 2 — Niedostępne lub błędne linki nie przerywają pracy (Priority: P1)

**Goal**: każdy rodzaj błędu jest wynikiem z czytelną przyczyną, pozostałe pliki są pobierane, brak
niekompletnych plików, kod 3.

**Independent Test**: 3 poprawne adresy + 404 + przekroczony czas → 3 pliki, 2 komunikaty z przyczynami,
„Pobrano 3 z 5”, kod 3.

### Biblioteka

- [ ] T024 [P] [US2] Test (red) w `dltests/DownloadErrorsTests.cs` — błędy odpowiedzi (FR-314, US2 1, 3, 4): 404 → `HttpStatus`, `HttpStatusCode` 404, komunikat „serwer zwrócił 404 Not Found”; 500 → analogicznie; `HttpRequestException` (DNS, odmowa połączenia) → `Connection` z komunikatem zawierającym host, `Detail` z treścią wyjątku; 200 z HTML → `NotPdf` („pod adresem nie ma pliku PDF”), plik nie zapisany; w każdym przypadku pozostałe adresy z tej samej listy `Downloaded`, brak wyjątku z `DownloadAllAsync`
- [ ] T025 [US2] Implementacja (green) w `dl-lib/SingleDownload.cs`: mapowanie kodów i wyjątków na `DownloadError` (szablony komunikatów po polsku, bez treści wyjątków w `Message`), sprawdzenie pierwszych 5 bajtów `%PDF-` przed zapisem reszty (R5)
- [ ] T026 [P] [US2] Test (red) w `dltests/DownloadLimitsTests.cs` — limity (FR-311, R4): `Timeout` 200 ms i atrapa czekająca bez końca na nagłówkach → `Timeout` („przekroczono limit czasu 0,2 s” wg szablonu) w czasie < 5 s; to samo, gdy atrapa wstrzymuje treść w połowie; `Content-Length` > `MaxFileSizeBytes` → `TooLarge` bez czytania treści; brak `Content-Length` i treść większa niż limit → `TooLarge` po przekroczeniu; pozostałe adresy `Downloaded`
- [ ] T027 [US2] Implementacja (green) w `dl-lib/SingleDownload.cs`: połączony `CancellationTokenSource` z `CancelAfter(Timeout)` na całe pobranie, rozróżnienie upływu limitu od anulowania użytkownika, licznik bajtów
- [ ] T028 [P] [US2] Test (red) w `dltests/RedirectTests.cs` — przekierowania (FR-312, R3): 301/302/303/307/308 do dozwolonego hosta → `Downloaded`, w wyniku i manifeście adres pierwotny; `Location` względny rozwiązany; przekierowanie na `https://evil.com/a.pdf` → `RedirectNotAllowed` z hostem `evil.com` w komunikacie, atrapa nie dostaje żądania do `evil.com`; `https` → `http` bez `AllowHttp` → odrzucone; łańcuch dłuższy niż `MaxRedirects` → `TooManyRedirects`
- [ ] T029 [US2] Implementacja (green) pętli przekierowań w `dl-lib/SingleDownload.cs` (sprawdzenie każdego adresu przez `HostAllowList` i regułę schematu przed wysłaniem żądania)
- [ ] T030 [P] [US2] Test (red) w `dltests/AtomicWriteTests.cs` (FR-322, SC-062, przypadki brzegowe): w katalogu jest `a.pdf` z poprzedniego uruchomienia; nowe pobranie `a.pdf` zrywa strumień w połowie → `Failed`, `a.pdf` ma starą treść, brak `a.pdf.part`; to samo dla `Timeout` i `NotPdf`; błąd zapisu wywołany deterministycznie: w katalogu istnieje podkatalog o nazwie `a.pdf.part` → `WriteFailed`, stary `a.pdf` nietknięty; wszystkie adresy nieudane → brak nowych PDF, zapisany tylko `manifest.json` z przyczynami (US2 scenariusz 5)
- [ ] T031 [US2] Implementacja (green) poprawek w `dl-lib/SingleDownload.cs` i `dl-lib/DocumentDownloader.cs`, jeśli T030 wykaże braki (w przeciwnym razie commit T030 jako charakteryzacja)
- [ ] T032 [P] [US2] Test (red) w `dltests/CancellationTests.cs` (FR-315): anulowanie tokenu w trakcie pobierania → wyniki nieskończonych adresów `Cancelled`, zapisany manifest, brak `*.part`, następnie `OperationCanceledException`; token anulowany przed startem → `OperationCanceledException` bez zmian w katalogu
- [ ] T033 [US2] Implementacja (green) obsługi anulowania w `dl-lib/DocumentDownloader.cs`

### Aplikacja

- [ ] T034 [P] [US2] Test (red) w `apptests/ErrorReportingTests.cs` (FR-330, FR-331, Independent Test US2): 3 PDF + 404 + timeout (`environment` `FAQGEN__Download__TimeoutSeconds=0.2`, atrapa wstrzymana do anulowania) → linie `[n/5] błąd: <adres> — <przyczyna>`, podsumowanie „Pobrano 3 z 5 plików”, pozycje „n. BŁĄD <adres> — <przyczyna>”, `Detail` na stderr, kod 3; wszystkie nieudane → kod 3; nieobsłużony wyjątek z biblioteki → komunikat na stderr i kod 1
- [ ] T035 [US2] Implementacja (green) w `app/ConsoleReport.cs` i `app/Program.cs` (kody 3 i 1 wg contracts/cli.md)

**Checkpoint**: US1 + US2 — obsługa błędów kompletna.

---

## Phase 5: User Story 3 — Uruchomienie jednym poleceniem, powtarzalność, sprzątanie (Priority: P2)

**Goal**: adresy z `--url` lub konfiguracji bez pytań, błędna lista → 2 przed pobraniem, ponowne
uruchomienie nadpisuje, po pełnym sukcesie katalog zawiera dokładnie bieżący zestaw.

**Independent Test**: 5 adresów w `environment` (`FAQGEN__Download__Urls__0..4`), pusty `stdin` → pobranie bez
pytań; drugie uruchomienie → te same nazwy, identyczny manifest.

### Biblioteka

- [ ] T036 [P] [US3] Test (red) w `dltests/CleanupTests.cs` (FR-325, R9, wyjaśnienie 4): w katalogu `stary.pdf`, `STARY2.PDF`, `notatki.txt`, podkatalog `x/` z `y.pdf`, pozostałość `z.pdf.part`; 5 z 5 pobranych → usunięte `stary.pdf`, `STARY2.PDF`, `z.pdf.part`, zostają `notatki.txt`, `x/y.pdf`, `manifest.json` i 5 bieżących; `RemovedFiles` posortowane `Ordinal`; plik bieżący różniący się wielkością liter nie jest usuwany; przy jednym nieudanym adresie nic nie jest usuwane i `RemovedFiles` puste; błąd usuwania → `DownloadDirectoryException`
- [ ] T037 [US3] Implementacja (green) `dl-lib/DirectoryCleaner.cs` (internal) i wywołanie po zapisie manifestu w `dl-lib/DocumentDownloader.cs`
- [ ] T038 [P] [US3] Test (red) w `dltests/RepeatRunTests.cs` (FR-323, SC-063): dwa uruchomienia z tą samą listą i atrapą → te same nazwy plików, ta sama liczba plików (bez kopii typu „plik (1).pdf”), identyczne treści i manifest; katalog niedający się utworzyć (ścieżka wskazująca na istniejący plik) → `DownloadDirectoryException` przed jakimkolwiek żądaniem; `manifest.json` istnieje jako katalog → `DownloadDirectoryException` (kod 4 w aplikacji)
- [ ] T039 [US3] Implementacja (green) poprawek w `dl-lib/DocumentDownloader.cs`, jeśli T038 wykaże braki (w przeciwnym razie commit T038 jako charakteryzacja)

### Aplikacja

- [ ] T040 [P] [US3] Test (red) w `apptests/AddressSourceTests.cs` (FR-303, wyjaśnienie 3, scenariusze US3 1–3): 5× `--url` → brak pytań na stdout, pobrane adresy z argumentów; `Urls` w konfiguracji (`environment` `FAQGEN__Download__Urls__0..4`) → brak pytań; oba źródła → użyte argumenty; liczba `--url` 1 lub 6 → kod 2; konfiguracja z 3 adresami → kod 2; pusta lista `Urls` → pytania jak bez konfiguracji; dostarczany `app/appsettings.json` ma `AllowedHosts: ["mbank.pl"]` i pustą listę `Urls`; niepoprawny adres lub duplikat na pozycji 4 → komunikat z „4” na stderr, kod 2, atrapa bez żądań, katalog bez zmian; `--output <katalog>` nadpisuje `OutputDirectory`; nieznana opcja → kod 2; `--help` i `--version` → kod 0
- [ ] T041 [US3] Implementacja (green) `app/AppArguments.cs` i wybór źródła adresów w `app/Program.cs`
- [ ] T042 [P] [US3] Test (red) w `apptests/ExitCodeTests.cs` (FR-304, FR-315, FR-331, scenariusz US3 5): pusty `stdin` (EOF) przed 5. adresem → komunikat z contracts/cli.md na stderr, kod 2; anulowanie tokenu w trakcie pytań → kod 130; anulowanie w trakcie pobierania → kod 130, manifest zapisany; niepoprawna konfiguracja (`TimeoutSeconds=0`, pusta `AllowedHosts`) → kod 2; `DownloadDirectoryException` → kod 4; po pełnym sukcesie ze sprzątaniem linia „Usunięto pliki spoza bieżącej listy: …”
- [ ] T043 [US3] Implementacja (green) mapowania kodów 2/4/130 i komunikatu o usuniętych plikach w `app/Program.cs` i `app/ConsoleReport.cs`

**Checkpoint**: wszystkie historyjki działają niezależnie na atrapie.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [ ] T044 [P] README.md: sekcja „mBank.FaqGenerator — pobieranie regulaminów” (cel, uruchomienie w trybie pytań i z `--url`/konfiguracją, `appsettings.json`/`appsettings.Local.json`/`FAQGEN__…`, katalog `downloads/` i `manifest.json`, kody wyjścia, uruchamianie testów) — FR-333, zasada VII
- [ ] T045 [P] CLAUDE.md: projekty `LegalAgent.Downloads` i `mBank.FaqGenerator` w „What this is”, polecenia uruchomienia w „Commands”, `downloads/` jako katalog niecommitowany
- [ ] T046 Uruchom `dotnet build LegalAgent.slnx -c Release` i `dotnet test LegalAgent.slnx --filter "Category!=Performance"` (wszystkie projekty zielone, istniejące testy bez zmian); sprawdź, że `dl-lib/` nie zawiera „mbank” (`grep -ri mbank src/LegalAgent.Downloads` pusty)
- [ ] T047 Ręczna weryfikacja wg quickstart.md 2–6 z prawdziwymi adresami regulaminów z `www.mbank.pl` (bez commitowania pobranych plików); wynik (w tym ewentualne 403 i decyzja o `UserAgent`) zapisz w handoffie
- [ ] T048 Uzupełnij „Stan prac i przekazanie” w `specs/005-regulation-download/plan.md` (zrobione, walidacja, decyzje, otwarte punkty) i oznacz zadania w tym pliku

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (T001–T005)**: T001 → T002; T003, T004, T005 po T001/T002 (różne pliki, [P]).
- **Foundational (T006–T013)**: po Setup; pary test → implementacja; pary T008/T009, T010/T011, T012/T013
  niezależne od siebie (T010 używa `HostAllowList` — T011 po T009).
- **US1 (T014–T023)**: po Foundational. Biblioteka T014 → T015 → T016 → T017 → T018 → T019; aplikacja
  T020 → T021 → T022 → T023 (T021 wymaga T015/T017 dla pełnego przebiegu).
- **US2 (T024–T035)**: po US1 (rozszerza `SingleDownload` i `DocumentDownloader`).
- **US3 (T036–T043)**: po US1; niezależna od US2 poza wspólnym plikiem `DocumentDownloader.cs` (wykonywać
  po US2, by uniknąć konfliktów).
- **Polish (T044–T048)**: po wszystkich historyjkach.

### Within Each User Story

- Test (red commit) przed implementacją (green commit), zawsze.
- Biblioteka przed aplikacją.

### Parallel Opportunities

- Setup: T003, T004, T005.
- Foundational: testy T008, T010, T012 (różne pliki), potem implementacje.
- US1: T014, T018, T020, T022 jako testy w różnych plikach (implementacje sekwencyjnie).
- US2: testy T024, T026, T028, T030, T032, T034.
- US3: testy T036, T038, T040, T042.
- Polish: T044, T045.

---

## Parallel Example: User Story 2

```text
Task: "T024 [US2] Test błędów odpowiedzi w dltests/DownloadErrorsTests.cs"
Task: "T026 [US2] Test limitów w dltests/DownloadLimitsTests.cs"
Task: "T028 [US2] Test przekierowań w dltests/RedirectTests.cs"
Task: "T034 [US2] Test raportowania błędów w apptests/ErrorReportingTests.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1)

1. Phase 1 + Phase 2.
2. Phase 3 (US1) → STOP i walidacja: `RunAsync` z 5 adresami na `stdin` daje 5 plików i kod 0.
3. Ręczna próba na jednym prawdziwym adresie mBanku (wczesne wykrycie 403).

### Incremental Delivery

1. US1 → MVP (pytania, pobieranie, manifest, podsumowanie).
2. US2 → odporność na błędy (wymaganie z opisu właściciela, zasada IV).
3. US3 → jedno polecenie, powtarzalność, sprzątanie (zasady III, V).
4. Polish → README, CLAUDE.md, weryfikacja ręczna, handoff.

---

## Notes

- Wszystkie zadania testowe dotyczą atrapy HTTP — żadnych połączeń z `mbank.pl` w `dotnet test`.
- Pobrane regulaminy (prawdziwy bank) nie trafiają do repozytorium ani do korpusu syntetycznego.
- Nowe poprawki w trakcie implementacji dopisywać jako T0xxa (np. T025a) i do handoffu.
