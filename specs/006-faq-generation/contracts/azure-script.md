# Kontrakt: skrypt `scripts/azure/create-openai.sh`

Bash (`#!/usr/bin/env bash`, `set -euo pipefail`), Azure CLI. Działa na Linuksie, macOS i w Git Bash na Windows.

## Wywołanie

```text
scripts/azure/create-openai.sh [--resource-group <nazwa>] [--location <region>] [--name <zasób>]
                               [--deployment <nazwa>] [--model <model>] [--model-version <wersja>]
                               [--sku <GlobalStandard|Standard|DataZoneStandard>] [--capacity <tys. TPM>]
scripts/azure/create-openai.sh --help
```

| Parametr | Domyślnie |
|----------|-----------|
| `--resource-group` | `rg-faqgen` |
| `--location` | `swedencentral` |
| `--name` | `faqgen-<pierwsze 8 znaków sha256(identyfikator subskrypcji)>` |
| `--deployment` | `gpt-4o-mini` |
| `--model` | `gpt-4o-mini` |
| `--model-version` | `2024-07-18` |
| `--sku` | `GlobalStandard` |
| `--capacity` | `200` |

Nieznany parametr lub brak wartości → kod 2 z pomocą.

## Kroki

1. Jest `az` w `PATH`? Nie → „Brak Azure CLI (az). Zainstaluj: https://aka.ms/azure-cli” (kod 3).
2. `az account show` się udaje? Nie → „Nie zalogowano. Uruchom: az login” (kod 3).
3. `az cognitiveservices model list --location <region>` zawiera `<model>` `<wersja>` dla `<sku>`? Nie → „Model
   <model> <wersja> (<sku>) nie jest dostępny w <region> dla tej subskrypcji. Wybierz inny region lub model, np.
   --model gpt-5.4-mini --model-version 2026-03-17 (wtedy ustaw AzureOpenAI:Temperature na null).” (kod 4)
4. `az group create` (idempotentne).
5. `az cognitiveservices account show` → istnieje: „Zasób <name> już istnieje.”; nie istnieje: `account create
   --kind OpenAI --sku S0 --custom-domain <name> --yes`.
6. `az cognitiveservices account deployment show` → istnieje: komunikat; nie istnieje: `deployment create
   --model-format OpenAI --model-name … --model-version … --sku-name … --sku-capacity …`.
7. Odczyt endpointu: `account show --query properties.endpoint -o tsv`.
8. Wyjście końcowe (stdout):

```text
Gotowe. Wpisz do src/mBank.FaqGenerator/appsettings.Local.json:
{
  "AzureOpenAI": { "Endpoint": "https://faqgen-1a2b3c4d.openai.azure.com/", "Deployment": "gpt-4o-mini", "Model": "gpt-4o-mini" }
}
albo ustaw zmienne:
  FAQGEN__AzureOpenAI__Endpoint=https://faqgen-1a2b3c4d.openai.azure.com/
  FAQGEN__AzureOpenAI__Deployment=gpt-4o-mini

Klucz API: portal Azure → zasób faqgen-1a2b3c4d → Klucze i punkt końcowy,
albo przekaż go potokiem prosto do aplikacji (klucz nie trafia na ekran ani do pliku):
  az cognitiveservices account keys list -g rg-faqgen -n faqgen-1a2b3c4d --query key1 -o tsv | mBank.FaqGenerator --url …

Usunięcie zasobów (koniec kosztów): az group delete --name rg-faqgen --yes
```

## Gwarancje

- Skrypt nie wywołuje `az … keys list`, nie zapisuje plików i nie eksportuje zmiennych (FR-442).
- Ponowne uruchomienie z tymi samymi parametrami kończy się kodem 0 i nie wywołuje żadnego `create` poza
  idempotentnym `group create` (FR-441).
- Błąd `az` w kroku 4–7 → komunikat „Polecenie az nie powiodło się: <polecenie>” i kod 5. Wyjście `az` na stderr
  jest przekazywane dalej.

| Kod | Znaczenie |
|-----|-----------|
| 0 | gotowe (utworzone lub już istniało) |
| 2 | błędne parametry |
| 3 | brak `az` lub brak logowania |
| 4 | model niedostępny |
| 5 | błąd polecenia `az` |
