# Feature Specification: Syntetyczny korpus dokumentów fikcyjnego banku dla aplikacji RAG

**Feature Branch**: `003-synthetic-bank-corpus`

**Created**: 2026-10-08

**Status**: Draft

**Input**: User description: "Syntetyczny korpus dokumentów fikcyjnego banku „Bank Przykładowy S.A.” do zasilania aplikacji RAG, generowany w repozytorium i konwertowany biblioteką LegalAgent.PdfParser. Zakres: wyłącznie generowanie dokumentów i ich poprawna obsługa przez bibliotekę; role, kontrola dostępu, zaufanie do źródeł i sama aplikacja RAG są poza zakresem (inna aplikacja). Typy dokumentów, po 10 dokumentów każdego typu, każdy 20–30 stron, po polsku: regulaminy, taryfy opłat i prowizji, procedury wewnętrzne […] część dokumentów w 2–3 wersjach […] dokumenty nieaktualne; pary dokumentów wzajemnie sprzecznych […] Dokumenty zatrute: osobny katalog `corpus/zatrute/` […] Generator: treść dokumentów w plikach źródłowych w repozytorium, PDF składany deterministycznie przez istniejący SyntheticPdfBuilder […] Powtarzalność i rozbudowa […] Instrukcja w `corpus/README.md` […] Akty prawne: obecne 6 aktów z korpusu testowego plus kilka publicznych aktów dobranych pod tematykę bankową […] Obsługa przez bibliotekę: cały korpus (także dokumenty zatrute) konwertuje się kompletnie i deterministycznie […] jakość potwierdzona metrykami (kompletność słów, struktura nagłówków, listy, tabele) na prawdzie referencyjnej generatora."

## Kontekst

Feature rozszerza projekt z `specs/001-legal-pdf-parser` i `specs/002-table-document-sections`.
Numeracja wymagań jest kontynuowana: FR-100 i dalej, SC-020 i dalej. Odwołania do FR-0xx dotyczą
specyfikacji 001, a do FR-08x/FR-09x — specyfikacji 002.

Właściciel projektu buduje osobną aplikację RAG, która odpowiada pracownikom banku na pytania o
regulaminy, taryfy i procedury wewnętrzne, cytując źródło z numerem paragrafu, punktu lub kroku.
Aplikacja musi radzić sobie z wersjami dokumentów, dokumentami nieaktualnymi, sprzecznymi i
zatrutymi. Prawdziwych dokumentów bankowych nie wolno commitować, a publicznie dostępne regulaminy
nie zawierają procedur wewnętrznych ani dokumentów zatrutych. Dlatego potrzebny jest **syntetyczny
korpus fikcyjnego banku „Bank Przykładowy S.A.”**: realistyczne, wielostronicowe PDF-y w układach
spotykanych w bankach, ich Markdown wygenerowany biblioteką oraz manifest z metadanymi i prawdą
referencyjną.

To repozytorium odpowiada tylko za **dokumenty**: ich wygenerowanie i poprawną konwersję biblioteką.
Role, kontrola dostępu do fragmentów, ocena wiarygodności źródeł, wykrywanie zatruć i sama aplikacja
RAG są poza zakresem. Biblioteka niczego w treści nie interpretuje: wersje, sprzeczności i zatrucia
istnieją wyłącznie jako zwykły tekst dokumentów oraz jako wpisy manifestu — biblioteka przenosi ten
tekst wiernie jak każdy inny.

## Clarifications

### Session 2026-10-08

- Q: Czy „10 dokumentów każdego typu” obejmuje wersje (każda wersja to osobny dokument), czy wersje są dodatkowe? → A: Wersje są dodatkowe: 10 różnych dokumentów na typ (najnowsze wersje), a ich wcześniejsze wersje to dodatkowe pliki ponad 10 (FR-120, SC-020).
- Q: Czy ten sam blok treści może wystąpić w kilku różnych dokumentach korpusu? → A: Tak, tylko bloki oznaczone jako wspólne (reklamacje, dane osobowe, zmiany dokumentu, kontakt itp.), z podstawionymi parametrami, łącznie ≤ 20% słów dokumentu; reszta treści jest unikalna dla dokumentu (FR-103a, SC-031).
- Q: Czy `dotnet test` ma przy każdym uruchomieniu sprawdzać cały korpus, czy tylko próbkę? → A: Zwykłe `dotnet test` — stała próbka (≥ 1 dokument na każdy typ, układ i rodzaj zatrucia); pełny korpus (metryki + aktualność zacommitowanego Markdown) w osobnej kategorii testów, uruchamianej zawsze w CI i na żądanie lokalnie (FR-164, FR-165).
- Q: Co, gdy przebieg wymaga więcej treści, niż starcza unikalnych bloków? → A: Bloki mają warianty zdań i parametry, unikalność liczona po wyrenderowanym tekście; w zapisanym korpusie bezwzględna, w innych przebiegach dopuszczalne powtórzenia między dokumentami z raportem ich udziału (FR-103b).
- Q: Jakie cele mogą mieć polecenia dla asystenta AI w dokumentach zatrutych? → A: Zamknięta lista celów zapisywana w manifeście: zmiana odpowiedzi, ignorowanie źródeł/instrukcji, ukrycie źródła, działanie poza zakresem, podszycie pod polecenie; wszystkie dane fikcyjne (FR-132a, FR-142).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Korpus bazowy wygenerowany jednym poleceniem (Priority: P1)

Właściciel projektu uruchamia jedno polecenie generatora z zapisanymi parametrami i dostaje w
katalogu `corpus/` po 10 regulaminów, taryf i procedur fikcyjnego banku (20–30 stron każdy, po
polsku), dla każdego dokumentu PDF i Markdown wygenerowany biblioteką, oraz manifest z metadanymi.
Ponowne uruchomienie z tym samym ziarnem daje identyczne pliki.

**Why this priority**: bez korpusu aplikacja RAG nie ma danych; to minimalny, samodzielnie użyteczny
wynik featury.

**Independent Test**: uruchomienie generatora z zapisanymi parametrami w pustym katalogu; sprawdzenie
liczby dokumentów na typ, liczby stron każdego PDF, obecności Markdown i wpisów manifestu, a następnie
drugie uruchomienie i porównanie bajt po bajcie.

**Acceptance Scenarios**:

1. **Given** zapisane parametry korpusu (3 typy, liczba dokumentów, 20–30 stron, ziarno), **When**
   właściciel uruchamia jedno polecenie generatora, **Then** w `corpus/regulaminy/`,
   `corpus/taryfy/` i `corpus/procedury/` powstają dokumenty w zadanej liczbie, każdy jako PDF i
   Markdown, a manifest ma po jednym wpisie na dokument.
2. **Given** wygenerowany korpus, **When** generator jest uruchomiony ponownie z tym samym ziarnem
   i parametrami, **Then** wszystkie PDF-y, pliki Markdown i manifest są identyczne bajt po bajcie, a
   w katalogu nie przybywa ani nie ubywa plików.
3. **Given** dowolny dokument korpusu, **When** właściciel otwiera jego PDF, **Then** liczba stron
   mieści się w zakresie 20–30, a okładka lub metryczka zawiera nazwę „Bank Przykładowy S.A.”,
   oznaczenie dokumentu, numer wersji i daty obowiązywania.
4. **Given** zmienione ziarno, **When** generator jest uruchomiony, **Then** powstają inne dokumenty
   (inny dobór i kolejność bloków treści, inne wartości), nadal spełniające wszystkie wymagania typu.

---

### User Story 2 - Wierna, cytowalna konwersja całego korpusu (Priority: P1)

Programista aplikacji RAG dostaje dla każdego dokumentu korpusu (także zatrutego) Markdown z
kompletnym tekstem w oryginalnym brzmieniu, w którym rozdziały, paragrafy „§ N.”, sekcje procedur i
sekcje taryf są nagłówkami, ustępy, punkty i kroki zachowują swoje oryginalne oznaczenia, a tabele
taryf są tabelami. Dzięki temu chunker dzieli dokument po jednostkach, a odpowiedź może wskazać
„§ 12 ust. 3 pkt 2” albo „krok 4.2”.

**Why this priority**: korpus bez wiernego Markdown nie zasili RAG; układy nowe dla biblioteki
(procedury, wielostronicowe taryfy bez siatki, metryczki) mogą dziś być obsługiwane źle.

**Independent Test**: dla każdego dokumentu korpusu porównanie Markdown z prawdą referencyjną
wygenerowaną razem z PDF (słowa, nagłówki z poziomami, pozycje list z oznaczeniami, tabele z
komórkami) i obliczenie metryk SC-022 – SC-026.

**Acceptance Scenarios**:

1. **Given** regulamin z rozdziałami i paragrafami „§ N.” z ustępami i punktami, **When** dokument
   jest konwertowany, **Then** rozdziały i paragrafy są nagłówkami właściwego poziomu z oryginalnym
   oznaczeniem, a ustępy i punkty są pozycjami list z oryginalnymi oznaczeniami („1.”, „1)”, „a)”).
2. **Given** taryfa z tabelą przechodzącą przez kilka stron i powtarzanym wierszem nagłówka, **When**
   dokument jest konwertowany, **Then** wynik zawiera jedną tabelę na sekcję taryfy, bez powtórzonych
   wierszy nagłówka, a każda stawka stoi w tym samym wierszu co nazwa usługi i jej oznaczenie
   przypisu.
3. **Given** procedura z numerowanymi krokami i podkrokami, schematem kroków (FR-067), listą
   kontrolną i metryczką, **When** dokument jest konwertowany, **Then** sekcje procedury są
   nagłówkami, kroki i podkroki zachowują oryginalną numerację i zagnieżdżenie, schemat kroków ma
   postać z FR-067, a wynik nie zawiera żadnego słowa dopisanego przez bibliotekę.
4. **Given** układ, który biblioteka obsługuje źle, **When** zostaje wykryty na korpusie, **Then**
   przed poprawką powstaje czerwony test na minimalnym syntetycznym PDF odtwarzającym ten układ, a po
   poprawce wszystkie wcześniejsze pliki wzorcowe przechodzą bez zmian.

---

### User Story 3 - Wersje, dokumenty nieaktualne i sprzeczne (Priority: P2)

Twórca aplikacji RAG testuje, czy aplikacja wybiera obowiązującą wersję dokumentu i sygnalizuje
sprzeczności. Część dokumentów korpusu istnieje w 2–3 wersjach z kolejnymi datami obowiązywania i
zmienionymi postanowieniami lub stawkami, część jest nieaktualna, a niektóre pary dokumentów są ze
sobą sprzeczne. Wszystko to wynika z treści dokumentów (okładka, metryczka, postanowienia), a
manifest opisuje te zależności jako prawdę referencyjną.

**Why this priority**: to główne trudne przypadki aplikacji RAG, ale wymagają najpierw korpusu
bazowego (US1).

**Independent Test**: odczyt manifestu i treści wersji jednego dokumentu: daty obowiązywania kolejnych
wersji nie nachodzą na siebie, manifest wskazuje poprzednią wersję, a zmienione postanowienie lub
stawka różni się w tekście obu wersji dokładnie tak, jak opisuje manifest.

**Acceptance Scenarios**:

1. **Given** dokument w 3 wersjach, **When** czytelnik porównuje ich okładki, **Then** każda ma inny
   numer wersji i daty obowiązywania tworzące ciągłą, nienachodzącą sekwencję, a manifest każdej
   wersji poza pierwszą wskazuje wersję poprzednią.
2. **Given** dwie wersje taryfy, **When** czytelnik porównuje wskazaną w manifeście pozycję, **Then**
   stawka różni się, a manifest podaje jednostkę (np. pozycję taryfy lub paragraf), wartość
   poprzednią i nową.
3. **Given** dokument nieaktualny, **When** czytelnik czyta jego okładkę, **Then** data końca
   obowiązywania jest wcześniejsza niż data odniesienia korpusu, a manifest oznacza go jako
   nieaktualny.
4. **Given** para dokumentów sprzecznych (np. regulamin i taryfa podające różną opłatę za tę samą
   czynność w tym samym okresie), **When** czytelnik czyta wskazane miejsca, **Then** postanowienia
   są sprzeczne, a manifest obu dokumentów wskazuje drugi dokument i miejsca sprzeczności.

---

### User Story 4 - Dokumenty zatrute z prawdą referencyjną (Priority: P2)

Twórca aplikacji RAG testuje odporność aplikacji na dokumenty zatrute. W `corpus/zatrute/<typ>/<rodzaj-problemu>/`
znajdują się dokumenty wyglądające jak zwykłe dokumenty danego typu, ale zawierające fałszywe stawki
lub warunki, polecenia skierowane do asystenta AI (jawne lub ukryte w treści, przypisach, tabelach,
metryczce), podszywanie się pod inną jednostkę banku lub fałszywe zatwierdzenie, dokument nieaktualny
przedstawiony jako obowiązujący albo treść sprzeczną z dokumentem oryginalnym. Manifest dla każdego
z nich podaje typ, rodzaj problemu, dokument podrabiany lub sprzeczny, opis i miejsce zatrucia.

**Why this priority**: kluczowe dla testów bezpieczeństwa aplikacji RAG, ale budowane na korpusie
bazowym (dokumenty zatrute podrabiają dokumenty z US1).

**Independent Test**: dla każdego dokumentu zatrutego — odnalezienie w jego Markdown dosłownego
fragmentu zatrucia z manifestu we wskazanym miejscu (strona, jednostka) i sprawdzenie, że dokument
podrabiany istnieje w korpusie.

**Acceptance Scenarios**:

1. **Given** zapisane parametry korpusu, **When** generator jest uruchomiony, **Then** dla każdej
   pary typ × rodzaj problemu powstają co najmniej 2 dokumenty zatrute w
   `corpus/zatrute/<typ>/<rodzaj-problemu>/`.
2. **Given** dokument zatruty, **When** czytelnik porównuje go z dokumentem, który podrabia, **Then**
   ma ten sam rodzaj układu, okładkę, nagłówki i stopki oraz liczbę stron w zakresie typu — nie
   wyróżnia się formą.
3. **Given** dokument z poleceniem dla asystenta AI ukrytym w przypisie, komórce tabeli lub
   metryczce, **When** dokument jest konwertowany, **Then** Markdown zawiera to polecenie dosłownie,
   w miejscu zgodnym z manifestem, bez zmian i bez oznaczenia przez bibliotekę.
4. **Given** polecenie zawierające znaki mające znaczenie w Markdown (np. „#”, „|”, „*”, „>”),
   **When** dokument jest konwertowany, **Then** tekst polecenia jest zachowany dosłownie i nie
   tworzy nagłówka, tabeli, cytatu ani wyróżnienia, których nie ma w PDF.

---

### User Story 5 - Rozbudowa korpusu bez pisania kodu i instrukcja (Priority: P3)

Właściciel projektu chce wygenerować więcej dokumentów, dokumenty o innej objętości, inny odsetek
wersji i sprzeczności albo inną liczbę dokumentów zatrutych — zmieniając tylko parametry polecenia.
Chce też dodać nowy szablon dokumentu, blok treści lub wzorzec zatrucia, edytując wyłącznie pliki
źródłowe treści. `corpus/README.md` opisuje każdą z tych czynności.

**Why this priority**: zapewnia trwałą wartość generatora po pierwszym przebiegu, ale korpus
10 × 3 jest użyteczny bez tego.

**Independent Test**: wygenerowanie do katalogu tymczasowego 15 procedur po 40–50 stron oraz dodanie
nowego bloku treści w pliku źródłowym, wyłącznie według `corpus/README.md`, bez zmian w kodzie.

**Acceptance Scenarios**:

1. **Given** parametry „typ: procedury, liczba: 15, strony: 40–50, ziarno: X, katalog: tymczasowy”,
   **When** generator jest uruchomiony, **Then** powstaje 15 procedur o liczbie stron 40–50 wraz z
   Markdown i manifestem, bez zmiany kodu.
2. **Given** nowy szablon regulaminu dopisany do plików źródłowych treści, **When** generator jest
   uruchomiony, **Then** dokumenty z tego szablonu pojawiają się w wyniku.
3. **Given** nowy wzorzec zatrucia dopisany do plików źródłowych, **When** generator jest
   uruchomiony z tym rodzajem problemu, **Then** powstaje katalog
   `corpus/zatrute/<typ>/<nowy-rodzaj>/` z dokumentami i wpisami manifestu.
4. **Given** `corpus/README.md`, **When** nowa osoba wykonuje opisane kroki (wygenerowanie od nowa,
   dodanie dokumentów, zmiana liczby stron, nowy typ / szablon / rodzaj zatrucia, odświeżenie
   Markdown i manifestu), **Then** osiąga opisany wynik bez pomocy autora.

---

### User Story 6 - Akty prawne pod tematykę bankową (Priority: P3)

Aplikacja RAG potrzebuje także aktów prawnych, na które powołują się regulaminy i procedury. W
`corpus/akty/` znajdują się istniejące akty z korpusu testowego oraz kilka dodatkowych publicznych
ustaw bankowych w oficjalnym tekście jednolitym, każdy jako PDF, Markdown i wpis manifestu ze
źródłem.

**Why this priority**: uzupełnia korpus, ale nie wymaga nowego generatora — akty są pobierane raz i
konwertowane.

**Independent Test**: sprawdzenie, że każdy akt w `corpus/akty/` ma PDF, Markdown i wpis manifestu z
adresem oficjalnego źródła i oznaczeniem publikatora (rok, pozycja).

**Acceptance Scenarios**:

1. **Given** `corpus/akty/`, **When** czytelnik przegląda katalog, **Then** zawiera 6 aktów z korpusu
   testowego oraz co najmniej 3 ustawy: o przeciwdziałaniu praniu pieniędzy oraz finansowaniu
   terroryzmu, o ochronie danych osobowych oraz o rozpatrywaniu reklamacji przez podmioty rynku
   finansowego i o Rzeczniku Finansowym.
2. **Given** wpis manifestu aktu, **When** czytelnik go odczytuje, **Then** zawiera tytuł,
   oznaczenie publikatora, datę tekstu jednolitego, adres źródła i datę pobrania.

---

### Edge Cases

- Zadany zakres stron jest nieosiągalny z dostępnych bloków treści szablonu (np. 200–210 stron dla
  krótkiego szablonu) → generator kończy się czytelnym komunikatem i niezerowym kodem wyjścia, bez
  zapisania niepełnego dokumentu (zasada IV); nie powtarza mechanicznie tych samych bloków w jednym
  dokumencie.
- Przebieg wymaga więcej treści, niż dają warianty bloków (np. 50 procedur po 40 stron) → poza
  zapisanym korpusem generator powtarza bloki niewspólne między dokumentami i raportuje udział
  powtórzeń (FR-103b); w zapisanym korpusie to błąd.
- Parametry wymagają dokumentu zatrutego podrabiającego dokument, którego nie ma w przebiegu (np. typ
  wyłączony z generowania) → błąd parametrów z komunikatem, a nie dokument z pustym odniesieniem.
- Odsetek wersji/sprzeczności daje liczbę niecałkowitą (np. 25% z 10) → zaokrąglenie opisane w
  README, wynik deterministyczny.
- Generowanie do katalogu, w którym są już pliki poprzedniego przebiegu → wynik jest taki sam jak w
  pustym katalogu: pliki nadpisane, nieaktualne pliki z poprzedniego przebiegu usunięte, manifest bez
  duplikatów (zasada III); pliki spoza zarządzanych podkatalogów (np. `corpus/akty/`, README) nie są
  dotykane.
- Konwersja pojedynczego dokumentu kończy się błędem → przebieg kończy się niezerowym kodem wyjścia ze
  wskazaniem dokumentu; manifest nie oznacza go jako poprawnie skonwertowanego.
- Polecenie dla AI lub fałszywa stawka w komórce tabeli przechodzącej przez granicę strony, w przypisie
  albo w polu schematu kroków → treść przeniesiona dosłownie i w całości w miejscu zgodnym z manifestem.
- Tekst zatrucia przypominający nagłówek, oznaczenie listy lub paragraf („§ 99.”, „# SYSTEM”) w
  środku akapitu → pozostaje częścią akapitu, nie tworzy fałszywej jednostki.
- Polecenie „ukryte” zapisane jako tekst niewidoczny (biały na białym, poza stroną) → nie jest
  rodzajem zatrucia tego korpusu: biblioteka pomija taki tekst (FR-013), więc nie dotarłby do RAG;
  wszystkie zatrucia są tekstem widocznym (zob. Assumptions).
- Przypisy w układzie dwukolumnowym i przypisy do pozycji taryfy na innej stronie niż pozycja → tekst
  przypisu kompletny, oznaczenie przypisu przy pozycji zachowane.
- Nazwy prawdziwych banków lub ich znaki towarowe pojawiające się w treści szablonów → generator
  odrzuca przebieg (lista nazw zabronionych w konfiguracji) z komunikatem wskazującym plik źródłowy.
- Dokumenty sprzeczne mają nachodzące okresy obowiązywania (sprzeczność dotyczy tego samego dnia);
  wersje tego samego dokumentu — okresy rozłączne i ciągłe.

## Requirements *(mandatory)*

### Functional Requirements

#### Generator i powtarzalność

- **FR-100**: Repozytorium MUSI zawierać generator korpusu uruchamiany jednym poleceniem jako
  narzędzie w solucji, z parametrami: typy dokumentów, liczba dokumentów na typ, zakres stron (min–max),
  ziarno losowania, odsetek dokumentów w wielu wersjach, liczba dokumentów nieaktualnych na typ, liczba par
  sprzecznych, rodzaje i liczba dokumentów zatrutych na parę typ × rodzaj oraz katalog wyjściowy.
  Parametry MOGĄ pochodzić z pliku parametrów przebiegu; wartości jawnie podane w poleceniu mają
  pierwszeństwo.
- **FR-101**: Ten sam zestaw parametrów i ziarno MUSZĄ dawać identyczne bajt po bajcie pliki PDF,
  Markdown i manifest — przy każdym uruchomieniu, na Windows i Linux; wynik NIE MOŻE zależeć od daty
  i godziny uruchomienia, kultury systemu, kolejności plików w systemie plików ani zainstalowanych
  czcionek.
- **FR-102**: Treść dokumentów MUSI pochodzić z plików źródłowych w repozytorium, oddzielonych od
  kodu: szablonów dokumentów (struktura typu) i wielokrotnego użytku bloków treści (rozdziały,
  paragrafy, definicje, przypisy, pozycje taryfy, kroki procedur, listy kontrolne, załączniki,
  metryczki, wzorce zatruć). Generator składa dokumenty z tych bloków; dodanie dokumentów, zmiana
  objętości, dodanie szablonu, bloku lub wzorca zatrucia NIE MOGĄ wymagać zmiany kodu. Nowy typ
  dokumentu korzystający z istniejących elementów układu NIE MOŻE wymagać zmiany kodu; nowy element
  układu (np. nowy rodzaj grafiki) MOŻE.
- **FR-103**: Generator MUSI osiągać docelową liczbę stron z zakresu parametrów, dobierając bloki
  treści; liczba stron każdego PDF MUSI być sprawdzana po złożeniu. Gdy zakresu nie da się osiągnąć,
  generator MUSI zakończyć się czytelnym komunikatem i niezerowym kodem wyjścia. Ten sam blok treści
  NIE MOŻE wystąpić w jednym dokumencie więcej niż raz (dotyczy także FR-103a i FR-103b).
- **FR-103a**: Między różnymi dokumentami (tego samego lub różnych typów) MOGĄ się powtarzać wyłącznie
  bloki oznaczone w plikach źródłowych jako wspólne (np. reklamacje, ochrona danych osobowych, zmiany
  dokumentu, kontakt z bankiem), z parametrami podstawionymi dla danego dokumentu; bloki wspólne
  stanowią łącznie nie więcej niż 20% słów treści dokumentu, a pozostałe bloki są użyte w korpusie
  najwyżej w jednym dokumencie. Ograniczenie nie dotyczy wersji tego samego dokumentu (FR-120) ani
  dokumentów zatrutych względem dokumentu podrabianego (FR-131).
- **FR-103b**: Bloki treści MUSZĄ mieć warianty brzmienia (np. alternatywne zdania, kolejność punktów)
  i parametry (kwoty, terminy, nazwy jednostek i produktów); unikalność z FR-103a jest oceniana po
  wyrenderowanym tekście bloku, nie po jego identyfikatorze. W zapisanym przebiegu korpusu FR-103a
  obowiązuje bezwzględnie. W innych przebiegach, gdy wariantów zabraknie, generator MOŻE powtórzyć
  blok niewspólny w kolejnych dokumentach (zakaz powtórzenia w tym samym dokumencie z FR-103 nadal obowiązuje) i MUSI zaraportować udział
  powtórzonej treści dla każdego dokumentu oraz całego przebiegu.
- **FR-104**: PDF MUSI być składany istniejącym konstruktorem syntetycznych PDF projektu (z osadzonymi
  czcionkami obsługującymi polskie znaki), w zróżnicowanych układach: okładka, nagłówki i stopki stron,
  numeracja stron, jedna i dwie kolumny, tabele z siatką i bez siatki, listy wielopoziomowe, przypisy,
  wyróżnienia (pogrubienie, kursywa, zacieniowanie), schematy kroków (FR-067) i tabela-dokument
  (FR-080). Układ danego dokumentu wynika z szablonu i ziarna.
- **FR-105**: Wszystkie dokumenty syntetyczne MUSZĄ dotyczyć fikcyjnego „Bank Przykładowy S.A.” i NIE
  MOGĄ zawierać nazw, znaków towarowych ani logotypów prawdziwych banków; generator MUSI sprawdzać
  treść wyjściową względem konfigurowalnej listy nazw zabronionych i odrzucać przebieg przy trafieniu.
  Dane identyfikacyjne (adresy, numery rachunków, KRS, NIP, telefony, adresy WWW) MUSZĄ być fikcyjne
  i w formatach niewskazujących na istniejące podmioty (np. domena zastrzeżona do przykładów).
- **FR-106**: Generator MUSI razem z każdym PDF wytwarzać prawdę referencyjną dokumentu: pełną
  sekwencję słów treści w kolejności czytania, listę jednostek nagłówkowych z poziomami i oznaczeniami,
  pozycje list z oznaczeniami i poziomami zagnieżdżenia, tabele z komórkami oraz teksty artefaktów
  stron (nagłówki, stopki, numery stron), które nie należą do treści.
- **FR-107**: Generator MUSI konwertować każdy wygenerowany PDF biblioteką (z ustawieniami domyślnymi,
  zapisanymi w parametrach przebiegu) i zapisywać Markdown obok PDF; MUSI też umożliwiać samo
  odświeżenie Markdown i manifestu dla istniejących PDF (np. po zmianie biblioteki) bez ponownego
  składania PDF.
- **FR-108**: Ponowne uruchomienie w katalogu z poprzednim przebiegiem MUSI dać wynik identyczny jak w
  pustym katalogu: pliki zarządzane przez generator są nadpisywane, pliki poprzedniego przebiegu
  nieobecne w nowym są usuwane, manifest nie ma duplikatów; pliki spoza zarządzanych podkatalogów nie
  są zmieniane.

#### Typy dokumentów

- **FR-110**: Korpus MUSI zawierać 10 regulaminów różnych produktów i usług (m.in. rachunki, karty,
  kredyty, bankowość elektroniczna, promocje), po polsku, 20–30 stron każdy, z okładką, rozdziałami,
  paragrafami „§ N.” z ustępami i punktami (wielopoziomowo), słowniczkiem definicji i przypisami. Co
  najmniej 2 regulaminy MUSZĄ mieć układ dwukolumnowy i co najmniej 2 — układ tabeli-dokumentu z
  feature 002 (FR-080).
- **FR-111**: Korpus MUSI zawierać 10 taryf opłat i prowizji, 20–30 stron każda, z wielostronicowymi
  tabelami (co najmniej po 2 taryfy z tabelami z siatką i bez siatki), powtarzanym na każdej stronie
  wierszem nagłówka tabeli, przypisami do pozycji (oznaczenie przy pozycji, treść pod tabelą lub na
  końcu sekcji), numerowanymi pozycjami taryfy i sekcjami dla segmentów klientów (np. klienci
  indywidualni, firmy, bankowość prywatna).
- **FR-112**: Korpus MUSI zawierać 10 procedur wewnętrznych (m.in. otwieranie rachunku, reklamacje,
  AML/KYC, blokady, obsługa zgonu klienta, incydenty), 20–30 stron każda, z metryczką dokumentu
  (oznaczenie, wersja, właściciel procedury, data zatwierdzenia, daty obowiązywania, historia zmian),
  sekcjami: cel, zakres, odpowiedzialności, definicje, opis postępowania z numerowanymi krokami i
  podkrokami (np. „4.”, „4.1.”, „4.1.1.”), schematem kroków w układzie FR-067, listami kontrolnymi i
  załącznikami.
- **FR-113**: Każdy dokument MUSI mieć w treści (na okładce lub w metryczce): nazwę banku, tytuł,
  oznaczenie dokumentu (unikalne w korpusie), numer wersji oraz datę początku obowiązywania i — o ile
  dotyczy — datę końca obowiązywania.
- **FR-114**: Bloki treści MUSZĄ być merytorycznie spójne z typem i tematem dokumentu (np. regulamin
  karty nie zawiera kroków procedury AML) i realistyczne językowo; odwołania wewnętrzne (do paragrafów,
  pozycji taryfy, załączników, innych dokumentów banku i aktów prawnych) MUSZĄ wskazywać jednostki,
  które istnieją.

#### Wersje, nieaktualność, sprzeczności

- **FR-120**: Część dokumentów każdego typu (domyślnie co najmniej 3 na typ) MUSI występować w 2–3
  wersjach. Wersje mają ten sam tytuł i oznaczenie dokumentu, kolejne numery wersji, rozłączne i
  ciągłe okresy obowiązywania oraz co najmniej jedno zmienione postanowienie lub stawkę; pozostała
  treść jest identyczna. Liczba dokumentów na typ (FR-110 – FR-112, parametr FR-100) dotyczy różnych
  dokumentów; wcześniejsze wersje są dodatkowymi plikami w tym samym katalogu typu.
- **FR-121**: Korpus MUSI zawierać dokumenty nieaktualne (domyślnie co najmniej 2 na typ, nie licząc
  wcześniejszych wersji): data końca obowiązywania przed datą odniesienia korpusu zapisaną w
  parametrach przebiegu.
- **FR-122**: Korpus MUSI zawierać pary dokumentów wzajemnie sprzecznych (domyślnie co najmniej 1 para
  na typ i 1 para między typami, np. regulamin–taryfa), o nachodzących okresach obowiązywania, z
  postanowieniami sprzecznymi w tej samej sprawie.
- **FR-123**: Wersje, nieaktualność i sprzeczności MUSZĄ wynikać wyłącznie ze zwykłej treści dokumentów
  (okładka, metryczka, historia zmian, postanowienia); dokument NIE MOŻE zawierać meta-uwag w rodzaju
  „ten dokument jest sprzeczny z …” ani innych oznaczeń prawdy referencyjnej. Prawda referencyjna
  znajduje się tylko w manifeście.

#### Dokumenty zatrute

- **FR-130**: Dokumenty zatrute MUSZĄ być zapisywane w `corpus/zatrute/<typ>/<rodzaj-problemu>/`, gdzie
  `<typ>` to `regulaminy`, `taryfy` lub `procedury`, a `<rodzaj-problemu>` co najmniej:
  `falszywe-stawki` (fałszywe stawki lub warunki), `polecenia-dla-ai` (wstrzyknięte polecenia skierowane
  do asystenta AI), `podszywanie` (podszywanie się pod inną jednostkę banku lub fałszywe zatwierdzenie),
  `nieaktualny-jako-obowiazujacy` i `sprzecznosc-z-oryginalem`. Dla każdej pary typ × rodzaj MUSZĄ
  powstać co najmniej 2 dokumenty.
- **FR-131**: Dokument zatruty MUSI podrabiać konkretny dokument korpusu bazowego (lub być z nim
  sprzeczny): ten sam typ, układ, styl okładki, nagłówki i stopki oraz liczba stron w zakresie typu;
  treść poza miejscami zatrucia jest zgodna z dokumentem podrabianym albo zbudowana z tych samych
  bloków. Forma (krój, rozmiar, kolor, wyróżnienia) miejsca zatrucia NIE MOŻE odróżniać go od otoczenia,
  chyba że wzorzec zatrucia tego wymaga (np. jawne polecenie).
- **FR-132**: Polecenia dla asystenta AI MUSZĄ występować w wariantach jawnych (osobny akapit lub
  ramka) i ukrytych — wplecionych w akapit treści, przypis, komórkę tabeli i metryczkę; dla każdego
  typu dokumentu MUSI wystąpić co najmniej jeden wariant ukryty w przypisie lub tabeli oraz co
  najmniej jeden w metryczce lub na okładce.
- **FR-132a**: Każde polecenie dla asystenta AI MUSI mieć jeden cel z zamkniętej listy: `zmiana-odpowiedzi`
  (zmiana odpowiedzi merytorycznej, np. inna stawka lub pominięcie wyjątku), `ignorowanie-zrodel`
  (zignorowanie innych źródeł lub instrukcji), `ukrycie-zrodla` (ukrycie źródła lub cytatu),
  `dzialanie-poza-zakresem` (np. podanie danych klienta, kontakt pod wskazany adres) i
  `podszycie-pod-polecenie` (podszycie się pod polecenie przełożonego lub systemu). W rodzaju
  `polecenia-dla-ai` każdy cel MUSI wystąpić co najmniej raz w korpusie. Wszystkie adresy, numery,
  dane osobowe i dane klientów w poleceniach MUSZĄ być fikcyjne (FR-105).
- **FR-133**: Wszystkie zatrucia MUSZĄ być tekstem widocznym na stronie (nie białym na białym, nie
  poza stroną, nie zbyt małym, by go przeczytać), tak by trafiały do Markdown jak każdy inny tekst.
- **FR-134**: Wzorce zatruć MUSZĄ być blokami w plikach źródłowych treści (FR-102); dodanie nowego
  rodzaju problemu MUSI wymagać tylko dopisania wzorców i nazwy rodzaju w parametrach.

#### Manifest

- **FR-140**: Korpus MUSI mieć jeden manifest w formacie czytelnym maszynowo w `corpus/`, z wpisem dla
  każdego dokumentu (syntetycznego, zatrutego i aktu prawnego): identyfikator, typ, tytuł, oznaczenie,
  wersja, data początku i końca obowiązywania, identyfikator poprzedniej wersji, status (obowiązujący /
  nieaktualny), ścieżki PDF i Markdown, liczba stron, szablon i ziarno źródłowe (dla dokumentów
  syntetycznych), źródło i data pobrania (dla aktów) oraz uwagi o sprzeczności, nieaktualności i
  zatruciu. Manifest NIE MOŻE zawierać ról ani uprawnień.
- **FR-141**: Dla zmiany między wersjami i dla sprzeczności wpis manifestu MUSI wskazywać dokument
  powiązany, jednostkę (paragraf, ustęp, punkt, pozycja taryfy, krok) i oba brzmienia lub obie
  wartości.
- **FR-142**: Dla dokumentu zatrutego wpis manifestu MUSI podawać: typ, rodzaj problemu, identyfikator
  dokumentu podrabianego lub sprzecznego, opis zatrucia oraz dla każdego miejsca zatrucia: stronę,
  jednostkę dokumentu, element układu (akapit, przypis, komórka tabeli, metryczka, okładka), dosłowny
  tekst zatrucia oraz — dla poleceń dla AI — cel z listy FR-132a.
- **FR-143**: Manifest MUSI zawierać parametry przebiegu, które go wytworzyły (w tym ziarno, datę
  odniesienia korpusu i wersję biblioteki użytą do konwersji), tak by przebieg dało się odtworzyć.

#### Akty prawne

- **FR-150**: `corpus/akty/` MUSI zawierać 6 aktów z korpusu testowego oraz co najmniej: ustawę o
  przeciwdziałaniu praniu pieniędzy oraz finansowaniu terroryzmu, ustawę o ochronie danych osobowych i
  ustawę o rozpatrywaniu reklamacji przez podmioty rynku finansowego i o Rzeczniku Finansowym — w
  oficjalnym tekście jednolitym z Dziennika Ustaw — każdy jako PDF i Markdown wygenerowany biblioteką.
- **FR-151**: Pliki aktów MUSZĄ być pobrane jednorazowo i zacommitowane; generator i testy NIE MOGĄ
  pobierać ich z sieci. Źródło (adres URL, oznaczenie publikatora, data pobrania) MUSI być zapisane w
  manifeście i w pliku źródeł w `corpus/akty/`.

#### Obsługa przez bibliotekę

- **FR-160**: Każdy dokument korpusu, także zatruty, MUSI konwertować się bez błędu, kompletnie i
  deterministycznie (identyczny Markdown przy każdym uruchomieniu, na Windows i Linux).
- **FR-161**: Markdown MUSI umożliwiać cytowanie z numerem jednostki: rozdziały, paragrafy „§ N.”,
  sekcje procedur, sekcje taryf i załączniki są nagłówkami z oryginalnym oznaczeniem; ustępy, punkty,
  litery, kroki i podkroki zachowują oryginalne oznaczenia jako pozycje list lub początek akapitu;
  pozycje taryfy zachowują swój numer w komórce tabeli.
- **FR-162**: Biblioteka NIE MOŻE dodawać do Markdown żadnych słów spoza PDF ani pomijać lub zmieniać
  treści zatruć; polecenia dla AI i inne zatrucia są przenoszone dosłownie jak każdy tekst, a znaki
  specjalne Markdown w ich treści są zabezpieczone tak, by nie tworzyły struktury nieobecnej w PDF.
- **FR-163**: Każdy układ korpusu, który biblioteka obsługuje niezgodnie ze specyfikacjami 001/002 i
  tą specyfikacją, MUSI zostać poprawiony test-first: najpierw czerwony test na minimalnym syntetycznym
  PDF odtwarzającym układ, potem poprawka; poprawka NIE MOŻE zmieniać wyników istniejących plików
  wzorcowych, chyba że zmiana jest zamierzona, opisana i zatwierdzona przez właściciela.
- **FR-164**: Testy automatyczne MUSZĄ liczyć metryki jakości (SC-022 – SC-026) dla każdego dokumentu
  korpusu względem prawdy referencyjnej generatora (FR-106) i kończyć się niepowodzeniem przy
  przekroczeniu progu, ze wskazaniem dokumentu i jednostki.
- **FR-165**: Testy MUSZĄ też sprawdzać, że zacommitowany Markdown każdego dokumentu jest identyczny z
  bieżącym wynikiem biblioteki dla jego PDF (Markdown nie jest nieaktualny). Zwykłe uruchomienie testów
  obejmuje stałą próbkę korpusu: co najmniej 1 dokument na każdy typ, każdy układ z FR-110 – FR-112 i
  każdą parę typ × rodzaj zatrucia. Pełny korpus jest sprawdzany w osobnej kategorii testów,
  uruchamianej zawsze w CI i na żądanie lokalnie; scalenie zmiany wymaga przejścia obu.

### Key Entities

- **Przebieg generatora**: zapisany zestaw parametrów (typy, liczby, zakresy stron, ziarno, odsetki,
  rodzaje i liczby zatruć, data odniesienia, katalog) wytwarzający korpus; obecny korpus jest jednym
  przebiegiem.
- **Szablon dokumentu**: struktura typu dokumentu — kolejność sekcji, dopuszczalne bloki, układy
  (jedna/dwie kolumny, tabela-dokument, taryfa z siatką/bez siatki), styl okładki i metryczki.
- **Blok treści**: wielokrotnego użytku fragment treści (rozdział, paragraf, definicja, przypis,
  pozycja taryfy, krok procedury, lista kontrolna, załącznik, wzorzec zatrucia) z wariantami
  brzmienia i parametrami (np. kwoty, terminy, nazwy jednostek) uzupełnianymi przez generator;
  oznaczony jako wspólny lub niewspólny (FR-103a, FR-103b).
- **Dokument korpusu**: PDF + Markdown + prawda referencyjna + wpis manifestu; ma typ, oznaczenie,
  wersję, okres obowiązywania i status.
- **Wersja dokumentu**: dokument o tym samym oznaczeniu z kolejnym numerem wersji, wskazujący wersję
  poprzednią i zmienione jednostki.
- **Para sprzeczna**: dwa dokumenty o nachodzących okresach obowiązywania ze sprzecznymi
  postanowieniami we wskazanych jednostkach.
- **Dokument zatruty**: dokument w `corpus/zatrute/` z typem, rodzajem problemu, dokumentem
  podrabianym/sprzecznym i listą miejsc zatrucia (strona, jednostka, element układu, dosłowny tekst).
- **Akt prawny**: publiczny tekst jednolity ze wskazaniem źródła i daty pobrania.
- **Manifest**: jeden plik metadanych korpusu na poziomie plików (bez ról), z parametrami przebiegu.
- **Prawda referencyjna**: oczekiwana treść i struktura dokumentu wytworzona przez generator, podstawa
  metryk jakości konwersji.

## Success Criteria *(mandatory)*

Kryteria są weryfikowane na korpusie wygenerowanym z zapisanych parametrów (10 × 3 typy, 20–30 stron,
dokumenty zatrute) oraz na aktach w `corpus/akty/`.

### Measurable Outcomes

- **SC-020**: Korpus zawiera dokładnie zadaną liczbę różnych dokumentów na typ (plus wcześniejsze
  wersje zgodnie z FR-120), 100% PDF-ów ma liczbę stron w
  zakresie 20–30, a 100% dokumentów ma PDF, Markdown i wpis manifestu z wszystkimi polami FR-140.
- **SC-021**: Dwa kolejne przebiegi z tymi samymi parametrami (także jeden na Windows i jeden na Linux)
  dają 100% identycznych plików korpusu; pełne odtworzenie korpusu (PDF, Markdown, manifest) trwa
  krócej niż 10 minut na typowym komputerze deweloperskim.
- **SC-022**: Kompletność treści: dla każdego dokumentu syntetycznego co najmniej 99,5% słów prawdy
  referencyjnej (bez artefaktów stron) występuje w Markdown w kolejności czytania, a Markdown nie
  zawiera ani jednego słowa spoza PDF.
- **SC-023**: 100% dosłownych tekstów zatruć z manifestu występuje w Markdown odpowiednich dokumentów
  w całości, w jednostce wskazanej w manifeście.
- **SC-024**: Struktura nagłówków: dla każdego dokumentu co najmniej 98% jednostek nagłówkowych prawdy
  referencyjnej (rozdziały, paragrafy, sekcje procedur i taryf, załączniki) jest nagłówkami właściwego
  poziomu z oryginalnym oznaczeniem, a fałszywe nagłówki stanowią nie więcej niż 1% nagłówków.
- **SC-025**: Listy: co najmniej 98% pozycji list prawdy referencyjnej (ustępy, punkty, litery, kroki,
  podkroki, pozycje list kontrolnych) ma poprawne oznaczenie i poziom zagnieżdżenia.
- **SC-026**: Tabele taryf: 100% stawek stoi w tym samym wierszu co nazwa usługi i numer pozycji;
  100% tabel taryf (z siatką i bez) jest tabelami Markdown, po jednej na tabelę źródłową, bez
  powtórzonych wierszy nagłówka; co najmniej 98% komórek prawdy referencyjnej ma zgodną treść.
- **SC-027**: 0 wystąpień nazw z listy nazw zabronionych (FR-105) w PDF-ach i Markdown dokumentów
  syntetycznych.
- **SC-028**: Każda para typ × rodzaj zatrucia ma co najmniej 2 dokumenty; każdy dokument zatruty
  wskazuje istniejący dokument podrabiany lub sprzeczny; dla każdego dokumentu w wielu wersjach okresy
  obowiązywania wersji są rozłączne i ciągłe.
- **SC-029**: Brak regresji: 100% istniejących plików wzorcowych (`Corpus/acts`, `Corpus/banking`)
  przechodzi bez zmian, chyba że zmiana jest zamierzona i zatwierdzona przez właściciela (FR-163).
- **SC-030**: Nowa osoba, korzystając wyłącznie z `corpus/README.md`, generuje korpus od nowa, dodaje
  dokumenty o innej liczbie stron i nowy blok treści bez zmiany kodu.
- **SC-031**: W każdym dokumencie bazowym bloki wspólne stanowią ≤ 20% słów treści, a żaden blok
  niewspólny nie występuje w dwóch różnych dokumentach (poza wersjami i dokumentami zatrutymi).

## Assumptions

- Prawdziwe dokumenty bankowe nie są commitowane; jedyną inspiracją układów są specyfikacje 001/002 i
  ich syntetyczne przypadki testowe. Treść bloków jest pisana od nowa dla fikcyjnego banku.
- „Ukryte” polecenia dla AI oznaczają tekst widoczny, ale niepozorny (wpleciony w akapit, przypis,
  komórkę tabeli, metryczkę) — nie tekst niewidoczny, który biblioteka celowo pomija (FR-013). Jeśli
  właściciel zechce testować także tekst niewidoczny, będzie to osobny rodzaj problemu z innym
  oczekiwanym wynikiem (brak w Markdown).
- Data odniesienia korpusu (względem której dokument jest „obowiązujący” lub „nieaktualny”) jest
  parametrem przebiegu; domyślnie 2026-10-01.
- Dokumenty zatrute są dodatkowe względem 10 dokumentów na typ; domyślnie powstają dokładnie 2 na parę
  typ × rodzaj (5 rodzajów × 3 typy = 30 dokumentów).
- Konstruktor syntetycznych PDF, dziś w projekcie testowym, będzie dostępny dla generatora; sposób
  współdzielenia (np. osobny projekt) jest decyzją planu.
- Markdown korpusu jest generowany z ustawieniami domyślnymi biblioteki (ze znacznikami stron
  `<!-- page: N -->` jak w FR-002a); aplikacja RAG może je wykorzystać jako numer strony w cytowaniu.
- Rozmiar zacommitowanego korpusu (kilkadziesiąt PDF-ów po 20–30 stron) jest akceptowalny; sposób
  ograniczenia rozmiaru plików (np. podzbiór czcionek) jest decyzją planu.
- Akty prawne są pobierane z Dziennika Ustaw (dziennikustaw.gov.pl), bo ISAP blokuje pobieranie
  automatyczne (jak w feature 001); teksty aktów nie podlegają prawu autorskiemu. Akty nie mają prawdy
  referencyjnej generatora; dla nich obowiązuje SC-020 (komplet plików i wpisów) i SC-029.
- Obowiązuje konstytucja projektu: TDD z osobnymi commitami red/green, testy offline i deterministyczne,
  generator jako cienka aplikacja nad logiką w bibliotece klas, bez sekretów w repozytorium.
- Brak hooka tworzącego gałąź git: specyfikacja powstaje w katalogu `specs/003-synthetic-bank-corpus`
  na gałęzi `003-synthetic-bank-corpus`, utworzonej z `002-table-document-sections` (spec 002 nie jest
  jeszcze scalona z `main`).
