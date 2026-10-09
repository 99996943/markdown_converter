# Kontrakt: manifest pobrania `manifest.json` (schemaVersion 1)

Plik `<katalog pobrań>/manifest.json`, zapisywany atomowo po każdym uruchomieniu, w którym rozpoczęto pobieranie
(także przy błędach i przerwaniu). Czytają go kolejne etapy (konwersja, FAQ): zestaw regulaminów to pozycje ze
statusem `downloaded`, a `url` jest polem `resource` w OKF.

## Format

JSON, UTF-8 bez BOM, wcięcia 2 spacje, końce linii LF, końcowy znak nowej linii. Klucze w kolejności z
przykładu; pola o wartości `null` pomijane. Bez znaczników czasu uruchomienia (SC-063).

```json
{
  "schemaVersion": 1,
  "items": [
    {
      "index": 1,
      "url": "https://www.mbank.pl/pdf/regulaminy/reg-konta.pdf",
      "status": "downloaded",
      "file": "reg-konta.pdf",
      "size": 831898,
      "sha256": "9f2c…64 hex…",
      "lastModified": "2026-09-30T08:15:00Z"
    },
    {
      "index": 3,
      "url": "https://www.mbank.pl/pdf/stary.pdf",
      "status": "failed",
      "file": "stary.pdf",
      "error": {
        "kind": "http-status",
        "httpStatus": 404,
        "message": "serwer zwrócił 404 Not Found"
      }
    }
  ]
}
```

## Pola

| Pole | Typ | Opis |
|------|-----|------|
| `schemaVersion` | int | 1 |
| `items[]` | array | dokładnie jedna pozycja na adres, rosnąco po `index` |
| `index` | int | numer adresu 1–5 |
| `url` | string | adres podany przez użytkownika (`Uri.AbsoluteUri`), nie po przekierowaniu |
| `status` | string | `downloaded` / `failed` |
| `file` | string | nazwa docelowa w katalogu pobrań (także przy `failed` — plik może nie istnieć albo być z poprzedniego uruchomienia; nie należy go używać) |
| `size` | int | bajty, tylko `downloaded` |
| `sha256` | string | 64 małe hex, tylko `downloaded` |
| `lastModified` | string | ISO 8601 UTC z `Last-Modified`, tylko gdy serwer podał |
| `error.kind` | string | `http-status`, `timeout`, `connection`, `not-pdf`, `too-large`, `redirect-not-allowed`, `too-many-redirects`, `write-failed`, `cancelled` |
| `error.httpStatus` | int | tylko `http-status` |
| `error.message` | string | polski komunikat z szablonu (bez treści wyjątków systemowych) |

Zmiana niezgodna (usunięcie/zmiana znaczenia pola) → `schemaVersion` 2. Dodanie pola opcjonalnego nie zmienia wersji.
