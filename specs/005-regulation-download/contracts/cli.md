# Kontrakt: aplikacja `mBank.FaqGenerator` — etap pobierania

## Wywołanie

```text
mBank.FaqGenerator [--url <adres>]... [--output <katalog>]
mBank.FaqGenerator --help | --version

dotnet run --project src/mBank.FaqGenerator -c Release
dotnet run --project src/mBank.FaqGenerator -c Release -- --url https://www.mbank.pl/a.pdf --url ... (5×)
```

| Opcja | Znaczenie |
|-------|-----------|
| `--url <adres>` | adres regulaminu; podany **dokładnie 5 razy** albo wcale. Pomija pytania i adresy z konfiguracji. |
| `--output <katalog>` | katalog pobrań (nadpisuje `Download:OutputDirectory`) |
| `-h`, `--help` | pomoc, kod 0 |
| `--version` | wersja, kod 0 |

Nieznana opcja, `--url` bez wartości, liczba `--url` inna niż 0 i 5 → kod 2.

## Źródła adresów (FR-301, FR-303)

1. `--url` (5×) — jeśli podano;
2. w przeciwnym razie `Download:Urls` z konfiguracji — jeśli niepusta (musi mieć 5 pozycji);
3. w przeciwnym razie pytania w konsoli.

W trybach 1–2 błędny adres, duplikat lub zła liczba → komunikat z numerem pozycji na stderr, kod 2, bez
pobierania i bez zmian w katalogu.

## Konfiguracja

Warstwy (rosnący priorytet): `appsettings.json` (obok pliku wykonywalnego) → `appsettings.Local.json`
(opcjonalny, ignorowany przez git) → zmienne `FAQGEN__<Sekcja>__<Pole>` → argumenty.

```json
{
  "Download": {
    "Urls": [],
    "AllowedHosts": [ "mbank.pl" ],
    "AllowHttp": false,
    "OutputDirectory": "downloads",
    "TimeoutSeconds": 60,
    "MaxFileSizeMegabytes": 50,
    "MaxRedirects": 5,
    "UserAgent": "mBank.FaqGenerator/1.0"
  }
}
```

Przykład: `FAQGEN__Download__TimeoutSeconds=120`, `FAQGEN__Download__Urls__0=https://…` (… `__4`).
Katalog z plikami `appsettings*.json` to domyślnie `AppContext.BaseDirectory`. `Program.RunAsync`
przyjmuje go parametrem `configDirectory`, więc testy podają katalog tymczasowy z własnym
`appsettings.json`.

`TimeoutSeconds` jest liczbą (może być ułamkowa, zapis z kropką, np. `0.5`). `OutputDirectory` względny liczony od
bieżącego katalogu roboczego. Niepoprawna wartość (np. `TimeoutSeconds=0`,
pusta `AllowedHosts`) → kod 2.

## Pytania (tryb domyślny)

```text
Podaj adres regulaminu 1 z 5: abc
  Niepoprawny adres: to nie jest pełny adres URL (oczekiwano https://…).
Podaj adres regulaminu 1 z 5: https://www.mbank.pl/pdf/regulaminy/reg-konta.pdf
Podaj adres regulaminu 2 z 5: ...
```

Komunikaty błędów adresu na stdout (część dialogu). Koniec wejścia przed zebraniem 5 adresów → stderr:
„Brak adresów: wejście zostało zamknięte. Podaj 5 adresów opcją --url albo w konfiguracji (Download:Urls).”,
kod 2.

## Postęp i podsumowanie (stdout)

```text
[1/5] pobieranie https://www.mbank.pl/pdf/regulaminy/reg-konta.pdf
[3/5] błąd: https://www.mbank.pl/pdf/stary.pdf — serwer zwrócił 404 Not Found
[1/5] pobrano reg-konta.pdf (812,4 KB)
...
Pobrano 4 z 5 plików do downloads:
  1. reg-konta.pdf — 812,4 KB
  2. taryfa.pdf — 1,2 MB
  3. BŁĄD https://www.mbank.pl/pdf/stary.pdf — serwer zwrócił 404 Not Found
  4. ...
Usunięto pliki spoza bieżącej listy: stary-regulamin.pdf      # tylko gdy pobrano 5 z 5 i coś usunięto
Manifest: downloads/manifest.json
```

Linie postępu mogą pojawiać się w różnej kolejności; podsumowanie zawsze w kolejności numerów. Rozmiary w
formacie `pl-PL`. Szczegóły wyjątków (`DownloadError.Detail`) na stderr.

## Kody wyjścia (FR-331)

| Kod | Znaczenie |
|-----|-----------|
| 0 | pobrano i zapisano 5 z 5 plików (sprzątanie wykonane) |
| 1 | błąd nieoczekiwany |
| 2 | błędne argumenty lub konfiguracja, błędna lista adresów, brak wejścia przy pytaniach |
| 3 | co najmniej jedno pobranie nieudane (pozostałe zapisane, manifest zapisany) |
| 4 | błąd katalogu pobrań: nie można go utworzyć, zapisać manifestu albo usunąć starego pliku |
| 130 | przerwane przez użytkownika (Ctrl+C) |
