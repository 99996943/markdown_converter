# Feature Specification: LegalAgent.PdfParser — konwersja PDF aktów prawnych i regulaminów do Markdown

**Feature Branch**: `001-legal-pdf-parser`

**Created**: 2026-10-07

**Status**: Draft

**Input**: User description: "Przygotuj specyfikację dla niezależnej biblioteki w .NET. Projekt ma nazywać się LegalAgent.PdfParser. Użyj darmowej paczki UglyToad.PdfPig. Biblioteka ma przyjmować strumień (Stream) pliku PDF i zwracać wyczyszczony tekst w formacie Markdown. Ponieważ dokumenty to polskie akty prawne (ustawy) i regulaminy bankowe (np. mBank), zaprojektuj w specyfikacji zestaw zaawansowanych heurystyk i filtrów. Wymyśl i opisz mechanizmy, które: wykrywają nagłówki na podstawie rozmiaru czcionki lub pogrubienia (zamieniając je na # lub ##); usuwają powtarzające się stopki, nagłówki stron i numery stron (tzw. artefakty PDF); próbują zachować strukturę list punktowanych oraz minimalizują 'rozsypywanie się' tabel opłat, np. poprzez łączenie tekstu w tej samej linii (Y-axis). Biblioteka musi być asynchroniczna i łatwa do wstrzyknięcia (Dependency Injection)."

## Kontekst

LegalAgent.PdfParser to samodzielna, wielokrotnego użytku biblioteka, która zamienia dowolny
plik PDF z warstwą tekstową na uporządkowany dokument i jego czysty zapis w Markdown. Docelowe
dokumenty to polskie akty prawne (teksty ujednolicone z ISAP / Dziennika Ustaw) oraz regulaminy,
taryfy opłat i prowizji instytucji finansowych (np. mBank). Odbiorcą są programiści systemów
(m.in. aplikacji konsolowej tego repozytorium i agentów RAG), którzy potrzebują tekstu wolnego od
„śmieci” układu strony, z zachowaną hierarchią jednostek redakcyjnych, listami i tabelami.

Zgodnie z konstytucją biblioteka zwraca **ustrukturyzowany model dokumentu** (sekcje z numeracją
i metadanymi źródła), a Markdown jest jego deterministycznym renderingiem.

## Clarifications

### Session 2026-10-07

- Q: Jakim elementem Markdown mają być artykuły („Art. 5.”) i paragrafy („§ 12.”)? → A: Nagłówkiem najniższego poziomu (zwykle `###`, `##` gdy dokument nie ma rozdziałów/działów) i osobną sekcją w modelu — jednostka cytowania i naturalna granica fragmentu (chunka) dla RAG.
- Q: Czy Markdown ma zawierać niewidoczne znaczniki numerów stron źródłowego PDF? → A: Tak, domyślnie włączone jako komentarze HTML `<!-- page: N -->` w miejscu przejścia strony (także wewnątrz akapitu), wyłączalne w ustawieniach; numery stron zawsze także w modelu.
- Q: Co zrobić, gdy jedna strona PDF jest uszkodzona, a pozostałe są poprawne? → A: Domyślnie błąd całej konwersji z numerem strony; ustawienie „dopuszczaj wynik częściowy” pomija stronę (znacznik + ostrzeżenie) i oznacza wynik flagą „niepełny”.
- Q: Czy biblioteka ma sama ograniczać zasoby na dokument (rozmiar, strony, czas)? → A: Tak — limity wbudowane z domyślnymi wartościami 100 MB, 2000 stron, 120 s; każdy konfigurowalny lub wyłączalny; przekroczenie = dedykowany błąd.
- Q: Gdzie w Markdown mają trafić przypisy dolne? → A: Na koniec najmniejszej sekcji (artykułu/paragrafu), w której pierwszy raz występuje odnośnik; numeracja `[^n]` globalna w dokumencie.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Czysty Markdown z PDF bez artefaktów stron (Priority: P1)

Programista przekazuje bibliotece strumień pliku PDF (np. ustawy pobranej z ISAP) i otrzymuje
ciągły tekst Markdown, w którym nie ma numerów stron, powtarzających się nagłówków i stopek
(„Dziennik Ustaw – 3 – Poz. 1234”, „©Kancelaria Sejmu s. 3/120”, stopka rejestrowa banku), a
akapity przerwane końcem strony lub dzieleniem wyrazów są sklejone.

**Why this priority**: Bez tego wynik jest bezużyteczny dla dalszego przetwarzania (wyszukiwanie,
podział na fragmenty, odpowiadanie na pytania) — artefakty przerywają zdania i zaśmiecają indeks.
To minimalny, samodzielny produkt.

**Independent Test**: Konwersja wzorcowego PDF ustawy i regulaminu, porównanie z ręcznie
przygotowanym plikiem oczekiwanym (golden file): brak artefaktów, brak utraconej treści.

**Acceptance Scenarios**:

1. **Given** PDF ustawy, w którym każda strona ma nagłówek „Dziennik Ustaw – N – Poz. 1234”, **When** dokument zostanie skonwertowany, **Then** wynik nie zawiera żadnego wystąpienia tego nagłówka, a pierwsza strona (gdzie nagłówek ma inną postać) również jest oczyszczona.
2. **Given** PDF z numerem strony w stopce w formach „3”, „- 3 -”, „Strona 3 z 40”, „s. 3/40”, **When** dokument zostanie skonwertowany, **Then** żaden numer strony nie występuje w wyniku.
3. **Given** zdanie rozpoczęte na dole strony 4 i dokończone na stronie 5, **When** dokument zostanie skonwertowany, **Then** zdanie jest jednym akapitem, bez wstawionej pustej linii ani artefaktu pomiędzy; jedynym dodatkiem jest niewidoczny znacznik `<!-- page: 5 -->` na granicy słów (o ile znaczniki stron nie zostały wyłączone).
4. **Given** wyraz przeniesiony z dzieleniem („przedsiębior-” / „ca”), **When** dokument zostanie skonwertowany, **Then** w wyniku występuje „przedsiębiorca”; natomiast złożenia z łącznikiem („biało-czerwony”, „e-mail”) pozostają nienaruszone.
5. **Given** ten sam plik konwertowany dwukrotnie, **When** porównamy wyniki, **Then** są identyczne bajt po bajcie.

---

### User Story 2 - Hierarchia nagłówków i jednostek redakcyjnych (Priority: P1)

Programista otrzymuje Markdown, w którym tytuł dokumentu, działy, rozdziały oraz artykuły /
paragrafy są nagłówkami właściwego poziomu (`#`, `##`, `###`), a model dokumentu zawiera drzewo
sekcji z numeracją (np. „Rozdział 2”, „Art. 15”, „§ 7”), co pozwala dzielić tekst na fragmenty
według jednostek prawnych.

**Why this priority**: Struktura jest podstawą do cytowania źródła (zasada II konstytucji) i do
sensownego dzielenia treści; bez niej wynik to „ściana tekstu”.

**Independent Test**: Konwersja PDF ustawy z działami i rozdziałami oraz regulaminu z
paragrafami; weryfikacja listy nagłówków i ich poziomów względem pliku oczekiwanego.

**Acceptance Scenarios**:

1. **Given** PDF, w którym tytuł ma czcionkę wyraźnie większą od tekstu podstawowego, **When** dokument zostanie skonwertowany, **Then** tytuł jest nagłówkiem `#`.
2. **Given** linia „Rozdział 3” i następna linia „Ochrona konsumenta” złożone pogrubioną czcionką o rozmiarze tekstu podstawowego, **When** dokument zostanie skonwertowany, **Then** powstaje jeden nagłówek `## Rozdział 3. Ochrona konsumenta`.
3. **Given** linia zaczynająca się od „Art. 5.” lub „§ 12.” z treścią w tej samej linii, **When** dokument zostanie skonwertowany, **Then** powstaje nagłówek jednostki („### Art. 5.”), a treść występuje jako akapit pod nim; numeracja trafia do modelu sekcji.
4. **Given** pogrubione wyróżnienie w środku akapitu (np. pojedyncze słowo), **When** dokument zostanie skonwertowany, **Then** nie staje się nagłówkiem, a jest zapisane jako `**wyróżnienie**`.
5. **Given** dokument bez różnic typograficznych (jedna czcionka i rozmiar), **When** dokument zostanie skonwertowany, **Then** nagłówki są wykrywane wyłącznie na podstawie wzorców jednostek redakcyjnych, a w razie ich braku wynik nie zawiera nagłówków (nie są zgadywane).

---

### User Story 3 - Zachowanie list wyliczeniowych (Priority: P2)

Programista otrzymuje wyliczenia z aktów prawnych (ust., pkt „1)”, lit. „a)”, tiret „–”) oraz
listy punktowane z regulaminów („•”, „1.”, „a)”) jako listy Markdown z zachowanym zagnieżdżeniem
i oryginalnymi oznaczeniami, a wiersze zawinięte wewnątrz punktu są sklejone z tym punktem.

**Why this priority**: Wyliczenia niosą sens prawny („pkt 3 lit. b”); ich spłaszczenie lub
rozbicie utrudnia cytowanie, ale dokument pozostaje czytelny i bez tego.

**Independent Test**: Konwersja fragmentu ustawy z trzypoziomowym wyliczeniem oraz regulaminu z
listą punktowaną; porównanie struktury listy z plikiem oczekiwanym.

**Acceptance Scenarios**:

1. **Given** wyliczenie „1) … 2) …” z punktem 2 zawierającym litery „a) … b) …” wciętymi względem punktów, **When** dokument zostanie skonwertowany, **Then** litery są listą zagnieżdżoną w punkcie 2, a oznaczenia „1)”, „a)” zostają zachowane dosłownie.
2. **Given** punkt listy, którego treść zawija się na 3 linie wyrównane do treści punktu, **When** dokument zostanie skonwertowany, **Then** punkt jest jedną pozycją listy.
3. **Given** punkt listy rozdzielony końcem strony, **When** dokument zostanie skonwertowany, **Then** jego kontynuacja na następnej stronie pozostaje w tej samej pozycji listy.
4. **Given** znak „•”, „▪”, „◦” lub symbol z czcionki ozdobnej jako punktor, **When** dokument zostanie skonwertowany, **Then** pozycja jest elementem listy punktowanej, a sam znak nie pojawia się jako tekst.
5. **Given** akapit, który zaczyna się od liczby niebędącej oznaczeniem punktu (np. „2024 r. weszła…”), **When** dokument zostanie skonwertowany, **Then** nie staje się pozycją listy.

---

### User Story 4 - Czytelne tabele opłat (Priority: P2)

Programista konwertujący tabelę opłat i prowizji (np. „Prowadzenie rachunku | 0 zł | miesięcznie”)
otrzymuje każdy wiersz tabeli jako spójną całość — w postaci tabeli Markdown, gdy siatka kolumn
jest jednoznaczna, albo co najmniej jako jedną linię z rozdzielonymi komórkami — zamiast
rozsypanych pojedynczych słów i kwot.

**Why this priority**: Taryfy opłat to kluczowa treść regulaminów bankowych; „rozsypana” kwota
oderwana od nazwy usługi jest bezwartościowa lub wręcz myląca.

**Independent Test**: Konwersja strony taryfy opłat z kilkoma kolumnami i wieloliniowymi
komórkami; sprawdzenie, że każda kwota występuje w tym samym wierszu co nazwa usługi.

**Acceptance Scenarios**:

1. **Given** wiersz tabeli, którego komórki mają tę samą linię bazową z niewielkimi odchyleniami pionowymi, **When** dokument zostanie skonwertowany, **Then** komórki tworzą jeden wiersz wyniku w kolejności od lewej do prawej.
2. **Given** tabela o stałej liczbie kolumn wyrównanych w pionie przez co najmniej 3 wiersze, **When** dokument zostanie skonwertowany, **Then** wynik jest tabelą Markdown z wierszem nagłówka.
3. **Given** komórka „Prowadzenie rachunku dla osób do 26 roku życia” zawinięta na 2 linie, **When** dokument zostanie skonwertowany, **Then** tekst komórki jest sklejony w jednej komórce tego samego wiersza, a nie tworzy nowego wiersza.
4. **Given** tabela kontynuowana na następnej stronie z powtórzonym wierszem nagłówka, **When** dokument zostanie skonwertowany, **Then** tabela jest jedną tabelą, a powtórzony nagłówek pojawia się tylko raz.
5. **Given** układ, którego nie da się jednoznacznie przypisać do siatki kolumn, **When** dokument zostanie skonwertowany, **Then** każdy wiersz wizualny jest zapisany jako jedna linia z komórkami rozdzielonymi separatorem, a w diagnostyce pojawia się ostrzeżenie o obniżonej pewności.

---

### User Story 5 - Łatwa integracja w aplikacji (Priority: P1)

Programista rejestruje bibliotekę w kontenerze zależności aplikacji jednym wywołaniem, opcjonalnie
nadpisuje progi heurystyk w konfiguracji i wywołuje konwersję asynchronicznie, z możliwością
anulowania. Może też podmienić lub wyłączyć pojedynczy filtr bez modyfikowania biblioteki.

**Why this priority**: Wymóg zleceniodawcy; bez tego bibliotekę trudno używać w aplikacji
konsolowej i usługach.

**Independent Test**: Test integracyjny tworzący kontener zależności, rejestrujący bibliotekę,
pobierający usługę konwersji i konwertujący przykładowy PDF; drugi test anulujący konwersję.

**Acceptance Scenarios**:

1. **Given** pusty kontener zależności, **When** programista zarejestruje bibliotekę jednym wywołaniem, **Then** może pobrać z kontenera usługę konwersji bez dodatkowej konfiguracji.
2. **Given** zarejestrowana biblioteka i zmieniony próg wykrywania artefaktów w konfiguracji, **When** nastąpi konwersja, **Then** użyty jest nowy próg.
3. **Given** trwająca konwersja dużego dokumentu, **When** wywołujący zażąda anulowania, **Then** operacja kończy się sygnałem anulowania w ciągu czasu przetwarzania jednej strony, bez częściowego wyniku uznanego za poprawny.
4. **Given** programista zarejestrował własny dodatkowy filtr, **When** nastąpi konwersja, **Then** filtr zostaje wykonany w ustalonym miejscu potoku przetwarzania.

---

### User Story 6 - Diagnostyka jakości konwersji (Priority: P3)

Programista otrzymuje wraz z wynikiem raport: liczbę stron, liczbę usuniętych artefaktów (z
przykładami), wykryte nagłówki, tabele i ostrzeżenia (np. strona bez warstwy tekstowej, tabela
o niskiej pewności), aby móc ocenić, czy wynik nadaje się do dalszego użycia.

**Why this priority**: Zasada IV konstytucji — niepełny wynik nie może być po cichu uznany za
poprawny. Funkcja jest jednak dodatkiem do podstawowej konwersji.

**Independent Test**: Konwersja PDF z jedną stroną zeskanowaną (obraz); sprawdzenie, że raport
zawiera ostrzeżenie wskazujące numer tej strony.

**Acceptance Scenarios**:

1. **Given** PDF, którego strona 7 nie ma warstwy tekstowej, **When** dokument zostanie skonwertowany, **Then** raport zawiera ostrzeżenie „brak tekstu na stronie 7”, a Markdown zawiera w tym miejscu jawny znacznik pominięcia.
2. **Given** dowolna konwersja, **When** zakończy się powodzeniem, **Then** raport zawiera listę unikalnych usuniętych wzorców artefaktów wraz z liczbą wystąpień.

---

### Edge Cases

- Strumień pusty, niebędący PDF lub uszkodzony → czytelny błąd wskazujący przyczynę; brak częściowego wyniku.
- Uszkodzona pojedyncza strona przy poprawnych pozostałych → domyślnie błąd z numerem strony; przy włączonym „dopuszczaj wynik częściowy” wynik bez tej strony z flagą „niepełny” (FR-009a).
- PDF zaszyfrowany hasłem → czytelny błąd „dokument chroniony hasłem”; PDF z ograniczeniami uprawnień, ale bez hasła otwarcia, jest przetwarzany.
- PDF w całości bez warstwy tekstowej (skan) → błąd `PdfNoTextException` (FR-071), nigdy pusty wynik; OCR poza zakresem.
- Strumień nieprzewijalny (np. sieciowy) → biblioteka przyjmuje go poprawnie (buforując treść) bez wymagania od wywołującego wcześniejszego kopiowania.
- Dokument jednostronicowy lub dwustronicowy → wykrywanie powtórzeń nie może usunąć treści merytorycznej (brak wystarczającej liczby stron do potwierdzenia artefaktu).
- Linia powtarzająca się na wielu stronach, ale będąca treścią (np. „uchylony”, „(pominięty)”, „Opłata: 0 zł”) → nie jest usuwana, bo nie znajduje się w strefie marginesu lub jej pozycja się zmienia.
- Tekst obrócony (pionowy znak wodny, sygnatura druku na marginesie) → pomijany i odnotowany w diagnostyce.
- Strony w orientacji poziomej (tabele opłat) przemieszane z pionowymi → strefy nagłówka/stopki liczone względem wymiarów każdej strony osobno.
- Układ dwukolumnowy → kolejność czytania: cała lewa kolumna, potem prawa; łączenie po osi Y nie może sklejać linii z różnych kolumn tekstu ciągłego.
- Przypisy aktu prawnego (małą czcionką na dole strony, odnośniki „1)” w indeksie górnym) → zachowane jako przypisy Markdown na końcu sekcji zawierającej odnośnik (FR-026), a nie wtopione w akapit; nie są traktowane jako stopka strony.
- Fragmenty uchylone i zmiany oznaczone w tekście ujednoliconym („Art. 12. (uchylony)”) → zachowane dosłownie.
- Ligatury, znaki z prywatnych obszarów Unicode, polskie znaki zapisane jako litera + znak diakrytyczny → normalizowane do standardowej postaci złożonej; brak „krzaczków” w polskich literach.
- Czcionki bez mapowania Unicode (tekst niemożliwy do odczytania) → ostrzeżenie dla danej strony zamiast cichego wstawienia nieczytelnych znaków.
- Bardzo duży dokument (ponad 1000 stron, w granicach limitów) → przetwarzanie bez wyczerpania pamięci przy stałym narzucie na stronę.
- Plik przekraczający limit rozmiaru, liczby stron lub czasu (np. spreparowany PDF) → dedykowany błąd limitu (FR-009b), bez zawieszenia procesu i bez częściowego wyniku.
- Nagłówek na samym dole strony, a jego treść na następnej → nagłówek pozostaje przed treścią, nie zostaje uznany za stopkę.

## Requirements *(mandatory)*

### Functional Requirements

#### Wejście, wyjście i integracja

- **FR-001**: Biblioteka MUSI przyjmować dokument PDF jako strumień danych oraz opcjonalnie nazwę/identyfikator źródła, który trafia do metadanych wyniku.
- **FR-002**: Biblioteka MUSI zwracać ustrukturyzowany model dokumentu (metadane źródła, drzewo sekcji z poziomem, numeracją i tytułem, bloki treści: akapity, listy, tabele, przypisy) oraz jego rendering do Markdown (CommonMark z rozszerzeniem tabel GFM).
- **FR-002a**: Rendering Markdown MUSI domyślnie wstawiać znacznik strony źródłowej w postaci komentarza HTML `<!-- page: N -->` (N = numer fizycznej strony PDF, liczony od 1) w miejscu, w którym zaczyna się treść strony N: przed pierwszym blokiem strony albo — gdy akapit lub pozycja listy przechodzi przez granicę strony — wewnątrz tekstu na granicy słów. Wewnątrz tabeli Markdown znacznik NIE jest wstawiany (złamałby składnię); zakres stron tabeli jest dostępny w modelu, a znacznik następnej strony pojawia się po tabeli. Znaczniki MUSZĄ dać się wyłączyć w ustawieniach; niezależnie od tego każda sekcja i każdy blok w modelu zawiera zakres stron źródłowych.
- **FR-003**: Operacja konwersji MUSI być asynchroniczna i MUSI obsługiwać żądanie anulowania, sprawdzane co najmniej przed przetworzeniem każdej strony.
- **FR-004**: Biblioteka MUSI udostępniać rejestrację wszystkich swoich usług w kontenerze zależności jednym wywołaniem, z opcjonalnym przekazaniem ustawień.
- **FR-005**: Wszystkie progi i przełączniki heurystyk (FR-010 – FR-066, w tym FR-031 i FR-043a) MUSZĄ być konfigurowalne z wartościami domyślnymi dostrojonymi do polskich aktów prawnych i regulaminów bankowych; każdy filtr MUSI dać się wyłączyć.
- **FR-006**: Potok przetwarzania MUSI składać się z uporządkowanych, niezależnie testowalnych etapów (ekstrakcja → normalizacja → usuwanie artefaktów → składanie linii i bloków → wykrywanie tabel → wykrywanie list → wykrywanie nagłówków → rendering), a programista MUSI móc dodać własny etap lub zastąpić istniejący poprzez kontener zależności.
- **FR-007**: Biblioteka NIE MOŻE zawierać kodu ani reguł specyficznych dla konkretnego wydawcy (np. nazw banku czy adresów); wzorce domenowe (jednostki redakcyjne, typowe formaty numerów stron) są ogólne dla polskich dokumentów prawnych i finansowych.
- **FR-008**: Ten sam strumień wejściowy i te same ustawienia MUSZĄ dawać identyczny wynik (model, Markdown i raport) przy każdym uruchomieniu.
- **FR-009**: Błędy wejścia (pusty strumień, nie-PDF, uszkodzony plik, hasło) MUSZĄ być zgłaszane dedykowanym, czytelnym błędem z przyczyną; biblioteka NIE MOŻE zwrócić częściowego wyniku jako sukcesu.
- **FR-009a**: Gdy odczyt pojedynczej strony się nie powiedzie (uszkodzona treść strony przy poprawnej strukturze pliku), biblioteka MUSI domyślnie przerwać konwersję dedykowanym błędem wskazującym numer strony i przyczynę. Gdy w ustawieniach włączono „dopuszczaj wynik częściowy” (domyślnie wyłączone), strona MUSI zostać pominięta: w Markdown pojawia się jawny znacznik pominięcia z numerem strony, w raporcie ostrzeżenie, a wynik MUSI mieć flagę „niepełny” z listą pominiętych stron. Jeśli pominięte zostałyby wszystkie strony, konwersja kończy się błędem niezależnie od ustawienia.
- **FR-009b**: Biblioteka MUSI egzekwować limity zasobów na jedną konwersję, z wartościami domyślnymi: rozmiar danych wejściowych ≤ 100 MB (sprawdzany także w trakcie buforowania strumienia nieprzewijalnego, bez wczytywania nadmiaru), liczba stron ≤ 2000 (sprawdzana przed przetwarzaniem treści), czas konwersji ≤ 120 s (liczony niezależnie od anulowania przez wywołującego). Każdy limit MUSI być konfigurowalny i wyłączalny. Przekroczenie limitu MUSI kończyć się dedykowanym błędem wskazującym limit, jego wartość i wartość zmierzoną; ustawienie „dopuszczaj wynik częściowy” NIE dotyczy przekroczenia limitów.

#### Normalizacja tekstu

- **FR-010**: Tekst MUSI być normalizowany do złożonej postaci Unicode (NFC), z rozwinięciem ligatur (np. „ﬁ” → „fi”), zamianą twardych spacji i spacji o zmiennej szerokości na zwykłe spacje oraz zachowaniem polskich znaków diakrytycznych.
- **FR-011**: Odstępy między słowami MUSZĄ być odtwarzane na podstawie odległości między znakami, gdy plik nie zawiera jawnych spacji (próg względem szerokości znaku czcionki), tak aby nie powstawały zlepione ani rozstrzelone wyrazy. Próg jest liczony względem typowego odstępu między literami danej linii, dzięki czemu tekst złożony z rozstrzeleniem liter (np. tytuł „U S T A W A”) pozostaje jednym wyrazem.
- **FR-012**: Wyraz podzielony na końcu linii łącznikiem MUSI zostać scalony, jeśli łącznik jest ostatnim znakiem linii, a następna linia zaczyna się małą literą; łącznik zostaje zachowany, gdy część przed nim jest skrótem pisanym wersalikami (np. „PKB-owski”), gdy część po nim zaczyna się wielką literą (nazwy złożone, np. „Bielsko-Biała”), gdy część przed nim jest jednoliterowa, albo gdy połączenie tworzy znane złożenie z łącznikiem (konfigurowalna lista wyjątków). Sama wielka litera na początku części przed łącznikiem NIE powoduje zachowania łącznika („Zagra-” + „nicznych” → „Zagranicznych”).
- **FR-013**: Tekst obrócony względem orientacji strony oraz tekst niewidoczny (np. biały na białym, poza obszarem strony) MUSI zostać pominięty i odnotowany w raporcie.

#### Usuwanie artefaktów stron (nagłówki, stopki, numery stron)

- **FR-020**: Dla każdej strony MUSZĄ być wyznaczone strefy marginesu górnego i dolnego (domyślnie po 8% wysokości strony, liczone dla każdej strony osobno); tylko linie w tych strefach są kandydatami na artefakt powtarzalny.
- **FR-021**: Linie-kandydaci MUSZĄ być porównywane między stronami po „odcisku”: tekst po zamianie każdej sekwencji cyfr na symbol zastępczy, ujednoliceniu białych znaków i wielkości liter. Odcisk występujący w tej samej strefie na co najmniej 50% stron (minimum 3 strony) z podobną pozycją pionową (tolerancja domyślnie 2% wysokości strony) MUSI być uznany za artefakt i usunięty ze wszystkich stron, na których występuje.
- **FR-022**: Dopasowanie odcisków MUSI tolerować drobne różnice (podobieństwo tekstu co najmniej 85%), aby usuwać nagłówki różniące się numerem pozycji, datą lub nazwą sekcji bieżącej, oraz osobno rozpatrywać strony parzyste i nieparzyste (układ lustrzany). Dodatkowo linia w strefie marginesu strony, na której wzorzec nie został potwierdzony (typowo strona 1), MUSI zostać usunięta, gdy jej odcisk **zawiera** odcisk potwierdzonego artefaktu tej samej strefy jako podciąg (np. „Dziennik Ustaw – 1 – Poz. 1234 · Ustawa z dnia …” zawiera „dziennik ustaw – # – poz. #”); pozostała część linii spoza dopasowania zostaje zachowana jako treść.
- **FR-023**: Samodzielne numery stron MUSZĄ być usuwane niezależnie od powtarzalności, gdy linia w strefie marginesu pasuje do jednego z wzorców: liczba, liczba rzymska, „- N -”, „– N –”, „N / M”, „Strona N z M”, „Str. N”, „s. N/M”, a jej wartość jest zgodna (z tolerancją stałego przesunięcia) z kolejnym numerem strony.
- **FR-024**: Linie w strefach marginesu, które nie spełniają warunków powtarzalności, NIE MOGĄ być usuwane (ochrona przed utratą treści, np. ostatniej linii akapitu lub nagłówka na dole strony).
- **FR-025**: Na dokumentach krótszych niż 3 strony usuwane są wyłącznie numery stron wg FR-023.
- **FR-026**: Przypisy dolne (blok mniejszej czcionki na dole strony, poprzedzony kreską lub zaczynający się od oznaczenia przypisu odpowiadającego odnośnikowi w tekście) MUSZĄ być wyodrębniane jako przypisy i renderowane jako przypisy Markdown (`[^n]` w miejscu odnośnika, `[^n]: treść` jako definicja), a nie usuwane ani wtapiane w treść. Definicja przypisu MUSI zostać umieszczona na końcu najmniejszej sekcji (np. artykułu lub paragrafu), w której odnośnik występuje po raz pierwszy — tak, by trafiła do tego samego fragmentu (chunka) co treść; odnośnik w tytule lub przed pierwszą sekcją umieszcza definicję na końcu tej części wstępnej. Numeracja przypisów MUSI być unikalna w obrębie całego dokumentu; kolejne odnośniki do tego samego przypisu nie powielają definicji. Przypis bez odnalezionego odnośnika trafia na koniec sekcji, na której stronie się znajduje, z ostrzeżeniem w raporcie.
- **FR-027**: Raport MUSI zawierać listę usuniętych wzorców artefaktów z liczbą wystąpień i numerami stron.

#### Składanie linii, akapitów i kolejności czytania

- **FR-030**: Znaki/słowa MUSZĄ być grupowane w linie wizualne, gdy ich pionowe zakresy (linia bazowa ± wysokość) nakładają się co najmniej w 50% lub różnica linii bazowej nie przekracza 30% wysokości mniejszej czcionki; indeksy górne i dolne (odnośniki przypisów, „m²”) MUSZĄ zostać przypisane do linii, w której występują. Znaki pełnej wielkości leżące poziomo daleko od linii (ponad 3 em od jej zakresu, np. sąsiednia kolumna z przesuniętą linią bazową) dołączają wyłącznie przy zgodnej linii bazowej; kryterium nakładania się obejmuje znaki bliskie oraz mniejsze (indeksy, odnośniki).
- **FR-031**: Kolejność czytania MUSI uwzględniać układ wielokolumnowy: wykryte kolumny tekstu ciągłego (pionowy pas pustej przestrzeni o szerokości ≥ 2% szerokości strony, przecinający ≥ 60% wysokości regionu tekstu, przy medianie długości linii po obu stronach ≥ 25% szerokości strony; segmenty jednej linii po tej samej stronie, np. punktor i tekst, liczą się jako jedna linia) są czytane kolejno od lewej do prawej, a linie z różnych kolumn tekstu ciągłego nie są łączone.
- **FR-032**: Kolejne linie MUSZĄ być łączone w akapit, gdy odstęp pionowy nie przekracza 1,5× typowego odstępu międzywierszowego, czcionka i wcięcie lewe są zgodne, a poprzednia linia nie kończy się w sposób sygnalizujący koniec akapitu (kropka + linia krótsza niż 75% szerokości kolumny tekstu).
- **FR-033**: Akapit przerwany granicą strony (po usunięciu artefaktów) MUSI być kontynuowany na następnej stronie, gdy ostatnia linia strony nie kończy zdania, a pierwsza linia następnej strony zaczyna się małą literą lub ma to samo wcięcie co tekst ciągły.
- **FR-034**: Wąska kolumna adnotacji bocznych przy krawędzi strony (oddzielona od tekstu głównego pionowym pasem pustej przestrzeni na całej wysokości regionu treści, o szerokości ≤ 25% szerokości strony, czcionce mniejszej niż 90% czcionki tekstu głównego, z co najmniej dwiema liniami) — np. noty redakcyjne ISAP o wejściu w życie zmian — MUSI zostać wydzielona przed składaniem linii: jej znaki nie są łączone z liniami tekstu głównego, nie przerywa ona akapitów, list ani nagłówków, a jej treść jest zapisywana jako osobny akapit bezpośrednio po akapicie lub liście, obok których stoi.

#### Wykrywanie nagłówków

- **FR-040**: Biblioteka MUSI wyznaczyć styl tekstu podstawowego jako kombinację (rozmiar czcionki, grubość) obejmującą największą liczbę znaków w dokumencie.
- **FR-041**: Linia jest kandydatem na nagłówek typograficzny, gdy jest krótka (domyślnie ≤ 120 znaków / ≤ 2 linie), stoi samodzielnie (odstęp przed nią > 1,3× typowej interlinii), nie kończy się przecinkiem ani średnikiem i spełnia przynajmniej jeden warunek: rozmiar czcionki ≥ 1,15× tekstu podstawowego, cała linia pogrubiona, cała linia wersalikami przy co najmniej 3 literach, lub wyśrodkowanie względem kolumny tekstu (środek linii w odległości ≤ 5% szerokości kolumny od jej środka, przy marginesach obustronnych ≥ 10% szerokości kolumny).
- **FR-042**: Rozmiary czcionek kandydatów MUSZĄ być grupowane w klasy (tolerancja 0,5 pt); klasy sortowane malejąco wyznaczają poziomy: największa → `#`, kolejna → `##`, każda następna → `###`; pogrubienie bez powiększenia daje poziom o jeden niższy niż najniższa klasa powiększona (minimum `##`, gdy brak klas powiększonych). Maksymalna głębokość jest konfigurowalna (domyślnie 3).
- **FR-043**: Wzorce jednostek redakcyjnych MUSZĄ mieć pierwszeństwo przed typografią przy ustalaniu poziomu. Tytuł dokumentu → `#`: jest nim linia pierwszej strony zaczynająca się (wersalikami) od nazwy rodzaju aktu — USTAWA, ROZPORZĄDZENIE, OBWIESZCZENIE, ZARZĄDZENIE, UCHWAŁA, POSTANOWIENIE, DECYZJA, KODEKS, REGULAMIN, KOMUNIKAT — a gdy takiej nie ma, największy nagłówek typograficzny będący pierwszą linią dokumentu; linie pierwszej strony położone nad tytułem rozpoznanym po nazwie aktu (np. winieta dziennika urzędowego „DZIENNIK USTAW … Poz. 1298”) są treścią wstępną, a nie nagłówkami; bezpośrednio następujące po nim linie bloku tytułowego — wyśrodkowane (np. „z dnia …”, „o …”, „w sprawie …”) albo złożone tą samą czcionką co tytuł (tytuł wieloliniowy), z odstępem ≤ 2× interlinii — są dołączane do tytułu („USTAWA z dnia 1 stycznia 2026 r. o …”); jednostki strukturalne w kolejności „Księga”/„CZĘŚĆ” → „DZIAŁ” → „Rozdział” → „Oddział” otrzymują kolejne poziomy od `##`, przy czym poziom nadawany jest tylko typom faktycznie występującym w dokumencie (brak luk). „Art. N.” i „§ N.” na początku linii są ZAWSZE nagłówkiem o jeden poziom niższym niż najgłębsza jednostka strukturalna występująca w dokumencie (lecz nie głębiej niż poziom bieżącej jednostki nadrzędnej + 1 — np. artykuł w dziale bez rozdziałów) (typowa ustawa z rozdziałami → `###`; dokument bez jednostek strukturalnych → `##`) i ZAWSZE tworzą osobną sekcję w modelu. Limit głębokości z FR-042 dotyczy wyłącznie nagłówków typograficznych; jednostki redakcyjne mogą sięgać poziomu `######`. Numery mogą zawierać litery i indeksy („Art. 12a.”, „§ 5¹”).
- **FR-043a**: W dokumencie zawierającym jednostki redakcyjne nagłówek typograficzny (niepasujący do wzorców FR-043) otrzymuje poziom: (a) `##`, gdy występuje przed pierwszą jednostką strukturalną lub po ostatniej sekcji (np. „Załącznik”, „Spis treści”) — staje się sekcją najwyższego poziomu pod tytułem; (b) w przeciwnym razie poziom bieżącej najgłębszej otwartej sekcji + 1, nie głębiej niż `######`. Nagłówek typograficzny nigdy nie zamyka sekcji Art./§ — poza przypadkiem (a). Poziom w wyniku nie może wzrosnąć o więcej niż 1 względem rodzica.
- **FR-044**: Linia z samym oznaczeniem jednostki („Rozdział 3”, „DZIAŁ II”), po której bezpośrednio następuje krótka linia z tytułem w tym samym stylu lub pogrubiona (teksty ujednolicone ISAP składają oznaczenie zwykłą czcionką, a tytuł pogrubioną), MUSI zostać połączona w jeden nagłówek („Rozdział 3. Tytuł”).
- **FR-045**: Gdy oznaczenie „Art. N.” lub „§ N.” rozpoczyna linię zawierającą dalszą treść, oznaczenie MUSI stać się nagłówkiem, a pozostała treść — pierwszym akapitem sekcji; numer jednostki trafia do modelu sekcji.
- **FR-046**: Pogrubienie fragmentu wewnątrz linii (nie całej linii) NIE MOŻE tworzyć nagłówka; MUSI być renderowane jako wyróżnienie `**…**`. Kursywa analogicznie jako `*…*`.
- **FR-047**: Nagłówki wewnątrz tabel i w przypisach NIE MOGĄ być promowane do nagłówków dokumentu.

#### Listy i wyliczenia

- **FR-050**: Linia MUSI być rozpoznana jako pozycja listy, gdy zaczyna się od punktora (•, ▪, ◦, ‣, –, —, -, *, lub znaku z czcionki symbolicznej) albo od oznaczenia wyliczenia: „N)”, „N.”, „Na)”, „a)”, „ust. N”, liczby rzymskiej z kropką/nawiasem, „N.N.” — po którym następuje spacja lub odstęp oraz tekst. Oznaczenie wyliczenia może być poprzedzone nawiasem „[” lub „<”, którym teksty ujednolicone oznaczają brzmienie uchylane i przyszłe (np. „[1) …]”, „<2a. …>”); nawias pozostaje częścią dosłownego oznaczenia.
- **FR-051**: Oznaczenie „N.” na początku linii MUSI być uznane za listę tylko, jeśli tworzy ciąg (co najmniej dwie kolejne pozycje o rosnących numerach i tym samym wcięciu) lub stanowi ustęp artykułu; w przeciwnym razie linia jest akapitem (ochrona przed datami i kwotami).
- **FR-052**: Poziom zagnieżdżenia MUSI wynikać z wcięcia (pozycji X oznaczenia) względem pozostałych pozycji tej samej listy oraz z hierarchii oznaczeń prawnych (ustęp → punkt → litera → tiret); oznaczenia MUSZĄ zostać zachowane dosłownie w wyniku (listy numerowane nie są przenumerowywane).
- **FR-053**: Linia z wcięciem równym początkowi tekstu pozycji (a nie oznaczeniu) MUSI być traktowana jako kontynuacja tej pozycji, także po przejściu na następną stronę.
- **FR-054**: Tekst po zakończeniu wyliczenia, wyrównany do tekstu nadrzędnego (tzw. część wspólna), MUSI zostać zapisany jako akapit po liście, a nie jako jej pozycja.

#### Tabele (w tym tabele opłat)

- **FR-060**: Elementy tekstu w tej samej linii wizualnej (FR-030), rozdzielone odstępem poziomym większym niż próg (domyślnie 2× średnia szerokość spacji), MUSZĄ być traktowane jako osobne komórki.
- **FR-061**: Region tabeli MUSI zostać rozpoznany, gdy co najmniej 3 kolejne linie wizualne mają co najmniej 2 komórki, a granice komórek układają się w spójne pionowe pasy (pozycje X początków/końców zgodne z tolerancją domyślnie 3% szerokości strony); jako dodatkowy sygnał MOGĄ służyć linie siatki narysowane na stronie. Fragment w siatce linii (co najmniej jedna linia z ≥ 2 komórkami i co najmniej dwa wiersze między poziomymi liniami siatki przeciętymi linią pionową) również jest regionem tabeli — np. początek tabeli na dole strony kontynuowanej na następnej (FR-065).
- **FR-062**: Linie wewnątrz regionu tabeli zawierające tekst tylko w części kolumn i z odstępem pionowym od poprzedniej linii ≤ 1,2× typowej interlinii w tabeli MUSZĄ być scalane z poprzednim wierszem (komórki wieloliniowe), chyba że linie siatki wskazują granicę wiersza.
- **FR-063**: Region tabeli o jednoznacznej siatce MUSI zostać wyrenderowany jako tabela Markdown; pierwszy wiersz (lub wiersz pogrubiony u góry) jest nagłówkiem; znaki „|” w treści są cytowane; kwoty i jednostki („0,00 zł”, „1,5%”, „min. 10 zł”) pozostają w tej samej komórce.
- **FR-064**: Region, którego siatka nie jest jednoznaczna (zmienna liczba kolumn, nakładające się pasy), MUSI zostać wyrenderowany awaryjnie: jedna linia wynikowa na wiersz wizualny z komórkami rozdzielonymi separatorem „ | ” w kolejności od lewej do prawej, z ostrzeżeniem w raporcie. Gdy poziome linie siatki wyznaczają wiersze, zmienna liczba komórek w poszczególnych liniach wizualnych (komórki wyśrodkowane w pionie) nie czyni siatki niejednoznaczną.
- **FR-065**: Tabela kontynuowana na następnej stronie (zgodna liczba i położenie kolumn, opcjonalnie powtórzony wiersz nagłówka) MUSI zostać połączona w jedną tabelę z pominięciem powtórzonego nagłówka.
- **FR-066**: Komórki scalone poziomo (tekst przekraczający granicę kolumny) MUSZĄ zostać przypisane do pierwszej obejmowanej kolumny, a pozostałe pozostawione puste; treść nie może zostać zgubiona.

#### Diagnostyka

- **FR-070**: Wynik MUSI zawierać raport: liczba stron, liczba stron bez tekstu (z numerami), usunięte artefakty (FR-027), liczba i poziomy nagłówków, liczba list i tabel (w tym renderowanych awaryjnie), pominięty tekst obrócony/niewidoczny oraz listę ostrzeżeń z numerem strony.
- **FR-071**: Strona bez warstwy tekstowej, ale z treścią graficzną (np. skan), MUSI być oznaczona w Markdown jawnym komentarzem-znacznikiem pominięcia z numerem strony, odnotowana w raporcie i ustawiać flagę wyniku „niepełny” (nie jest to błąd, bo plik jest poprawny). Strona całkowicie pusta jest pomijana bez ostrzeżenia. Dokument bez żadnego tekstu MUSI kończyć się błędem.
- **FR-072**: Biblioteka MUSI raportować postęp przetwarzania (strona N z M) do opcjonalnego odbiorcy przekazanego przez wywołującego.

### Key Entities

- **Dokument źródłowy**: strumień PDF wraz z opcjonalnym identyfikatorem źródła (nazwa pliku, URL) i metadanymi odczytanymi z pliku (tytuł, liczba stron).
- **Ustawienia konwersji**: zbiór progów i przełączników heurystyk z wartościami domyślnymi; niezmienny w trakcie jednej konwersji.
- **Model dokumentu**: wynik konwersji — metadane źródła, lista sekcji i bloków, przypisy, raport.
- **Sekcja**: węzeł hierarchii z poziomem (1–N), rodzajem (tytuł, dział, rozdział, artykuł, paragraf, nagłówek typograficzny), numerem/oznaczeniem, tytułem, zakresem stron źródłowych, ścieżką przodków (np. „Dział II › Rozdział 3 › Art. 15”, do użycia jako kontekst fragmentu) oraz zawartością (bloki i podsekcje). Artykuł i paragraf są zawsze odrębnymi sekcjami.
- **Blok treści**: akapit (z wyróżnieniami), lista (pozycje z oznaczeniem, poziomem i treścią), tabela (wiersze, komórki, flaga renderingu awaryjnego), znacznik pominięcia strony; każdy z numerem strony początkowej.
- **Przypis**: globalny numer, oryginalne oznaczenie, treść, strona źródłowa, miejsce(a) odnośnika w tekście oraz sekcja, do której przypisano definicję.
- **Raport konwersji**: statystyki i ostrzeżenia opisane w FR-070 oraz flaga kompletności („pełny”/„niepełny”) z listą pominiętych stron i przyczyn.
- **Etap potoku**: wymienny krok przetwarzania przyjmujący i zwracający pośrednią reprezentację stron/bloków.

## Success Criteria *(mandatory)*

Wszystkie kryteria są weryfikowane na korpusie referencyjnym przechowywanym w repozytorium:
co najmniej 4 akty prawne (w tym jeden z działami i przypisami, jeden ponad 100-stronicowy) i co
najmniej 4 regulaminy/taryfy opłat (w tym jeden z tabelami wielostronicowymi), każdy z ręcznie
przygotowanym oczekiwanym wynikiem.

### Measurable Outcomes

- **SC-001**: 100% numerów stron i ≥ 99% wystąpień powtarzalnych nagłówków/stopek z korpusu zostaje usuniętych.
- **SC-002**: Kompletność treści: co najmniej 99,5% słów treści merytorycznej z oczekiwanego wyniku występuje w wyniku konwersji (żadna linia treści nie jest usuwana jako artefakt).
- **SC-003**: Co najmniej 95% jednostek redakcyjnych (działy, rozdziały, artykuły, paragrafy) jest wykrytych jako nagłówki właściwego poziomu, a odsetek fałszywych nagłówków nie przekracza 2% wszystkich nagłówków.
- **SC-004**: Co najmniej 95% pozycji wyliczeń ma poprawne oznaczenie i poziom zagnieżdżenia.
- **SC-005**: W tabelach opłat 100% kwot znajduje się w tym samym wierszu wyniku co nazwa usługi, której dotyczą; co najmniej 80% tabel z korpusu jest renderowanych jako pełne tabele Markdown.
- **SC-006**: Wynik dla tego samego pliku i ustawień jest identyczny przy 100% powtórzeń, również na różnych systemach operacyjnych.
- **SC-007**: Dokument 100-stronicowy jest konwertowany w czasie poniżej 10 sekund na typowym komputerze deweloperskim, a zapotrzebowanie na pamięć rośnie liniowo z liczbą stron.
- **SC-008**: Programista integruje bibliotekę z nową aplikacją (rejestracja + pierwsza konwersja) w nie więcej niż 3 linijkach kodu konfiguracji, korzystając wyłącznie z README.
- **SC-009**: Anulowanie konwersji kończy operację w czasie nie dłuższym niż przetwarzanie jednej strony.

## Assumptions

- **Ograniczenia zleceniodawcy (świadomie technologiczne)**: projekt nazywa się `LegalAgent.PdfParser`, jest biblioteką .NET (zgodnie z konstytucją: C#, .NET 9, Linux), do odczytu PDF używa darmowej biblioteki UglyToad.PdfPig (wersja przypięta), przyjmuje `Stream`, udostępnia asynchroniczne API z obsługą anulowania i rejestrację w standardowym kontenerze DI platformy .NET z konfiguracją ustawień w standardowym mechanizmie opcji.
- Obsługiwane są wyłącznie PDF z warstwą tekstową; OCR dokumentów skanowanych jest poza zakresem (strony bez tekstu są raportowane).
- Pobieranie dokumentów z sieci, lista źródeł, zapis plików, podział na fragmenty dla RAG i generowanie FAQ (OKF) należą do aplikacji wykonawczej lub kolejnych funkcjonalności — biblioteka jedynie konwertuje przekazany strumień.
- Biblioteka nie zamyka ani nie zwalnia przekazanego strumienia — odpowiada za to wywołujący.
- Domyślne wartości progów w FR-020 – FR-066 są punktem startowym; ich ostateczne dostrojenie nastąpi na korpusie referencyjnym w trakcie implementacji (TDD), a zmiany wartości domyślnych są dokumentowane.
- Teksty aktów prawnych nie podlegają prawu autorskiemu (art. 4 ustawy o prawie autorskim), więc mogą być przechowywane w repozytorium jako dane testowe; dla regulaminów bankowych, jeśli licencja nie pozwala na ich umieszczenie w repozytorium, korpus testowy zawiera syntetyczne PDF odtwarzające ich układ (stopki rejestrowe, taryfy opłat).
- Formatem wyjściowym jest Markdown zgodny z CommonMark + tabele GFM; przypisy w składni `[^n]`. Rendering używa znaku nowej linii LF niezależnie od systemu.
- Wyróżnienia typograficzne poza pogrubieniem i kursywą (podkreślenie, kolor) nie są odwzorowywane.
- Obrazy, wykresy i podpisy graficzne są pomijane (z odnotowaniem w raporcie); formularze PDF i adnotacje nie są przetwarzane.
- Przetwarzanie odbywa się lokalnie, bez wywołań sieciowych.
