# Feature Specification: Linki z FAQ do miejsca źródła (strona PDF-u i nagłówek Markdownu)

**Feature Branch**: `013-faq-source-links`

**Created**: 2026-10-13

**Status**: Draft

**Input**: User description: "zastanów się, czy w aplikacji do FAQ można do każdego generowanego pytania dodać link do odpowiedniego miejsca w Markdownie odpowiedniego dokumentu" — decyzja właściciela 2026-10-13: wariant 3 (link do strony PDF-u i link do nagłówka w Markdownie).

## Kontekst

Aplikacja `mBank.FaqGenerator` (spec 006) pisze `faq/FAQ_mBank.md`: 10 pytań, każde z odpowiedzią i wierszem źródeł,
np.

```text
Źródło: [Regulamin usług gotówkowych](https://www.mbank.pl/…/regulamin-uslug-gotowkowych-pl.pdf), § 26
```

Link prowadzi do początku PDF-u, a jednostka („§ 26”, „8. Mobilna wpłata”) jest zwykłym tekstem. Czytelnik musi sam
odszukać miejsce w dokumencie. Jednostka jest zawsze dosłownie jedną z jednostek dokumentu (oznaczenie albo tekst
nagłówka sekcji, kontrakt spec 006 `model-exchange.md`), a parser zna stronę początku sekcji i jej nagłówek w
Markdownie zapisanym obok PDF-u (`downloads/<nazwa>.md`). Da się więc wskazać miejsce dokładnie.

Numeracja wymagań: FR-1000…, SC-140….

## Clarifications

### Session 2026-10-13

- Q: Jaki rodzaj linku? → A: wariant 3 — link do strony PDF-u (`…pdf#page=N`) i link do nagłówka sekcji w Markdownie
  dokumentu (`../downloads/<nazwa>.md#kotwica`).
- Q: Który element wiersza źródła prowadzi do strony PDF-u, a który do nagłówka Markdownu? → A: nazwa dokumentu →
  PDF z `#page=N`, jednostka → nagłówek w Markdownie dokumentu.
- Q: Czy śledzony `faq/FAQ_mBank.md` ma linki do Markdownów z `downloads/` (poza gitem)? → A: tak, zawsze, bez
  przełącznika; ograniczenie (na GitHubie link do Markdownu nie działa) opisane w README i kontrakcie FAQ.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Link do strony PDF-u (Priority: P1)

Czytelnik FAQ klika źródło odpowiedzi i przeglądarka otwiera PDF regulaminu od razu na stronie, na której zaczyna się
wskazana jednostka.

**Why this priority**: działa u każdego, także poza repozytorium (link do publicznego PDF-u, który FAQ już podaje).

**Independent Test**: FAQ wygenerowane na fałszywym modelu z jednostką zaczynającą się na stronie 12; wiersz źródła
ma link do PDF-u z `#page=12`.

**Acceptance Scenarios**:

1. **Given** źródło z jednostką „§ 26” zaczynającą się na stronie 12, **When** powstaje FAQ, **Then** link do
   dokumentu kończy się `#page=12`.
2. **Given** źródło bez jednostki, **When** powstaje FAQ, **Then** link prowadzi do PDF-u jak dotąd (bez `#page`).

---

### User Story 2 - Link do nagłówka w Markdownie (Priority: P2)

Właściciel czyta FAQ w repozytorium lub w VS Code i klika link do jednostki: otwiera się Markdown dokumentu
przewinięty do nagłówka tej sekcji.

**Why this priority**: trafia dokładnie w sekcję i pozwala porównać odpowiedź z tekstem, który czytał model.

**Independent Test**: FAQ z jednostką „8. Mobilna wpłata” ma link `…/regulamin-uslug-gotowkowych-pl.md#8-mobilna-wpłata`
(ścieżka względna od pliku FAQ do Markdownu dokumentu); nagłówek o tej kotwicy istnieje w tym Markdownie.

**Acceptance Scenarios**:

1. **Given** jednostka będąca nagłówkiem sekcji, **When** powstaje FAQ, **Then** jednostka w wierszu źródła jest
   linkiem do Markdownu dokumentu z kotwicą tego nagłówka.
2. **Given** dwa nagłówki o tym samym tekście w dokumencie, **When** jednostka wskazuje pierwszy (lista jednostek nie
   ma powtórzeń), **Then** kotwica wskazuje pierwszy nagłówek.
3. **Given** jednostka będąca oznaczeniem („§ 26”) sekcji z nagłówkiem „§ 26. Tytuł”, **When** powstaje FAQ, **Then**
   kotwica wskazuje nagłówek tej sekcji.

### Edge Cases

- Jednostki nie da się przypisać do sekcji (model podał pusty tekst): wiersz źródła jak dziś, bez nowych linków.
- Jednostka bez własnego nagłówka nie występuje: lista „Jednostki dokumentu” powstaje z oznaczeń i nagłówków sekcji, a
  jednostka spoza listy jest odrzucana (spec 006 `model-exchange.md`, `UnitMatcher`). Jeśli mimo to sekcja nie ma
  nagłówka w Markdownie, obowiązuje FR-1003 (bez linku do Markdownu, link do strony PDF-u zostaje).
- Nagłówek z polskimi literami, kropkami, „§”, cudzysłowami, pogrubieniem lub ucieczkami Markdownu: kotwica liczona
  jak w podglądzie GitHub/VS Code (z tekstu nagłówka po usunięciu formatowania).
- Powtórzone nagłówki w jednym Markdownie: kotwica z dopiskiem `-1`, `-2` jak w podglądzie GitHub.
- Katalog FAQ i katalog pobranych dokumentów są w różnych miejscach (`--faq-output`, `--output`): ścieżka względna
  liczona między nimi; gdy nie da się jej zbudować (inne dyski), link do Markdownu pomijany.
- W repozytorium `faq/FAQ_mBank.md` jest śledzony, a `downloads/` nie: linki do Markdownu są zapisywane zawsze (bez
  przełącznika) i działają lokalnie po pobraniu dokumentów, na GitHubie nie; link do strony PDF-u działa zawsze.
  Ograniczenie MUST być opisane w README aplikacji i w kontrakcie pliku FAQ.
- Strona sekcji nieznana (sekcja bez stron): link do PDF-u bez `#page`.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-1000**: Dla źródła z jednostką link na nazwie dokumentu MUST prowadzić do PDF-u z numerem strony, na której
  zaczyna się sekcja tej jednostki (`#page=N`, parametr otwarcia PDF-u w przeglądarkach).
- **FR-1001**: Jednostka w wierszu źródła MUST być linkiem do nagłówka tej sekcji w Markdownie dokumentu zapisanym
  przez aplikację; ścieżka MUST być względna od pliku FAQ.
- **FR-1002**: Kotwica MUST być taka, jaką generuje podgląd GitHub dla tego nagłówka (także dla powtórzonych
  nagłówków i znaków spoza ASCII).
- **FR-1003**: Gdy jednostki brak, nie da się jej przypisać do sekcji albo nie da się zbudować ścieżki, wiersz źródła
  MUST wyglądać jak dziś (bez nowego linku), a FAQ MUST powstać normalnie.
- **FR-1004**: Tekst wiersza źródła MUST pozostać tym samym tekstem co dziś (nazwa dokumentu, jednostka); zmieniają
  się tylko adresy linków (FR-432 spec 006: link to formatowanie, nie treść).
- **FR-1005**: Nagłówek YAML pliku FAQ (`resource`) MUST zostać bez zmian (adresy PDF-ów bez `#page`).
- **FR-1006**: Biblioteka `LegalAgent.Faq` MUST pozostać bez nazw banku, hostów i plików; ścieżki i adresy przychodzą
  od aplikacji.
- **FR-1007**: Testy MUST działać offline (fałszywy model, syntetyczne regulaminy); kontrakt pliku FAQ (spec 006
  `faq-file.md`) MUST opisać nowe linki.

### Key Entities

- **Jednostka dokumentu**: tekst cytowany przez model (oznaczenie albo nagłówek sekcji) + strona początku sekcji +
  kotwica nagłówka w Markdownie dokumentu.
- **Źródło odpowiedzi** (`FaqSource`): dokument i jednostka; wiersz źródła dostaje adres strony PDF-u i adres
  nagłówka.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-140**: W FAQ z 10 pytań 100% źródeł z jednostką ma link do strony PDF-u i link do nagłówka Markdownu.
- **SC-141**: 100% linków do nagłówków w FAQ wygenerowanym na pobranych regulaminach trafia w istniejący nagłówek
  (sprawdzone automatycznie: kotwica ∈ kotwice Markdownu dokumentu).
- **SC-142**: Strona z `#page=N` to strona, na której w PDF-ie stoi nagłówek jednostki (sprawdzone ręcznie na 10
  pytaniach jednego przebiegu).
- **SC-143**: Plik FAQ różni się od dzisiejszego wyłącznie adresami linków w wierszach źródeł.

## Assumptions

- `#page=N` obsługują przeglądarki z wbudowanym podglądem PDF (Chrome, Edge, Firefox) — standard parametrów otwarcia
  PDF; inne przeglądarki otworzą dokument od początku.
- Kotwice liczone według algorytmu GitHub (`github-slugger`): małe litery, usunięcie znaków spoza liter, cyfr, spacji,
  `-` i `_`, spacje → `-`, powtórzenia `-1`, `-2`; VS Code używa tego samego algorytmu.
- Numer strony to numer strony fizycznej PDF-u (1 = pierwsza strona pliku), jak w znacznikach `<!-- page: N -->`.
- Format wiersza źródła (Clarifications): `[<Name>](<pdf>#page=N), [<jednostka>](<ścieżka.md>#kotwica)`; dokładna
  postać w kontrakcie planu.
- Spec 012 (identyfikatory przypisów) nie wpływa na nagłówki ani strony — specyfikacje są niezależne.
