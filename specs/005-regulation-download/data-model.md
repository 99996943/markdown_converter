# Data Model: Pobieranie regulaminów (spec 005)

Modele biblioteki `LegalAgent.Downloads` (przestrzeń nazw `LegalAgent.Downloads`). Wszystkie są niemutowalne
(`sealed record` / `init`). Publiczne API: `contracts/library-api.md`.

## DownloadOptions

| Pole | Typ | Domyślnie (biblioteka) | Walidacja |
|------|-----|------------------------|-----------|
| `OutputDirectory` | `string` | `"downloads"` | niepusta |
| `AllowedHosts` | `IReadOnlyList<string>` | pusta | co najmniej 1 wpis; wpis to nazwa hosta bez schematu, portu i ścieżki |
| `AllowHttp` | `bool` | `false` | — |
| `Timeout` | `TimeSpan` | 60 s | > 0 |
| `MaxFileSizeBytes` | `long` | 50 × 1024 × 1024 | > 0 |
| `MaxRedirects` | `int` | 5 | 0–20 |
| `UserAgent` | `string?` | `null` | — |

Biblioteka nie zna mBanku: lista hostów pochodzi z konfiguracji aplikacji (`mbank.pl` w `appsettings.json`).
Niepoprawne opcje → `ArgumentException` przy tworzeniu `DocumentDownloader` (aplikacja: kod 2).

## AddressCheck (wynik sprawdzenia adresu)

| Pole | Typ | Opis |
|------|-----|------|
| `IsValid` | `bool` | |
| `Address` | `Uri?` | adres po `Trim`, gdy poprawny |
| `Error` | `AddressError?` | `Empty`, `NotAbsolute`, `SchemeNotAllowed`, `HostNotAllowed`, `HasUserInfo`, `Duplicate` |
| `Message` | `string?` | komunikat po polsku, np. „host example.com nie jest na liście dozwolonych (mbank.pl)” |

Reguły (FR-302): `Trim` → niepusty → bezwzględny URI → schemat `https` (lub `http` przy `AllowHttp`) → brak
`user@` → host dozwolony (R3) → nie równy (`Uri.Equals`, bez fragmentu) wcześniejszemu adresowi.

## DownloadPlan

Lista `PlannedDownload(int Index, Uri Address, string FileName)` z `FileNamePlanner.Plan` (R7). `Index` 1-based
w kolejności podania. Nazwy unikalne bez rozróżniania wielkości liter.

## DownloadResult (jeden adres)

| Pole | Typ | Gdy |
|------|-----|-----|
| `Index` | `int` | zawsze |
| `Address` | `Uri` | zawsze — adres podany przez użytkownika (nie po przekierowaniu) |
| `FileName` | `string` | zawsze — nazwa docelowa z planu |
| `Status` | `DownloadStatus` | `Downloaded` / `Failed` |
| `SizeBytes` | `long?` | `Downloaded` |
| `Sha256` | `string?` | `Downloaded` — 64 małe znaki hex |
| `LastModified` | `DateTimeOffset?` | `Downloaded`, gdy serwer podał `Last-Modified` (UTC) |
| `Error` | `DownloadError?` | `Failed` |

### DownloadError

| Pole | Typ | Opis |
|------|-----|------|
| `Kind` | `DownloadErrorKind` | `HttpStatus`, `Timeout`, `Connection`, `NotPdf`, `TooLarge`, `RedirectNotAllowed`, `TooManyRedirects`, `WriteFailed`, `Cancelled` |
| `Message` | `string` | polski komunikat z własnego szablonu (deterministyczny), np. „serwer zwrócił 404 Not Found” |
| `HttpStatusCode` | `int?` | dla `HttpStatus` |
| `Detail` | `string?` | treść wyjątku do logu na stderr; **nie** trafia do manifestu |

Przejścia stanu jednego pobrania:

```text
Planned → Requesting ⇄ Redirecting (≤ MaxRedirects, host sprawdzany) → Receiving (.part) → Verified (%PDF-, rozmiar) → Moved → Downloaded
każdy stan → Failed (kind) ; .part usuwany
```

## DownloadRun (wynik całego uruchomienia)

| Pole | Typ | Opis |
|------|-----|------|
| `Results` | `IReadOnlyList<DownloadResult>` | w kolejności `Index` |
| `AllSucceeded` | `bool` | wszystkie `Downloaded` |
| `RemovedFiles` | `IReadOnlyList<string>` | pliki usunięte przez sprzątanie (R9), puste gdy nie wszystkie pobrane |
| `ManifestPath` | `string` | ścieżka zapisanego manifestu |

## DownloadEvent (postęp)

`DownloadEvent(int Index, Uri Address, DownloadEventKind Kind, DownloadResult? Result)`, `Kind` ∈ `Started`,
`Finished`. Zgłaszane przez `IProgress<DownloadEvent>`; kolejność między adresami niedeterministyczna.

## Konfiguracja aplikacji (`mBank.FaqGenerator`)

Sekcja `Download` w `appsettings.json` → `DownloadOptions` + `Urls: string[]`. Liczba adresów `RequiredCount`
= 5 jest stałą aplikacji. Szczegóły: `contracts/cli.md`.
