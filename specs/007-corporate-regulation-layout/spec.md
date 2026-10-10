# Feature Specification: Regulaminy z etykietami w wysuniętej kolumnie i paragrafami „§ N” (układ regulaminów dla firm)

**Feature Branch**: `007-corporate-regulation-layout`

**Created**: 2026-10-10

**Status**: Draft

**Input**: User description: "Parser: układ regulaminów mBanku dla firm (msp-korporacje) — zakres (a). Cztery prawdziwe dokumenty (regulamin zintegrowanego rachunku bankowego, regulamin stosowania polecenia zapłaty, regulamin usług gotówkowych, zasady współpracy w zakresie transakcji rynku finansowego) konwertują się źle, bo mają wspólny układ: (1) etykiety punktów w osobnej, wysuniętej kolumnie w formacie „1/”, „2/”, „a/”, „b/” […], a tekst punktu obok — parser bierze odstęp za granicę kolumn tabeli i zapisuje treść jako tabele zastępcze „a \| b” z ostrzeżeniem TBL001 […]; (2) paragrafy „§ N” jako wyśrodkowany pogrubiony wiersz, który nie jest nagłówkiem […]. Cel: etykiety „1/”, „a/” w wysuniętej kolumnie stają się elementami list […]; „§ N” jako nagłówki jednostek redakcyjnych […]. Bez regresji […]. Biblioteka parsera nie zna mBanku — reguły ogólne dla układu, nie dla nazw dokumentów."

## Kontekst

Feature rozszerza bibliotekę `LegalAgent.PdfParser` (specyfikacje 001 i 002; numeracja wymagań jest kontynuowana od
FR-500 i SC-080). Markdown biblioteki zasila chunker RAG (spec 004), który dzieli tekst po nagłówkach, i generator
FAQ (spec 006), który pozwala modelowi wskazywać tylko jednostki istniejące jako nagłówki. Liczą się więc: kompletny
tekst w oryginalnym brzmieniu, właściwa kolejność czytania, nagłówki na granicach jednostek redakcyjnych i brak
fałszywych tabel.

Część regulaminów dla firm ma inny skład niż regulaminy detaliczne, na których parser był dotąd dostrajany:

- **Etykiety w wysuniętej kolumnie.** Ustępy („1.”, „2.”), punkty („1/”, „2/”) i litery („a/”, „b/”) mają etykietę w
  osobnej, wąskiej kolumnie po lewej, a tekst zaczyna się wyraźnie dalej w prawo; kolejne wiersze tego samego punktu
  zaczynają się w kolumnie tekstu, nie pod etykietą. Etykiety zagnieżdżają się: punkty „1/” pod ustępem „2.”, litery
  „a/” pod punktem.
- **Paragrafy „§ N”** jako osobny, wyśrodkowany, pogrubiony wiersz (czasem z tytułem: „§ 3. Porady ogólne”) pod
  numerowanym rozdziałem („2. Rachunki bankowe oraz rachunek VAT”).
- **Słowniczek** („§ 2. To jest spis określeń…”): numerowany termin pogrubiony w lewej kolumnie („1/ administrator
  (kontroler)”), wielowierszowa definicja w prawej, termin wyrównany do środka definicji.

Obecny wynik dla takiego dokumentu (fragment, regulamin zintegrowanego rachunku bankowego):

```
### 2. Rachunki bankowe oraz rachunek VAT

**§ 5**

1\. \| Na podstawie umowy Klienci mogą otwierać rachunki bieżące i pomocnicze, w złotych i walutach obcych.

2\. \| Rachunki bieżące służą do:

1/ \| gromadzenia środków pieniężnych

5\. \| Dla rachunków bieżących i pomocniczych klienta w złotych prowadzimy rachunek VAT w złotych. Na wniosek Klienta możemy prowadzić więcej

niż jeden rachunek VAT powiązany z jego rachunkami bieżącymi lub pomocniczymi .
```

Etykieta i tekst są rozdzielone separatorem tabeli zastępczej, zagnieżdżenie znika, zawinięty wiersz staje się
osobnym akapitem, a „§ 5” nie jest nagłówkiem. W słowniczku definicje mieszają się z terminami.

**Pomiar wyjściowy** (15 pobranych dokumentów mBanku; obecny parser):

| Dokument | Stron | TBL001 | Wiersze z „ \| ” | Wiersze z etykietą „1/”, „a/” | „§ N” jako zwykły tekst |
|---|---|---|---|---|---|
| D-A: regulamin zintegrowanego rachunku bankowego | 49 | 132 | 1137 | 665 | 153 |
| D-B: regulamin stosowania polecenia zapłaty | 8 | 16 | 142 | 100 | 35 |
| D-C: regulamin usług gotówkowych | 23 | 58 | 552 | 303 | 121 |
| D-D: zasady współpracy (transakcje rynku finansowego) | 27 | 79 | 665 | 389 | część z tytułem |
| 11 pozostałych (5 detalicznych, 6 dla firm) | 1–46 | 0–7 | 0–82 | 0–16 | 0 |

## Clarifications

### Session 2026-10-10

- Zakres (decyzja właściciela): **(a)** — etykiety w wysuniętej kolumnie i paragrafy „§ N”. Spis treści z kropkami,
  numery stron „N/M” w tekście i fałszywe nagłówki z wierszy tabel są poza zakresem (US5, opcjonalnie).
- Biblioteka nie zna mBanku: reguły opisują układ strony, nie nazwy dokumentów ani wydawcę.
- Q: Jak zapisać w Markdownie definicję ze słowniczka (numerowany termin w lewej kolumnie, definicja w prawej)? → A:
  jeden element listy — etykieta, pogrubiony termin i od razu definicja (`- 1/ **administrator (kontroler)** osoba
  fizyczna, którą Klient wskazał…`), bez dopisanych znaków; wyliczenie z definicji zagnieżdżone pod elementem.
- Q: Czy na 11 pozostałych dokumentach mBanku dopuszczamy zmiany Markdown, jeśli miary się nie pogarszają? → A: 5
  dokumentów detalicznych (zestaw prezentacji): każda różnica wymaga akceptacji właściciela; 6 dokumentów dla firm:
  wystarczy, że żadna miara się nie pogarsza (różnice przegląda wykonawca i zapisuje w handoffie).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Paragrafy „§ N” jako jednostki redakcyjne (Priority: P1)

Jako twórca aplikacji RAG i generatora FAQ chcę, żeby każdy paragraf „§ N” regulaminu był nagłówkiem, bo fragmenty
dla RAG i źródła odpowiedzi FAQ opierają się na jednostkach redakcyjnych.

**Why this priority**: bez nagłówków „§ N” cały dokument dzieli się tylko na rozdziały po kilka stron; generator FAQ
odrzuca kandydatów wskazujących „§ 5.”, a chunker tworzy zbyt duże fragmenty bez klucza jednostki.

**Independent Test**: konwersja syntetycznej repliki strony z rozdziałem „2. …” i wyśrodkowanym pogrubionym „§ 5”
(oraz „§ 3. Porady ogólne”) daje nagłówki „§ 5” i „§ 3. Porady ogólne” o poziom niżej niż rozdział.

**Acceptance Scenarios**:

1. **Given** strona z numerowanym rozdziałem „2. Rachunki bankowe oraz rachunek VAT” i pod nim wyśrodkowanym,
   pogrubionym, samodzielnym wierszem „§ 5”, **When** dokument jest konwertowany, **Then** „§ 5” jest nagłówkiem
   jednostki o jeden poziom niżej niż rozdział, a treść paragrafu należy do niego.
2. **Given** wiersz „§ 3. Porady ogólne” w tym samym stylu, **When** konwersja, **Then** nagłówkiem jest cały wiersz
   (oznaczenie i tytuł), a oznaczeniem jednostki jest „§ 3”.
3. **Given** odwołanie do paragrafu w tekście ciągłym („zgodnie z § 5 ust. 2”), **When** konwersja, **Then** nie
   powstaje nagłówek.
4. **Given** dokument, w którym „§ N” już są nagłówkami (akty prawne, regulaminy z goldenów), **When** konwersja,
   **Then** wynik się nie zmienia.

---

### User Story 2 - Etykiety w wysuniętej kolumnie jako listy (Priority: P1)

Jako czytelnik Markdown i chunker chcę, żeby ustępy, punkty i litery z etykietą w wysuniętej kolumnie były
elementami list z zachowanym zagnieżdżeniem i pełnym tekstem w jednym elemencie, a nie tabelami zastępczymi.

**Why this priority**: to główne źródło ostrzeżeń TBL001 i nieczytelnej treści w czterech dokumentach; bez tego
tekst punktu rozpada się na wiersze, a zagnieżdżenie (wyliczenie pod ustępem) ginie.

**Independent Test**: syntetyczna replika strony z ustępami „1.”, „2.” w wysuniętej kolumnie, punktami „1/”, „2/” i
literami „a/”, „b/” oraz tekstem zawijanym na 2–3 wiersze daje zagnieżdżoną listę Markdown z ucieczką etykiet,
bez separatora „ \| ” i bez ostrzeżenia TBL001.

**Acceptance Scenarios**:

1. **Given** ustęp „2.” z etykietą w wysuniętej kolumnie i tekstem „Rachunki bieżące służą do:”, a pod nim punkty
   „1/”, „2/” z etykietą w kolumnie dalej w prawo, **When** konwersja, **Then** powstaje element listy „2\.” z
   zagnieżdżonymi elementami „1/”, „2/”, w kolejności strony.
2. **Given** tekst punktu zawinięty na trzy wiersze, z drugim i trzecim wierszem w kolumnie tekstu, **When**
   konwersja, **Then** cały tekst jest jednym elementem listy (wiersze scalone spacją, bez podziału akapitu).
3. **Given** litery „a/”, „b/” pod punktem „1/”, **When** konwersja, **Then** są zagnieżdżone pod tym punktem.
4. **Given** prawdziwa tabela danych z siatką lub z kilkoma kolumnami tekstu (taryfa, zestawienie), **When**
   konwersja, **Then** pozostaje tabelą jak dotąd.
5. **Given** element listy zaczynający się etykietą, **When** renderowanie, **Then** etykieta jest zapisana dosłownie
   (ucieczka znaków jak w kontrakcie Markdown, np. `- 2\.`, `- 1/`), bez dopisanych słów.

---

### User Story 3 - Słowniczek z terminem w lewej kolumnie (Priority: P2)

Jako czytelnik chcę, żeby każda definicja słowniczka była jednym elementem: numer i termin, a po nim cała definicja,
w kolejności czytania.

**Why this priority**: słowniczek jest krótki (1–3 strony na dokument), ale dziś termin i definicja się mieszają;
generator FAQ i RAG często pytają o pojęcia. Ważne, ale mniej niż US1–US2.

**Independent Test**: replika strony słowniczka (pogrubiony termin „1/ administrator (kontroler)” po lewej, wyrównany
do środka wielowierszowej definicji po prawej) daje element listy „1/ **administrator (kontroler)**” z pełną
definicją, bez przeplotu z sąsiednimi definicjami.

**Acceptance Scenarios**:

1. **Given** termin w lewej kolumnie wyrównany do środka definicji z prawej (definicja zaczyna się wyżej niż termin),
   **When** konwersja, **Then** element zaczyna się terminem, a definicja jest w całości po nim.
2. **Given** definicja z własnym wyliczeniem „a/ … b/ … c/”, **When** konwersja, **Then** wyliczenie jest
   zagnieżdżone w elemencie definicji.

---

### User Story 4 - Bez regresji na pozostałych dokumentach (Priority: P1)

Jako właściciel chcę mieć pewność, że zmiany nie psują dokumentów, które dziś konwertują się dobrze.

**Why this priority**: zestaw detaliczny jest podstawą prezentacji; regresja byłaby gorsza niż brak poprawki.

**Independent Test**: goldeny parsera, prywatny korpus z 15 dokumentami mBanku (z goldenami obecnego wyniku jako
punkt odniesienia) i `verify` korpusu syntetycznego.

**Acceptance Scenarios**:

1. **Given** goldeny parsera i korpus syntetyczny, **When** pełne testy, **Then** bez zmian (albo zmiany pokazane
   właścicielowi i przez niego zatwierdzone).
2. **Given** 5 dokumentów detalicznych (zestaw prezentacji), **When** konwersja, **Then** Markdown jest identyczny z
   punktem odniesienia albo każdą różnicę zaakceptował właściciel.
3. **Given** 6 pozostałych dokumentów dla firm spoza D-A…D-D, **When** konwersja, **Then** żadna miara z tabeli
   pomiaru nie pogarsza się, a różnice w Markdown są przejrzane i zapisane w handoffie.

---

### User Story 5 - Drobne artefakty (Priority: P3, opcjonalnie, poza zakresem (a))

Spis treści z kropkami prowadzącymi nie tworzy nagłówków (D-B: 9); numery stron „N/M” nie zostają w tekście
(regulamin rachunków detalicznych: 35); wiersz nazw kolumn tabeli nie staje się nagłówkiem. Realizowane tylko, jeśli
zostanie czas przed zamrożeniem (patrz Założenia).

### Edge Cases

- Etykieta bez tekstu w tym samym wierszu (tekst zaczyna się wierszem niżej) — nadal jeden element listy.
- Etykieta „1.” w wysuniętej kolumnie na początku strony po przeniesieniu (kontynuacja punktu z poprzedniej strony
  bez etykiety) — kontynuacja dołącza do elementu z poprzedniej strony.
- Mieszanie stylów etykiet w jednym dokumencie („1.”, „1)”, „1/”) — każdy styl rozpoznawany, poziomy według
  wcięcia kolumny etykiety.
- „§ N” jako pierwszy wiersz strony albo bez rozdziału nad nim — nagłówek na poziomie jednostki według zasad
  poziomów (bez luk).
- Dwa paragrafy z tym samym numerem (np. załącznik ze swoją numeracją) — oba są nagłówkami; unikalność kluczy
  zapewnia chunker (ścieżka segmentów).
- Pogrubiony wiersz zaczynający się od „§” w środku akapitu (cytat przepisu) — nie jest nagłówkiem.
- Tabela danych, której pierwsza kolumna zawiera krótkie numery („1.”, „2.”) — pozostaje tabelą, jeśli ma więcej niż
  dwie kolumny tekstu, siatkę linii albo wiersze bez struktury etykieta–tekst.

## Requirements *(mandatory)*

### Functional Requirements

**Paragrafy**

- **FR-500**: Samodzielny wiersz zaczynający się oznaczeniem „§ N” (opcjonalnie z kropką i tytułem), wyróżniony
  pogrubieniem lub wyśrodkowaniem i oddzielony od tekstu ciągłego, MUSI być nagłówkiem jednostki redakcyjnej z
  oznaczeniem „§ N”.
- **FR-501**: Poziom nagłówka „§ N” MUSI wynikać z istniejących zasad poziomów (jednostki o poziom niżej niż
  najgłębszy rodzaj strukturalny, np. numerowany rozdział), bez luk w hierarchii (kontrakt Markdown).
- **FR-502**: Odwołania do paragrafów w tekście ciągłym i pogrubione fragmenty w środku akapitu NIE MOGĄ stać się
  nagłówkami.

**Etykiety w wysuniętej kolumnie**

- **FR-510**: Wiersz zaczynający się etykietą ustępu, punktu lub litery („1.”, „1)”, „1/”, „a)”, „a/”, także
  pogrubioną), po której tekst zaczyna się w wyraźnie dalszej kolumnie, MUSI rozpoczynać element listy, a nie
  komórkę tabeli.
- **FR-511**: Kolejne wiersze zaczynające się w kolumnie tekstu tego elementu (bez etykiety) MUSZĄ należeć do tego
  samego elementu listy; podział strony nie przerywa elementu.
- **FR-512**: Poziom zagnieżdżenia MUSI wynikać z położenia kolumny etykiet (etykiety dalej w prawo = głębiej) i ze
  zmiany stylu etykiety pod elementem nadrzędnym (punkty „1/” pod ustępem „2.”, litery „a/” pod punktem).
- **FR-513**: Etykiety MUSZĄ być zapisane dosłownie, z ucieczką wymaganą przez kontrakt Markdown; NIE WOLNO dopisywać
  żadnych słów (zasada wierności źródłu).
- **FR-514**: Rozpoznanie MUSI dotyczyć tylko obszarów o strukturze „etykieta + tekst” (dwie kolumny, z których
  lewa zawiera wyłącznie etykiety); tabele z siatką, tabele o więcej niż dwóch kolumnach treści i tabele-dokumenty
  (spec 002) MUSZĄ być wykrywane jak dotąd.
- **FR-515**: Dla takich obszarów NIE MOŻE powstawać ostrzeżenie TBL001.

**Słowniczek**

- **FR-520**: W obszarze, w którym lewa kolumna zawiera numerowany, pogrubiony termin, a prawa wielowierszową
  definicję, każdy termin z definicją MUSI być jednym elementem listy w jednym wierszu Markdown: etykieta, termin
  (pogrubiony jak w źródle), spacja i pełna definicja — np. `- 1/ **administrator (kontroler)** osoba fizyczna, którą
  Klient wskazał…` — bez dopisanych znaków (np. „–”); definicja zaczynająca się wyżej niż termin nie może zostać
  rozdzielona.
- **FR-521**: Wyliczenia wewnątrz definicji MUSZĄ być zagnieżdżone pod jej elementem.

**Ogólność i regresja**

- **FR-530**: Reguły MUSZĄ opierać się wyłącznie na geometrii i typografii strony (położenie, odstępy, pogrubienie,
  wyrównanie, wzorce etykiet), nie na nazwie dokumentu, wydawcy ani adresie.
- **FR-531**: Zmiana goldenów parsera wymaga zgody właściciela (FR-163); każda zmiana jest pokazana jako różnica i
  zapisana w handoffie.
- **FR-532**: Korpus syntetyczny po `refresh` MUSI przejść `verify`; zmiany jego Markdown i fragmentów wymagają
  przejrzenia i commita jak dotąd.
- **FR-533**: Prawdziwe dokumenty banku NIE MOGĄ trafić do repozytorium; testy jednostkowe i integracyjne używają
  syntetycznych replik stron, a prawdziwe PDF-y są sprawdzane w prywatnym korpusie właściciela.

**Opcjonalnie (US5)**

- **FR-540**: Wiersze spisu treści z kropkami prowadzącymi i numerem strony nie są nagłówkami.
- **FR-541**: Numery stron w formacie „N/M” w stopce nie zostają w tekście.

### Key Entities

- **Obszar etykieta–tekst**: część strony z wąską kolumną etykiet i szeroką kolumną tekstu; dla każdej etykiety
  element listy z poziomem zagnieżdżenia i scalonym tekstem.
- **Jednostka „§ N”**: nagłówek z oznaczeniem paragrafu i opcjonalnym tytułem, rodzic ustępów i punktów pod nim.
- **Definicja słowniczka**: etykieta, termin, tekst definicji, opcjonalne zagnieżdżone wyliczenie.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-080**: W dokumentach D-A…D-D liczba ostrzeżeń TBL001 i wierszy z separatorem „ \| ” spada do najwyżej 10%
  wartości z pomiaru wyjściowego (D-A: ≤ 13 i ≤ 114; D-B: ≤ 2 i ≤ 14; D-C: ≤ 6 i ≤ 55; D-D: ≤ 8 i ≤ 67).
- **SC-081**: W D-A…D-D każdy samodzielny wiersz „§ N” jest nagłówkiem (0 wierszy „§ N” jako zwykły tekst; dziś 153,
  35, 121 i część w D-D).
- **SC-082**: W D-A…D-D żaden element zaczynający się etykietą „1/”, „a/”, „1.” nie jest rozdzielony od swojego
  tekstu, a zawinięte wiersze nie tworzą osobnych akapitów (sprawdzone na próbie co najmniej 20 punktów na dokument).
- **SC-083**: Przebieg generatora FAQ na D-A…D-D nie odrzuca żadnego kandydata z powodu nieznanej jednostki „§ N”
  (dziś: m.in. „§ 5.”, „§ 7.”, „§ 10.” w D-D).
- **SC-084**: Goldeny parsera, prywatny korpus właściciela i `verify` korpusu syntetycznego przechodzą bez zmian albo
  z różnicami zatwierdzonymi przez właściciela.
- **SC-085**: W 5 dokumentach detalicznych Markdown jest identyczny z punktem odniesienia albo różnice są zaakceptowane
  przez właściciela; w 6 pozostałych dokumentach dla firm żadna miara z tabeli pomiaru wyjściowego nie rośnie.
- **SC-086**: (US3) W słowniczkach D-A…D-D każda definicja jest jednym elementem zaczynającym się swoim terminem.

## Assumptions

- Harmonogram: specyfikacja i plan gotowe przed konsultacjami (dzień 3); implementacja do dnia 8 w kolejności US1,
  US2, US4 (ciągle), US3, US5; dni 9–10 zamrożenie zmian parsera przed prezentacją. Co nie zdąży, zostaje opisane
  jako następny krok.
- Prywatny korpus właściciela zostaje rozszerzony o 15 pobranych dokumentów (5 detalicznych, 10 dla firm) z
  goldenami obecnego wyniku; zmiany tych goldenów w ramach tej specyfikacji są oczekiwane i przeglądane z
  właścicielem.
- Pomiar wyjściowy i końcowy liczy ten sam skrypt (TBL001 z raportu, wiersze z „ \| ”, wiersze z etykietą na
  początku, „§ N” jako zwykły tekst, nagłówki spisu treści, numery „N/M”).
- Kolejność czytania, wykrywanie tabel i list, poziomy nagłówków oraz kontrakt Markdown z 001/002 pozostają
  podstawą; zmiany je rozszerzają, nie zastępują.
- Tekst dokumentu nie zawiera informacji o etykietach poza wyglądem strony (brak struktury logicznej PDF), więc
  rozpoznanie opiera się na geometrii i typografii.
