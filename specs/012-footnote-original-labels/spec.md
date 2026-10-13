# Feature Specification: Oryginalne etykiety przypisów w Markdownie (parser 1.4.0)

**Feature Branch**: `012-footnote-original-labels`

**Created**: 2026-10-13

**Status**: Draft

**Input**: User description: "Oryginalne etykiety przypisów w Markdownie (parser 1.4.0). Problem: renderer zamienia etykiety przypisów z PDF-u na własną ciągłą numerację [^1], [^2]… (DocumentBuildStage.NumberFootnotes, kontrakt spec 001). Przykład: w regulaminie kredytu gotówkowego z publish/mBank.FaqGenerator-win-x64/downloads przypisy „21”, „22” z PDF-u (tabela na s. 21, „O dacie wprowadzenia tej funkcjonalności poinformujemy na naszej stronie internetowej.”) są w Markdownie jako [^3]–[^6], a numeracja nie idzie w kolejności dokumentu (7…17, 1, 18, 19, 2, 20…24, 3–6 w tabeli, 25 na końcu) — odnośniki w tabelach dostają numery przed odnośnikami z tekstu. Narusza to zasadę, że wynik zawiera tylko tekst z PDF-u. Oczekiwane: identyfikator przypisu w Markdownie to oryginalna etykieta z PDF-u (Footnote.OriginalLabel, np. [^21], [^1)] → sprawdzić, które znaki GFM dopuszcza w identyfikatorze); przy etykiecie powtarzającej się w dokumencie potrzebne rozróżnienie — decyzja właściciela. Ograniczenia: kontrakt Markdown nowa wersja (MAJOR lub MINOR do ustalenia), goldeny parsera i chunków zmienią się — każdą zmianę pokazać właścicielowi do akceptacji (FR-163), TDD red→green, repliki w SyntheticPdfBuilder, prywatny korpus z LEGALAGENT_PRIVATE_CORPUS, bramka FTN001 aktów bez zmian liczb, refresh/verify korpusu, publish/. Chunker korzysta z MarkdownRenderer — chunki też dostaną oryginalne etykiety."

## Kontekst

Feature zmienia sposób zapisu przypisów w Markdownie biblioteki `LegalAgent.PdfParser` (numeracja wymagań jest
kontynuowana od FR-900 i SC-130, po spec 011). Zasada projektu brzmi: wynik zawiera wyłącznie tekst z PDF-u, a
struktura jest wyrażona tylko formatowaniem Markdownu. Przypisy jej dziś nie spełniają: parser zna etykietę każdego
przypisu z PDF-u („21”, „1)”, „*)”), ale w Markdownie zastępuje ją własnym numerem `[^n]` nadawanym kolejno od 1.

Przykład (regulamin kredytu gotówkowego, `publish/mBank.FaqGenerator-win-x64/downloads`, parser 1.3.1): PDF ma
przypisy o etykietach 1–25 w kolejności stron. W Markdownie:

| W PDF-ie | W Markdownie 1.3.1 |
|---|---|
| tabela s. 21: „TAK²¹”, „TAK²²”, … | `TAK[^3]`, `TAK[^4]`, `TAK[^5]`, `TAK[^6]` |
| kolejność numerów w dokumencie | 7…17, 1, 18, 19, 2, 20…24, 3–6, 25 |

Czytelnik Markdownu (człowiek, model językowy w aplikacji RAG, recenzent porównujący wynik z PDF-em) nie może
odnaleźć przypisu po numerze z PDF-u. Numeracja przeskakuje, bo parser numeruje najpierw przypisy z rozpoznanym
odnośnikiem (w kolejności odnośników), a potem przypisy bez rozpoznanego odnośnika (w kolejności stron). W tym
regulaminie rozpoznanych jest tylko 8 z 25 odnośników (17 × FTN001: „BOK13.”, „umowy16,”, „tabeli20,” — odnośnik z
pełnowymiarową interpunkcją za nim, spec 011 K1/K2), więc numery 1–6 dostały odnośniki z list i tabeli, a 7–25
przypisy-sieroty (research R2).

Pomiar etykiet w korpusie (parser 1.3.1, szkice przypisów po etapie wykrywania):

| Grupa | Etykiety | Powtórzenia etykiety w jednym dokumencie |
|---|---|---|
| akty `Corpus/acts` (6) | „1)”, „2)”…, „*)”, „**)”, „***)” | prawo-bankowe: „1)”–„7)” ×2; obwieszczenie-msz: „1)”, „*)”, „**)”, „***)” ×2 |
| regulaminy prywatne i `downloads` | „1”, „2”… (w reg1/pdp także „1)”, „2)” — punkty listy w przypisie, spec 011 K3) | brak |
| syntetyczny `Corpus/banking` | jeden przypis bez etykiety (odnośnik „¹” to znak pełnej wielkości) | brak |

## Clarifications

### Session 2026-10-13

- Q: Jaką postać ma identyfikator przypisu (FR-902)? → A: etykieta dosłownie, ze wszystkimi znakami z PDF-u
  (`[^1)]`, `[^*)]`, `[^21]`); „1)” i „1” to różne identyfikatory.
- Q: Jak rozróżnić etykietę powtórzoną w dokumencie (FR-903)? → A: pierwsze wystąpienie bez dopisku, kolejne z
  dopiskiem numeru wystąpienia (`[^1)]`, `[^1)-2]`, `[^1)-3]`); kolejność wystąpień = kolejność definicji w dokumencie.
- Q: Linki z FAQ do miejsc w dokumentach? → A: wariant 3 (link do strony PDF-u i do nagłówka w Markdownie) — osobna
  spec 013 w `LegalAgent.Faq`, nie w tej.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Przypis w Markdownie ma etykietę z PDF-u (Priority: P1)

Właściciel czyta Markdown regulaminu obok PDF-u. Przy słowie „TAK²¹” w tabeli PDF-u widzi w Markdownie odnośnik
z tą samą etykietą, a definicja przypisu ma tę samą etykietę co w stopce strony PDF-u.

**Why this priority**: to sedno zmiany — usuwa z wyniku tekst, którego nie ma w PDF-ie, i pozwala odszukać przypis
po numerze.

**Independent Test**: replika strony z tabelą i przypisami o etykietach „21”–„24” oraz przypisem w tekście o
etykiecie „7”; Markdown zawiera odnośniki i definicje z etykietami 21–24 i 7, żadnej innej.

**Acceptance Scenarios**:

1. **Given** dokument z przypisami o etykietach „21”, „22” w tabeli, **When** go konwertuję, **Then** odnośniki i
   definicje w Markdownie mają etykiety 21 i 22.
2. **Given** akt z etykietami „1)”, „2)”, „*)”, **When** go konwertuję, **Then** identyfikatory przypisów w Markdownie
   są zbudowane z tych etykiet zgodnie z decyzją o ich postaci (FR-902).
3. **Given** dowolny dokument z przypisami, **When** go konwertuję, **Then** każdy odnośnik ma dokładnie jedną
   definicję, a każda definicja ma unikalny identyfikator (poprawny GFM).

---

### User Story 2 - Przypisy w kolejności dokumentu (Priority: P1)

Definicje przypisów sekcji stoją w kolejności, w jakiej ich odnośniki występują w tekście i tabelach tej sekcji, a
przypisy bez odnośnika — w kolejności stron, na których stoją; ich etykiety pokazują czytelnikowi kolejność z PDF-u.

**Why this priority**: przeskakujące numery (1–6, potem 7–25 sierot) mylą czytelnika; po zmianie identyfikatory to
etykiety z PDF-u, a kolejność definicji pozostaje regułą kontraktu (research R2: dzisiejszy kod już ją spełnia).

**Independent Test**: replika z odnośnikiem w tekście przed tabelą i odnośnikami w tabeli; definicje w Markdownie
stoją w kolejności: tekst, potem tabela.

**Acceptance Scenarios**:

1. **Given** sekcja z odnośnikiem „7” w akapicie i „21”, „22” w tabeli pod nim, **When** ją konwertuję, **Then**
   definicje stoją w kolejności 7, 21, 22.

---

### User Story 3 - Te same etykiety w chunkach (Priority: P2)

Aplikacja RAG dostaje chunki z tymi samymi identyfikatorami przypisów co Markdown dokumentu.

**Why this priority**: chunker renderuje fragmenty tym samym rendererem; bez tego chunk i dokument by się różniły.

**Independent Test**: chunk sekcji z przypisem zawiera odnośnik i definicję z etykietą z PDF-u.

**Acceptance Scenarios**:

1. **Given** sekcja z przypisem „21” podzielona na części, **When** generuję chunki, **Then** część z odnośnikiem
   zawiera `[^21]` i definicję `[^21]:`.

### Edge Cases

- Etykieta powtarza się w dokumencie (dwie serie przypisów w akcie: prawo-bankowe, obwieszczenie-msz): identyfikator
  musi być unikalny — sposób rozróżnienia według FR-903.
- Przypis bez etykiety (odnośnik nie był podniesiony, definicja zaczyna się znakiem „¹” w tekście): według FR-904.
- Etykieta zawiera znaki szczególne Markdownu („*)”, „**)”): identyfikator musi dać poprawny GFM i nie zmienić się w
  wyróżnienie.
- Ta sama etykieta w kilku dokumentach jednego zbioru nie ma znaczenia: identyfikatory są lokalne dla dokumentu.
- Przypis, do którego prowadzi kilka odnośników: wszystkie mają ten sam identyfikator.
- Punkty listy „1)”, „2)” wewnątrz przypisu, rozpoznane jako osobne definicje (spec 011 K3), mogą mieć etykiety
  kolidujące z etykietami „1”, „2” — w GFM `1)` i `1` to różne identyfikatory, więc nie kolidują.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-900**: Identyfikator przypisu w Markdownie (odnośnik `[^…]` i definicja `[^…]:`) MUST być zbudowany z
  etykiety przypisu odczytanej z PDF-u, a nie z numeru nadanego przez parser.
- **FR-901**: Odnośnik i jego definicja MUST mieć ten sam identyfikator; każdy identyfikator MUST mieć dokładnie jedną
  definicję w dokumencie; Markdown MUST pozostać poprawnym GFM (żaden przypis nie znika ani nie łączy się z innym).
- **FR-902**: Identyfikator MUST być etykietą z PDF-u zapisaną dosłownie, ze wszystkimi jej znakami (`[^1)]`,
  `[^*)]`, `[^21]`); indeks górny zapisany zwykłymi cyframi („²¹” → „21”).
- **FR-903**: Gdy ta sama etykieta występuje w dokumencie więcej niż raz, pierwsza definicja (w kolejności stron)
  MUST mieć identyfikator bez dopisku, a kolejne dopisek `-2`, `-3`… (`[^1)]`, `[^1)-2]`); odnośniki MUST wskazywać
  definicję, z którą są dziś powiązane. Dopisek nie może dać identyfikatora równego innej etykiecie dokumentu.
- **FR-904**: Przypis bez etykiety w PDF-ie albo z etykietą, której nie da się zapisać w identyfikatorze GFM
  (pusta, ze spacją, tabulatorem, `[` lub `]`), MUST dostać identyfikator zastępczy (numer kolejny w dokumencie) i
  ostrzeżenie w raporcie; to jedyny przypadek identyfikatora spoza PDF-u (poza FR-903).
- **FR-905**: Definicje przypisów sekcji MUST stać w kolejności pierwszego odnośnika w dokumencie (akapity, listy i
  tabele w kolejności czytania); przypisy bez odnośnika po nich, w kolejności stron.
- **FR-906**: Przypisy MUST być przypisane do tych samych sekcji co w 1.3.1 (zmienia się tylko identyfikator; miejsce i
  kolejność definicji jak w 1.3.1 — research R2).
- **FR-907**: Chunki MUST używać tych samych identyfikatorów co Markdown dokumentu.
- **FR-908**: Poza identyfikatorami przypisów Markdown i chunki MUST być bez zmian; każda
  zmiana goldenów parsera i chunków MUST być pokazana właścicielowi do akceptacji przed zapisaniem (FR-163).
- **FR-909**: Raport konwersji MUST pozostać bez zmian poza ostrzeżeniem z FR-904; liczby FTN001 aktów (bramka spec
  011) MUST się nie zmienić.
- **FR-910**: Kontrakt Markdown MUST dostać nową wersję opisującą identyfikatory przypisów (zastępuje regułę
  „numeracja ciągła od 1”); README parsera opisuje zmianę i sposób rozróżniania powtórzeń.
- **FR-911**: Każda zmiana zachowania MUST mieć test red przed poprawką, z repliką strony (`SyntheticPdfBuilder`) bez
  treści dokumentów prywatnych.
- **FR-912**: Wydanie: wersja parsera 1.4.0, odświeżony korpus syntetyczny (`refresh`/`verify`), przebudowany
  `publish/`, testy z prywatnym korpusem zielone (w tym `RegulationNumbers_HaveNoExcess`).

### Key Entities

- **Przypis** (`Footnote`): treść, strona, znacznik sieroty, oryginalna etykieta z PDF-u; dostaje identyfikator
  Markdownu zamiast samego numeru.
- **Identyfikator przypisu**: tekst w `[^…]`, z etykiety (FR-902), unikalny w dokumencie (FR-903), zastępczy dla
  przypisu bez etykiety (FR-904).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-130**: W 100% przypisów z etykietą identyfikator w Markdownie da się odczytać jako etykietę z PDF-u
  (sprawdzone na wszystkich dokumentach korpusów publicznego i prywatnego oraz `downloads`).
- **SC-131**: W każdym dokumencie liczba odnośników, definicji i ostrzeżeń FTN001 jest taka sama jak w 1.3.1.
- **SC-132**: W każdej sekcji definicje przypisów stoją w kolejności pierwszego odnośnika (0 przeskoków w korpusach).
- **SC-133**: Poza identyfikatorami przypisów 0 różnic w Markdownie i chunkach względem 1.3.1
  (porównanie po znormalizowaniu identyfikatorów).
- **SC-134**: Dopiski `-k` (FR-903) i identyfikatory zastępcze (FR-904) występują tylko w dokumentach z powtórzoną lub brakującą
  etykietą (dziś: 2 akty i 1 dokument syntetyczny).

## Assumptions

- GFM dopuszcza w identyfikatorze przypisu dowolne znaki poza spacją i nawiasami kwadratowymi (do potwierdzenia w
  planie na implementacji referencyjnej); podglądy (np. GitHub) i tak wyświetlają przypisy własną numeracją, więc
  etykieta z PDF-u jest widoczna w źródle Markdownu, które czytają aplikacja RAG i model.
- Etykieta z indeksu górnego jest zapisywana zwykłymi cyframi („²¹” → „21”), tak jak dziś w modelu.
- Kontrakt Markdown dostaje wersję 2.0.0 (zmiana niezgodna: znika reguła numeracji ciągłej); publiczne API
  biblioteki jest rozszerzone addytywnie i zgodne wstecz (nowe właściwości identyfikatora przypisu), stąd wersja
  biblioteki 1.4.0 (MINOR), zgodnie z opisem właściciela.
- Konsumenci w repozytorium (chunker, aplikacja FAQ) nie interpretują identyfikatorów przypisów; zmiana ich nie
  psuje poza goldenami chunków.
- Linki z FAQ do miejsc w dokumentach to osobna spec 013 (wariant 3: strona PDF-u i nagłówek Markdownu).
