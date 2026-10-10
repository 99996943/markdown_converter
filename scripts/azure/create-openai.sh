#!/usr/bin/env bash
# Creates (idempotently) the resource group, Azure OpenAI account and model deployment for mBank.FaqGenerator.
# Never reads or prints the API key, writes no files and exports no variables.
set -euo pipefail

usage() {
  cat <<'EOF'
Użycie:
  scripts/azure/create-openai.sh [--resource-group <nazwa>] [--location <region>] [--name <zasób>]
                                 [--deployment <nazwa>] [--model <model>] [--model-version <wersja>]
                                 [--sku <GlobalStandard|Standard|DataZoneStandard>] [--capacity <tys. TPM>]
  scripts/azure/create-openai.sh --help

Domyślnie: --resource-group rg-faqgen, --location swedencentral, --name faqgen-<8 znaków sha256 identyfikatora
subskrypcji>, --deployment gpt-4o-mini, --model gpt-4o-mini, --model-version 2024-07-18,
--sku GlobalStandard, --capacity 200.

Kody wyjścia: 0 gotowe, 2 błędne parametry, 3 brak az lub logowania, 4 model niedostępny, 5 błąd polecenia az.
EOF
}

resource_group="rg-faqgen"
location="swedencentral"
name=""
deployment="gpt-4o-mini"
model="gpt-4o-mini"
model_version="2024-07-18"
sku="GlobalStandard"
capacity="200"

while [ $# -gt 0 ]; do
  case "$1" in
    --help|-h)
      usage
      exit 0
      ;;
    --resource-group|--location|--name|--deployment|--model|--model-version|--sku|--capacity)
      if [ $# -lt 2 ]; then
        echo "Brak wartości dla parametru $1." >&2
        usage >&2
        exit 2
      fi
      case "$1" in
        --resource-group) resource_group="$2" ;;
        --location) location="$2" ;;
        --name) name="$2" ;;
        --deployment) deployment="$2" ;;
        --model) model="$2" ;;
        --model-version) model_version="$2" ;;
        --sku) sku="$2" ;;
        --capacity) capacity="$2" ;;
      esac
      shift 2
      ;;
    *)
      echo "Nieznany parametr: $1" >&2
      usage >&2
      exit 2
      ;;
  esac
done

# Runs an az command; on failure reports it and exits with 5. az's own stderr is passed through.
run_az() {
  if ! "$@"; then
    echo "Polecenie az nie powiodło się: $*" >&2
    exit 5
  fi
}

# 1. Azure CLI present?
if ! command -v az >/dev/null 2>&1; then
  echo "Brak Azure CLI (az). Zainstaluj: https://aka.ms/azure-cli" >&2
  exit 3
fi

# 2. Logged in?
if ! subscription_id="$(az account show --query id -o tsv 2>/dev/null)"; then
  echo "Nie zalogowano. Uruchom: az login" >&2
  exit 3
fi

if [ -z "$name" ]; then
  if command -v sha256sum >/dev/null 2>&1; then
    hash="$(printf '%s' "$subscription_id" | sha256sum | cut -c1-8)"
  else
    hash="$(printf '%s' "$subscription_id" | shasum -a 256 | cut -c1-8)"
  fi
  name="faqgen-$hash"
fi

# 3. Model available in the region for the chosen SKU?
available=0
models="$(az cognitiveservices model list --location "$location" \
  --query "[].[model.name,model.version,join(',',model.skus[].name)]" -o tsv)" || {
  echo "Polecenie az nie powiodło się: az cognitiveservices model list --location $location" >&2
  exit 5
}
while IFS=$'\t' read -r m_name m_version m_skus; do
  m_skus="${m_skus%$'\r'}"
  if [ "$m_name" = "$model" ] && [ "$m_version" = "$model_version" ]; then
    case ",$m_skus," in
      *",$sku,"*) available=1 ;;
    esac
  fi
done <<< "$models"

if [ "$available" != 1 ]; then
  echo "Model $model $model_version ($sku) nie jest dostępny w $location dla tej subskrypcji. Wybierz inny region lub model, np. --model gpt-5.4-mini --model-version 2026-03-17 (wtedy ustaw AzureOpenAI:Temperature na null)." >&2
  exit 4
fi

# 4. Resource group (create is idempotent).
run_az az group create --name "$resource_group" --location "$location" --output none

# 5. Account.
if az cognitiveservices account show --name "$name" --resource-group "$resource_group" --output none 2>/dev/null; then
  echo "Zasób $name już istnieje."
else
  run_az az cognitiveservices account create --name "$name" --resource-group "$resource_group" \
    --location "$location" --kind OpenAI --sku S0 --custom-domain "$name" --yes --output none
fi

# 6. Deployment.
if az cognitiveservices account deployment show --name "$name" --resource-group "$resource_group" \
  --deployment-name "$deployment" --output none 2>/dev/null; then
  echo "Wdrożenie $deployment już istnieje."
else
  run_az az cognitiveservices account deployment create --name "$name" --resource-group "$resource_group" \
    --deployment-name "$deployment" --model-format OpenAI --model-name "$model" --model-version "$model_version" \
    --sku-name "$sku" --sku-capacity "$capacity" --output none
fi

# 7. Endpoint.
if ! endpoint="$(az cognitiveservices account show --name "$name" --resource-group "$resource_group" \
  --query properties.endpoint -o tsv)"; then
  echo "Polecenie az nie powiodło się: az cognitiveservices account show --name $name --query properties.endpoint" >&2
  exit 5
fi
endpoint="${endpoint%$'\r'}"

# 8. Final output.
cat <<EOF
Gotowe. Wpisz do src/mBank.FaqGenerator/appsettings.Local.json:
{
  "AzureOpenAI": { "Endpoint": "$endpoint", "Deployment": "$deployment", "Model": "$model" }
}
albo ustaw zmienne:
  FAQGEN__AzureOpenAI__Endpoint=$endpoint
  FAQGEN__AzureOpenAI__Deployment=$deployment

Klucz API: portal Azure → zasób $name → Klucze i punkt końcowy,
albo przekaż go potokiem prosto do aplikacji (klucz nie trafia na ekran ani do pliku):
  az cognitiveservices account keys list -g $resource_group -n $name --query key1 -o tsv | mBank.FaqGenerator --url …

Usunięcie zasobów (koniec kosztów): az group delete --name $resource_group --yes
EOF
