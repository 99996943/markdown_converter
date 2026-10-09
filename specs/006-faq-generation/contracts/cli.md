# Kontrakt: aplikacja `mBank.FaqGenerator` — pobieranie, konwersja, FAQ

Rozszerza `specs/005-regulation-download/contracts/cli.md`. Wszystko, czego tu nie zmieniono (źródła adresów,
pytania o adresy, postęp pobierania, manifest), obowiązuje bez zmian.

## Wywołanie

```text
mBank.FaqGenerator [--url <adres>]... [--output <katalog>] [--faq-output <katalog>]
mBank.FaqGenerator --help | --version

# klucz potokiem (bez wpisywania; wejście przekierowane):
az cognitiveservices account keys list -g rg-faqgen -n <zasób> --query key1 -o tsv \
  | mBank.FaqGenerator --url <a> --url <b> --url <c> --url <d> --url <e>
```

| Opcja | Znaczenie |
|-------|-----------|
| `--faq-output <katalog>` | katalog OKF z plikiem `FAQ_mBank.md` (nadpisuje `Faq:OutputDirectory`, domyślnie `faq`); nowa |

Klucza nie da się podać opcją ani zmienną (FR-412).

## Konfiguracja (nowe sekcje)

```json
{
  "Download": { "…": "jak w spec 005" },
  "AzureOpenAI": {
    "Endpoint": "",
    "Deployment": "gpt-4o-mini",
    "Model": "gpt-4o-mini",
    "TimeoutSeconds": 300,
    "Temperature": 0,
    "Seed": 42,
    "MaxOutputTokens": 4096
  },
  "Faq": {
    "OutputDirectory": "faq",
    "CandidatesPerDocument": 10,
    "MaxDocumentTokens": 100000
  }
}
```

- **Endpoint:** podaje się zwykle w `appsettings.Local.json` (ignorowany przez git) albo w zmiennej
  `FAQGEN__AzureOpenAI__Endpoint`. To nie jest sekret.
- **Walidacja przy starcie, przed pytaniami o adresy:** wymagane są `Endpoint` (bezwzględny `https`) i
  `Deployment` (niepusty). Wartości liczbowe muszą mieścić się w zakresach z `data-model.md`. Klucz
  `AzureOpenAI:ApiKey` nie może być obecny. Każdy błąd → kod 2 z nazwą pola, np.
  „Błąd konfiguracji: brak AzureOpenAI:Endpoint (adres zasobu Azure OpenAI, zob. scripts/azure/create-openai.sh).”
- **Temperature i Seed:** `null` oznacza, że parametr nie jest wysyłany. Przykład: `FAQGEN__AzureOpenAI__Temperature=`
  (pusta wartość) albo `"Temperature": null`.

## Przebieg na stdout

```text
[1/5] pobieranie …                       (spec 005)
Pobrano 5 z 5 plików do downloads: …     (spec 005)

Konwersja do Markdown:
[1/5] reg-konta.pdf → reg-konta.md (24 strony)
[2/5] taryfa.pdf → taryfa.md (8 stron, 2 ostrzeżenia)
      ostrzeżenie TBL001 (strona 3): …
…
Przekonwertowano 5 z 5 plików.

Klucz API Azure OpenAI: ******************************** (gwiazdka za każdy znak; brak przy wejściu przekierowanym)

Generowanie FAQ (gpt-4o-mini, wdrożenie gpt-4o-mini):
[D1] kandydaci z reg-konta.md — 141 233 znaki (~47 078 tokenów)…
[D1] 10 kandydatów (wejście 52 410, wyjście 2 118 tokenów)
…
[wybór] 50 kandydatów — 31 004 znaki (~10 335 tokenów)…
[wybór] 10 pytań (wejście 11 902, wyjście 3 006 tokenów)

Zapisano FAQ: /ścieżka/faq/FAQ_mBank.md (10 pytań; łącznie wejście 276 115, wyjście 13 490 tokenów)
```

Liczby są formatowane w `pl-PL`. Jeśli usługa nie podała zużycia, w miejscu liczb tokenów jest „zużycie tokenów
nieznane”.

## Pytanie o klucz

- **Konsola:** prompt „Klucz API Azure OpenAI: ”. Gwiazdka za każdy znak, Backspace cofa, Enter kończy. Pusty
  klucz → „Klucz nie może być pusty.” i ponowne pytanie. Ctrl+C → „Przerwano.” na stderr, kod 130.
- **Wejście przekierowane:** klucz to kolejny wiersz po wierszach z adresami, jeśli adresy też czytano z wejścia.
  Bez promptu i bez gwiazdek. Brak wiersza lub pusty wiersz → stderr:
  „Brak klucza API: wejście jest przekierowane, ale nie zawiera klucza. Przekaż klucz potokiem jako kolejny wiersz,
  np. `… keys list … -o tsv | mBank.FaqGenerator --url …`, albo uruchom w konsoli.”, kod 2.
- **Kolejność:** o klucz aplikacja pyta tylko wtedy, gdy pobieranie i konwersja się powiodły, a `CheckInput` nie
  znalazł zbyt długiego dokumentu.

## Błędy (stderr) i kody wyjścia

| Sytuacja | Komunikat (wzór) | Kod |
|----------|------------------|-----|
| konwersja | `Błąd konwersji taryfa.pdf: <przyczyna>` + podsumowanie „Przekonwertowano 4 z 5 plików.” | 5 |
| dokument za długi | `Dokument reg-konta.md jest za długi dla modelu: 412 000 znaków (~137 334 tokenów), limit 100 000 tokenów (Faq:MaxDocumentTokens).` | 6 |
| uwierzytelnienie | `Usługa Azure OpenAI odrzuciła klucz (401): klucz jest nieprawidłowy lub nie ma dostępu do zasobu.` | 6 |
| wdrożenie | `Nie znaleziono wdrożenia „gpt-4o-mini” w zasobie <endpoint> (404).` | 6 |
| limit zapytań | `Przekroczono limit zapytań wdrożenia (429) przy dokumencie D3. Spróbuj później lub zwiększ przepustowość wdrożenia.` | 6 |
| filtr treści | `Usługa zablokowała zapytanie filtrem treści (D2).` | 6 |
| czas | `Brak odpowiedzi usługi w ciągu 300 s (krok wyboru).` | 6 |
| sieć | `Błąd połączenia z usługą Azure OpenAI: <przyczyna>.` | 6 |
| odpowiedź odrzucona | `Odpowiedź modelu odrzucona (krok wyboru):` + lista problemów, każdy w wierszu `  - …` | 7 |
| zapis FAQ | `Nie można zapisać FAQ_mBank.md w <katalog>: <przyczyna>` | 4 |

Każdy komunikat z etapu FAQ przechodzi przez usuwanie klucza (`***`). Poprzedni `FAQ_mBank.md` zostaje
nienaruszony przy każdym kodzie ≠ 0.

| Kod | Znaczenie |
|-----|-----------|
| 0 | 5/5 pobrane, 5/5 przekonwertowane, `FAQ_mBank.md` zapisany |
| 1 | błąd nieoczekiwany |
| 2 | argumenty, konfiguracja, adresy, brak wejścia (adresy lub klucz) |
| 3 | nie wszystkie pliki pobrane |
| 4 | błąd zapisu (katalog pobrań, Markdown, FAQ) |
| 5 | błąd konwersji |
| 6 | błąd usługi modelu albo dokument za długi |
| 7 | odpowiedź modelu odrzucona |
| 130 | przerwano |

Tekst `--help` wymienia nową opcję, sekcje konfiguracji, sposób podania klucza i wszystkie kody.

## Pliki wynikowe

- `<katalog pobrań>/<nazwa>.md`: Markdown każdego PDF (zapis atomowy). Po udanej konwersji usuwane są `*.md`
  spoza bieżącego zestawu.
- `<Faq:OutputDirectory>/FAQ_mBank.md` (domyślnie `faq/FAQ_mBank.md`, katalog nie jest ignorowany przez git):
  `contracts/faq-file.md`.
- Żaden plik nie zawiera klucza.
