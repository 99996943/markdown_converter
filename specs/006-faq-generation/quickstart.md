# Quickstart: walidacja spec 006

Scenariusze, które dowodzą, że funkcjonalność działa od początku do końca. Szczegóły formatów są w `contracts/`.

## 0. Wymagania

- .NET SDK z `global.json`, runtime .NET 9.
- Dla scenariuszy 5–8: subskrypcja Azure, Azure CLI (`az login`), Bash (Linux/macOS albo Git Bash na Windows).

## 1. Testy automatyczne (offline)

```bash
dotnet build LegalAgent.slnx -c Release
dotnet test LegalAgent.slnx --filter "Category!=Performance"
dotnet test tests/LegalAgent.Faq.Tests
dotnet test tests/mBank.FaqGenerator.Tests
```

Oczekiwane: wszystkie zaliczone, bez połączeń sieciowych (atrapy HTTP i `IChatCompletionService`).
`AzureScriptTests` są pomijane tylko wtedy, gdy nie ma `bash`.

## 2. Konfiguracja niepełna → kod 2 przed pobieraniem

```bash
dotnet run --project src/mBank.FaqGenerator -c Release -- --url https://www.mbank.pl/a.pdf --url … (5×)
```

Bez `AzureOpenAI:Endpoint` oczekiwany jest komunikat o brakującym polu, kod 2 i brak zapytań HTTP (katalog pobrań
bez zmian).

## 3. Klucz w konfiguracji jest odrzucany

```bash
FAQGEN__AzureOpenAI__ApiKey=abc dotnet run --project src/mBank.FaqGenerator -c Release -- --help
```

`--help` działa (kod 0). Zwykłe uruchomienie z tą zmienną → kod 2, a komunikat nie zawiera wartości.

## 4. Gwiazdki przy wpisywaniu (ręcznie, konsola)

Z ustawionym endpointem i 5 adresami (scenariusz 7) wpisz lub wklej klucz. Za każdy znak pojawia się jedna `*`.
Backspace cofa jedną gwiazdkę. Ctrl+C → „Przerwano.”, kod 130, brak `FAQ_mBank.md`.

## 5. Utworzenie zasobu

```bash
scripts/azure/create-openai.sh
```

Oczekiwane: kod 0 i fragment `appsettings.Local.json` z endpointem i wdrożeniem. Drugie uruchomienie: kod 0,
komunikaty „już istnieje”. W wyjściu nie ma klucza. Jeśli subskrypcja nie może wdrożyć `gpt-4o-mini` (status
Deprecated, research R1), skrypt kończy się kodem 4 z podpowiedzią innego modelu.

## 6. Konfiguracja aplikacji

Wpisz wynik skryptu do `src/mBank.FaqGenerator/appsettings.Local.json` (plik ignorowany przez git; kopiowany do
katalogu wyjściowego jak `appsettings.json` — sprawdź `bin/…/appsettings.Local.json` po zbudowaniu) albo ustaw
zmienne `FAQGEN__AzureOpenAI__…`.

## 7. Pełny przebieg na prawdziwych regulaminach (ręcznie)

```bash
dotnet run --project src/mBank.FaqGenerator -c Release -- --url <a> --url <b> --url <c> --url <d> --url <e>
```

Oczekiwane:
- 5 plików PDF i 5 plików `.md` w `downloads/`;
- postęp konwersji i 6 zapytań do modelu;
- `faq/FAQ_mBank.md` z front matter (5 adresów w `resource`) i 10 sekcjami `##`, każda z wierszem „Źródło:”;
- kod 0.

Weryfikacja SC-074: dla każdej z 10 odpowiedzi otwórz wskazany dokument i jednostkę i potwierdź zgodność. Wynik
zapisz w handoffie planu.

## 8. Klucz potokiem (bez interakcji)

```bash
az cognitiveservices account keys list -g rg-faqgen -n <zasób> --query key1 -o tsv \
  | dotnet run --project src/mBank.FaqGenerator -c Release -- --url <a> … --url <e>
```

Oczekiwane: brak promptu i gwiazdek, przebieg jak w scenariuszu 7, kod 0. Sprawdź, że klucz nie występuje w
żadnym pliku:

```bash
grep -rF "<klucz>" downloads faq src/mBank.FaqGenerator/bin
```

Polecenie nie powinno nic znaleźć. Uwaga: wpisanie klucza w poleceniu `grep` zostawia go w historii powłoki, więc
wyczyść ją po sprawdzeniu albo pomiń ten krok.

## 9. Usunięcie zasobów

```bash
az group delete --name rg-faqgen --yes
```
