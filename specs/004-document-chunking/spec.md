# Feature Specification: Podział dokumentów na fragmenty dla demonstracyjnej aplikacji RAG

**Feature Branch**: `004-document-chunking`

**Created**: 2026-10-09

**Status**: Draft

**Input**: User description: "Podział dokumentów na fragmenty (chunki) dla demonstracyjnej aplikacji RAG. […] Nowa biblioteka `LegalAgent.Chunking` używana w serwisie .NET: zwraca wyłącznie modele w pamięci (dokument z metadanymi i lista fragmentów) […] Wejście: wynik parsera (`PdfConversionResult`) albo strumień PDF […] oraz metadane dokumentu od wywołującego […] dzieli model dokumentu (`LegalDocument`), nie Markdown; treść fragmentu renderuje istniejącym rendererem Markdown […] Granice: jedna jednostka (artykuł / § / sekcja tabeli-dokumentu / sekcja taryfy lub procedury); jednostka dłuższa niż limit dzielona między blokami — nigdy w środku pozycji listy ani wiersza tabeli; duże tabele dzielone po wierszach z powtórzonym nagłówkiem […] Metadane: metadane dokumentu, oznaczenie do cytatu, ścieżka sekcji, zakres stron, stabilny identyfikator fragmentu i klucz jednostki wspólny dla tej samej jednostki w różnych wersjach dokumentu. Serializacja modelu do JSON opisana kontraktem z wersją […] Polecenie CLI zapisujące fragmenty dokumentu jako JSONL. Generator korpusu zapisuje fragmenty każdego dokumentu obok Markdown […] Testy: jednostkowe podziału i metadanych, golden fragmentów dla kilku dokumentów korpusu, deterministyczność, oraz jeden test na manifeście […]"

## Kontekst

Feature rozszerza projekt ze specyfikacji 001 (parser), 002 (tabele-dokumenty) i 003 (korpus
syntetyczny). Numeracja wymagań jest kontynuowana: FR-200 i dalej, SC-040 i dalej. Odwołania do
FR-0xx dotyczą specyfikacji 001, do FR-08x/FR-09x — 002, do FR-1xx — 003.

Właściciel buduje osobną, demonstracyjną aplikację RAG, która odpowiada pracownikom fikcyjnego
banku „Bank Przykładowy S.A.” na pytania o regulaminy, taryfy opłat i procedury wewnętrzne, na
podstawie korpusu syntetycznego (`corpus/`) i publicznych aktów prawnych. Agent aplikacji wyszukuje
fragmenty dokumentów, cytuje źródło z numerem paragrafu, wskazuje sprzeczności i dokumenty
nieaktualne; frontend pokazuje tekst cytowanego fragmentu i otwiera PDF na właściwej stronie (bez
podświetlania). Backend aplikacji odpowiada za wersjonowanie i indeksowanie.

To repozytorium dostarcza **fragmenty z metadanymi**, które to umożliwiają: stabilne identyfikatory
do indeksowania, metadane wersji i klucz jednostki do porównań między wersjami, treść, oznaczenie do
cytatu i zakres stron do podglądu. Wyszukiwanie, embeddingi, baza wektorowa i generowanie odpowiedzi
są poza zakresem.

To **demo**: ma działać od początku do końca. Nie obejmuje rozbudowanych pomiarów jakości podziału
ani kontroli spójności numeracji jednostek. Znane błędy rozpoznania struktury przez parser są
akceptowanym ryzykiem: podział przyjmuje strukturę z modelu dokumentu taką, jaka jest, a ewentualne
błędy struktury poprawia się w parserze, nie w podziale.

## Clarifications

### Session 2026-10-09

- Q: Jaki ma być domyślny limit długości fragmentu (znaki treści Markdown)? → A: 2000 znaków (FR-221).
- Q: Czy kolejne części jednostki podzielonej na kilka fragmentów zaczynają się od nagłówka tej jednostki? → A: Tak, każda część zaczyna się od własnego nagłówka jednostki (oryginalny tekst, powtarzany jak wiersz nagłówka tabeli); nagłówki sekcji nadrzędnych i tytuł dokumentu tylko w metadanych (FR-231).
- Q: Czy oznaczenie do cytatu zawiera etykiety pozycji listy dosłownie, czy gotowy cytat w formie „§ 13 ust. 3 pkt 2”? → A: Dosłownie: oznaczenie jednostki i oryginalne etykiety pozycji (np. „3.”, „2)”); sformułowanie cytatu należy do aplikacji RAG (FR-241).
- (implementacja T006) Część zaczynająca się od pozycji zagnieżdżonej listy nie zachowuje wcięcia: wcięcie ≥ 4 spacji na początku treści CommonMark czyta jako blok kodu; położenie pozycji opisują `listLabels` (FR-222).
- (implementacja T033a) Przypisy jednostki bez odwołania są pakowane na końcu jednostki jak inne niepodzielne elementy — gdy nie mieszczą się w ostatniej części, tworzą kolejne części (prawo bankowe, Art. 4: przypisy tytułu ustawy dawały część 3796 znaków) (FR-232).
- (implementacja T045) Nagłówki sekcji bez własnej treści (np. „Rozdział 1. Przepisy ogólne” nad samymi artykułami) nie trafiają do treści żadnego fragmentu, tylko do `sectionPath` fragmentów podrzędnych; FR-234 je pomija (FR-220, FR-234).
- Q: Czy każda linia pliku JSONL zawiera pełne metadane dokumentu? → A: Tak, każda linia jest samodzielna: wersja schematu, pełne metadane dokumentu i fragment (FR-251).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Fragmenty dokumentu z biblioteki w serwisie (Priority: P1)

Programista backendu aplikacji RAG rejestruje bibliotekę podziału w swoim serwisie .NET, przekazuje
wynik konwersji PDF (albo sam strumień PDF) razem z metadanymi dokumentu (identyfikator, oznaczenie
wspólne dla wersji, typ, tytuł, wersja, daty obowiązywania, status, poprzednia wersja) i dostaje w
pamięci dokument z metadanymi oraz uporządkowaną listę fragmentów. Każdy fragment to jedna jednostka
dokumentu (artykuł, paragraf, sekcja tabeli-dokumentu, sekcja taryfy lub procedury) albo jej część,
z oryginalną treścią w Markdown i metadanymi gotowymi do indeksowania i cytowania.

**Why this priority**: to minimalny, samodzielnie użyteczny wynik — bez fragmentów aplikacja RAG
nie ma czego indeksować.

**Independent Test**: wywołanie biblioteki na kilku syntetycznych dokumentach testowych i na
dokumentach korpusu; sprawdzenie granic fragmentów, ich treści i metadanych względem oczekiwań
(testy jednostkowe i pliki wzorcowe fragmentów).

**Acceptance Scenarios**:

1. **Given** regulamin z rozdziałami i paragrafami „§ N.”, **When** programista dzieli dokument,
   **Then** każdy paragraf jest osobnym fragmentem (albo kilkoma, jeśli przekracza limit), żaden
   fragment nie zawiera treści dwóch paragrafów, a ścieżka sekcji fragmentu zawiera rozdział i
   paragraf.
2. **Given** fragment paragrafu „§ 13.”, **When** programista czyta jego metadane, **Then** zawierają
   metadane dokumentu, oznaczenie jednostki „§ 13”, ścieżkę sekcji, zakres stron, identyfikator
   fragmentu i klucz jednostki.
3. **Given** paragraf dłuższy niż limit, złożony z ustępów z punktami, **When** dokument jest
   dzielony, **Then** paragraf jest podzielony na kilka fragmentów wyłącznie na granicach bloków lub
   pozycji listy, żadna pozycja listy nie jest rozcięta, a fragment zaczynający się od pozycji listy
   ma w metadanych jej oryginalną etykietę (np. „3.”) obok oznaczenia „§ 13”.
4. **Given** taryfa z tabelą dłuższą niż limit, **When** dokument jest dzielony, **Then** tabela jest
   podzielona na kilka fragmentów po całych wierszach, a każdy fragment z częścią tabeli powtarza jej
   wiersz nagłówka.
5. **Given** dowolny dokument, **When** treść wszystkich jego fragmentów zostanie połączona, **Then**
   zawiera cały tekst dokumentu z Markdown (poza nagłówkami jednostek i tabel powtórzonymi w kolejnych częściach)
   i żadnego słowa spoza dokumentu; tytuł dokumentu i ścieżka sekcji występują tylko w metadanych.
6. **Given** ten sam dokument i te same metadane i opcje, **When** podział jest wykonany wielokrotnie,
   także równolegle w wielu wątkach, **Then** każdy wynik jest identyczny.
7. **Given** strumień PDF zamiast wyniku konwersji, **When** programista wywołuje podział, **Then**
   biblioteka sama konwertuje dokument parserem i zwraca ten sam wynik, co przy podaniu wyniku
   konwersji tego PDF z tymi samymi opcjami parsera.

---

### User Story 2 - Porównanie wersji przez klucz jednostki (Priority: P1)

Agent aplikacji RAG, mając fragment „§ 11” obowiązującej wersji regulaminu, odnajduje w indeksie
fragment tej samej jednostki w poprzedniej wersji i porównuje je, by wskazać zmianę albo
nieaktualność. Umożliwia to klucz jednostki: jednakowy dla tej samej jednostki w różnych wersjach
dokumentu, różny dla różnych jednostek, różnych dokumentów i — w przeciwieństwie do identyfikatora
fragmentu — niezależny od wersji.

**Why this priority**: wskazywanie zmian i dokumentów nieaktualnych to jedna z głównych funkcji
demo; korpus zawiera wersje ze znanymi zmianami (spec 003).

**Independent Test**: dla każdej pary wersji z listą zmian w `corpus/manifest.json` — odnalezienie w
obu wersjach fragmentu zawierającego zmienioną jednostkę i sprawdzenie, że oba fragmenty mają ten sam
klucz jednostki.

**Acceptance Scenarios**:

1. **Given** dwie wersje regulaminu i zmiana w „§ 11 ust. 3” zapisana w manifeście, **When** obie
   wersje są dzielone, **Then** fragment z „§ 11” w nowszej wersji i fragment z „§ 11” w starszej
   wersji mają ten sam klucz jednostki i różne identyfikatory fragmentów.
2. **Given** zmiana stawki w pozycji taryfy albo zmiana w kroku procedury, **When** obie wersje są
   dzielone, **Then** fragmenty zawierające tę pozycję lub krok w obu wersjach mają ten sam klucz
   jednostki (klucz sekcji taryfy lub procedury, w której leżą).
3. **Given** dwa różne dokumenty z paragrafem „§ 11”, **When** oba są dzielone, **Then** klucze
   jednostek ich fragmentów są różne.
4. **Given** jednostka podzielona na kilka fragmentów, **When** programista czyta ich metadane,
   **Then** wszystkie mają ten sam klucz jednostki, różne identyfikatory i kolejne numery części.

---

### User Story 3 - Fragmenty korpusu i dowolnego PDF w plikach (Priority: P2)

Właściciel uruchamia generator korpusu (`generate`, `refresh`) i dostaje obok Markdown każdego
dokumentu korpusu plik z fragmentami w JSONL, z metadanymi dokumentu wziętymi z manifestu korpusu;
`verify` wykrywa nieaktualne pliki fragmentów tak samo jak nieaktualny Markdown. Dla dowolnego
innego PDF uruchamia polecenie CLI parsera, które zapisuje fragmenty dokumentu jako JSONL. Pliki
mają ten sam format JSON co model zwracany serwisowi, opisany wersjonowanym kontraktem.

**Why this priority**: aplikacja demo może zaindeksować gotowe pliki korpusu bez uruchamiania
biblioteki, ale to wygoda nad US1.

**Independent Test**: uruchomienie `generate` lub `refresh` i sprawdzenie, że każdy dokument korpusu
ma plik fragmentów zgodny z kontraktem, z metadanymi zgodnymi z manifestem; uruchomienie `verify` po
ręcznej zmianie pliku fragmentów (oczekiwany błąd); uruchomienie polecenia CLI na PDF i walidacja
wyniku względem kontraktu.

**Acceptance Scenarios**:

1. **Given** wygenerowany korpus, **When** właściciel uruchamia `refresh`, **Then** obok każdego
   pliku Markdown korpusu (także dokumentów zatrutych i aktów) jest plik fragmentów, a jego metadane
   dokumentu (identyfikator, oznaczenie, typ, tytuł, wersja, daty, status, poprzednia wersja) są
   zgodne z wpisem manifestu.
2. **Given** korpus po `refresh`, **When** właściciel uruchamia `refresh` ponownie, **Then** pliki
   fragmentów są identyczne bajt po bajcie.
3. **Given** plik fragmentów niezgodny z aktualnym wynikiem podziału, **When** uruchomiony jest
   `verify`, **Then** kończy się błędem wskazującym nieaktualny plik.
4. **Given** dowolny PDF i metadane podane w argumentach, **When** właściciel uruchamia polecenie
   CLI podziału, **Then** powstaje plik JSONL zgodny z kontraktem, a polecenie kończy się kodem 0;
   przy błędzie (brak pliku, PDF nieczytelny, brak wymaganych metadanych) — czytelnym komunikatem i
   niezerowym kodem wyjścia.

---

### Edge Cases

- **Treść przed pierwszą jednostką** (wstęp, preambuła, metryczka): tworzy własne fragmenty bez
  oznaczenia jednostki, z kluczem jednostki wstępu; nie jest doklejana do pierwszej jednostki.
- **Sekcja nadrzędna z własną treścią** (np. rozdział z tekstem przed pierwszym paragrafem, sekcja
  procedury z tekstem przed podsekcjami): jej własna treść to osobna jednostka; nigdy nie łączy się z
  treścią podsekcji.
- **Sekcja bez własnej treści** (np. rozdział zawierający tylko paragrafy): nie tworzy fragmentu;
  występuje tylko w ścieżkach sekcji swoich podsekcji.
- **Jednostka bardzo krótka** (np. „Art. 5. (uchylony)”): osobny fragment, bez łączenia z sąsiednią.
- **Niepodzielny element dłuższy niż limit** (jedna pozycja listy, jeden akapit, jeden wiersz
  tabeli): zostaje w całości w jednym fragmencie przekraczającym limit; fragment jest oznaczony jako
  przekraczający limit. Pozycja listy z zagnieżdżonymi pozycjami może zostać podzielona między swoimi
  pozycjami podrzędnymi, nigdy w środku tekstu pojedynczej pozycji.
- **Tabela bez wiersza nagłówka**: dzielona po całych wierszach, bez powtarzania czegokolwiek.
- **Tabela zastępcza** (format awaryjny z ostrzeżeniem TBL001): dzielona po wierszach jak inne tabele.
- **Przypisy**: fragment zawiera definicje dokładnie tych przypisów, do których odwołuje się jego
  treść; definicja przypisu nie trafia do fragmentu bez odwołania.
- **Powtórzone oznaczenie jednostki w jednym dokumencie** (np. ten sam numer artykułu w ustawie
  zmieniającej, dwie sekcje o tym samym nagłówku): klucze jednostek pozostają unikalne w dokumencie,
  a identyfikatory fragmentów — unikalne i deterministyczne.
- **Jednostka bez oznaczenia** (nagłówek typograficzny, np. sekcja procedury lub taryfy): klucz
  jednostki pochodzi z tekstu nagłówka i ścieżki; oznaczenie do cytatu to tekst nagłówka.
- **Strony pominięte przez parser**: strona pominięta nie wnosi treści do fragmentów; zakres stron
  fragmentu zaczyna się i kończy na stronach z jego treścią, ale może obejmować stronę pominiętą
  leżącą między nimi.
- **Wynik konwersji niekompletny**: podział działa na tym, co jest w modelu; dokument w wyniku jest
  oznaczony jako niekompletny.
- **Dokument pusty** (brak treści): wynik zawiera dokument z metadanymi i pustą listę fragmentów.
- **Dokument bez wersji** (np. akt prawny): wersja jest opcjonalna; identyfikatory i klucze nadal są
  stabilne.
- **Przerwanie** (anulowanie przez wywołującego): operacja kończy się anulowaniem, bez częściowego
  wyniku.

## Requirements *(mandatory)*

### Functional Requirements

#### Biblioteka i jej API

- **FR-200**: Projekt MUSI zawierać nową bibliotekę `LegalAgent.Chunking`, która dzieli dokument na
  fragmenty i zwraca wyłącznie modele w pamięci: dokument z metadanymi i uporządkowaną listę
  fragmentów. Biblioteka NIE MOŻE czytać ani zapisywać plików, zależeć od manifestu korpusu,
  konsoli ani globalnego stanu.
- **FR-201**: API biblioteki MUSI mieć ten sam styl co `LegalAgent.PdfParser`: rejestrację w
  kontenerze zależności (`AddLegalAgentChunking`), opcje domyślne z konfiguracji nadpisywane per
  wywołanie, metody asynchroniczne przyjmujące token anulowania; publiczne API MUSI być
  udokumentowane.
- **FR-202**: Biblioteka MUSI przyjmować na wejściu (a) wynik konwersji parsera albo (b) strumień
  PDF — wtedy sama wywołuje parser z podanymi lub domyślnymi opcjami parsera — oraz w obu przypadkach
  metadane dokumentu od wywołującego (FR-210).
- **FR-203**: Podział MUSI działać na modelu dokumentu z wyniku parsera (sekcje, bloki, tabele,
  przypisy, strony), nie na tekście Markdown, i NIE MOŻE być etapem potoku parsera. Biblioteka NIE
  MOŻE mieć własnych heurystyk rozpoznawania struktury: przyjmuje sekcje, listy i tabele z modelu
  takimi, jakie są.
- **FR-204**: Treść fragmentu MUSI być renderowana istniejącym rendererem Markdown parsera.
  Dopuszczalne jest małe, addytywne rozszerzenie publicznego API parsera potrzebne do renderowania
  części dokumentu; NIE MOŻE ono zmienić Markdown całego dokumentu (pliki wzorcowe parsera i Markdown
  korpusu pozostają bez zmian).
- **FR-205**: Wynik MUSI być deterministyczny: te same dane wejściowe, metadane i opcje dają
  identyczny model i identyczną serializację, niezależnie od maszyny, kultury systemu i liczby
  równoległych wywołań. Biblioteka MUSI być bezpieczna przy wywołaniach równoległych.
- **FR-206**: Błędy MUSZĄ być zgłaszane czytelnie: brak wymaganych metadanych lub nieprawidłowe
  opcje (np. limit mniejszy niż 200 znaków) — błędem walidacji przed podziałem; błędy parsera przy wejściu PDF —
  przekazane wywołującemu bez utraty informacji; anulowanie — zakończeniem bez częściowego wyniku.

#### Metadane dokumentu

- **FR-210**: Wywołujący MUSI podać identyfikator dokumentu (unikalny dla wersji, np. `REG-05-w1`) i
  MOŻE podać: oznaczenie dokumentu wspólne dla jego wersji (np. `BP/REG/05`), typ, tytuł, numer
  wersji, daty początku i końca obowiązywania, status oraz identyfikator poprzedniej wersji. Brak
  oznaczenia wspólnego oznacza, że oznaczeniem jest identyfikator dokumentu.
- **FR-211**: Dokument w wyniku MUSI zawierać metadane od wywołującego oraz dane źródła z wyniku
  konwersji (liczba stron, skrót zawartości PDF, informacja o kompletności konwersji). Tytuł z
  metadanych ma pierwszeństwo przed tytułem wykrytym przez parser.

#### Granice fragmentów

- **FR-220**: Jednostką podziału MUSI być własna treść jednej sekcji modelu dokumentu (artykuł,
  paragraf, sekcja tabeli-dokumentu, sekcja taryfy, sekcja procedury, inna sekcja z nagłówkiem)
  oraz treść przed pierwszą sekcją (wstęp). Fragment NIE MOŻE zawierać treści dwóch sekcji ani
  łączyć wstępu z sekcją. Sekcja bez własnej treści nie tworzy fragmentu.
- **FR-221**: Jednostka, której treść nie przekracza limitu długości, MUSI tworzyć dokładnie jeden
  fragment. Limit jest liczony w znakach treści fragmentu, konfigurowalny w opcjach (co najmniej 200
  znaków); wartość domyślna to 2000 znaków.
- **FR-222**: Jednostka dłuższa niż limit MUSI być podzielona na kolejne części wyłącznie na
  granicach bloków (akapitów, list, tabel), pozycji listy lub wierszy tabeli, tak by każda część
  możliwie najlepiej wykorzystywała limit. Podział NIE MOŻE przeciąć tekstu pozycji listy, wiersza
  tabeli ani akapitu. Lista może być dzielona między pozycjami dowolnego poziomu zagnieżdżenia;
  część zaczynająca się od pozycji zagnieżdżonej zaczyna się bez wcięcia (wcięcie o 4 spacje byłoby w
  Markdown blokiem kodu), zachowuje zagnieżdżenie względne i oryginalną etykietę, a jej położenie w liście
  opisują etykiety w metadanych (FR-241).
- **FR-223**: Tabela dłuższa niż limit MUSI być dzielona po całych wierszach; jeśli tabela ma wiersz
  nagłówka, każda część tabeli MUSI zaczynać się od tego wiersza nagłówka.
- **FR-224**: Element niepodzielny dłuższy niż limit (FR-222) MUSI trafić w całości do jednego
  fragmentu, oznaczonego w metadanych jako przekraczający limit.
- **FR-225**: Kolejność fragmentów MUSI odpowiadać kolejności treści w dokumencie.

#### Treść fragmentu

- **FR-230**: Treść fragmentu MUSI być oryginalnym tekstem dokumentu w Markdown zgodnym z
  kontraktem wyjścia parsera, bez żadnych dopisanych słów (np. tytułu dokumentu, nazw sekcji
  nadrzędnych, „cd.”, „część 2”). Kontekst (tytuł dokumentu, ścieżka sekcji) występuje wyłącznie w
  metadanych.
- **FR-231**: Każda część jednostki MUSI zaczynać się od nagłówka tej jednostki w oryginalnym
  brzmieniu (wstęp nie ma nagłówka). Jedynymi dopuszczalnymi powtórzeniami tekstu są nagłówek
  jednostki w jej kolejnych częściach i wiersz nagłówka tabeli (FR-223); nagłówki sekcji nadrzędnych
  NIE są powtarzane.
- **FR-232**: Treść fragmentu MUSI zawierać definicje przypisów, do których się odwołuje (przypis,
  do którego odwołuje się kilka części jednostki, jest w każdej z nich). Przypisy jednostki, do których
  nie odwołuje się żadna jej część, trafiają na koniec jednostki: do ostatniej części, a gdy się w niej
  nie mieszczą — do kolejnych części z nagłówkiem jednostki; poza tym fragment nie zawiera definicji
  przypisów.
- **FR-233**: Treść fragmentu NIE MOŻE zawierać znaczników stron; strony fragmentu są wyłącznie w
  metadanych (FR-242).
- **FR-234**: Połączona treść wszystkich fragmentów dokumentu MUSI zawierać każde słowo Markdown
  całego dokumentu (z pominięciem znaczników stron, znaczników stron pominiętych, wiersza tytułu
  dokumentu i nagłówków sekcji bez własnej treści — te są w metadanych: tytuł i ścieżki sekcji
  fragmentów) dokładnie tyle razy, ile występuje w dokumencie, z wyjątkiem słów powtórzonych zgodnie z
  FR-231 i FR-232.

#### Metadane fragmentu

- **FR-240**: Każdy fragment MUSI zawierać: identyfikator fragmentu, klucz jednostki, numer części w
  jednostce i liczbę części, oznaczenie jednostki do cytatu, etykiety pozycji listy, od której
  zaczyna się fragment, ścieżkę sekcji, rodzaj jednostki, zakres stron, długość treści, znacznik
  przekroczenia limitu (FR-224) oraz treść. Metadane dokumentu są dostępne przy każdym fragmencie.
- **FR-241**: Oznaczenie jednostki do cytatu MUSI pochodzić z tekstu dokumentu: oznaczenie sekcji
  (np. „§ 13”, „Art. 5”) albo — dla sekcji bez oznaczenia — tekst jej nagłówka; dla wstępu jest
  puste. Jeśli fragment zaczyna się od pozycji listy, metadane MUSZĄ zawierać oryginalne etykiety tej
  pozycji i jej pozycji nadrzędnych (np. „3.” i „2)”), bez dopisywania słów takich jak „ust.” czy
  „pkt”.
- **FR-242**: Zakres stron fragmentu MUSI obejmować strony, na których leży jego treść, i NIE MOŻE
  wychodzić poza strony jego jednostki; pierwsza strona zakresu MUSI być stroną, na której zaczyna
  się treść fragmentu (strona, na której frontend otwiera PDF).
- **FR-243**: Klucz jednostki MUSI być zbudowany z oznaczenia dokumentu wspólnego dla wersji
  (FR-210) i ścieżki jednostki w dokumencie (oznaczeń, a dla sekcji bez oznaczenia — tekstów
  nagłówków), skróconej do najkrótszej ścieżki od jednostki w górę, która jest unikalna w dokumencie
  (np. „§ 11”, a przy numeracji paragrafów od nowa w sekcjach — „Oprocentowanie > § 2”), bez numeru
  wersji i bez treści. Ta sama jednostka w różnych wersjach dokumentu ma
  ten sam klucz; różne jednostki dokumentu mają różne klucze (powtórzenia rozróżnia kolejny numer
  wystąpienia).
- **FR-244**: Identyfikator fragmentu MUSI być unikalny w całym korpusie i stabilny: zależy wyłącznie
  od identyfikatora dokumentu, klucza jednostki i numeru części, więc ponowny podział tej samej wersji
  daje te same identyfikatory. Identyfikator MUSI być bezpieczny do użycia jako klucz w indeksie
  (ograniczony zbiór znaków, ograniczona długość).

#### Serializacja i kontrakt

- **FR-250**: Format JSON modelu (dokument i fragmenty) MUSI być opisany kontraktem z numerem wersji
  schematu; ten sam kontrakt obowiązuje serwis i pliki. Biblioteka MUSI udostępniać serializację
  modelu do tego formatu i jego odczyt.
- **FR-251**: Format plików fragmentów to JSONL: jedna linia na fragment, każda linia samodzielna
  (zawiera wersję schematu, metadane dokumentu i fragment), kodowanie UTF-8, końce linii LF,
  stała kolejność pól i deterministyczny zapis liczb i dat.
- **FR-252**: Zmiana kontraktu łamiąca zgodność MUSI podnosić wersję schematu.

#### Narzędzia

- **FR-260**: CLI parsera MUSI mieć polecenie podziału, które przyjmuje PDF i metadane dokumentu w
  argumentach (identyfikator domyślnie z nazwy pliku), opcjonalnie limit długości, i zapisuje
  fragmenty jako JSONL do wskazanego pliku. Błędy kończą się czytelnym komunikatem i niezerowym
  kodem wyjścia.
- **FR-261**: Generator korpusu przy `generate` i `refresh` MUSI zapisywać obok Markdown każdego
  dokumentu korpusu (regulaminy, taryfy, procedury, akty, zatrute) plik fragmentów JSONL z
  metadanymi dokumentu z manifestu korpusu; `verify` MUSI odtwarzać pliki fragmentów w pamięci i
  zgłaszać błąd przy niezgodności z dyskiem lub braku pliku. Plik fragmentów MUSI być wskazany we
  wpisie dokumentu w manifeście.
- **FR-262**: `corpus/README.md` i README repozytorium MUSZĄ opisywać pliki fragmentów, polecenie
  CLI i użycie biblioteki.

#### Testy

- **FR-270**: Testy jednostkowe MUSZĄ pokrywać granice fragmentów (FR-220 – FR-225), treść
  (FR-230 – FR-234) i metadane (FR-240 – FR-244) na minimalnych modelach lub syntetycznych PDF.
- **FR-271**: Pliki wzorcowe fragmentów MUSZĄ istnieć dla co najmniej jednego regulaminu, jednej
  taryfy, jednej procedury i jednego aktu prawnego z korpusu; zmiana pliku wzorcowego wymaga zgody
  właściciela, jak w FR-163.
- **FR-272**: Test deterministyczności MUSI sprawdzać identyczność serializacji przy wielokrotnym i
  równoległym podziale.
- **FR-273**: Test na manifeście korpusu MUSI sprawdzać, że dla każdej pary wersji z listą zmian i
  dla każdej zmiany fragment zawierający zmienioną jednostkę istnieje w obu wersjach i ma w nich ten
  sam klucz jednostki.

### Key Entities

- **Dokument z metadanymi**: identyfikator wersji, oznaczenie wspólne dla wersji, typ, tytuł,
  wersja, daty obowiązywania, status, poprzednia wersja, dane źródła (liczba stron, skrót PDF,
  kompletność konwersji).
- **Fragment**: identyfikator, klucz jednostki, numer części i liczba części, oznaczenie do cytatu,
  etykiety pozycji listy na początku, ścieżka sekcji, rodzaj jednostki, zakres stron, długość,
  znacznik przekroczenia limitu, treść w Markdown. Należy do jednego dokumentu i jednej jednostki.
- **Jednostka**: własna treść jednej sekcji modelu dokumentu albo wstęp; ma klucz wspólny dla wersji.
- **Opcje podziału**: limit długości fragmentu w znakach, opcje parsera dla wejścia PDF.
- **Kontrakt fragmentów**: wersjonowany opis formatu JSON/JSONL dokumentu i fragmentów.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-040**: 100% dokumentów korpusu (regulaminy, taryfy, procedury, akty, zatrute) dzieli się bez
  błędu, a każdy ma zacommitowany plik fragmentów zgodny z kontraktem.
- **SC-041**: Dla 100% dokumentów korpusu połączona treść fragmentów zawiera każde słowo Markdown
  dokumentu dokładnie tyle razy, ile w dokumencie (FR-234), i żadnego słowa spoza niego.
- **SC-042**: 100% fragmentów korpusu mieści się w limicie albo jest oznaczonych jako przekraczające
  limit z powodu elementu niepodzielnego; żaden fragment nie zawiera treści dwóch jednostek.
- **SC-043**: 100% zmian zapisanych w manifeście dla par wersji przechodzi test FR-273.
- **SC-044**: Identyfikatory fragmentów są unikalne w całym korpusie (0 duplikatów), a dwukrotny
  `refresh` i równoległy podział dają pliki identyczne bajt po bajcie.
- **SC-045**: Podział dokumentu korpusu z gotowego wyniku konwersji trwa poniżej 1 sekundy, a
  dołożenie fragmentów nie wydłuża `refresh` całego korpusu o więcej niż 20%.
- **SC-046**: Pliki wzorcowe parsera i Markdown korpusu pozostają bez zmian po wprowadzeniu featury.

## Assumptions

- Odbiorcami są programiści aplikacji RAG i właściciel projektu, dlatego specyfikacja nazywa
  elementy API, które opis featury wprost narzuca (nazwa biblioteki, rejestracja w DI, token
  anulowania, wejście z wyniku parsera); szczegóły projektu API należą do planu.
- Oznaczenie wspólne dla wersji (FR-210) jest dodatkiem do metadanych wymienionych w opisie: bez niego
  klucz jednostki nie może być wspólny dla wersji, bo identyfikatory wersji korpusu się różnią
  (`REG-05`, `REG-05-w1`). W korpusie jest nim pole `designation` manifestu.
- Klucz jednostki opiera się na oznaczeniach z dokumentu; jeśli między wersjami zmieni się numeracja
  jednostki, klucze się rozejdą — zgodnie z opisem nie ma kontroli spójności numeracji.
- Zmiany w manifeście wskazują jednostki różnej szczegółowości („§ 11 ust. 3”, „poz. 75”,
  „krok 10.11”, „sekcja I”); test FR-273 odnajduje fragment zawierający tę jednostkę w każdej wersji,
  a sposób odnalezienia określa plan.
- Etykiety pozycji listy są przekazywane dosłownie; składanie cytatu w formie „§ 13 ust. 3 pkt 2”
  należy do aplikacji RAG (zasada braku dopisanych słów obejmuje też metadane).
- Treść fragmentu nie zawiera znaczników stron `<!-- page: N -->`, bo zakres stron jest w metadanych,
  a znaczniki nie są tekstem PDF.
- Fragmenty dokumentów zatrutych są tworzone jak dla każdego innego dokumentu; ich oznaczenie jako
  zatrutych pochodzi z metadanych od wywołującego (status, typ), nie z treści.
- Jeśli model parsera nie pozwala ustalić stron pojedynczych wierszy tabeli lub pozycji listy,
  dopuszczalne jest addytywne rozszerzenie modelu parsera (FR-204); bez niego zakres stron części
  tabeli jest zakresem całej tabeli przyciętym do jednostki.
- Poza zakresem: wyszukiwanie, embeddingi, baza wektorowa, generowanie odpowiedzi, podświetlanie w
  PDF, wersjonowanie i indeksowanie po stronie aplikacji, pomiary jakości podziału, kontrola
  spójności numeracji, poprawianie błędów struktury parsera w ramach tej featury.
