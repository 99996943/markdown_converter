# Feature Specification: Konwersja i generowanie FAQ — aplikacja mBank.FaqGenerator

**Feature Branch**: `006-faq-generation`

**Created**: 2026-10-09

**Status**: Draft

**Input**: User description: "Przygotuj specyfikację będącą rozszerzeniem aplikacji mBank.FaqGenerator. Aplikacja ma wywołać bibliotekę LegalAgent.PdfParser dla każdego pobranego pliku. Następnie aplikacja ma poprosić użytkownika w konsoli o podanie klucza API do Azure OpenAI (ukrywając wpisywane znaki jak przy wpisywaniu hasła). Klucz ma być trzymany WYŁĄCZNIE w pamięci procesu, bez zapisywania do jakichkolwiek plików czy zmiennych środowiskowych systemu. Używając tego klucza, Microsoft.SemanticKernel i modelu GPT-4o-mini (chyba że ustalimy inny), skrypt wyśle połączony tekst Markdown i wygeneruje na jego podstawie 10 najważniejszych pytań i odpowiedzi (FAQ). Wynik zostanie zapisany w pliku FAQ_mBank.md. Dodamy też skrypt który stworzy zasób na azure."

## Kontekst

Spec 005 zbudowała etap 1 aplikacji **mBank.FaqGenerator**: pobranie 5 regulaminów PDF do katalogu
pobrań wraz z manifestem (adres źródła, nazwa pliku, skrót treści). Ta specyfikacja dodaje dwa
kolejne etapy tego samego uruchomienia:

2. **Konwersja** — każdy pobrany PDF jest zamieniany na Markdown istniejącym parserem (spec 001/002).
3. **FAQ** — Markdown każdego dokumentu trafia do modelu językowego w usłudze Azure OpenAI, który
   proponuje pytania kandydujące; potem model wybiera z kandydatów ze wszystkich dokumentów 10
   najważniejszych pytań i odpowiedzi; wynik jest zapisywany w `FAQ_mBank.md`.

Do tego dochodzi skrypt, który tworzy w Azure zasób usługi i wdrożenie modelu potrzebne w etapie 3.

Numeracja wymagań jest kontynuowana: FR-400 i dalej, SC-070 i dalej.

## Clarifications

### Session 2026-10-09

- Q: Połączony Markdown 5 regulaminów (>200 tys. tokenów) nie mieści się w 128 tys. tokenów GPT-4o-mini — jak generować FAQ? → A: Zostaje GPT-4o-mini, praca w dwóch krokach: pytania kandydujące z każdego dokumentu osobno, potem jedno zapytanie wybiera 10 najważniejszych (FR-421).
- Q: Skąd aplikacja bierze klucz, gdy nikt go nie wpisuje (uruchomienie jednym poleceniem, CI)? → A: Z kolejnego wiersza przekierowanego wejścia standardowego; nigdy z plików, argumentów ani zmiennych środowiskowych. Przy wpisywaniu w konsoli zamiast każdego znaku (także wklejonego) wyświetlana jest gwiazdka (FR-410, FR-412).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Pełny przebieg: pobranie, konwersja, FAQ (Priority: P1)

Użytkownik uruchamia aplikację tak jak w spec 005 (adresy wpisane w konsoli albo podane w
argumentach lub konfiguracji). Po pobraniu 5 plików aplikacja konwertuje każdy z nich do Markdown i
pokazuje postęp. Następnie prosi o klucz API usługi Azure OpenAI; wpisywane znaki nie pojawiają się
na ekranie. Aplikacja wysyła treść dokumentów do modelu, odbiera 10 pytań i odpowiedzi, zapisuje
je w `FAQ_mBank.md` i wypisuje podsumowanie wszystkich etapów.

**Why this priority**: to cel całej aplikacji — plik FAQ zbudowany z prawdziwych regulaminów.

**Independent Test**: uruchomienie z 5 adresami (atrapa serwera plików), atrapą usługi modelu
zwracającą poprawne FAQ i kluczem podanym na wejściu daje 5 plików Markdown, plik `FAQ_mBank.md`
z 10 parami pytanie–odpowiedź i kod wyjścia 0.

**Acceptance Scenarios**:

1. **Given** 5 plików zostało pobranych, **When** rozpoczyna się etap konwersji, **Then** dla
   każdego pliku powstaje plik Markdown obok pliku PDF, a użytkownik widzi wynik konwersji każdego
   pliku (nazwa, liczba stron, ostrzeżenia parsera).
2. **Given** konwersja się powiodła, **When** aplikacja prosi o klucz, **Then** zamiast każdego
   wpisanego lub wklejonego znaku pojawia się gwiazdka (`*`), Backspace usuwa ostatni znak i jedną
   gwiazdkę, a Enter kończy wpisywanie.
3. **Given** podano klucz, **When** model zwróci poprawną odpowiedź, **Then** powstaje plik
   `FAQ_mBank.md` z dokładnie 10 pytaniami i odpowiedziami w języku polskim, a każda odpowiedź
   wskazuje dokument źródłowy.
4. **Given** plik `FAQ_mBank.md` istnieje z poprzedniego uruchomienia, **When** nowe FAQ zostanie
   wygenerowane, **Then** plik jest zastąpiony w całości; **When** generowanie się nie powiedzie,
   **Then** poprzedni plik pozostaje nienaruszony.

---

### User Story 2 - Klucz API nigdy nie opuszcza pamięci procesu (Priority: P1)

Właściciel chce mieć pewność, że klucz do płatnej usługi nie wycieknie: nie trafi do żadnego pliku
(konfiguracja, manifest, Markdown, FAQ, logi), do zmiennych środowiskowych ani na ekran — także w
komunikatach o błędach.

**Why this priority**: wymaganie wprost z opisu („WYŁĄCZNIE w pamięci procesu”) i z konstytucji
(zasada V: brak sekretów w repozytorium).

**Independent Test**: uruchomienie z charakterystycznym kluczem testowym, w tym z błędem
uwierzytelnienia zwracanym przez atrapę usługi; przeszukanie całego wyjścia konsoli, katalogu
pobrań, pliku FAQ i zmiennych środowiskowych procesu nie znajduje klucza.

**Acceptance Scenarios**:

1. **Given** użytkownik wpisuje klucz, **When** wpisywanie trwa i po jego zakończeniu, **Then**
   na ekranie widać tylko gwiazdki (po jednej na znak), a żaden znak klucza się nie pojawia.
2. **Given** wejście aplikacji jest przekierowane (np. klucz podany potokiem z pliku lub menedżera
   haseł), **When** aplikacja potrzebuje klucza, **Then** odczytuje go z kolejnego wiersza wejścia,
   bez gwiazdek i bez echa, i nie wypisuje go.
3. **Given** usługa odrzuca klucz (błąd uwierzytelnienia), **When** aplikacja zgłasza błąd,
   **Then** komunikat mówi, że klucz jest nieprawidłowy lub nie ma dostępu do wdrożenia, ale nie
   zawiera klucza ani jego fragmentu, a aplikacja kończy się niezerowym kodem.
4. **Given** aplikacja zakończyła pracę (sukcesem lub błędem), **When** sprawdzi się pliki
   utworzone lub zmienione przez aplikację oraz zmienne środowiskowe procesu, **Then** klucz nie
   występuje w żadnym z nich.
5. **Given** użytkownik wpisze pusty klucz, **When** naciśnie Enter, **Then** aplikacja ponownie
   prosi o klucz, nie wysyłając żadnego zapytania.

---

### User Story 3 - Błędy konwersji i usługi modelu są obsłużone (Priority: P2)

Pobranie się udało, ale jeden PDF nie daje się przetworzyć, usługa modelu nie odpowiada, odrzuca
zapytanie (limit, filtr treści, zbyt długi tekst) albo zwraca odpowiedź niezgodną z oczekiwanym
formatem. Aplikacja nie kończy się awarią, nie zapisuje niepełnego FAQ i mówi, co poszło nie tak.

**Why this priority**: konstytucja (zasada IV) — niepełny wynik nie może być uznany za poprawny;
odpowiedź modelu jest niepewna z natury.

**Independent Test**: atrapy usługi zwracające kolejno: przekroczenie czasu, błąd limitu zapytań,
odpowiedź z 7 pytaniami, odpowiedź powołującą się na nieistniejący dokument — każde uruchomienie
kończy się czytelnym komunikatem, niezerowym kodem i bez nowego `FAQ_mBank.md`.

**Acceptance Scenarios**:

1. **Given** nie wszystkie 5 plików zostało pobranych, **When** kończy się etap pobierania,
   **Then** aplikacja nie konwertuje plików, nie prosi o klucz i kończy się kodem błędu pobierania
   (jak w spec 005).
2. **Given** jeden plik PDF nie daje się przekonwertować, **When** kończy się etap konwersji,
   **Then** aplikacja wypisuje plik i przyczynę, nie prosi o klucz i kończy się niezerowym kodem.
3. **Given** usługa nie odpowiada w ustalonym czasie lub zwraca błąd (limit zapytań, przeciążenie,
   filtr treści, zbyt długie wejście, nieznane wdrożenie), **When** aplikacja go odbierze, **Then**
   wypisuje zrozumiałą przyczynę i kończy się niezerowym kodem, a plik FAQ nie powstaje.
4. **Given** model zwrócił liczbę pytań inną niż 10, puste pytanie lub odpowiedź, albo powołanie na
   dokument spoza przesłanych, **When** aplikacja sprawdza odpowiedź, **Then** odrzuca ją z
   komunikatem, co jest nie tak, i nie zapisuje FAQ.
5. **Given** konfiguracja usługi jest niepełna (brak adresu usługi lub nazwy wdrożenia), **When**
   aplikacja startuje, **Then** kończy się niezerowym kodem z komunikatem, czego brakuje — przed
   pobieraniem plików i przed pytaniem o klucz.

---

### User Story 4 - Utworzenie zasobu w Azure jednym skryptem (Priority: P2)

Właściciel (lub nowa osoba) nie ma jeszcze usługi Azure OpenAI. Uruchamia dostarczony skrypt,
który tworzy grupę zasobów, zasób usługi i wdrożenie modelu, a na końcu wypisuje wartości do
wpisania w konfiguracji aplikacji (adres usługi, nazwa wdrożenia). Klucz użytkownik odczytuje sam
z portalu lub podanym poleceniem i wpisuje go dopiero w aplikacji albo przekazuje jej potokiem.

**Why this priority**: bez zasobu etap FAQ nie działa, ale zasób tworzy się raz; aplikację można
rozwijać i testować bez niego (atrapa usługi).

**Independent Test**: uruchomienie skryptu na subskrypcji testowej tworzy zasób i wdrożenie;
ponowne uruchomienie z tymi samymi parametrami kończy się sukcesem bez tworzenia duplikatów;
skrypt nie wypisuje i nie zapisuje klucza.

**Acceptance Scenarios**:

1. **Given** zalogowany użytkownik z uprawnieniami do subskrypcji, **When** uruchomi skrypt z
   domyślnymi parametrami, **Then** powstaje zasób z wdrożeniem modelu, a skrypt wypisuje adres
   usługi i nazwę wdrożenia oraz instrukcję, skąd wziąć klucz.
2. **Given** zasób już istnieje, **When** skrypt zostanie uruchomiony ponownie, **Then** kończy się
   sukcesem i niczego nie dubluje.
3. **Given** brak zalogowania, brak narzędzia wiersza poleceń Azure albo model niedostępny w
   wybranym regionie, **When** skrypt startuje, **Then** kończy się niezerowym kodem z komunikatem,
   co zrobić.
4. **Given** skrypt zakończył się sukcesem, **When** sprawdzi się jego wyjście i pliki w
   repozytorium, **Then** nie ma w nich klucza usługi.

---

### Edge Cases

- Połączony tekst 5 regulaminów jest dłuższy, niż model przyjmuje w jednym zapytaniu (dwa
  prawdziwe regulaminy mają po ok. 140–155 tys. znaków Markdown; 5 dokumentów to rząd 700 tys.
  znaków, czyli ponad 200 tys. tokenów — więcej niż 128 tys. tokenów kontekstu modelu GPT-4o-mini)
  — FAQ powstaje w dwóch krokach, każdy dokument w osobnym zapytaniu (FR-421).
- Pojedynczy dokument przekracza limit wejścia jednego zapytania — błąd z nazwą i rozmiarem
  dokumentu przed pytaniem o klucz, bez przycinania treści (FR-421).
- Jedno z 5 zapytań kroku kandydatów się nie powiedzie — krok wyboru nie jest wykonywany, FAQ nie
  powstaje (FR-424).
- Parser zgłasza ostrzeżenia (np. tabela w formacie zastępczym) — konwersja jest udana, ostrzeżenia
  są wypisane, FAQ powstaje.
- PDF jest skanem bez warstwy tekstowej i daje (prawie) pusty Markdown — traktowane jak błąd
  konwersji, bo FAQ z takiego dokumentu nie miałoby źródła.
- Użytkownik naciska Ctrl+C podczas wpisywania klucza lub w trakcie zapytania do usługi — aplikacja
  kończy się kodem przerwania, bez pliku FAQ i bez wypisania klucza.
- Wejście standardowe jest przekierowane — klucz to kolejny wiersz wejścia; wejście zamknięte lub
  bez tego wiersza — błąd z podpowiedzią (FR-412).
- Klucz wklejony ze schowka ze spacją lub znakiem nowego wiersza na końcu — białe znaki na
  brzegach są pomijane.
- Treść usługi w komunikacie błędu zawiera adres zapytania lub nagłówki — komunikat wypisany
  użytkownikowi nie zawiera klucza.
- Model zwraca odpowiedź w bloku kodu, z dodatkowym tekstem przed lub po FAQ — aplikacja albo
  wydobywa z niej FAQ jednoznacznie, albo ją odrzuca (FR-432), nigdy nie zapisuje tego tekstu.
- Ponowne uruchomienie z innymi adresami — pliki Markdown nienależące do bieżącego zestawu są
  usuwane razem z plikami PDF (FR-403).
- Plik `FAQ_mBank.md` jest otwarty w innym programie lub katalog wynikowy nie pozwala na zapis —
  czytelny komunikat, niezerowy kod, poprzedni plik nienaruszony.

## Requirements *(mandatory)*

### Functional Requirements

**Konwersja do Markdown**

- **FR-400**: Po pobraniu wszystkich 5 plików aplikacja MUSI przekonwertować każdy z nich do
  Markdown tym samym parserem i z tymi samymi ustawieniami domyślnymi, co narzędzie `legalagent-pdf
  convert`. Gdy pobieranie nie powiodło się w całości, aplikacja NIE MOŻE przechodzić do konwersji
  ani do FAQ.
- **FR-401**: Plik Markdown MUSI być zapisany w katalogu pobrań obok pliku PDF, pod tą samą nazwą z
  rozszerzeniem `.md`; zapis MUSI być atomowy (bez niekompletnych plików po przerwaniu) i nadpisywać
  plik z poprzedniego uruchomienia.
- **FR-402**: Dla każdego pliku aplikacja MUSI wypisać wynik konwersji: nazwę pliku, liczbę stron i
  ostrzeżenia parsera. Błąd konwersji jednego pliku (uszkodzony PDF, brak tekstu, wyjątek parsera)
  MUSI być zgłoszony z nazwą pliku i przyczyną; aplikacja konwertuje pozostałe pliki, ale nie
  przechodzi do FAQ i kończy się niezerowym kodem. Dokument, którego Markdown nie zawiera tekstu
  poza znacznikami stron, jest błędem konwersji.
- **FR-403**: Sprzątanie katalogu pobrań (spec 005, FR-325) MUSI obejmować także pliki Markdown:
  po udanym przebiegu w katalogu zostają tylko pliki `.md` odpowiadające bieżącym 5 plikom PDF.

**Klucz API**

- **FR-410**: Przed wysłaniem zapytania do usługi modelu aplikacja MUSI poprosić o klucz API w
  konsoli. Wpisywane i wklejane znaki NIE MOGĄ być wyświetlane; zamiast każdego znaku aplikacja
  MUSI wyświetlać gwiazdkę (`*`). Backspace MUSI usuwać ostatni znak i jego gwiazdkę, Enter kończy
  wpisywanie, a białe znaki na brzegach klucza są pomijane. Pusty klucz MUSI skutkować ponownym
  pytaniem.
- **FR-411**: Klucz MUSI istnieć wyłącznie w pamięci procesu. Aplikacja NIE MOŻE zapisywać go do
  żadnego pliku (konfiguracja, manifest, Markdown, FAQ, logi, pliki tymczasowe), ustawiać go w
  zmiennych środowiskowych (procesu ani systemu), wypisywać go ani jego fragmentu (poza gwiazdkami z FR-410), ani
  umieszczać go w komunikatach o błędach. Komunikaty błędów pochodzące z usługi MUSZĄ być
  wypisywane w postaci, z której usunięto klucz, jeśli by w nich wystąpił.
- **FR-412**: Gdy wejście standardowe jest przekierowane (nie jest konsolą), aplikacja MUSI
  odczytać klucz z kolejnego wiersza wejścia (po wierszach z adresami, jeśli adresy też czytano z
  wejścia), bez wyświetlania gwiazdek ani echa. To jedyny sposób podania klucza bez wpisywania:
  aplikacja NIE czyta klucza z plików konfiguracji, argumentów wywołania ani zmiennych
  środowiskowych. Gdy wejście jest zamknięte albo kolejny wiersz jest pusty lub go brak, aplikacja
  MUSI zakończyć się niezerowym kodem z komunikatem, jak podać klucz, zamiast czekać w
  nieskończoność.
- **FR-413**: Aplikacja MUSI pytać o klucz dopiero po udanej konwersji wszystkich plików, tak aby
  użytkownik nie wpisywał klucza, gdy FAQ i tak nie powstanie.

**Generowanie FAQ**

- **FR-420**: Adres usługi, nazwa wdrożenia modelu i pozostałe niesekretne parametry połączenia
  MUSZĄ pochodzić z konfiguracji aplikacji (plik ustawień, zmienne `FAQGEN__…`, jak w spec 005),
  nie z kodu. Domyślnym modelem jest GPT-4o-mini; zmiana modelu MUSI wymagać tylko zmiany
  konfiguracji. Aplikacja MUSI sprawdzić kompletność tej konfiguracji przy starcie, przed
  pobieraniem.
- **FR-421**: FAQ MUSI powstawać w dwóch krokach, bo połączona treść 5 dokumentów nie mieści się w
  jednym zapytaniu do modelu:
  1. **Kandydaci** — dla każdego dokumentu osobne zapytanie z pełnym Markdown tego dokumentu i jego
     oznaczeniem (nazwa pliku, adres źródła z manifestu); model zwraca pytania kandydujące z
     odpowiedziami i źródłami (liczba kandydatów na dokument w konfiguracji, domyślnie 10).
  2. **Wybór** — jedno zapytanie z kandydatami ze wszystkich 5 dokumentów (bez pełnej treści
     dokumentów); model wybiera 10 najważniejszych, może je zredagować i połączyć, ale każda
     odpowiedź MUSI opierać się na treści i źródłach kandydatów.
  Dokument, którego treść przekracza limit wejścia z konfiguracji (domyślnie dopasowany do
  kontekstu GPT-4o-mini z zapasem na polecenie i odpowiedź), MUSI zakończyć program błędem z nazwą
  dokumentu i jego rozmiarem przed pytaniem o klucz; aplikacja nie przycina treści dokumentu.
- **FR-422**: Polecenia dla modelu MUSZĄ wymagać: pytań, które klient banku najczęściej zadałby o
  te dokumenty, wraz z odpowiedziami (w kroku wyboru — dokładnie 10); odpowiedzi opartych wyłącznie
  na przesłanym tekście; wskazania dla każdej odpowiedzi dokumentu źródłowego oraz — gdy to możliwe
  — jednostki (np. „§ 12”, „Art. 5”); jawnego stwierdzenia, gdy dokumenty nie rozstrzygają sprawy
  (konstytucja, zasada II); języka polskiego. Odpowiedź kroku kandydatów MUSI być sprawdzana jak w
  FR-430 (niepuste pary, źródło = ten dokument, jednostka odpowiada nagłówkowi), z wyjątkiem liczby
  par.
- **FR-423**: Każde zapytanie MUSI mieć limit czasu (domyślnie 300 s, wartość w konfiguracji) i ustawienia
  ograniczające losowość odpowiedzi (np. zerowa temperatura), podane w konfiguracji.
- **FR-424**: Każdy błąd usługi (brak odpowiedzi w limicie czasu, błąd uwierzytelnienia, nieznane
  wdrożenie, limit zapytań, filtr treści, zbyt długie wejście, błąd sieci) MUSI być obsłużony i
  zgłoszony zrozumiałym komunikatem; aplikacja NIE MOŻE kończyć się nieobsłużonym wyjątkiem.
  Aplikacja nie ponawia zapytania automatycznie. Niepowodzenie któregokolwiek zapytania kroku
  kandydatów kończy etap FAQ bez kroku wyboru.
- **FR-425**: Aplikacja MUSI wypisać przed każdym zapytaniem, którego dotyczy (dokument lub krok
  wyboru) i jaki ma rozmiar (liczba znaków i przybliżona liczba tokenów), a po odpowiedzi — zużycie
  tokenów podane przez usługę, jeśli je podała; podsumowanie podaje łączne zużycie.

**Sprawdzenie odpowiedzi i zapis FAQ**

- **FR-430**: Odpowiedź kroku wyboru MUSI zostać sprawdzona przed zapisem: dokładnie 10 par, każde
  pytanie i odpowiedź niepuste, pytania niepowtarzające się, każdy wskazany dokument należy do
  przesłanych 5 i jest źródłem co najmniej jednego kandydata. Wskazana jednostka (np. „§ 12”,
  „§ 12 ust. 3”) MUSI odpowiadać nagłówkowi tego dokumentu: jej oznaczenie jest równe oznaczeniu
  lub tekstowi nagłówka albo zaczyna się od oznaczenia nagłówka (doprecyzowanie: ust., pkt); w
  przeciwnym razie odpowiedź jest odrzucana.
- **FR-431**: FAQ MUSI być zapisane w pliku `FAQ_mBank.md` w katalogu wynikowym `faq/` względem bieżącego
  katalogu roboczego (katalog OKF; ścieżka zmienialna w konfiguracji i argumentem wywołania;
  tworzony, jeśli brak). Plik MUSI mieć
  nagłówek YAML zgodny z OKF v0.1 (konstytucja, „Format wiedzy”): `type`, `title`, `description`,
  `resource` (adresy 5 dokumentów źródłowych) i `timestamp` (chwila wygenerowania), oraz informację
  o modelu, który wygenerował treść. Treść: 10 pytań jako nagłówki, pod każdym odpowiedź i jej
  źródło (nazwa dokumentu z adresem, jednostka). Dokładny układ ustala kontrakt w planie.
- **FR-432**: Plik FAQ MUSI zawierać wyłącznie pytania, odpowiedzi i źródła z odpowiedzi modelu oraz
  nagłówek z FR-431; aplikacja NIE MOŻE dopisywać do treści FAQ tekstu spoza odpowiedzi modelu ani
  zapisywać dodatkowego tekstu, który model dołączył poza FAQ.
- **FR-433**: Zapis `FAQ_mBank.md` MUSI być atomowy: nieudane lub przerwane generowanie NIE MOŻE
  zostawić niekompletnego pliku ani uszkodzić pliku z poprzedniego uruchomienia.

**Skrypt zasobu Azure**

- **FR-440**: Repozytorium MUSI zawierać skrypt, który tworzy (jeśli nie istnieją) grupę zasobów,
  zasób usługi Azure OpenAI i wdrożenie modelu. Nazwa grupy, nazwa zasobu, region, model, wersja
  modelu i przepustowość wdrożenia MUSZĄ być parametrami z wartościami domyślnymi (model domyślny
  zgodny z FR-420).
- **FR-441**: Skrypt MUSI być idempotentny (ponowne uruchomienie kończy się sukcesem bez duplikatów)
  i działać na Linuksie (konstytucja, „Platforma”). Na końcu MUSI wypisać adres usługi i nazwę
  wdrożenia w postaci gotowej do wpisania w konfigurację aplikacji.
- **FR-442**: Skrypt NIE MOŻE wypisywać, zapisywać do pliku ani eksportować klucza usługi; wypisuje
  jedynie, skąd go odczytać.
- **FR-443**: Skrypt MUSI sprawdzić warunki wstępne (narzędzie wiersza poleceń Azure, zalogowanie,
  dostępność modelu w regionie, gdy da się to sprawdzić) i przy ich braku zakończyć się niezerowym
  kodem z instrukcją.
- **FR-444**: README MUSI opisywać utworzenie zasobu skryptem, usunięcie zasobu (by nie ponosić
  kosztów), konfigurację połączenia w aplikacji i pełny przebieg aplikacji.

**Wynik i kody wyjścia**

- **FR-450**: Podsumowanie MUSI obejmować wszystkie etapy: pobranie (jak w spec 005), konwersję
  (pliki, strony, ostrzeżenia) i FAQ (ścieżka pliku, liczba pytań, zużycie tokenów).
- **FR-451**: Aplikacja MUSI kończyć się kodem 0 wyłącznie wtedy, gdy wszystkie trzy etapy zakończyły
  się sukcesem i zapisano `FAQ_mBank.md`. Błąd konwersji, błąd konfiguracji usługi, błąd klucza
  lub usługi i odrzucona odpowiedź modelu MUSZĄ dawać niezerowe kody, rozróżnialne od kodów spec 005;
  ich znaczenie ustala plan i opisuje README.
- **FR-452**: Logika konwersji wielu plików, budowania zapytania, sprawdzania odpowiedzi i
  renderowania FAQ MUSI być dostępna niezależnie od konsoli i od konkretnej usługi modelu (do
  ponownego użycia i testów atrapą usługi); nazwa „mBank” i nazwa pliku `FAQ_mBank.md` należą do
  aplikacji, nie do tej logiki (konstytucja, „Architektura”).

### Key Entities

- **Dokument przekonwertowany**: pobrany plik PDF z manifestu, jego plik Markdown, adres źródła,
  liczba stron, ostrzeżenia parsera.
- **Klucz API**: sekret podany przez użytkownika; istnieje tylko w pamięci procesu przez czas
  uruchomienia.
- **Konfiguracja usługi modelu**: adres usługi, nazwa wdrożenia, model, limit czasu, ustawienia
  losowości; bez klucza.
- **Pozycja FAQ**: pytanie, odpowiedź, źródło (dokument i — jeśli wskazana — jednostka).
- **Kandydat**: pozycja FAQ zaproponowana dla jednego dokumentu w kroku kandydatów; wejście kroku
  wyboru.
- **Plik FAQ**: nagłówek OKF (typ, tytuł, opis, adresy źródeł, czas wygenerowania, model) i 10
  pozycji FAQ.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-070**: Od uruchomienia do gotowego `FAQ_mBank.md` użytkownik wykonuje tylko dwie czynności:
  podanie 5 adresów (lub ich konfigurację) i wpisanie klucza (lub przekazanie go potokiem).
- **SC-071**: W 100% scenariuszy testowych (sukces, błąd uwierzytelnienia, przekroczony czas,
  odrzucona odpowiedź, przerwanie) klucz testowy nie występuje w wyjściu konsoli, w plikach
  utworzonych przez aplikację ani w zmiennych środowiskowych.
- **SC-072**: Każde udane uruchomienie daje plik FAQ z dokładnie 10 pytaniami, z których każde ma
  odpowiedź i źródło wskazujące jeden z 5 przesłanych dokumentów.
- **SC-073**: W 100% scenariuszy błędów z User Story 3 aplikacja kończy pracę bez awarii, z
  komunikatem przyczyny i niezerowym kodem, a poprzedni `FAQ_mBank.md` (jeśli był) pozostaje
  nienaruszony.
- **SC-074**: Przy ręcznej weryfikacji na 5 prawdziwych regulaminach właściciel potwierdza, że
  każda z 10 odpowiedzi jest zgodna ze wskazanym fragmentem dokumentu.
- **SC-075**: Nowa osoba według README tworzy zasób skryptem i generuje FAQ, bez pomocy autora,
  za pierwszym razem; ponowne uruchomienie skryptu nie zmienia istniejącego zasobu.
- **SC-076**: Wszystkie scenariusze akceptacyjne aplikacji są sprawdzane testami automatycznymi bez
  dostępu do sieci i bez prawdziwej usługi modelu.

## Assumptions

- **Zakres**: rozszerzenie tej samej aplikacji i tego samego uruchomienia — pobranie, konwersja i
  FAQ następują po sobie; osobne uruchamianie samej konwersji lub samego FAQ na wcześniej pobranych
  plikach jest poza zakresem.
- **Format FAQ a OKF**: konstytucja wymaga OKF (katalog plików Markdown z nagłówkiem YAML). Opis
  wskazuje jeden plik `FAQ_mBank.md`; przyjęto jeden plik OKF z nagłówkiem YAML w osobnym
  katalogu `faq/`, który jest katalogiem OKF z jednym dokumentem. `type` = `faq`.
- **Powtarzalność (zasada III)**: odpowiedź modelu nie jest w pełni deterministyczna, a `timestamp`
  zmienia się przy każdym uruchomieniu, więc `FAQ_mBank.md` może się różnić między uruchomieniami.
  Odstępstwo jest jawne; aplikacja ogranicza losowość (FR-423), a powtarzalne pozostają pobranie,
  konwersja i sprawdzenie odpowiedzi.
- **Sprawdzanie treści odpowiedzi**: aplikacja sprawdza format i źródła (FR-430), ale nie
  zgodność merytoryczną odpowiedzi z dokumentem; tę ocenia człowiek (SC-074).
- **Usługa modelu**: Azure OpenAI z kluczem API (nie z tożsamością Entra ID). Preferencje
  techniczne z opisu (do planu): Microsoft.SemanticKernel jako klient modelu, model GPT-4o-mini
  (potwierdzony przez właściciela; długość dokumentów obsługuje podział na kroki, FR-421).
  Dostępność i termin wycofania wersji GPT-4o-mini w Azure trzeba sprawdzić w planie; dlatego
  model jest parametrem konfiguracji i skryptu.
- **Bez ponowień**: nieudane zapytanie nie jest ponawiane; użytkownik uruchamia aplikację ponownie
  (pliki są pobierane i konwertowane od nowa).
- **Konstytucja, zasada V**: klucz nie pochodzi ze zmiennych środowiskowych, lecz z konsoli lub
  przekierowanego wejścia (decyzja właściciela) — sekret i tak nie trafia do repozytorium ani
  plików; odstępstwo opisze plan. Zasada III jest spełniona dzięki przekierowanemu wejściu.
- **Gwiazdki** (decyzja właściciela): ujawniają długość klucza osobie patrzącej na ekran; to
  świadomy kompromis na rzecz informacji zwrotnej przy wklejaniu.
- **Klucz w pamięci**: aplikacja nie przechowuje klucza dłużej, niż to potrzebne, ale nie gwarantuje
  wymazania go z pamięci procesu (zarządzane środowisko uruchomieniowe kopiuje teksty).
- **Skrypt Azure**: wymaga zainstalowanego narzędzia wiersza poleceń Azure i zalogowanego
  użytkownika; język skryptu (np. powłoka Bash) ustala plan, z warunkiem działania na Linuksie.
  Testy automatyczne nie tworzą zasobów w Azure; skrypt jest weryfikowany ręcznie oraz testem
  automatycznym z atrapą Azure CLI (bez tworzenia zasobów).
- **Testy** (zasada I): usługa modelu jest zastępowana atrapą zwracającą zaprogramowane
  odpowiedzi; konsola — atrapą wejścia i wyjścia, przez którą testy podają klucz.
- **Prywatność**: pliki Markdown z prawdziwych regulaminów leżą w katalogu pobrań (git-ignored).
  Katalog `faq/` nie jest ignorowany przez git — FAQ jest rezultatem do oddania; trafia do
  repozytorium tylko świadomym commitem właściciela.
