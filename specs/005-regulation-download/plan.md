# Implementation Plan: Pobieranie regulaminów — aplikacja mBank.FaqGenerator

**Branch**: `005-regulation-download` | **Date**: 2026-10-09 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/005-regulation-download/spec.md`

## Summary

Nowa biblioteka `LegalAgent.Downloads` pobiera listę adresów PDF: sprawdza adresy (https, host z listy
dozwolonych, bez duplikatów), planuje deterministyczne nazwy plików, pobiera równolegle przez `HttpClient` z
limitem czasu i rozmiaru na plik, wykonuje przekierowania ręcznie tylko do dozwolonych hostów, rozpoznaje PDF po
sygnaturze `%PDF-`, zapisuje atomowo (`.part` + przeniesienie) z SHA-256, zapisuje `manifest.json` i — gdy
wszystko się udało — usuwa pliki PDF spoza bieżącej listy. Błędy pojedynczych adresów są wynikami, nie
wyjątkami. Nowa aplikacja konsolowa `mBank.FaqGenerator` to cienka warstwa: adresy z `--url` (5×), z
konfiguracji (`appsettings.json`, `FAQGEN__…`) albo z pytań w konsoli, postęp, podsumowanie i kody wyjścia
(0/2/3/4/130/1). Testy offline na atrapie `HttpMessageHandler`.

## Technical Context

**Language/Version**: C# 13 / .NET 9 (SDK z `global.json`)

**Primary Dependencies**: BCL (`System.Net.Http`, `System.Text.Json`, `System.Security.Cryptography`);
w aplikacji `Microsoft.Extensions.Configuration.EnvironmentVariables` i `.Binder` 9.0.20 (już przypięte) oraz
**nowy** `Microsoft.Extensions.Configuration.Json` 9.0.20 (uzasadnienie: research R10)

**Storage**: pliki w katalogu pobrań (`./downloads`, git-ignored): PDF-y i `manifest.json`

**Testing**: xUnit v3 na Microsoft Testing Platform; nowe projekty `tests/LegalAgent.Downloads.Tests` i
`tests/mBank.FaqGenerator.Tests`; atrapa `FakeHttpHandler`, katalogi tymczasowe

**Target Platform**: Linux i Windows (CI na Linuksie)

**Project Type**: biblioteka klas + aplikacja konsolowa

**Performance Goals**: pobieranie równoległe — czas ≈ najdłuższe pojedyncze pobranie (SC-060), dowód testem
współbieżności, nie pomiarem czasu (R12)

**Constraints**: 60 s i 50 MB na plik (konfigurowalne); brak niekompletnych plików po błędzie (SC-062);
deterministyczne nazwy i manifest (SC-063); zero połączeń sieciowych w testach

**Scale/Scope**: 5 plików po kilkaset KB–kilka MB na uruchomienie

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Zasada / ograniczenie | Ocena | Uwagi |
|-----------------------|-------|-------|
| I. TDD | ✅ | czerwony commit z testem, potem zielony; testy offline (atrapa HTTP, katalogi tymczasowe), deterministyczne (test współbieżności na barierze, nie na czasie) |
| II. Wierność źródłu | ✅ | pliki zapisywane bajt w bajt; manifest wiąże plik z adresem źródła (`resource` w OKF dla etapu FAQ) |
| III. Powtarzalność | ✅ | jedno polecenie z `--url`/konfiguracją; nazwy i manifest deterministyczne; ponowne uruchomienie nadpisuje, nie dubluje |
| IV. Błędy zewnętrzne | ✅ | jawny limit czasu na plik, `ConnectTimeout`, obsługa każdego rodzaju błędu, czytelne komunikaty, kody ≠ 0 przy niepełnym wyniku, zapis atomowy |
| V. Bezpieczeństwo i konfiguracja | ✅ | brak sekretów; adresy i hosty w konfiguracji (`appsettings.json`, `FAQGEN__…`), nie w kodzie; `appsettings.Local.json` już w `.gitignore`; lista dozwolonych hostów także dla przekierowań |
| VI. Prostota, zależności | ✅ | jedna nowa zależność (`Configuration.Json` 9.0.20, przypięta centralnie, uzasadniona R10); bez DI i `IHttpClientFactory` |
| VII. Dokumentacja | ✅ | README: cel, uruchomienie w obu trybach, konfiguracja, kody wyjścia, testy; `CLAUDE.md` |
| Biblioteka + aplikacja | ✅ | logika w `LegalAgent.Downloads`; `mBank.FaqGenerator` = argumenty, konfiguracja, konsola, kody |
| Biblioteka ogólna, bez zaszytych źródeł | ✅ | `mbank.pl` i liczba 5 tylko w aplikacji; biblioteka przyjmuje dowolną listę |
| Biblioteka bez konsoli i stanu globalnego | ✅ | `HttpClient` i opcje wstrzykiwane; postęp przez `IProgress` |
| Solucja `.slnx`, `dotnet test` na Linuksie | ✅ | 4 nowe projekty w `LegalAgent.slnx`; brak API Windows (ścieżki przez `Path`, nazwy plików bezpieczne dla obu systemów) |
| Publiczne API udokumentowane | ✅ | `contracts/library-api.md`, komentarze XML, wersja 1.0.0 |
| Format OKF | n/d | dotyczy etapu FAQ (osobna specyfikacja) |

Ponowna ocena po fazie 1: bez zmian — data-model i kontrakty nie wprowadzają odstępstw.

## Project Structure

### Documentation (this feature)

```text
specs/005-regulation-download/
├── spec.md
├── plan.md                    # ten plik
├── research.md                # R1–R13
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── cli.md                 # wywołanie, konfiguracja, pytania, wyjście, kody
│   ├── download-manifest.md   # manifest.json (schemaVersion 1)
│   └── library-api.md         # publiczne API LegalAgent.Downloads
├── checklists/requirements.md
└── tasks.md                   # /speckit-tasks
```

### Source Code (repository root)

```text
src/
├── LegalAgent.Downloads/                     # NOWA biblioteka (bez zależności pakietowych)
│   ├── LegalAgent.Downloads.csproj
│   ├── DownloadOptions.cs                    # + walidacja
│   ├── AddressValidator.cs, AddressCheck.cs  # FR-302, R3
│   ├── HostAllowList.cs                      # R3 (IdnHost, subdomeny)
│   ├── FileNamePlanner.cs, PlannedDownload.cs  # R7
│   ├── DocumentDownloader.cs                 # orkiestracja: katalog, WhenAll, manifest, sprzątanie
│   ├── SingleDownload.cs                     # przekierowania, limit czasu/rozmiaru, %PDF-, .part, SHA-256 (R3–R6)
│   ├── DirectoryCleaner.cs                   # R9
│   ├── Model/DownloadResult.cs, DownloadError.cs, DownloadRun.cs, DownloadEvent.cs, enums
│   ├── DownloadManifestJson.cs               # R8
│   └── DownloadDirectoryException.cs
└── mBank.FaqGenerator/                       # NOWA aplikacja konsolowa
    ├── mBank.FaqGenerator.csproj             # ref: LegalAgent.Downloads; Configuration.Json/EnvVars/Binder
    ├── appsettings.json                      # AllowedHosts: mbank.pl, Urls: [], limity (kopiowany do wyjścia)
    ├── Program.cs                            # Main + RunAsync(args, stdin, stdout, stderr, env, configDirectory, handler?, ct)
    ├── AppArguments.cs                       # --url ×5, --output, --help, --version
    ├── AppSettings.cs                        # wiązanie sekcji Download → DownloadOptions
    ├── AddressPrompt.cs                      # pętla pytań (R11)
    └── ConsoleReport.cs                      # postęp, podsumowanie, formatowanie rozmiarów pl-PL

tests/
├── LegalAgent.Downloads.Tests/               # NOWY
│   ├── Fakes/FakeHttpHandler.cs              # skryptowane odpowiedzi, opóźnienia, wyjątki, bariera
│   ├── AddressValidatorTests.cs, HostAllowListTests.cs, FileNamePlannerTests.cs
│   ├── DocumentDownloaderTests.cs            # sukces, 404/500, timeout, DNS, HTML, rozmiar, przekierowania, równoległość
│   ├── AtomicWriteTests.cs                   # przerwanie/błąd nie zostawia .part, stara wersja nietknięta
│   ├── ManifestTests.cs                      # kontrakt, determinizm
│   └── CleanupTests.cs                       # FR-325
└── mBank.FaqGenerator.Tests/                 # NOWY
    └── ProgramTests.cs                       # pytania, ponowienie numeru, EOF, pierwszeństwo źródeł, kody wyjścia, podsumowanie

LegalAgent.slnx                               # + 4 projekty
Directory.Packages.props                      # + Microsoft.Extensions.Configuration.Json 9.0.20
.gitignore                                    # + downloads/
README.md, CLAUDE.md                          # sekcja mBank.FaqGenerator
```

**Structure Decision**: osobna biblioteka i osobna aplikacja (konstytucja; opis wymaga aplikacji
`mBank.FaqGenerator`, w której pojawią się kolejne etapy — konwersja i FAQ). Biblioteka nie zależy od parsera;
etap konwersji (kolejna specyfikacja) połączy oba w aplikacji.

## Kolejność prac (dla /speckit-tasks)

1. Szkielet: projekty, `.slnx`, `Directory.Packages.props`, `.gitignore`, `appsettings.json`, atrapa HTTP.
2. Biblioteka (test-first): `DownloadOptions` → `HostAllowList` → `AddressValidator` → `FileNamePlanner` →
   pojedyncze pobranie (sukces, kody błędów, połączenie, `%PDF-`, rozmiar, limit czasu, przekierowania, zapis
   atomowy, SHA-256, `Last-Modified`) → równoległość i niezależność błędów → anulowanie → manifest → sprzątanie.
3. US1 w aplikacji: pytania, ponowienie numeru, duplikaty, postęp, podsumowanie, kod 0.
4. US2 w aplikacji: komunikaty błędów, kod 3, szczegóły na stderr, wszystkie nieudane.
5. US3 w aplikacji: `--url`, konfiguracja i jej warstwy, pierwszeństwo, błędna lista → 2, EOF → 2, Ctrl+C → 130,
   błąd katalogu → 4.
6. README, `CLAUDE.md`, ręczna weryfikacja z prawdziwymi adresami mBanku (quickstart 4–6), handoff poniżej.

## Ryzyka

- **Ochrona przed botami na `mbank.pl`** może zwracać 403 dla klienta innego niż przeglądarka — `UserAgent`
  konfigurowalny; jeśli to nie wystarczy, decyzja właściciela (bez obchodzenia zabezpieczeń).
- **Pliki regulaminów na innych hostach** (CDN banku) — odrzucane do czasu dopisania hosta w konfiguracji;
  komunikat podaje host.
- **Ctrl+C w konsoli Windows** — obsługa przez `Console.CancelKeyPress` z `e.Cancel = true` i anulowaniem tokenu;
  testy wywołują anulowanie tokenem.

## Complexity Tracking

Brak naruszeń konstytucji.

## Stan prac i przekazanie (T048, 2026-10-09)

**Zrobione**: zadania T001–T048 (`tasks.md`), gałąź `005-regulation-download` (niewypchnięta; PR do zrobienia).
Każda zmiana zachowania jako para commitów red/green; T030, T032, T034 częściowo, T038 w całości przeszły od razu
(charakteryzacja, opisane w commitach).

- Biblioteka `LegalAgent.Downloads`: `AddressValidator`, `HostAllowList` (IDN, subdomeny), `FileNamePlanner`,
  `DocumentDownloader` (równolegle, `IProgress`), `SingleDownload` (ręczne przekierowania, jeden limit czasu na
  pobranie, limit rozmiaru z nagłówka i licznika, `%PDF-`, `.part` + `File.Move`, SHA-256, `Last-Modified`),
  `DownloadManifestJson`, `DirectoryCleaner`. Brak zależności pakietowych; brak „mbank” w kodzie biblioteki.
- Aplikacja `mBank.FaqGenerator`: `--url` ×5, `--output`, `--help`, `--version`; konfiguracja `appsettings.json` →
  `appsettings.Local.json` → `FAQGEN__…`; pytania w konsoli; postęp, podsumowanie (rozmiary `pl-PL`), kody
  0/1/2/3/4/130.
- Testy: `tests/LegalAgent.Downloads.Tests` (122) i `tests/mBank.FaqGenerator.Tests` (42), bez sieci.
- README (sekcja „Pobieranie regulaminów”), `CLAUDE.md`, `.gitignore` (`downloads/`).

**Walidacja (T046, T047)**: `dotnet build -c Release` bez ostrzeżeń; `dotnet test LegalAgent.slnx --filter
"Category!=Performance"` — 1639 zaliczonych, 8 pominiętych (te same co przed zmianą). Quickstart 2–3 na zbudowanej
aplikacji: `--help` → 0, 1× `--url` → 2, obcy host → 2 z „Adres 1: …”, zamknięte wejście → 2 z podpowiedzią.
Sieć: 5 nieistniejących adresów `www.mbank.pl/pdf/…` → serwer odpowiada 404 (nie 403 — `User-Agent` aplikacji nie jest
blokowany), podsumowanie 0 z 5, manifest z przyczynami, kod 3. **Nie sprawdzono pobrania prawdziwych regulaminów**
(quickstart 4–6): wymaga 5 adresów wskazanych przez właściciela.

**Odstępstwa od planu i decyzje w trakcie**:

- Pakiet `Microsoft.Extensions.Configuration.EnvironmentVariables` niepotrzebny: zmienne `FAQGEN__` czytane są ze
  słownika przekazanego do `RunAsync` (`AddInMemoryCollection`), więc testy nie dotykają środowiska procesu. Jedyna
  nowa zależność: `Configuration.Json` 9.0.20.
- Kilka drobnych typów w jednym pliku zamiast osobnych (`AddressError` w `AddressCheck.cs`, `PlannedDownload` w
  `FileNamePlanner.cs`, wyliczenia modelu obok rekordów); publiczna stała `DocumentDownloader.ManifestFileName`.
- Zerwane połączenie w trakcie treści → `connection` („połączenie … zostało przerwane”); błędy systemu plików →
  `write-failed`. Komunikat statusu HTTP używa kanonicznej frazy .NET (serwery HTTP/2 nie wysyłają własnej).
- Kontrakt manifestu: przykład obiektu `error` pokazany z wcięciami jak reszta pliku (bez zmiany formatu).
- T047: `Main` ustawia UTF-8 na konsoli (Windows pokazywał polskie znaki jako `�`), jak `legalagent-pdf`.

**Otwarte / do wiadomości**:

- Ręczne pobranie 5 prawdziwych regulaminów mBanku (quickstart 4–6) — po podaniu adresów przez właściciela.
- Test „nie da się usunąć starego pliku” działa tylko na Windows (na Linuksie otwarty plik da się usunąć) — na CI jest
  pomijany (`Assert.SkipUnless`).
- Następne etapy aplikacji: konwersja PDF → Markdown (parser z spec 001/002, wejście = pozycje `downloaded` z
  `manifest.json`) i FAQ w OKF (`resource` = `url` z manifestu) — osobne specyfikacje.
