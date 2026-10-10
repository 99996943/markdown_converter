# mBank.FaqGenerator

Aplikacja konsolowa zadania: pobiera 5 regulaminów mBanku (PDF), konwertuje je do Markdown i generuje modelem Azure
OpenAI plik `faq/FAQ_mBank.md` — 10 pytań i odpowiedzi dla klientów, każda ze źródłem (dokument i rozdział).

Aplikacja tylko **składa etapy** i rozmawia z użytkownikiem (argumenty, konfiguracja, klucz, konsola, kody
wyjścia). Właściwa praca dzieje się w bibliotekach:

| Etap | Biblioteka | Opis działania |
|---|---|---|
| pobieranie | `LegalAgent.Downloads` | spec 005 |
| konwersja PDF → Markdown | `LegalAgent.PdfParser` (przez `LegalAgent.Faq.Conversion`) | [README parsera](../LegalAgent.PdfParser/README.md) |
| generowanie i weryfikacja FAQ | `LegalAgent.Faq` | [README FAQ](../LegalAgent.Faq/README.md) |

Nazwa „mBank”, liczba 5 dokumentów i domena `mbank.pl` są tylko tutaj — biblioteki ich nie znają. Kontrakt
wiersza poleceń: `specs/006-faq-generation/contracts/cli.md` (oraz spec 005 dla pobierania).

## Uruchomienie

```powershell
mBank.FaqGenerator.exe                                   # adresy z konfiguracji albo pytania w konsoli
mBank.FaqGenerator.exe --url <a> --url <b> --url <c> --url <d> --url <e> [--output <katalog>] [--faq-output <katalog>]
az cognitiveservices account keys list -g <grupa> -n <zasób> --query key1 -o tsv | mBank.FaqGenerator.exe   # klucz potokiem
mBank.FaqGenerator.exe --help | --version
```

Z repozytorium: `dotnet run --project src/mBank.FaqGenerator -c Release -- …`. Publikacja jako jeden plik (Windows):

```bash
dotnet publish src/mBank.FaqGenerator -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none \
  -p:SatelliteResourceLanguages=pl -o publish/mBank.FaqGenerator-win-x64
```

Obok `.exe` muszą leżeć `appsettings.json` (i opcjonalnie `appsettings.Local.json`) — aplikacja czyta konfigurację
z katalogu, w którym leży, a pliki wynikowe zapisuje względem bieżącego katalogu.

## Przebieg

```
adresy ──► pobieranie 5/5 ──► konwersja 5/5 ──► kontrola rozmiaru ──► klucz API ──► FAQ ──► faq/FAQ_mBank.md
 (--url /    (downloads/,       (<nazwa>.md       (bez zapytań        (gwiazdki      (2 kroki     (zapis atomowy)
 config /     manifest.json)     obok PDF-ów)      do modelu)          lub potok)     modelu)
 konsola)
```

1. **Adresy** — kolejno: opcje `--url` (dokładnie 5 razy), `Download:Urls` z konfiguracji (dokładnie 5 pozycji),
   a gdy obie listy są puste — pytania w konsoli. Każdy adres musi mieć dozwolony host (`Download:AllowedHosts`,
   domyślnie `mbank.pl` z poddomenami) i HTTPS.
2. **Pobieranie** — równoległe, z limitami czasu i rozmiaru, ręczną obsługą przekierowań (każde sprawdzane z listą
   hostów), zapisem przez plik `.part` i atomową zamianą; na końcu `manifest.json` (adres, plik, rozmiar, SHA-256).
   Każdy błąd jednego pliku kończy aplikację kodem 3 — FAQ powstaje tylko z kompletu.
3. **Konwersja** — każdy PDF parserem z domyślnymi opcjami; `<nazwa>.md` obok PDF-u, ostrzeżenia parsera w
   konsoli. Po konwersji 5/5 usuwane są nieaktualne `*.md` spoza bieżącej listy.
4. **Kontrola rozmiaru** — szacunek tokenów każdego dokumentu z limitem `Faq:MaxDocumentTokens`, **zanim** aplikacja
   zapyta o klucz (zbyt długi dokument: kod 6, bez zapytań do modelu).
5. **Klucz API** — patrz niżej.
6. **FAQ** — `LegalAgent.Faq`: kandydaci z każdego dokumentu, ugruntowanie w tekście, wybór 10 pozycji (opis w
   [README FAQ](../LegalAgent.Faq/README.md)). Konektor Azure OpenAI z Semantic Kernel (`ChatServiceFactory`): bez
   ponowień, z limitem czasu zapytania, odpowiedź jako JSON w trybie strict, `Temperature` i `Seed` z konfiguracji.
7. **Zapis** — `FAQ_mBank.md` w katalogu `Faq:OutputDirectory` (domyślnie `faq`), atomowo; gdy generowanie się
   nie uda, poprzedni plik zostaje bez zmian.

Konsola pokazuje postęp każdego etapu, a w etapie FAQ także rozmiar i zużycie tokenów każdego zapytania oraz
ostrzeżenia weryfikacji, np.:

```
[D2] pominięto kandydata D2-K3: liczby „13”, „18” nie występują w jednostce „5. Rachunki dla osób małoletnich”
[wybór] odpowiedź odrzucona (liczba poprawnych pozycji 9, potrzeba co najmniej 10) — prośba o poprawkę…
```

## Weryfikacja faktów

Model pisze pytania i odpowiedzi, ale aplikacja **nie przyjmuje ich na wiarę**: każda propozycja jest sprawdzana w
kodzie z tekstem regulaminu, bez dodatkowych zapytań do modelu. Szczegóły i reguły:
[README FAQ, „Weryfikacja propozycji modelu”](../LegalAgent.Faq/README.md#weryfikacja-propozycji-modelu).

| Co jest sprawdzane | Jak | Skutek błędu |
|---|---|---|
| **źródło** (rozdział) | model wybiera jednostkę z listy nagłówków dokumentu; kod sprawdza, że taka jednostka istnieje | odrzucenie odpowiedzi |
| **cytat** | kandydat podaje dosłowny fragment dokumentu; ≥ 80% jego trójek kolejnych słów musi wystąpić w tekście wskazanego rozdziału | kandydat odpada |
| **liczby** | każda kwota, termin, godzina, data z odpowiedzi musi wystąpić w tekście rozdziału | kandydat odpada |
| **liczby w FAQ** | każda liczba końcowej odpowiedzi musi wystąpić w odpowiedziach lub cytatach kandydatów, na których się opiera | pozycja odpada |
| **źródła w FAQ** | model ich nie podaje — kod bierze je z kandydatów, na których oparta jest pozycja | — (nie da się wskazać nieistniejącego paragrafu) |
| **kompletność i równowaga** | 10 pozycji, każdy dokument 1–3; wybiera kod z uszeregowanej przez model puli | jedna prośba o poprawkę, potem kod 7 |

Każde odrzucenie jest widoczne w konsoli z powodem (np. cytatem, którego nie znaleziono, albo liczbą spoza
rozdziału), więc wynik można zweryfikować ręcznie.

**Czego kod nie sprawdza:** znaczenia odpowiedzi. Model może poprawnie zacytować rozdział, a w odpowiedzi
przeinaczyć warunek bez liczb (np. dopisać „rażące niedbalstwo” tam, gdzie regulamin mówi tylko „umyślnie”). Na to
pomaga przegląd człowieka albo — planowane — sprawdzenie odpowiedzi drugim zapytaniem do modelu.

## Klucz API

Klucz **nigdy nie jest konfiguracją**:

- nie ma opcji ani zmiennej środowiskowej na klucz; `AzureOpenAI:ApiKey` w konfiguracji lub w zmiennych
  `FAQGEN__…` kończy aplikację kodem 2;
- w konsoli klucz wpisuje się po pobraniu i konwersji; zamiast znaków widać gwiazdki (`KeyPrompt`), Backspace
  działa, Ctrl+C przerywa (kod 130);
- przy przekierowanym wejściu klucz jest kolejnym wierszem (np. z `az … keys list … -o tsv |`);
- od chwili podania klucza każdy komunikat błędu przechodzi przez `SecretRedactor`, który usuwa klucz z tekstu
  (odpowiedzi usługi bywają cytowane w błędach).

Zasób Azure OpenAI tworzy skrypt `scripts/azure/create-openai.sh` (idempotentny, nigdy nie czyta klucza) albo
portal; adres zasobu trafia do `AzureOpenAI:Endpoint`.

## Konfiguracja

Warstwy (późniejsza nadpisuje wcześniejszą): `appsettings.json` → `appsettings.Local.json` (nieśledzony przez git,
np. endpoint i adresy na pokaz) → zmienne `FAQGEN__<Sekcja>__<Pole>` → opcje wiersza poleceń.

| Klucz | Domyślnie | Znaczenie |
|---|---|---|
| `Download:Urls` | `[]` | 5 adresów; pusta lista = pytania w konsoli |
| `Download:AllowedHosts` | `["mbank.pl"]` | dozwolone hosty (z poddomenami) |
| `Download:OutputDirectory` | `downloads` | katalog pobrań (`--output`) |
| `Download:TimeoutSeconds`, `MaxFileSizeMegabytes`, `MaxRedirects` | 60, 50, 5 | limity pobierania |
| `AzureOpenAI:Endpoint` | — | adres zasobu (wymagany, HTTPS) |
| `AzureOpenAI:Deployment`, `Model` | `gpt-4o-mini` | nazwa wdrożenia i modelu (do nagłówka FAQ) |
| `AzureOpenAI:TimeoutSeconds` | 300 | limit czasu jednego zapytania |
| `AzureOpenAI:Temperature`, `Seed` | 0, 42 | pusta wartość = nie wysyłaj |
| `AzureOpenAI:MaxOutputTokens` | 4096 | limit odpowiedzi |
| `Faq:OutputDirectory` | `faq` | katalog pliku FAQ (`--faq-output`) |
| `Faq:CandidatesPerDocument` | 10 | najwyżej tylu kandydatów z dokumentu (1–30) |
| `Faq:MaxDocumentTokens` | 100 000 | limit szacowanych tokenów dokumentu |

## Kody wyjścia

| Kod | Znaczenie |
|---|---|
| 0 | pobrano 5/5, przekonwertowano 5/5, zapisano FAQ |
| 1 | błąd nieoczekiwany |
| 2 | błędne argumenty, konfiguracja lub adresy; brak adresów albo klucza na wejściu |
| 3 | nie wszystkie pliki pobrane |
| 4 | błąd zapisu (katalog pobrań, Markdown, FAQ) |
| 5 | błąd konwersji |
| 6 | błąd usługi modelu (klucz, wdrożenie, limit 429, czas, sieć) albo dokument za długi |
| 7 | odpowiedź modelu odrzucona przez weryfikację |
| 130 | przerwano (Ctrl+C) |

## Testy

`tests/mBank.FaqGenerator.Tests` uruchamiają `Program.RunAsync` przez `AppHarness` — z własnym `appsettings.json`,
stałym zegarem, atrapą wejścia klucza i bez sieci:

- pobieranie: `FakeHttpHandler` (z testów `LegalAgent.Downloads`) odgrywa zaplanowane odpowiedzi;
- model: `FakeChatCompletionService` (z testów `LegalAgent.Faq`) albo prawdziwy konektor nad `ModelHttpHandler`;
- skrypt Azure: `AzureScriptTests` z atrapą `az` na początku `PATH` (pomijane bez `bash`);
- bezpieczeństwo klucza: klucz nie trafia do wyjścia, plików ani komunikatów (`SecretSafetyTests`).

```bash
dotnet test tests/mBank.FaqGenerator.Tests
```
