# Research: linki z FAQ do miejsca źródła

## R1. Skąd wziąć stronę i nagłówek jednostki

Lista jednostek (`UnitExtractor.FromDocument`) powstaje z sekcji parsera w kolejności dokumentu: dla każdej sekcji
`Designation` i `HeadingText`, bez powtórzeń (pierwsze wystąpienie wygrywa). Sekcja zna stronę początku
(`Section.Pages.First`), a `MarkdownRenderer` pisze dla każdej sekcji jeden nagłówek ATX w tej samej kolejności
(przed nimi `# tytuł`, gdy dokument ma tytuł).

**Decyzja**: `UnitExtractor` zwraca obok listy jednostek ich położenie (`FaqUnitLocation`: strona, kotwica). Kotwica
powstaje z nagłówków zapisanego Markdownu: k-ta sekcja w kolejności renderowania = k-ty nagłówek sekcji w pliku. Gdy
liczba nagłówków nie zgadza się z liczbą sekcji (np. inna wersja renderera), kotwic brak, strony zostają (FR-1003).

**Alternatywy**: szukanie nagłówka po tekście w Markdownie — zawodne przy powtórzonych nagłówkach i ucieczkach;
kotwice w modelu parsera — parser nie zna reguł GitHuba, a FAQ jest jedynym konsumentem.

## R2. Jednostka zacytowana inaczej niż na liście

`UnitMatcher.Matches` przyjmuje jednostkę równą pozycji listy, zaczynającą się od niej („§ 12 ust. 3”) albo równą
numerowi numerowanego nagłówka („6” dla „6. Jakie rachunki…”), po normalizacji. `FaqSource.Unit` jest tekstem modelu po
przycięciu, więc nie zawsze jest kluczem listy.

**Decyzja**: nowa funkcja `UnitMatcher.Find(cited, units)` zwraca pierwszą w kolejności dokumentu pozycję listy, którą
`Matches` by przyjął (ta sama normalizacja); położenie bierze się z tej pozycji. Tekst w wierszu źródła zostaje tekstem
modelu (FR-1004).

## R3. Algorytm kotwic (GitHub i VS Code)

Sprawdzone w VS Code zainstalowanym lokalnie (`markdown-language-features/dist/serverWorkerMain.js`,
`githubSlugifier`): `heading.trim().toLowerCase()`, usunięcie znaków wyrażeniem `githubSlugReplaceRegex` (to samo co
`github-slugger`), każdy biały znak → `-`; powtórzenie kotwicy w dokumencie dostaje `-1`, `-2`… GitHub używa
`github-slugger` na tekście nagłówka po wyrenderowaniu (bez znaczników i ucieczek).

Wyrażenie usuwa wszystko poza literami (L), znakami łączącymi (M), cyframi dziesiętnymi i literowymi (Nd, Nl),
łącznikami (Pc), spacją i `-`; usuwa więc `§`, kropki, nawiasy, cudzysłowy „”, indeksy górne (¹²³, kategoria No).
Przykłady: „§ 26. Tytuł” → `-26-tytuł`; „8. Mobilna wpłata” → `8-mobilna-wpłata`; „Rozdział 1. Przepisy ogólne” →
`rozdział-1-przepisy-ogólne`.

**Decyzja**: `MarkdownAnchors` w `LegalAgent.Faq` — tekst nagłówka bez znaczników `#`, z usuniętymi ucieczkami
(`\` przed znakiem interpunkcji ASCII) i znacznikami `**`/`*`, potem reguła kategorii Unicode jak wyżej i licznik
powtórzeń w obrębie pliku (wszystkie nagłówki, także tytuł). Test porównuje wynik z przykładami policzonymi
algorytmem VS Code (tabela w teście, w tym §, cudzysłowy „”, ¹, powtórzenia).

**Alternatywy**: przeniesienie wyrażenia z `github-slugger` dosłownie (kilka kB zakresów) — równoważne kategoriom
Unicode .NET dla znaków występujących w regulaminach, a trudniejsze w utrzymaniu.

## R4. Strona PDF-u w linku

`#page=N` to parametr otwarcia PDF (RFC 8118, Adobe „PDF Open Parameters”), obsługiwany przez podgląd PDF w Chrome,
Edge i Firefox. Numer N = numer strony fizycznej pliku, ten sam co `Section.Pages.First` i znaczniki
`<!-- page: N -->`.

**Decyzja**: link na nazwie dokumentu = adres źródła + `#page=N` strony jednostki; gdy adres ma już fragment (`#`),
strona nie jest dopisywana (FR-1003). Nagłówek YAML (`resource`) bez zmian (FR-1005).

## R5. Ścieżka do Markdownu dokumentu

Aplikacja zna katalog pobranych plików (`Download:OutputDirectory`, `--output`) i katalog FAQ
(`Faq:OutputDirectory`, `--faq-output`); Markdown dokumentu to `<downloads>/<MarkdownFileName>`.

**Decyzja**: aplikacja liczy `Path.GetRelativePath(faqDirectory, markdownPath)`; wynik bezwzględny (inny dysk) →
brak linku do Markdownu. Separatory `/`, każdy segment kodowany procentowo (`Uri.EscapeDataString`), żeby spacje i
nawiasy w nazwach plików nie psuły linku. Biblioteka dostaje gotowy adres (FR-1006). Kotwica bez kodowania
procentowego (GitHub i VS Code przyjmują litery spoza ASCII; kotwica nie zawiera spacji ani nawiasów).

## R6. Tekst linku jednostki

Dziś jednostka jest zwykłym tekstem po przecinku. Jako tekst linku `[` i `]` musiałyby mieć ucieczkę; w jednostkach z
korpusu i pobranych regulaminów nawiasów kwadratowych nie ma.

**Decyzja**: tekst linku = jednostka z ucieczką `\[`, `\]` (widoczny tekst bez zmian, SC-143).

## R7. Wersje i zgodność

Zmiany addytywne w `LegalAgent.Faq` (nowe opcjonalne właściwości `init` i nowe typy) → wszystkie biblioteki 1.5.0
(MINOR; wspólna wersja w `Directory.Build.props`). Kontrakt pliku FAQ (spec 006 `faq-file.md`) dostaje addendum:
format wiersza źródła z linkami.
