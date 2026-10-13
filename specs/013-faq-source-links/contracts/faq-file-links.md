# Kontrakt: linki w wierszu źródeł FAQ (addendum do spec 006 `faq-file.md`)

Zastępuje punkt „źródło to `[<Name dokumentu>](<Resource>)`, a gdy wskazano jednostkę: `, <jednostka>`”. Pozostałe
reguły pliku FAQ (front matter, `resource`, sekcje, odstępy, stabilność) bez zmian.

## Postać źródła

```text
[<Name>](<Resource>#page=<N>), [<jednostka>](<MarkdownLink>#<kotwica>)
```

- **Nazwa dokumentu** prowadzi do adresu źródła (jak w `resource`) z fragmentem `#page=N`, gdzie N to numer strony
  fizycznej PDF-u, na której zaczyna się sekcja jednostki (1 = pierwsza strona pliku).
- **Jednostka** to tekst modelu (jak dotąd), jako tekst linku z ucieczką `\[`, `\]`. Link prowadzi do Markdownu
  dokumentu zapisanego przez aplikację, ścieżką względną od pliku FAQ (separator `/`, segmenty kodowane procentowo),
  z kotwicą nagłówka sekcji.
- **Kotwica** jest liczona jak w GitHub i VS Code (`github-slugger`): tekst nagłówka bez znaczników i ucieczek, przycięty,
  małe litery, usunięte znaki inne niż litery, znaki łączące, cyfry (Nd, Nl), `_`, `-` i białe znaki, każdy biały
  znak → `-`; powtórzona kotwica w tym samym pliku dostaje `-1`, `-2`… (licząc wszystkie nagłówki pliku, także tytuł).

## Przypadki bez linku (wiersz jak w spec 006)

| Sytuacja | Nazwa dokumentu | Jednostka |
|---|---|---|
| źródło bez jednostki | `[Name](Resource)` | — |
| jednostki nie ma na liście dokumentu albo strona nieznana | `[Name](Resource)` | zgodnie z wierszem niżej |
| adres źródła ma już fragment `#` | `[Name](Resource)` | zgodnie z wierszem niżej |
| kotwica nieznana albo brak ścieżki do Markdownu (inny dysk) | — | zwykły tekst `jednostka` |

## Przykłady

```text
Źródło: [Regulamin usług gotówkowych](https://bank.example/regulamin.pdf#page=12), [§ 26](../downloads/regulamin.md#-26-wpłaty)
Źródła: [Regulamin A](https://bank.example/a.pdf#page=3), [8. Mobilna wpłata](../downloads/a.md#8-mobilna-wpłata); [Taryfa B](https://bank.example/b.pdf)
```

## Ograniczenia

- Plik `faq/FAQ_mBank.md` w repozytorium jest śledzony, a `downloads/` nie: na GitHubie link do nagłówka prowadzi do
  nieistniejącego pliku; działa lokalnie (VS Code, przeglądarka plików) po pobraniu dokumentów. Link do strony PDF-u
  działa zawsze.
- `#page=N` obsługują podglądy PDF w Chrome, Edge i Firefox; inne przeglądarki otworzą dokument od początku.
