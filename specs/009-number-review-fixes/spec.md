# Feature Specification: Poprawki parsera po przeglądzie liczb w regulaminach (indeks górny po „art.”, przeplecione stopki, stała kontrola liczb)

**Feature Branch**: `009-number-review-fixes`

**Created**: 2026-10-11

**Status**: Draft

**Input**: User description: "Poprawki parsera po przeglądzie liczb w regulaminach (spec 009). Kontekst: po spec 008 właściciel uruchomił generator FAQ dla trzech zestawów prywatnych regulaminów mBanku (korporacje, klienci indywidualni, firmy) i wyłączył z zestawów dokumenty niebędące regulaminami (firm-1, corp-4). Przegląd liczb na 15 regulaminach (handoff spec 008, wpis „przegląd liczb”) znalazł dwie usterki. Cel: Markdown regulaminów bez zmienionych liczb i bez tekstu przemieszanego literami, oraz stała kontrola liczb w testach. 1. (P1) Indeks górny po „art.” pisanym małą literą (corp-5 s. 8: „art. 66¹ … art. 68²” → „art. 661 … art. 682”). 2. (P2) Stopka z danymi banku przemieszana literami (corp-2, corp-6); najpierw research, możliwy wynik: „Znane ograniczenia” — decyzja w clarify. 3. (P1) Stała kontrola liczb w `LayoutMetrics` i raporcie `PrivateCorpusTests`; test pada, gdy regulamin z listy ma liczby nadmiarowe. Poza zakresem: tabela limitów firm-1, przypisy w indeksie górnym przed liczbą, wiersze strony tytułowej jako nagłówki, tytuł corp-4. Goldeny corp-*/firm-* wykonawca może aktualizować przy czystych miarach i zbiorach słów (oczekiwane: corp-5, corp-2, corp-6); każda zmiana ind-*, reg*, Corpus/acts, Corpus/banking, goldenów chunków i korpusu syntetycznego — stop i pytanie. Wersja 1.2.0 → 1.2.1, refresh/verify, README, CLAUDE.md, handoff, publish/."

## Kontekst

Feature poprawia bibliotekę `LegalAgent.PdfParser` po specyfikacji 008 (numeracja wymagań jest kontynuowana od
FR-650 i SC-100). Markdown biblioteki zasila chunker RAG (spec 004) i generator FAQ (spec 006); model cytuje tekst
jednostek dosłownie, więc zmieniona liczba w Markdownie (inny numer artykułu, inna kwota) staje się błędną
odpowiedzią FAQ, a przemieszany tekst — cytatem bez sensu.

Po spec 008 właściciel uruchomił generator FAQ dla trzech zestawów prywatnych regulaminów (korporacje, klienci
indywidualni, firmy). Z zestawów wyłączył dokumenty niebędące regulaminami: tabelę „Limity dla płatności z rachunków
dla firm” (firm-1) i formularz wniosku (corp-4). Przegląd liczb na 15 regulaminach (warstwa tekstowa PDF kontra
Markdown bieżącego parsera; opis w handoffie `specs/008-parser-fixes-after-007/plan.md`, wpis „przegląd liczb”)
pokazał, że brakujące liczby to wyłącznie usunięte numery stron „N/M”, i znalazł dwie usterki:

1. **Indeks górny po „art.” małą literą** — w corp-5 („Zasady współpracy w zakresie transakcji rynku finansowego”,
   s. 8) tekst „art. 66¹ § 1 do § 3 oraz art. 68² Kodeksu cywilnego” wychodzi jako „art. 661 … art. 682”, czyli
   odwołanie do innych artykułów. Cyfry ¹ i ² mają w PDF ok. 4 pt przy 8 pt tekstu i są podniesione. Zamiana małej
   podniesionej cyfry na znak indeksu górnego działa dziś tylko po „Art.” wielką literą i po „§” — „§ 7¹” w corp-1
   wychodzi poprawnie.
2. **Stopka z danymi banku przemieszana literami** — na ostatniej stronie regulaminów korporacyjnych (szablon:
   „mBank S.A., ul. Prosta 18 … kapitale zakładowym, którego wysokość wg stanu na dzień 01.01.2022 r. wynosi …”) dwa
   nakładające się wiersze tekstu i logo „mBank.pl” są składane w jeden wiersz z przeplecionymi literami: corp-2
   „G0p1oo.0ds1pa.2tok0do2aw2r cerz.j y” (data „01.01.2022 r.” z „Gospodarczej”), corp-6
   „wXidIgeII nsttyafinkua ncjai” („XIII Wydział” z „identyfikacji”). Research (Clarifications) pokazał tę samą
   stopkę także w corp-1, corp-3 i corp-5 („wXIgII” = „wg” + „XIII”). Kwota kapitału i treść regulaminu są
   nienaruszone.

Przegląd był jednorazową sondą poza testami. Trzecim celem jest stała kontrola liczb w testach, żeby kolejne takie
błędy wychodziły w testach, a nie w odpowiedziach FAQ.

**Pomiar wyjściowy** (z przeglądu liczb; dokładne wartości ustala pierwszy krok implementacji nową miarą):

| Usterka | Dokument | Stan dziś |
|---|---|---|
| 1. Indeks górny sklejony z numerem artykułu | corp-5 / pozostałe regulaminy | 2 („661”, „682”) / 0 |
| 1. Indeks górny po „§” i „Art.” | corp-1 („§ 7¹”) | poprawny (bez zmian) |
| 2. Wiersze przeplecione literami | corp-1 / corp-2 / corp-3 / corp-5 / corp-6 / pozostałe 16 dokumentów | 1 / 1 / 1 / 1 / 1 / 0 (research) |
| 3. Liczby nadmiarowe w Markdownie (znane) | corp-5 / firm-1 (poza listą regulaminów) | 661, 682 / 150.000, 230.000, 320.000, 450.000 |
| 3. Liczby brakujące poza numerami stron | 15 regulaminów | 0 |

## Clarifications

### Session 2026-10-11

- Research usterki 2 (przed pytaniem): w PDF stopka to cztery czyste wiersze 6,7 pt (linie bazowe co 7 pt), logo
  „mBank.pl” 22,7 pt po lewej, na linii bazowej ostatniego wiersza; drobne litery stopki (0,3 rozmiaru logo) dołączają
  do wiersza logo jako znaki indeksu (pionowe nakładanie w zasięgu kilku em logo), więc pierwsze słowa wierszy 2–4 są
  przeplatane. Ta sama stopka jest w corp-1, corp-2, corp-3, corp-5 i corp-6 („wXIgII” = „wg” + „XIII” w corp-1, 3, 5);
  w ind-*, reg*, regulamin-pdp i firm-* brak (tylko glify strzałek w schematach firm-2, 5, 6, obsłużone w spec 008).
- Q: Czy usterkę 2 naprawiamy w kodzie, czy tylko opisujemy w „Znanych ograniczeniach”? → A: Naprawa w kodzie dla
  pięciu dokumentów corp (corp-1, 2, 3, 5, 6); jeśli próba złamie kryterium ryzyka FR-661 — powrót do opisu w
  „Znanych ograniczeniach”.

### Session 2026-10-11 (plan)

- Research P1 (skan wszystkich PDF-ów): ten sam błąd („art. N” + mała podniesiona cyfra sklejona z numerem albo
  wzięta za odnośnik przypisu) jest w aktach — `Corpus/acts` (prawa-konsumenta 4, kredyt-konsumencki 1,
  prawo-bankowe 7, usługi-płatnicze 3 miejsca; np. „art. 3855, art. 5564, art. 556[^2] i art. 576[^2]” zamiast
  „art. 385⁵, art. 556⁴, art. 556⁵, art. 576⁵”), w korpusie syntetycznym `corpus/akty` (7 aktów, Markdown i fragmenty)
  i w goldenie fragmentów `dz-u-2019-1781-ochrona-danych` (8 miejsc).
- Q: Jak postępować ze zmianami goldenów aktów, goldenu fragmentów i korpusu syntetycznego wynikającymi z P1? → A:
  Zgoda z góry, wąska: wykonawca commituje różnice, jeśli są wyłącznie zamianą cyfr po „art.” na znaki indeksu
  górnego (wraz ze zniknięciem fałszywych odnośników `[^n]` w tych miejscach i wynikającą z tego zmianą numeracji
  przypisów oraz pól fragmentów zależnych od tekstu); diff opisuje w handoffie. Każda inna różnica w tych plikach —
  stop i pytanie do właściciela.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Numery artykułów z indeksem górnym zostają poprawne (Priority: P1)

Jako twórca aplikacji RAG i generatora FAQ chcę, żeby odwołanie „art. 66¹” zostało w Markdownie „art. 66¹”, a nie
„art. 661”, bo model cytuje i streszcza te odwołania, a zmieniony numer wskazuje inny przepis.

**Why this priority**: usterka zmienia sens tekstu prawnego w regulaminie z zestawu FAQ; poprawka jest wąska i dobrze
odgraniczona.

**Independent Test**: syntetyczna replika strony (bez nazw prawdziwych banków) z tekstem 8 pt zawierającym
„art. 66¹ § 1 do § 3 oraz art. 68²”, „Art. 5²”, „§ 7¹” (cyfry indeksu ok. 4 pt, podniesione), odnośnikiem przypisu
przyklejonym do słowa („umowy³”) i zwykłymi liczbami („66 dni”, „art. 66 ust. 1”) daje Markdown z „art. 66¹”,
„art. 68²”, „Art. 5²”, „§ 7¹”, przypisem jak dotąd i nietkniętymi zwykłymi liczbami; test nie przechodzi na kodzie
sprzed poprawki.

**Acceptance Scenarios**:

1. **Given** oznaczenie „art.” (małą literą) z numerem, po którym stoi cyfra wyraźnie mniejsza od tekstu i
   podniesiona względem linii bazowej, **When** konwersja, **Then** cyfra jest zapisana znakiem indeksu górnego
   („art. 66¹”), a nie doklejona do numeru.
2. **Given** to samo po „Art.” i po „§”, **When** konwersja, **Then** wynik jak dotąd („Art. 5²”, „§ 7¹”).
3. **Given** odnośnik przypisu przyklejony do słowa (rozpoznawany przez wykrywanie przypisów), **When** konwersja,
   **Then** jest renderowany jak dotąd (`[^n]`), a nie jako znak indeksu górnego.
4. **Given** liczba bez oznaczenia jednostki przed nią albo cyfra tej samej wielkości co tekst („art. 66 ust. 1”,
   „66 dni”), **When** konwersja, **Then** tekst bez zmian.

---

### User Story 2 - Stała kontrola liczb w testach (Priority: P1)

Jako właściciel chcę, żeby testy porównywały liczby z warstwy tekstowej PDF z liczbami w Markdownie i padały, gdy
regulamin z zestawów FAQ ma liczby zmienione lub sklejone, bo przegląd ręczny jest jednorazowy, a kolejna regresja
parsera wyszłaby dopiero w odpowiedziach FAQ.

**Why this priority**: zabezpiecza cel całego feature'u na przyszłość; bez miary nie da się też wykazać, że US1 i US3
niczego nie popsuły.

**Independent Test**: miara uruchomiona na prywatnym korpusie przed poprawką US1 wykazuje liczby nadmiarowe „661” i
„682” w corp-5 oraz „150.000”, „230.000”, „320.000”, „450.000” w firm-1; po poprawce US1 corp-5 ma 0 liczb
nadmiarowych, test prywatnego korpusu przechodzi (firm-1 nie jest na liście regulaminów), a dopisanie zmienionej
liczby do Markdownu dokumentu z listy (np. w teście miary na syntetycznym przykładzie) powoduje porażkę z czytelnym
komunikatem.

**Acceptance Scenarios**:

1. **Given** dokument i jego Markdown, **When** liczona jest miara liczb, **Then** raport podaje dla dokumentu liczby
   nadmiarowe w Markdownie (występujące w nim częściej niż w warstwie tekstowej PDF) i liczby brakujące (występujące w
   PDF częściej niż w Markdownie), z krotnościami.
2. **Given** liczba z PDF usunięta jako artefakt strony (numer strony, powtarzalna stopka) zgodnie z raportem
   usuniętych artefaktów, **When** liczona jest miara, **Then** nie jest liczona jako brakująca.
3. **Given** poprawnie zapisany indeks górny („66¹” w Markdownie przy podniesionej małej cyfrze w PDF), odnośnik
   przypisu `[^n]`, znacznik strony `<!-- page: N -->` i etykieta listy z ucieczką (`- 1\)`), **When** liczona jest
   miara, **Then** żadne z nich nie daje liczby nadmiarowej ani brakującej.
4. **Given** liczba sklejona lub zmieniona („661” z „66¹”, „150.000” z „¹50.000”), **When** liczona jest miara,
   **Then** jest liczbą nadmiarową, a liczba z PDF, z której powstała — brakującą.
5. **Given** dokument wymieniony na liście regulaminów w katalogu prywatnym, **When** test prywatnego korpusu wykrywa
   w jego Markdownie co najmniej jedną liczbę nadmiarową, **Then** test pada i wymienia dokument oraz liczby;
   dokumenty spoza listy (firm-1, corp-4) są tylko raportowane.

---

### User Story 3 - Stopka z danymi banku bez przeplecionych liter (Priority: P2)

Jako czytelnik Markdownu i generator FAQ chcę, żeby nakładające się wiersze stopki z danymi banku zostały osobnymi,
ciągłymi wierszami tekstu, bo przeplecione litery („G0p1oo.0ds1pa…”) to śmieci w ostatnim fragmencie dokumentu, a
data i nazwa sądu stają się nieczytelne.

**Why this priority**: kosmetyka — kwota kapitału i treść regulaminu są zachowane, usterka dotyczy jednego akapitu
stopki w pięciu dokumentach corp; zmiana składania wierszy dotyka rdzenia parsera, więc obowiązuje kryterium ryzyka
FR-661 z powrotem do opisu ograniczenia.

**Independent Test**: syntetyczna replika ostatniej strony: cztery wiersze tekstu stopki małą czcionką (ok. 6,7 pt,
linie bazowe co 7 pt) i obok, po lewej, napis logo czcionką ponad trzy razy większą (ok. 22,7 pt) na linii bazowej
ostatniego wiersza; Markdown zawiera wszystkie cztery wiersze jako ciągły tekst, w kolejności jak w PDF, a logo jako
osobny fragment, bez przeplecionych liter. Test nie przechodzi na kodzie sprzed poprawki.

**Acceptance Scenarios**:

1. **Given** kilka wierszy drobnego tekstu, które leżą w pionowym zasięgu napisu znacznie większą czcionką obok
   (logo), **When** konwersja, **Then** każdy wiersz jest ciągłym tekstem (słowa „Gospodarczy”, „podatkowej”,
   „01.01.2022 r.”, „XIII Wydział”, „wg”, „identyfikacji” w całości i we własnych wierszach), kolejność wierszy jest
   jak w PDF, a napis logo jest osobnym fragmentem.
2. **Given** litery jednego wiersza z indeksami górnymi lub dolnymi, odnośnikami przypisów i drobnymi przesunięciami
   linii bazowej w obrębie wiersza (np. wyjustowany tekst, różne rozmiary czcionki), **When** konwersja, **Then** nie
   są rozdzielane na osobne wiersze — wynik jak dotąd.
3. **Given** próba implementacji złamała kryterium ryzyka (FR-661), **When** zakończenie feature'u, **Then**
   README parsera opisuje zjawisko w „Znanych ograniczeniach”, a kod składania wierszy się nie zmienia.

---

### User Story 4 - Bez regresji, z udokumentowanymi miarami i wydaniem (Priority: P1)

Jako właściciel chcę mieć pewność, że poprawki nie psują innych dokumentów, że żaden tekst nie ginie ani nie jest
dopisywany, a wydanie (wersja, korpus syntetyczny, dokumentacja, publish/) jest spójne.

**Why this priority**: zestawy FAQ i prezentacja opierają się na goldenach detalicznych i aktach prawnych; przegląd
goldenów kosztuje czas właściciela.

**Independent Test**: pełny zestaw kontroli przed każdym commitem parsera, porównanie zbiorów słów prywatnego
korpusu przed i po, tabela miar przed/po w handoffie, `verify` korpusu syntetycznego czysty z wersją 1.2.1.

**Acceptance Scenarios**:

1. **Given** goldeny `Corpus/banking` i prywatne goldeny ind-*, reg*, regulamin-pdp, **When** pełne testy,
   `refresh` i `verify`, **Then** bez zmian; `Corpus/acts`, golden fragmentów `dz-u-2019-1781-ochrona-danych` i
   `corpus/akty` zmieniają się wyłącznie w zakresie wąskiej zgody (FR-674); każda inna różnica zatrzymuje pracę do
   decyzji właściciela.
2. **Given** prywatny korpus, **When** porównanie zbiorów słów Markdownu przed i po, **Then** brak utraconych słów i
   brak słów spoza PDF; zmienione słowa to wyłącznie numery z indeksem górnym w corp-5 i rozplecione wiersze stopek
   corp-1, corp-2, corp-3, corp-5 i corp-6 (każde wymienione w handoffie).
3. **Given** zakończona implementacja, **When** właściciel czyta handoff, **Then** znajduje tabelę miar przed/po
   (liczby nadmiarowe i brakujące dla każdego dokumentu prywatnego, przeplecione wiersze), listę zmienionych goldenów
   z uzasadnieniem i wynik researchu usterki 2.

### Edge Cases

- „art.” na końcu wiersza, a numer z indeksem górnym na początku następnego — indeks rozpoznawany tak samo, jeśli
  numer i cyfra indeksu są w tym samym wierszu.
- Kilka cyfr indeksu górnego („art. 79¹⁰”) — wszystkie zapisane znakami indeksu górnego.
- Odmiany oznaczenia: „art.”, „Art.”, „§”, a także „ust.”, „pkt” z indeksem górnym — w zakresie tylko „art.”, „Art.”
  i „§” (patrz Założenia); inne oznaczenia zostają jak dziś, a miara liczb wykaże ewentualne sklejenia.
- Mała podniesiona cyfra po numerze artykułu, którą wykrywanie przypisów uznało za odnośnik przypisu (jest przypis o
  tym numerze na stronie) — rozstrzyga dotychczasowa reguła przypisów; zmiana nie może zmienić żadnego przypisu w
  goldenach.
- Liczby w Markdownie w innej postaci niż w PDF wyłącznie przez formatowanie (pogrubienie `**150**`, ucieczka `1\.`,
  komórka tabeli) — ta sama liczba.
- Liczby z separatorami: „150.000”, „1 000,00”, „01.01.2022”, „12/2024”, „3,5%” — tokenizacja jednakowa po obu
  stronach; spacja jako separator tysięcy jest rozstrzygana w planie, tak by nie dawała fałszywych alarmów (miara
  porównuje wielozbiory, więc rozbicie „1 000” na „1” i „000” po obu stronach nie jest błędem).
- Tekst powtórzony w PDF (np. ten sam znak na dwóch warstwach, pogrubienie nadrukiem) albo litery poza obszarem
  strony — miara czyta warstwę tekstową PDF bezpośrednio, niezależnie od parsera (żeby nie powielać jego błędów);
  duplikaty dają co najwyżej liczby brakujące, które nie powodują porażki testu, a są klasyfikowane w handoffie.
- Dokument bez warstwy tekstowej (skan) — miara pomija dokument z adnotacją w raporcie.
- Ten sam układ (napis dużą czcionką obok kilku wierszy drobnego tekstu) na stronie innej niż ostatnia albo w innym
  dokumencie — reguła nie zależy od numeru strony ani dokumentu; research nie znalazł go poza pięcioma stopkami corp.
- Glify strzałek dużą czcionką obok tekstu w schematach kroków (firm-2, firm-5, firm-6) — mają ten sam stosunek
  rozmiarów; poprawka nie może pogorszyć ich obsługi ze spec 008 (zmiany goldenów firm-* tylko według FR-674).
- Logo „mBank.pl” zapisane tekstem w obszarze stopki — zostaje osobnym fragmentem tekstu, nie wplata się w wiersze.

## Requirements *(mandatory)*

### Functional Requirements

**Indeks górny po oznaczeniu jednostki**

- **FR-650**: Cyfra (lub ciąg cyfr) stojąca bezpośrednio za numerem po oznaczeniu „art.” lub „Art.” albo „§”,
  wyraźnie mniejsza od tekstu wiersza i podniesiona względem jego linii bazowej, MUSI być zapisana znakami indeksu
  górnego („art. 66¹”, „art. 68²”, „Art. 5²”, „§ 7¹”), a nie doklejona do numeru.
- **FR-651**: Odnośniki przypisów rozpoznawane przez wykrywanie przypisów MUSZĄ być renderowane jak dotąd; cyfry tej
  samej wielkości co tekst i liczby bez oznaczenia jednostki przed nimi NIE MOGĄ być zmieniane.
- **FR-652**: Zachowanie dla „Art.” i „§” MUSI pozostać takie jak dotąd; zmieniają się wyłącznie miejsca „art. N” z
  małą podniesioną cyfrą (corp-5, akty w `Corpus/acts` i `corpus/akty` — FR-674).

**Stała kontrola liczb**

- **FR-655**: Pomocnik miar testów MUSI liczyć dla dokumentu porównanie wielozbioru liczb z warstwy tekstowej PDF z
  wielozbiorem liczb w Markdownie i zwracać liczby nadmiarowe w Markdownie oraz brakujące, z krotnościami. Liczba to
  ciąg cyfr, także z separatorami między cyframi (kropka, przecinek), tokenizowany jednakowo po obu stronach.
- **FR-656**: Cyfry zapisane w PDF jako mniejsze i podniesione (indeks górny) MUSZĄ być po stronie PDF oddzielone od
  liczby, przy której stoją, a po stronie Markdownu znaki indeksu górnego nie są cyframi liczby — tak, by poprawny
  zapis „66¹” nie dawał rozbieżności, a sklejenie „661” dawało liczbę nadmiarową. Odnośniki przypisów `[^n]`, znaczniki
  stron i inne elementy dodawane przez renderowanie (nie tekst PDF) NIE MOGĄ być liczone jako liczby Markdownu;
  odpowiadające im cyfry odnośników w PDF nie są liczone jako brakujące.
- **FR-657**: Liczby z tekstu usuniętego jako artefakt strony (według raportu usuniętych artefaktów) NIE MOGĄ być
  liczone jako brakujące.
- **FR-658**: Raport prywatnego korpusu MUSI zawierać tę miarę dla każdego dokumentu (liczba nadmiarowych i
  brakujących oraz ich wartości). Test prywatnego korpusu MUSI padać, gdy dokument wymieniony na liście regulaminów
  (plik w katalogu prywatnym, w formacie jak lista układów etykiet) ma co najmniej jedną liczbę nadmiarową; komunikat
  wymienia dokument i liczby. Dokumenty spoza listy są tylko raportowane. Początkowa lista obejmuje 15 regulaminów z
  przeglądu liczb (zestawy FAQ): corp-1, corp-2, corp-3, corp-5, corp-6, ind-1…5, firm-2…6; firm-1 i corp-4 nie są na
  liście, a reg1–3 i regulamin-pdp (poza zestawami FAQ) są tylko raportowane.
- **FR-659**: Miara MUSI wykrywać przypadki znane z przeglądu: przed poprawką US1 — „661” i „682” w corp-5; w firm-1 —
  „150.000”, „230.000”, „320.000”, „450.000” jako liczby nadmiarowe. Działanie miary MUSI być też sprawdzone testem na
  syntetycznym dokumencie (bez korpusu prywatnego), w którym poprawny indeks górny daje 0 rozbieżności, a sklejony —
  liczbę nadmiarową.

**Przeplecione wiersze stopki**

- **FR-660**: Research (wykonany w clarify, szczegóły w planie) MUSI zostać zapisany w handoffie: geometria stopek
  (linie bazowe, rozmiary, kolejność w strumieniu treści, położenie logo) i lista wystąpień zjawiska w 21 dokumentach
  prywatnego korpusu — corp-1, corp-2, corp-3, corp-5, corp-6, w pozostałych brak.
- **FR-661**: Usterka 2 jest naprawiana w kodzie. Próba jest uznana za ryzykowną i wycofana, jeśli zmienia
  jakikolwiek golden poza stopkami pięciu dokumentów corp (poza dopuszczalnymi według FR-674 zmianami w firm-*, które
  nie pogarszają miar), rozbija na wiersze tekst z indeksami, odnośnikami przypisów lub różnymi rozmiarami czcionki w
  jednym wierszu, albo pogarsza miary `CorpusFull`. Wtedy wynikiem usterki 2 jest opis w „Znanych ograniczeniach”
  README parsera, a uzasadnienie trafia do handoffu.
- **FR-662**: Drobne litery tworzące własny wiersz tekstu (słowa na wspólnej linii bazowej, ciągnące się poza zasięg
  większego napisu obok) NIE MOGĄ być dołączane do wiersza napisu znacznie większą czcionką tylko dlatego, że leżą w
  jego pionowym zasięgu; każdy taki wiersz MUSI być ciągłym tekstem, w kolejności wierszy jak w PDF, a żadne słowo
  nie może być przeplecione z innym. Pojedyncze małe podniesione znaki (indeksy górne, odnośniki przypisów) MUSZĄ nadal
  dołączać do swojego wiersza jak dotąd.

**Wymagania wspólne**

- **FR-670**: Wynik MUSI zawierać wyłącznie tekst z PDF; poprawki wyrażają strukturę tylko formatowaniem i NIE WOLNO im
  dopisywać ani usuwać słów; zmienia się wyłącznie zapis cyfr indeksu górnego (FR-650) i kolejność liter w
  przeplecionych wierszach (FR-662).
- **FR-671**: Reguły MUSZĄ opierać się wyłącznie na geometrii, typografii i wzorcach tekstu, nie na nazwie dokumentu,
  wydawcy ani adresie.
- **FR-672**: Każda zmiana zachowania parsera MUSI być odtworzona syntetyczną repliką strony (bez nazw prawdziwych
  banków) najpierw jako test, który nie przechodzi na kodzie sprzed poprawki (osobny commit), a potem poprawką, z
  którą przechodzi (osobny commit). Dotyczy też nowej miary (test miary przed jej implementacją).
- **FR-673**: Przed każdym commitem zmieniającym parser MUSI przejść pełny zestaw kontroli jak w spec 007/008: testy
  solucji, prywatny korpus właściciela, `refresh` i `verify` korpusu syntetycznego oraz kategoria `CorpusFull`.
- **FR-674**: Goldeny prywatnych dokumentów dla firm (corp-1…6, firm-1…6) wykonawca MOŻE zaktualizować bez osobnej
  zgody, jeśli przejrzał różnice, żadna miara się nie pogarsza, a porównanie zbiorów słów jest czyste; oczekiwane
  zmiany tylko w corp-5 (US1) oraz stopkach corp-1, corp-2, corp-3, corp-5 i corp-6 (US3), każda opisana w handoffie.
  Zmiany `Corpus/acts`, goldenu fragmentów `dz-u-2019-1781-ochrona-danych` i `corpus/akty` wykonawca MOŻE
  zacommitować, jeśli są wyłącznie skutkiem FR-650 (cyfry po „art.” jako indeks górny, zniknięcie fałszywych
  odnośników `[^n]` w tych miejscach i wynikająca z tego numeracja przypisów oraz pola fragmentów zależne od tekstu);
  diff opisuje w handoffie. Każda inna zmiana goldenów ind-1…5, reg1–3, regulamin-pdp, `Corpus/acts`,
  `Corpus/banking`, goldenów fragmentów i korpusu syntetycznego MUSI zatrzymać pracę do decyzji właściciela (FR-163).
- **FR-675**: Postać wyjścia opisana w kontrakcie Markdown się nie zmienia (kontrakt 1.3.0 bez zmian); wersja
  wszystkich projektów MUSI zostać podbita z 1.2.0 do 1.2.1 (PATCH), a manifest korpusu syntetycznego odświeżony
  (`refresh`) tak, by `verify` był czysty.
- **FR-676**: README parsera MUSI opisać zmiany i w „Znanych ograniczeniach” sprawy poza zakresem: tabelę limitów
  firm-1 (4 stałe kolumny z komórkami wielowierszowymi na różnych liniach bazowych, TBL001, przeplatanie komórek),
  znaczniki przypisów w indeksie górnym przed liczbą („¹50.000” → „150.000”), podtytuł „obowiązują od …” jako
  nagłówek, wiersze strony tytułowej jako nagłówki („## Warszawa, Listopad 2020”, „## mBank.pl” w corp-2 i corp-6),
  niewykryty tytuł formularza corp-4 oraz — tylko jeśli poprawka zostanie wycofana według FR-661 — przeplecione
  wiersze stopki. CLAUDE.md i
  handoff („Stan prac i przekazanie” w `plan.md`) MUSZĄ zostać zaktualizowane, a publish/ przebudowany po zmianach.

### Key Entities

- **Indeks górny jednostki**: cyfry mniejsze i podniesione względem wiersza, stojące za numerem po „art.”, „Art.” lub
  „§”; część oznaczenia przepisu, zapisywana znakami indeksu górnego.
- **Wielozbiór liczb dokumentu**: liczby z warstwy tekstowej PDF (z oddzielonymi indeksami górnymi, bez artefaktów
  usuniętych według raportu) i liczby z Markdownu (bez elementów dodanych przez renderowanie), z krotnościami.
- **Liczba nadmiarowa / brakująca**: liczba występująca w Markdownie częściej niż w PDF (zmieniona, sklejona) /
  w PDF częściej niż w Markdownie (zgubiona, rozbita).
- **Lista regulaminów**: plik w katalogu prywatnego korpusu z dokumentami, dla których liczby nadmiarowe są błędem
  testu.
- **Przeplecione wiersze**: dwa wiersze tekstu o nakładających się obszarach i różnych liniach bazowych, których
  litery parser dziś składa w jeden wiersz.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-100**: W corp-5 Markdown zawiera „art. 66¹” i „art. 68²”, a liczba nadmiarowa „661”/„682” znika (2 → 0);
  w aktach (`Corpus/acts`, `corpus/akty`) wszystkie 15 + 33 miejsc „art. N” z małą podniesioną cyfrą ma znaki indeksu
  górnego (np. „art. 385⁵”, „art. 556⁵” zamiast „art. 3855”, „art. 556[^2]”); „§ 7¹” w corp-1 i dotychczasowe indeksy
  górne po „Art.” i „§” bez zmian; jedyne zmiany przypisów to zniknięcie fałszywych odnośników w tych miejscach.
- **SC-101**: Miara liczb na prywatnym korpusie: 0 liczb nadmiarowych we wszystkich 15 regulaminach z listy po
  poprawce US1; przed poprawką wykrywa „661”, „682” (corp-5); w firm-1 raportuje „150.000”, „230.000”, „320.000”,
  „450.000”; liczby brakujące poza artefaktami stron w regulaminach z listy są raportowane i każda sklasyfikowana w
  handoffie (np. etykieta definicji przypisu, duplikat warstwy tekstowej, rozbity wiersz); żadna nie jest skutkiem
  utraty tekstu.
- **SC-102**: Wprowadzenie zmienionej liczby do Markdownu dokumentu z listy (scenariusz testowy offline, bez korpusu
  prywatnego) daje porażkę testu
  z nazwą dokumentu i liczbą.
- **SC-103**: Wiersze z przeplecionymi literami w stopkach: 5 → 0 (corp-1, corp-2, corp-3, corp-5, corp-6), a w
  każdej stopce cztery wiersze tekstu w kolejności jak w PDF; jeśli poprawkę wycofano według FR-661: README opisuje
  zjawisko z przykładami, a goldeny stopek się nie zmieniają.
- **SC-104**: Goldeny ind-*, reg*, regulamin-pdp i `Corpus/banking` bez zmian; `Corpus/acts`, goldeny fragmentów i
  korpus syntetyczny zmienione wyłącznie według wąskiej zgody z FR-674 (albo za osobną zgodą właściciela); miary
  `CorpusFull` nie pogarszają się; `verify` czysty z wersją 1.2.1.
- **SC-105**: Porównanie zbiorów słów prywatnego korpusu przed i po: 0 utraconych słów, 0 słów spoza PDF; zmienione
  tokeny wyłącznie w corp-5 (indeks górny) i w stopkach corp-1, corp-2, corp-3, corp-5 i corp-6.
- **SC-106**: Handoff zawiera wynik researchu usterki 2, tabelę miar przed/po (liczby nadmiarowe i brakujące dla
  każdego dokumentu prywatnego, w którym miara jest niezerowa przed lub po; przeplecione wiersze) i listę zmienionych
  goldenów z uzasadnieniem.

## Assumptions

- Punktem odniesienia są goldeny po merge'u PR #11 (spec 008, wersja 1.2.0) i prywatny korpus 21 dokumentów
  (corp-1…6, firm-1…6, ind-1…5, reg1–3, regulamin-pdp; corp-6 i firm-6 z goldenami od 2026-10-11).
- Zakres indeksu górnego to oznaczenia „art.”, „Art.” i „§”; inne oznaczenia („ust.”, „pkt”) w regulaminach z
  przeglądu nie mają indeksów górnych, a ewentualne przyszłe przypadki wykaże miara liczb.
- Poprawki nie zmieniają postaci wyjścia opisanej w kontrakcie (znaki indeksu górnego już są w kontrakcie dla „Art.” i
  „§”), więc kontrakt zostaje 1.3.0, a wersja projektów rośnie jako PATCH do 1.2.1.
- Miara liczb jest narzędziem testów (pomocnik miar i raport prywatnego korpusu), nie częścią biblioteki ani raportu
  konwersji.
- Lista regulaminów jest plikiem prywatnym (jak lista układów etykiet), bo nazwy dokumentów prywatnych nie trafiają do
  repozytorium; bez katalogu prywatnego test jest pomijany jak dotąd.
- Kolejność realizacji: US2 (miara, by zmierzyć stan wyjściowy) i US1 (P1), potem US3 (P2); US4 obowiązuje przy
  każdym commicie parsera. Co nie zdąży, zostaje opisane w handoffie jako następny krok.
- Aplikacja RAG to demo (dostarczyć działający przebieg, bez wyczerpujących rund jakości); stąd US3 może skończyć się
  opisem ograniczenia, jeśli poprawka złamie FR-661.
