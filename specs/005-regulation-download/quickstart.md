# Quickstart: walidacja pobierania regulaminów (spec 005)

## Wymagania

- SDK z `global.json`, runtime .NET 9; Linux lub Windows.
- Testy automatyczne nie potrzebują sieci. Scenariusze ręczne 4–6 wymagają dostępu do `www.mbank.pl`.

## 1. Build i testy

```bash
dotnet build LegalAgent.slnx -c Release
dotnet test LegalAgent.slnx --filter "Category!=Performance"
dotnet test tests/LegalAgent.Downloads.Tests
dotnet test tests/mBank.FaqGenerator.Tests
```

Oczekiwane: wszystko zielone; scenariusze akceptacyjne US1–US3 pokryte testami na atrapie HTTP (SC-064).

## 2. Pomoc i błędne argumenty

```bash
dotnet run --project src/mBank.FaqGenerator -c Release -- --help          # kod 0, opis z contracts/cli.md
dotnet run --project src/mBank.FaqGenerator -c Release -- --url https://www.mbank.pl/a.pdf   # kod 2 (1 zamiast 5)
dotnet run --project src/mBank.FaqGenerator -c Release -- --url https://example.com/a.pdf --url ... (5×)  # kod 2, host niedozwolony
```

## 3. Brak wejścia (FR-304)

```bash
dotnet run --project src/mBank.FaqGenerator -c Release < /dev/null    # kod 2, komunikat jak podać adresy
```

## 4. Tryb interaktywny (US1) — ręcznie, z siecią

Uruchom bez argumentów w pustym katalogu roboczym i wpisz 5 adresów regulaminów PDF ze strony mBanku (np. z
sekcji „Regulaminy” na `www.mbank.pl`). Sprawdź po drodze: wpisanie `abc` i ponowienie tego samego adresu dają
komunikat i to samo pytanie.

Oczekiwane: `downloads/` z 5 plikami PDF (`head -c 5 downloads/*.pdf` → `%PDF-`), `downloads/manifest.json`
zgodny z `contracts/download-manifest.md`, kod 0.

## 5. Błędny link (US2)

Uruchom z 4 poprawnymi adresami i jednym nieistniejącym (`https://www.mbank.pl/pdf/nie-ma-takiego.pdf`):

Oczekiwane: 4 pliki, komunikat z 404 (lub innym kodem serwera), pozycja `failed` w manifeście, kod 3.

## 6. Powtórzenie i sprzątanie (US3, SC-063)

```bash
cp downloads/manifest.json /tmp/m1.json
touch downloads/stary.pdf
FAQGEN__Download__Urls__0=… FAQGEN__Download__Urls__4=… dotnet run --project src/mBank.FaqGenerator -c Release
diff /tmp/m1.json downloads/manifest.json      # brak różnic (pliki na serwerze niezmienione)
ls downloads                                   # 5 PDF + manifest.json; stary.pdf usunięty i wymieniony w podsumowaniu
```

Jeśli serwer odpowiada 403 dla każdego adresu, sprawdź `Download:UserAgent` (research R13).
