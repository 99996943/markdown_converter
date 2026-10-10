# Feature Specification: Pobieranie regulaminów — aplikacja mBank.FaqGenerator

**Feature Branch**: `005-regulation-download`

**Created**: 2026-10-09

**Status**: Draft

**Input**: User description: "Pobieranie regulaminów. Kontekst: Konwerter repozytorium regulaminów — zaimplementuj skrypt automatycznie pobierający 5 wskazanych regulaminów ze strony mBanku, konwertujący je z formatu PDF do Markdown oraz generujący na ich podstawie plik FAQ. Przygotuj specyfikację dla aplikacji konsolowej w .NET o nazwie mBank.FaqGenerator. Skrypt ma prosić po kolei o podanie 5 adresów URL do publicznych regulaminów w formacie PDF ze strony mBanku. Skrypt używa HttpClient do asynchronicznego pobrania tych plików i zapisania ich w lokalnym folderze ./downloads. Dodaj obsługę błędów (try-catch) dla niedostępnych linków."

## Kontekst

Feature rozpoczyna aplikację konsolową **mBank.FaqGenerator**, której docelowy przepływ ma trzy
etapy: (1) pobranie 5 publicznych regulaminów mBanku w PDF, (2) konwersja PDF → Markdown istniejącym
parserem (spec 001/002), (3) wygenerowanie pliku FAQ w formacie OKF (konstytucja, „Format wiedzy”).

**Ta specyfikacja obejmuje wyłącznie etap 1 — pobieranie.** Konwersja i generowanie FAQ to osobne
specyfikacje; ten etap zostawia w katalogu pobrań wszystko, czego one potrzebują (pliki PDF i adres
źródła każdego pliku — pole `resource` w OKF).

Numeracja wymagań jest kontynuowana: FR-300 i dalej, SC-060 i dalej.

## Clarifications

### Session 2026-10-09

- Q: Czy ta specyfikacja obejmuje tylko pobieranie, czy cały przepływ (pobranie, konwersja, FAQ)? → A: Tylko pobieranie; konwersja do Markdown i FAQ w OKF to kolejne specyfikacje.
- Q: Czy aplikacja przyjmuje tylko adresy z domeny mBanku, czy dowolny publiczny PDF? → A: Tylko hosty z listy w konfiguracji, domyślnie `mbank.pl` i jego subdomeny; adres i przekierowanie spoza listy są odrzucane (FR-302, FR-312).
- Q: Skąd aplikacja bierze adresy, gdy użytkownik ich nie wpisuje? → A: Pytania w konsoli są trybem domyślnym; 5 adresów z argumentów wywołania lub z konfiguracji pomija pytania, argumenty mają pierwszeństwo (FR-301, FR-303).
- Q: Co zrobić z plikami PDF w katalogu pobrań, które nie pochodzą z bieżącego uruchomienia? → A: Po udanym pobraniu usunąć pliki PDF spoza bieżącej listy, tak by katalog zawierał dokładnie bieżący zestaw (FR-325).
- (analiza) Sprzątanie po pełnym sukcesie usuwa też pozostałości `*.part` po przerwanym procesie (FR-325).
- (analiza) Pusta lista adresów w konfiguracji oznacza brak adresów — tryb pytań; kod 2 tylko dla niepustej listy o liczbie innej niż 5 (FR-303).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Interaktywne podanie 5 adresów i pobranie regulaminów (Priority: P1)

Użytkownik uruchamia aplikację bez parametrów. Aplikacja prosi po kolei o 5 adresów URL
regulaminów („Podaj adres regulaminu 1 z 5: ”), sprawdza każdy adres od razu po wpisaniu, a po
zebraniu wszystkich pobiera pliki i zapisuje je w katalogu `./downloads`. Na koniec wypisuje
podsumowanie: który adres pobrano do jakiego pliku i ile zajmuje.

**Why this priority**: to główny scenariusz z opisu — bez pobranych plików kolejne etapy
(konwersja, FAQ) nie mają danych wejściowych.

**Independent Test**: uruchomienie z 5 adresami wskazującymi na dostępne pliki PDF (w testach —
atrapa serwera) daje 5 plików PDF w `./downloads`, podsumowanie i kod wyjścia 0.

**Acceptance Scenarios**:

1. **Given** pusty lub nieistniejący katalog `./downloads`, **When** użytkownik poda 5 poprawnych
   adresów dostępnych regulaminów PDF, **Then** katalog zostaje utworzony, zawiera 5 plików PDF o
   treści identycznej z pobraną, a aplikacja kończy się kodem 0.
2. **Given** aplikacja prosi o adres 2 z 5, **When** użytkownik wpisze tekst niebędący poprawnym
   adresem (np. „abc”, adres `ftp://…`, pusty wiersz), **Then** aplikacja wypisuje, co jest nie tak,
   i ponownie prosi o adres 2 z 5, bez zmiany numeracji.
3. **Given** użytkownik podał już adres X, **When** poda ten sam adres ponownie, **Then** aplikacja
   odrzuca duplikat z komunikatem i prosi o inny adres.
4. **Given** wszystkie 5 adresów zostało zebranych, **When** rozpoczyna się pobieranie, **Then**
   pliki są pobierane równolegle, a użytkownik widzi postęp (rozpoczęcie i wynik dla każdego adresu).

---

### User Story 2 - Niedostępne lub błędne linki nie przerywają pracy (Priority: P1)

Część adresów okazuje się niedostępna: serwer zwraca błąd (np. 404, 500), nie odpowiada w zadanym
czasie, nazwa hosta nie istnieje albo pod adresem jest strona HTML zamiast PDF. Aplikacja nie
przerywa pobierania pozostałych plików; dla każdego błędnego adresu wypisuje czytelny komunikat z
przyczyną, a w podsumowaniu wyraźnie oddziela pobrane od niepobranych. Kończy się niezerowym kodem
wyjścia, żeby niepełny wynik nie został uznany za poprawny.

**Why this priority**: wymaganie wprost z opisu (obsługa niedostępnych linków) i z konstytucji
(zasada IV) — adresy regulaminów mBanku zmieniają się, więc martwe linki są normalną sytuacją.

**Independent Test**: uruchomienie z 3 poprawnymi adresami i 2 błędnymi (404 oraz przekroczony
czas odpowiedzi, z atrapy serwera) daje 3 pliki, 2 komunikaty błędów z przyczynami, podsumowanie
3/5 i niezerowy kod wyjścia.

**Acceptance Scenarios**:

1. **Given** jeden z adresów zwraca 404, **When** trwa pobieranie, **Then** pozostałe 4 pliki są
   pobrane, a dla tego adresu wypisany jest komunikat z adresem i przyczyną („serwer zwrócił 404 Not
   Found”).
2. **Given** serwer nie odpowiada dłużej niż ustalony limit czasu, **When** limit minie, **Then**
   pobieranie tego pliku jest przerwane z komunikatem o przekroczeniu czasu, a reszta trwa dalej.
3. **Given** adres zwraca odpowiedź 200 ze stroną HTML (np. strona błędu lub logowania), **When**
   plik zostanie odebrany, **Then** nie jest zapisywany jako regulamin, a komunikat mówi, że pod
   adresem nie ma pliku PDF.
4. **Given** nazwa hosta nie istnieje lub brak połączenia z siecią, **When** trwa pobieranie,
   **Then** komunikat podaje błąd połączenia dla danego adresu, a aplikacja nie kończy się
   nieobsłużonym wyjątkiem.
5. **Given** co najmniej jeden adres się nie powiódł, **When** pobieranie się kończy, **Then**
   aplikacja kończy się niezerowym kodem wyjścia; **Given** żaden adres się nie powiódł, **Then**
   także niezerowym kodem, a katalog pobrań nie zawiera nowych plików PDF (zapisany jest tylko
   manifest z przyczynami błędów).

---

### User Story 3 - Uruchomienie jednym poleceniem bez wpisywania adresów (Priority: P2)

Użytkownik (lub skrypt CI/odtworzeniowy) podaje 5 adresów w konfiguracji aplikacji albo w
argumentach wywołania. Aplikacja wtedy o nic nie pyta i od razu pobiera pliki. Ponowne
uruchomienie z tymi samymi adresami nadpisuje pliki tymi samymi nazwami zamiast tworzyć kopie.

**Why this priority**: konstytucja (zasada III) wymaga uruchomienia jednym poleceniem bez ręcznych
kroków, a (zasada V) trzymania adresów źródeł w konfiguracji, nie w kodzie. Tryb interaktywny z
opisu pozostaje domyślny, gdy adresów nie podano.

**Independent Test**: uruchomienie z 5 adresami w konfiguracji i zamkniętym wejściem
standardowym pobiera pliki bez żadnego pytania; drugie uruchomienie daje te same nazwy plików i
tę samą liczbę plików.

**Acceptance Scenarios**:

1. **Given** konfiguracja zawiera 5 adresów, **When** użytkownik uruchomi aplikację, **Then**
   aplikacja nie prosi o adresy i pobiera te z konfiguracji.
2. **Given** adresy podano w argumentach wywołania i w konfiguracji, **When** aplikacja startuje,
   **Then** pierwszeństwo mają argumenty wywołania.
3. **Given** konfiguracja zawiera niepustą listę o liczbie adresów innej niż 5 albo adres
   niepoprawny, **When**
   aplikacja startuje, **Then** kończy się niezerowym kodem z komunikatem wskazującym błędną
   pozycję, przed rozpoczęciem jakiegokolwiek pobierania.
4. **Given** pliki zostały już pobrane, **When** aplikacja zostanie uruchomiona ponownie z tymi
   samymi adresami, **Then** katalog zawiera te same nazwy plików (nadpisane), bez kopii typu
   „plik (1).pdf”.
6. **Given** katalog pobrań zawiera pliki PDF z wcześniejszego uruchomienia z innymi adresami,
   **When** wszystkie 5 nowych plików zostanie pobranych, **Then** katalog zawiera dokładnie 5
   bieżących plików PDF, a usunięte pliki są wymienione w podsumowaniu; **When** choć jeden adres
   się nie powiedzie, **Then** żaden plik nie jest usuwany.
5. **Given** adresy nie zostały podane, a wejście standardowe nie jest dostępne (np. potok
   zamknięty), **When** aplikacja startuje, **Then** kończy się niezerowym kodem z komunikatem, jak
   podać adresy, zamiast czekać w nieskończoność.

---

### Edge Cases

- Dwa różne adresy kończą się tą samą nazwą pliku (np. `.../a/regulamin.pdf` i
  `.../b/regulamin.pdf`) — nazwy w katalogu muszą być rozróżnione w sposób deterministyczny.
- Adres nie zawiera nazwy pliku albo nazwa zawiera znaki niedozwolone w systemie plików, polskie
  znaki lub zakodowane sekwencje (`%20`, `%C5%82`) — nazwa pliku jest czytelna i bezpieczna na
  Linuxie i Windows.
- Adres zawiera parametry zapytania (`?v=3`) lub fragment (`#page=2`) — nie trafiają do nazwy
  pliku.
- Serwer przekierowuje (301/302) na inny adres — przekierowanie jest wykonywane, a jako źródło
  zapisywany jest adres podany przez użytkownika.
- Połączenie zostaje zerwane w trakcie pobierania — w katalogu nie zostaje niekompletny plik, a
  wcześniej pobrana wersja tego pliku (z poprzedniego uruchomienia) nie zostaje uszkodzona.
- Plik jest bardzo duży (powyżej limitu) — pobieranie jest przerywane z komunikatem.
- Użytkownik przerywa program (Ctrl+C) w trakcie pobierania — aplikacja kończy się niezerowym
  kodem, bez niekompletnych plików w katalogu.
- Katalog `./downloads` nie może zostać utworzony lub zapisany (brak uprawnień, brak miejsca) —
  czytelny komunikat i niezerowy kod wyjścia.
- Adres wskazuje na host spoza dozwolonej listy (np. inny bank) — odrzucony przy wpisywaniu.
- Katalog pobrań zawiera pliki PDF z poprzednich uruchomień z innymi adresami — usuwane dopiero po
  pobraniu wszystkich 5 bieżących plików (FR-325).
- Wpisany adres ma spacje na początku lub końcu — są pomijane.

## Requirements *(mandatory)*

### Functional Requirements

**Zbieranie adresów**

- **FR-300**: Aplikacja MUSI zebrać dokładnie 5 adresów regulaminów przed rozpoczęciem
  pobierania.
- **FR-301**: Gdy adresów nie podano w argumentach ani w konfiguracji, aplikacja MUSI prosić o nie
  po kolei, pokazując numer bieżącego adresu i ich łączną liczbę (np. „2 z 5”).
- **FR-302**: Każdy adres MUSI być sprawdzany zaraz po wpisaniu; adres jest poprawny, gdy jest
  bezwzględnym adresem `https` (albo `http`, jeśli konfiguracja na to pozwala), jego host należy do
  listy dozwolonych hostów z konfiguracji (domyślnie `mbank.pl` i jego subdomeny) i nie powtarza
  adresu podanego wcześniej. Niepoprawny adres MUSI skutkować komunikatem z przyczyną i ponownym
  pytaniem o ten sam numer.
- **FR-303**: Aplikacja MUSI przyjmować 5 adresów także z argumentów wywołania lub z konfiguracji;
  argumenty mają pierwszeństwo przed konfiguracją. W tym trybie aplikacja o nic nie pyta, a
  niepoprawna lista (inna liczba adresów, niepoprawny adres, duplikat) MUSI zakończyć program
  niezerowym kodem przed rozpoczęciem pobierania. Pusta lista adresów w konfiguracji oznacza brak
  adresów (tryb pytań, FR-301).
- **FR-304**: Gdy adresy trzeba wpisać, a wejście standardowe jest niedostępne lub zamknięte,
  aplikacja MUSI zakończyć się niezerowym kodem z informacją, jak podać adresy.

**Pobieranie**

- **FR-310**: Aplikacja MUSI pobierać pliki asynchronicznie i równolegle; błąd jednego pobrania NIE
  MOŻE przerywać pozostałych.
- **FR-311**: Każde pobranie MUSI mieć limit czasu (domyślnie 60 s na plik, wartość w konfiguracji)
  i limit rozmiaru (domyślnie 50 MB, wartość w konfiguracji).
- **FR-312**: Aplikacja MUSI wykonywać przekierowania HTTP, ale tylko do hostów z listy
  dozwolonych; przekierowanie na host spoza listy kończy pobranie tego adresu błędem z nazwą
  hosta docelowego.
- **FR-313**: Plik uznaje się za regulamin PDF tylko wtedy, gdy jego treść zaczyna się od sygnatury
  PDF (`%PDF-`); w przeciwnym razie pobranie kończy się błędem „pod adresem nie ma pliku PDF”,
  niezależnie od deklarowanego typu odpowiedzi.
- **FR-314**: Każdy błąd pobrania (odpowiedź z kodem błędu, przekroczony czas, błąd połączenia lub
  DNS, przekroczony rozmiar, brak PDF, błąd zapisu) MUSI zostać obsłużony i zgłoszony komunikatem
  zawierającym adres oraz zrozumiałą przyczynę; aplikacja NIE MOŻE kończyć się nieobsłużonym
  wyjątkiem.
- **FR-315**: Przerwanie programu przez użytkownika MUSI anulować trwające pobrania i zakończyć
  program niezerowym kodem.

**Zapis plików**

- **FR-320**: Pliki MUSZĄ być zapisywane w katalogu `./downloads` względem bieżącego katalogu
  roboczego (ścieżka zmienialna w konfiguracji); brakujący katalog MUSI zostać utworzony.
- **FR-321**: Nazwa pliku MUSI pochodzić z ostatniego segmentu ścieżki adresu (zdekodowanego, bez
  parametrów zapytania i fragmentu), oczyszczonego ze znaków niedozwolonych na Linuxie i Windows,
  z rozszerzeniem `.pdf`. Gdy segmentu brak, nazwą jest `regulamin-N.pdf` (N — numer adresu 1–5).
  Gdy dwa adresy dają tę samą nazwę, drugi i kolejne dostają przyrostek `-N` z numerem adresu.
  Nazwy zależą wyłącznie od listy adresów i jej kolejności.
- **FR-322**: Plik MUSI trafiać pod docelową nazwę dopiero po pomyślnym pobraniu i sprawdzeniu
  całości; przerwane lub nieudane pobranie NIE MOŻE zostawić niekompletnego pliku ani uszkodzić
  pliku o tej nazwie z poprzedniego uruchomienia.
- **FR-323**: Ponowne uruchomienie z tymi samymi adresami MUSI nadpisywać pliki o tych samych
  nazwach, bez tworzenia kopii.
- **FR-324**: Aplikacja MUSI zapisać w katalogu pobrań plik opisu pobrania (manifest), który dla
  każdego z 5 adresów podaje: numer, adres podany przez użytkownika, wynik (pobrany / błąd),
  nazwę pliku, rozmiar, skrót SHA-256 treści, datę modyfikacji podaną przez serwer (jeśli ją
  podał) albo przyczynę błędu. Manifest jest zapisywany także przy częściowym niepowodzeniu i nie
  zawiera danych zależnych od chwili uruchomienia, więc ponowne pobranie niezmienionych plików daje
  identyczny manifest. Format manifestu ustala plan.
- **FR-325**: Gdy wszystkie 5 plików zostało pobranych i zapisanych, aplikacja MUSI usunąć z
  katalogu pobrań pliki PDF, których nazwy nie należą do bieżącego zestawu 5 nazw, i wypisać
  nazwy usuniętych plików. Przy jakimkolwiek niepowodzeniu (błąd adresu, pobrania, zapisu,
  przerwanie) aplikacja NIE MOŻE usuwać żadnych plików. Spośród innych plików usuwane są tylko
  pozostałości niedokończonych pobrań (`*.part`); pozostałe pliki (np. manifest) i podkatalogi
  nie są usuwane.

**Wynik i kody wyjścia**

- **FR-330**: Po zakończeniu aplikacja MUSI wypisać podsumowanie: liczbę pobranych plików na 5 oraz
  dla każdego adresu nazwę pliku i rozmiar albo przyczynę błędu.
- **FR-331**: Aplikacja MUSI kończyć się kodem 0 wyłącznie wtedy, gdy wszystkie 5 plików zostało
  pobranych i zapisanych; w każdym innym przypadku (błąd adresów, błąd pobrania, przerwanie, błąd
  zapisu) — niezerowym kodem. Znaczenie poszczególnych kodów ustala plan i opisuje README.
- **FR-332**: Logika sprawdzania adresów i pobierania MUSI być dostępna niezależnie od
  konsoli (do ponownego użycia i testów), a lista dozwolonych hostów i adresy źródeł należą
  wyłącznie do konfiguracji aplikacji, nie do tej logiki (konstytucja: „Architektura”).
- **FR-333**: README MUSI opisywać uruchomienie aplikacji w obu trybach, konfigurację (adresy,
  dozwolone hosty, limity, katalog) i kody wyjścia.

### Key Entities

- **Adres regulaminu**: adres podany przez użytkownika, z numerem pozycji 1–5; po sprawdzeniu
  poprawny albo odrzucony z przyczyną.
- **Wynik pobrania**: dla jednego adresu — pobrany (nazwa pliku, rozmiar, skrót treści, data
  modyfikacji od serwera) albo błąd (rodzaj i opis przyczyny).
- **Manifest pobrania**: plik w katalogu pobrań z wynikami dla wszystkich 5 adresów; wiąże każdy
  plik PDF z adresem źródła, z którego skorzysta etap FAQ (pole `resource` w OKF).
- **Konfiguracja pobierania**: lista adresów (opcjonalna), dozwolone hosty, katalog pobrań, limit
  czasu, limit rozmiaru.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-060**: Przy 5 dostępnych regulaminach użytkownik od uruchomienia do podsumowania
  potrzebuje tylko wpisania 5 adresów; pobranie 5 plików po kilka MB kończy się w czasie
  zbliżonym do najdłuższego pojedynczego pobrania, nie do sumy wszystkich (pobieranie równoległe).
- **SC-061**: W 100% scenariuszy błędów z User Story 2 (404, 500, przekroczony czas, nieistniejący
  host, HTML zamiast PDF, przekroczony rozmiar) aplikacja kończy pracę bez awarii, pobiera
  pozostałe pliki, zgłasza przyczynę dla każdego błędnego adresu i zwraca niezerowy kod wyjścia.
- **SC-062**: Po żadnym nieudanym ani przerwanym pobraniu w katalogu pobrań nie ma
  niekompletnego pliku PDF (każdy plik o nazwie docelowej zaczyna się sygnaturą PDF i ma pełny
  rozmiar).
- **SC-063**: Dwa kolejne uruchomienia z tymi samymi adresami i niezmienionymi plikami po stronie
  serwera dają identyczną zawartość katalogu pobrań (te same nazwy, treść i manifest); po udanym
  uruchomieniu katalog zawiera dokładnie 5 plików PDF — z bieżącej listy adresów.
- **SC-064**: Wszystkie scenariusze akceptacyjne są sprawdzane testami automatycznymi bez dostępu
  do sieci.
- **SC-065**: Nowa osoba uruchamia pobieranie według README, bez pomocy autora, za pierwszym
  razem.

## Assumptions

- **Zakres** (potwierdzony przez właściciela): wyłącznie pobieranie; konwersja do Markdown i
  generowanie FAQ (OKF) z kontekstu zgłoszenia będą osobnymi specyfikacjami, rozszerzającymi tę
  samą aplikację.
- **Tryb nieinteraktywny** (potwierdzony przez właściciela): opis wymaga pytania o adresy po kolei; konstytucja (zasady III i V)
  wymaga uruchomienia jednym poleceniem i adresów w konfiguracji. Pytanie pozostaje domyślnym
  trybem, gdy adresów nie podano, a adresy z argumentów lub konfiguracji je pomijają.
- **Dozwolone hosty** (potwierdzone przez właściciela): „ze strony mBanku” oznacza host `mbank.pl`
  lub jego subdomenę; lista jest w konfiguracji aplikacji, nie w kodzie logiki, więc można ją rozszerzyć (np. o
  serwer plików banku) bez zmiany kodu.
- **Sprawdzenie PDF**: wystarcza sygnatura `%PDF-`; pełna walidacja struktury PDF należy do etapu
  konwersji.
- **Bez ponowień**: nieudane pobranie nie jest automatycznie ponawiane; użytkownik uruchamia
  aplikację ponownie (pliki pobrane wcześniej są nadpisywane, FR-323).
- **Domyślne limity**: 60 s na plik i 50 MB na plik — regulaminy bankowe mają zwykle od kilkuset
  KB do kilku MB.
- **Brak uwierzytelniania**: regulaminy są publiczne; aplikacja nie obsługuje logowania, ciasteczek
  ani proxy wymagającego uwierzytelnienia (proxy systemowe jest używane, jeśli jest ustawione).
- **Preferencje techniczne z opisu** (do planu, nie wymagania funkcjonalne): .NET 9, nazwa
  aplikacji `mBank.FaqGenerator`, pobieranie przez `HttpClient` asynchronicznie, obsługa błędów
  przez `try-catch`. Zgodnie z konstytucją logika trafia do biblioteki, a aplikacja konsolowa jest
  cienką warstwą; projekty dołączane są do `LegalAgent.slnx`. Nazwa biblioteki i jej związek z
  istniejącymi projektami `LegalAgent.*` ustala plan.
- **Testy**: zgodnie z konstytucją (zasada I) testy używają atrapy serwera/HTTP i nie łączą się z
  mBankiem; prawdziwe adresy służą tylko do ręcznej weryfikacji.
- **Prywatność**: pobrane pliki to publiczne regulaminy prawdziwego banku — katalog `downloads/`
  nie jest częścią repozytorium (git-ignored) i nie trafia do korpusu syntetycznego.
