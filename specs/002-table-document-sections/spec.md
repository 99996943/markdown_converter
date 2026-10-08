# Feature Specification: Dokumenty zbudowane jako jedna wielostronicowa tabela dwukolumnowa (tabela-dokument)

**Feature Branch**: `002-table-document-sections`

**Created**: 2026-10-08

**Status**: Draft

**Input**: User description: "Poprawne odczytywanie dokumentów zbudowanych jako jedna wielostronicowa tabela dwukolumnowa z obramowaniem (regulaminy promocji mBanku) w bibliotece LegalAgent.PdfParser (feature 001-legal-pdf-parser jest już zaimplementowany — ten feature go rozszerza, nie zastępuje). […] Dokument wzorcowy: Corpus/private/mbank-reg3.pdf — „Regulamin promocji »Rozwijaj firmę z płatnościami od mBanku – edycja 1«”, 13 stron […] Oczekiwany wynik: dokument-tabela tego typu ma być zapisany jako sekwencja sekcji: nazwa sekcji z lewej kolumny jako nagłówek dokumentu (poziom pod tytułem), a pod nią cała treść prawej komórki — scalona przez granice stron — jako zwykłe akapity i listy z zachowanym zagnieżdżeniem, bez tabeli GFM; powtarzany wiersz „Definicje | Wyjaśnienie” pomijany; śródtytuły z komórki jako pogrubione akapity, nie nagłówki; justowanie nie tworzy kolumn. […] Zwykłe tabele danych nadal mają wychodzić jako GFM."

## Kontekst

Feature rozszerza bibliotekę z `specs/001-legal-pdf-parser` (numeracja wymagań jest kontynuowana:
FR-080 i dalej, SC-010 i dalej; odwołania do FR-0xx dotyczą specyfikacji 001). Markdown biblioteki
zasila osobny chunker dzielący tekst po nagłówkach (RAG), więc liczą się: kompletny tekst w
oryginalnym brzmieniu, poprawna kolejność czytania, nagłówki dokładnie na granicach merytorycznych
sekcji i brak fałszywych nagłówków.

Niektóre regulaminy (wzorzec: regulamin promocji mBanku, `mbank-reg3.pdf`) nie mają zwykłego tekstu
ciągłego: po okładce cały dokument jest **jedną tabelą z pełną siatką linii** o dwóch kolumnach —
wąskiej lewej z pogrubioną nazwą sekcji („Organizator promocji”, „Uczestnik promocji”, „Ważne
pojęcia”, „Korzyści promocji”, „Kiedy i jak możesz przystąpić do promocji?”, „Jak możesz złożyć
reklamację dotyczącą promocji?” …) i szerokiej prawej z treścią (akapity o nierównych odstępach międzywyrazowych, listy „•” i „o”
z zagnieżdżeniem, numeracja „1)”, pogrubione śródtytuły, kursywa, adresy stron). Wiersz nazw kolumn
„Definicje | Wyjaśnienie” powtarza się na górze każdej strony; komórki są długie i przechodzą przez
granice stron (kontynuacja ma pustą lewą komórkę).

Obecny wynik (feature 001) dla takiego dokumentu: tabela rozpada się na fragmenty GFM i luźne
akapity, nazwa sekcji rozbija się na kawałki („**Uczestnik** W promocji mogą uczestniczyć:
**promocji**”), justowany tekst tworzy fałszywe kolumny (tabela 4-kolumnowa na stronie 4), strona 11
wychodzi w trybie awaryjnym („\|”), pogrubione śródtytuły z komórek stają się nagłówkami
(`### Nie możesz uczestniczyć w promocji, jeśli:`, `### MOJE OŚWIADCZENIA`), podpis logo na okładce
staje się nagłówkiem (`## mBank.pl`), a nazwy sekcji — właściwe granice fragmentów — nagłówkami nie
są. Tekst jest kompletny i to MUSI zostać zachowane.

## Clarifications

### Session 2026-10-08

- Q: Czy tabela-dokument ma być rozpoznawana tylko, gdy zajmuje większość dokumentu, czy także jako krótszy fragment zwykłego regulaminu? → A: Tylko gdy zajmuje ≥ 50% stron z tekstem (min. 2 strony) i ma kształt tabeli-dokumentu; krótsze tabele tego kształtu pozostają zwykłymi tabelami (FR-080 c); próg konfigurowalny.
- Q: Jak zapisać definicje „termin – objaśnienie” z sekcji „Ważne pojęcia” (w PDF bez punktorów)? → A: Każda definicja jako osobny akapit z oryginalnym tekstem — bez punktorów i bez dodatkowego pogrubienia; podział wyznacza krótka linia w tekście justowanym (FR-085).
  - Doprecyzowanie z planu (2026-10-08): prawa kolumna `mbank-reg3` ma nierówny prawy brzeg, więc „krótka linia” oznacza linię, za którą zmieściłoby się pierwsze słowo następnej linii (research R9), a nie próg długości.
- Q: Czy zasada „kolumny wyznacza siatka, nie odstępy justowania” ma działać tylko w tabeli-dokumencie, czy w każdej tabeli z siatką? → A: Tylko w tabeli-dokumencie; pozostałe tabele (w tym taryfy z siatką) bez zmian (FR-081).
- Q: Czy reguła „podpis przy obrazie nie jest nagłówkiem” ma działać we wszystkich dokumentach, czy tylko na okładce tabeli-dokumentu? → A: We wszystkich dokumentach, wyłączalna w ustawieniach; brak regresji korpusu weryfikuje SC-016 (FR-088).
- Q: Czy podtytuł okładki „Obowiązuje od … do …” ma pozostać nagłówkiem `##`, czy być zwykłym akapitem pod tytułem? → A: Zwykły akapit pod tytułem we wszystkich dokumentach, w których stoi w bloku tytułowym (zastępuje decyzję domyślną z feature 001); pliki oczekiwane dotkniętych dokumentów są celowo aktualizowane (FR-093, SC-016).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Sekcje tabeli-dokumentu jako nagłówki z ciągłą treścią (Priority: P1)

Programista systemu RAG konwertuje regulamin promocji zbudowany jako tabela-dokument i dostaje
Markdown, w którym każda nazwa sekcji z lewej kolumny jest nagłówkiem dokumentu, a pod nią stoi cała
treść prawej komórki jako zwykłe akapity i listy — scalona przez granice stron, bez tabeli GFM i bez
powtarzanego wiersza „Definicje | Wyjaśnienie”. Chunker dzieli dokument dokładnie po sekcjach.

**Why this priority**: bez tego dokument jest dla chunkera bezużyteczny — fragmenty mieszają sekcje,
a granice merytoryczne nie są nagłówkami. To główna wartość featury.

**Independent Test**: syntetyczny PDF odtwarzający układ (okładka + tabela 2-kolumnowa z siatką na
≥ 3 stronach, komórki przechodzące przez strony, powtórzony wiersz nazw kolumn) konwertowany do
Markdown porównywanego z ręcznie przygotowanym wynikiem oczekiwanym.

**Acceptance Scenarios**:

1. **Given** tabela-dokument z wierszami „Organizator promocji | treść”, „Uczestnik promocji | treść”,
   **When** dokument jest konwertowany, **Then** wynik zawiera nagłówki `## Organizator promocji` i
   `## Uczestnik promocji` w tej kolejności, a pod każdym — treść jego prawej komórki, bez znaków
   tabeli GFM.
2. **Given** nazwa sekcji zawinięta w lewej komórce na kilka linii („Jak możesz / złożyć /
   reklamację / dotyczącą / promocji?”), **When** dokument jest konwertowany, **Then** powstaje jeden
   nagłówek `## Jak możesz złożyć reklamację dotyczącą promocji?`, a linie prawej komórki nie
   przeplatają się z kawałkami nazwy.
3. **Given** komórka przechodząca przez granicę strony (na następnej stronie powtórzony wiersz
   „Definicje | Wyjaśnienie” i wiersz z pustą lewą komórką), **When** dokument jest konwertowany,
   **Then** treść z obu stron jest jedną ciągłą treścią tej samej sekcji: brak nowego nagłówka, brak
   powtórzonego wiersza nazw kolumn, a akapit lub pozycja listy przerwana granicą strony jest jednym
   akapitem / jedną pozycją (ze znacznikiem `<!-- page: N -->` wg FR-002a).
4. **Given** prawa komórka z listą „•” i podpunktami „o”, **When** dokument jest konwertowany,
   **Then** pozycje „o” są zagnieżdżone pod pozycją „•”, do której należą, a oznaczenia są zachowane
   tak jak w FR-050 – FR-054.
5. **Given** wiersz nazw kolumn „Definicje | Wyjaśnienie” na górze każdej strony tabeli, **When**
   dokument jest konwertowany, **Then** żadne jego wystąpienie nie pojawia się w wyniku ani jako
   nagłówek, ani jako treść.

---

### User Story 2 - Kolumny wyznacza siatka, nie justowanie (Priority: P1)

Tekst prawej komórki ma miejscami szerokie, nierówne odstępy między słowami (justowanie lub
wyrównanie edytora). Programista oczekuje, że takie
odstępy nie tworzą fałszywych kolumn ani tabel awaryjnych — słowa zostają w swoich zdaniach.

**Why this priority**: fałszywe kolumny rozrzucają słowa po komórkach i niszczą kolejność czytania
(strona 4 i 11 wzorca); bez tego US1 nie może dać poprawnej treści.

**Independent Test**: syntetyczna strona tabeli-dokumentu z akapitem o nierównych
odstępach międzywyrazowych (także linie z dużą przerwą, np. „w EUR (SEPA) do krajów …”); wynik
zawiera akapit z pełnymi zdaniami w oryginalnej kolejności, bez tabeli i bez separatora „ | ”.

**Acceptance Scenarios**:

1. **Given** linia z odstępem międzywyrazowym przekraczającym próg komórki z FR-060,
   **When** leży w prawej kolumnie tabeli-dokumentu, **Then** pozostaje jedną linią tekstu tej
   kolumny.
2. **Given** strona tabeli-dokumentu, na której dziś powstaje tabela awaryjna (TBL001), **When**
   dokument jest konwertowany, **Then** raport nie zawiera ostrzeżenia o niejednoznacznej siatce dla
   tej strony, a wynik nie zawiera linii z separatorem „ | ”.

---

### User Story 3 - Brak fałszywych nagłówków w tabeli-dokumencie i na okładce (Priority: P2)

Pogrubione śródtytuły wewnątrz komórek („Korzyści obowiązujące przez pierwsze 24 miesiące od dnia
otwarcia rachunku bieżącego:”, „Nie możesz uczestniczyć w promocji, jeśli:”, „Dodatkowo:”,
„MOJE OŚWIADCZENIA”) i podpis pod logo na okładce („mBank.pl”) nie mogą tworzyć granic fragmentów.

**Why this priority**: fałszywy nagłówek tnie sekcję na fragmenty bez kontekstu nazwy sekcji; ważne
dla jakości RAG, ale wymaga najpierw poprawnych sekcji z US1.

**Independent Test**: syntetyczna tabela-dokument z pogrubionym, wieloliniowym śródtytułem w prawej
komórce oraz okładka z obrazem i krótkim podpisem pod nim; wynik zawiera śródtytuł jako jeden
pogrubiony akapit, podpis jako zwykły akapit, a jedynymi nagłówkami są tytuł i nazwy sekcji.

**Acceptance Scenarios**:

1. **Given** pogrubiona w całości linia (lub kilka kolejnych linii) w prawej komórce, **When**
   dokument jest konwertowany, **Then** wynik zawiera ją jako jeden akapit `**…**` w miejscu, w którym
   stoi, a nie jako nagłówek.
2. **Given** pogrubiony tekst po ostatnim wierszu tabeli-dokumentu (sekcja oświadczeń z miejscem na
   podpis), **When** dokument jest konwertowany, **Then** nie staje się nagłówkiem; pozostaje
   pogrubionym akapitem, a lista oświadczeń i linia podpisu pozostają treścią.
3. **Given** krótki tekst bezpośrednio pod obrazem na okładce, **When** dokument jest konwertowany,
   **Then** jest zwykłym akapitem, nie nagłówkiem.
4. **Given** linia „Obowiązuje od 01.09.2026 r. do 30.11.2026 r.” pod tytułem na okładce, **When**
   dokument jest konwertowany, **Then** jest zwykłym akapitem bezpośrednio pod tytułem, nie
   nagłówkiem `##` (FR-093).

---

### User Story 4 - Zwykłe tabele i dotychczasowe dokumenty bez zmian (Priority: P1)

Programista, który już korzysta z biblioteki, oczekuje, że taryfy opłat (wiele kolumn), tabela
definicji w regulaminie rachunku (również „Definicje | Wyjaśnienie”, krótkie wiersze „termin |
objaśnienie”), schematy kroków (FR-067) i akty prawne wychodzą dokładnie tak jak dotąd.

**Why this priority**: regresja na dotychczasowym korpusie unieważnia całą zmianę; rozpoznanie
tabeli-dokumentu musi być wąskie.

**Independent Test**: istniejące pliki wzorcowe `Corpus/acts` i `Corpus/banking` przechodzą bez
zmian; dodatkowo syntetyczna tabela definicji 2-kolumnowa z siatką i krótkimi wierszami (także
dwustronicowa) nadal wychodzi jako tabela GFM.

**Acceptance Scenarios**:

1. **Given** tabela definicji 2-kolumnowa z siatką, z wieloma krótkimi wierszami, zajmująca
   niewielką część dokumentu, **When** dokument jest konwertowany, **Then** wynik zawiera tabelę GFM
   identyczną jak przed zmianą.
2. **Given** prywatne dokumenty `mbank-regulamin-pdp.pdf`, `mbank-reg1.pdf`, `mbank-reg2.pdf`
   (weryfikacja ręczna / test opcjonalny), **When** są konwertowane, **Then** wynik jest identyczny z
   wynikiem sprzed zmiany (w tym schematy kroków FR-067), poza zamierzoną zmianą FR-093 (linia
   „Obowiązuje od …” jako akapit zamiast `##`).
3. **Given** rozpoznawanie tabeli-dokumentu wyłączone w ustawieniach, **When** `mbank-reg3.pdf` jest
   konwertowany, **Then** tabela wychodzi jak w feature 001 (GFM / tryb awaryjny), a jedyne różnice
   względem wyniku feature 001 wynikają z FR-088, FR-093, FR-094 i punktora „o”.

---

### Edge Cases

- Nazwa sekcji w lewej komórce przerwana granicą strony (część nazwy na dole strony, reszta w
  kontynuacji wiersza na następnej — np. „Warunki/zasady” / „promocji”) → jedna nazwa sekcji, jeden
  nagłówek przed treścią z obu stron (FR-083).
- Wiersz, którego lewa komórka jest pusta, a który nie jest kontynuacją z poprzedniej strony (pusty
  wiersz w środku strony) → kontynuacja bieżącej sekcji (FR-084).
- Pierwszy wiersz danych tabeli ma pustą lewą komórkę (brak sekcji, do której można dołączyć) → treść
  jest treścią wstępną przed pierwszą sekcją, bez dopisanej nazwy (FR-084, FR-091).
- Wiersz nazw kolumn obecny tylko na pierwszej stronie tabeli albo nieobecny wcale → tabela-dokument
  rozpoznana tak samo; brak wiersza nazw nie jest wymogiem (FR-080, FR-082).
- Pogrubiona w całości krótka linia w prawej komórce, której tekst jest identyczny z nazwą sekcji
  innego wiersza → nadal pogrubiony akapit, nie nagłówek (FR-086).
- Sekcja „Ważne pojęcia” z definicjami „termin – objaśnienie” w kolejnych liniach bez punktorów →
  każda definicja jako osobny akapit w oryginalnym brzmieniu, bez dopisanych punktorów (FR-085).
- Adres strony WWW zawinięty na końcu linii po łączniku („…/pierscien-platniczy-” / „mastercard”) →
  łącznik zostaje, a część z następnej linii dołączona bez spacji („…/pierscien-platniczy-mastercard”);
  łącznik w adresie (wyraz zawierający „://”, „www.” albo „/”) nigdy nie jest usuwany jako łącznik
  przeniesienia (FR-094).
- Wiersz nazw kolumn nie powtarza się na każdej stronie (w `mbank-reg3` brak go na stronach 4, 5 i 8,
  gdzie wiersz zaczyna się od górnej krawędzi siatki) → rozpoznanie i kontynuacja działają tak samo.
- Podkreślenia linków w komórce (krótkie poziome linie wewnątrz prawej kolumny) → nie są granicami
  wierszy; granicą wiersza jest tylko pozioma linia siatki przecinająca całą szerokość tabeli (FR-080).
- Tabela 2-kolumnowa z siatką o długich komórkach i listach, ale zajmująca mniej niż 50% stron z
  tekstem (np. 3 strony w 20-stronicowym regulaminie) → zwykła tabela (GFM lub awaryjna jak dotąd),
  nie tabela-dokument (FR-080 c).
- Schemat kroków (FR-067) obok lub wewnątrz dokumentu z tabelą-dokumentem → schemat ma pierwszeństwo;
  jego obszar nie wchodzi do tabeli-dokumentu (FR-089).
- Dokument z dwiema odrębnymi tabelami-dokumentami (przerwa z tekstem ciągłym między nimi) → każda
  rozpoznawana osobno według tych samych kryteriów; sekcje obu tworzą jedną sekwencję nagłówków w
  kolejności dokumentu.
- Obraz na stronie tabeli (np. logo w komórce) → pomijany jak dotąd (IMG001); jego podpis nie jest
  nagłówkiem (FR-088).

## Requirements *(mandatory)*

### Functional Requirements

#### Rozpoznanie tabeli-dokumentu

- **FR-080**: Region tabeli z siatką linii (FR-061, wraz z kontynuacjami na kolejnych stronach wg
  FR-065; pionowe linie siatki wyznaczają zewnętrzne krawędzie i granicę kolumn, a granicą wiersza jest
  tylko pozioma linia przecinająca całą szerokość tabeli) MUSI zostać rozpoznany jako **tabela-dokument**, gdy jednocześnie: (a) linie siatki
  wyznaczają dokładnie dwie kolumny na wszystkich stronach regionu; (b) lewa kolumna ma szerokość
  ≤ 35% szerokości tabeli; (c) region obejmuje co najmniej 2 strony i co najmniej 50% stron dokumentu
  zawierających tekst; (d) co najmniej jedna prawa komórka przechodzi przez granicę strony albo
  zawiera listę lub co najmniej dwa akapity; (e) mediana liczby słów prawych komórek wierszy z niepustą
  lewą komórką wynosi co najmniej 40. Region niespełniający któregokolwiek warunku jest przetwarzany
  jak dotąd (FR-060 – FR-066). Progi są konfigurowalne, a rozpoznawanie wyłączalne (FR-005); po
  wyłączeniu tabela jest przetwarzana jak w feature 001 (FR-060 – FR-066), a wynik różni się od wyniku
  feature 001 wyłącznie skutkami reguł ogólnych z tej specyfikacji: FR-088 i FR-093 (własne
  przełączniki), FR-094 i punktora „o” (FR-085).
- **FR-081**: W tabeli-dokumencie granice kolumn wyznaczają wyłącznie pionowe linie siatki: odstępy
  poziome między słowami (np. justowanie lub nierówne odstępy edytora) NIE tworzą komórek ani kolumn (FR-060 nie ma
  zastosowania), a tabela-dokument nigdy nie jest renderowana awaryjnie (FR-064) ani jako tabela GFM.
  Zasada dotyczy wyłącznie tabeli-dokumentu; wyznaczanie kolumn w pozostałych tabelach, także z siatką
  linii, pozostaje bez zmian (FR-060 – FR-066).

#### Wynik: sekcje

- **FR-082**: Wiersz nazw kolumn tabeli-dokumentu (pierwszy wiersz regionu oraz jego powtórzenia na
  górze kolejnych stron — wiersz o krótkim tekście w obu kolumnach, pogrubiony lub o tym samym
  odcisku tekstu co pierwszy wiersz) MUSI zostać pominięty przy każdym wystąpieniu: nie jest
  nagłówkiem, nazwą sekcji ani treścią. Pominięcie jest odnotowane w raporcie (FR-090). Gdy pierwszy
  wiersz nie spełnia tych warunków, jest zwykłym wierszem z nazwą sekcji.
- **FR-083**: Każdy wiersz tabeli-dokumentu z niepustą lewą komórką MUSI rozpoczynać sekcję: tekst
  całej lewej komórki — wszystkie jej linie złączone pojedynczą spacją w kolejności z góry na dół,
  także gdy komórka przechodzi przez granicę strony — staje się nagłówkiem dokumentu poziomu 2 (`##`;
  tytuł dokumentu ma zawsze poziom 1, także gdy nie został wykryty), bez znaczników wyróżnienia. W modelu powstaje
  sekcja z tą nazwą jako tytułem i zakresem stron jej treści. Nagłówek stoi przed całą treścią prawej
  komórki tego wiersza.
- **FR-084**: Wiersz z pustą lewą komórką (w tym pierwszy wiersz danych na stronie, będący
  kontynuacją komórki z poprzedniej strony) MUSI kontynuować bieżącą sekcję. Akapit lub pozycja listy
  przerwana granicą strony albo granicą wiersza MUSI być kontynuowana wg FR-033 i FR-053, a znacznik
  strony wstawiany wg FR-002a. Treść wierszy z pustą lewą komórką przed pierwszą sekcją jest treścią
  wstępną bez nagłówka.
- **FR-085**: Treść prawej kolumny sekcji MUSI być przetwarzana jak tekst ciągły jednej kolumny:
  akapity (FR-032, FR-033), listy z zagnieżdżeniem wg wcięcia i rodzaju oznaczenia (FR-050 – FR-054,
  np. „o” pod „•”, „1)”), wyróżnienia wewnątrz linii (FR-046) i adresy stron jako oryginalny tekst.
  Linia kończy akapit także bez kropki na końcu, gdy następna linia zaczyna się wielką literą, a w wolnym
  miejscu do prawej krawędzi kolumny zmieściłoby się jej pierwsze słowo (złamanie linii było
  zamierzone, a nie wymuszone
  szerokością kolumny); wyraz jednoliterowy na początku następnej linii („o”, „w”, „z”, „i” — polska
  typografia przenosi je do kolejnej linii) mierzony jest razem z wyrazem, który po nim następuje.
  Linia nazwy sekcji kontynuowanej na następnej stronie (FR-083) nie przerywa akapitu ani listy
  prawej kolumny. Ręczne złamanie linii w środku zdania (następna linia zaczyna się małą literą lub
  cyfrą) nie kończy akapitu, a data na początku linii („01.01.2026 r.”) nie jest oznaczeniem listy —
  dzięki temu kolejne definicje „termin – objaśnienie” stają się osobnymi
  akapitami z oryginalnym tekstem, bez punktorów listy i bez dodatkowego pogrubienia terminu. Znak „o”
  na początku linii złożony inną czcionką niż następujący po nim tekst (np. Courier New — drugi poziom
  list edytora tekstu) jest punktorem (FR-050); słowo „o” złożone czcionką tekstu punktorem nie jest.
  Linia pogrubiona w całości nie łączy się w akapit z linią niepogrubioną ani odwrotnie (FR-086).
- **FR-086**: Linia pogrubiona w całości w prawej kolumnie tabeli-dokumentu (oraz kilka takich linii
  bezpośrednio po sobie) MUSI być renderowana jako jeden pogrubiony akapit `**…**` w miejscu
  wystąpienia i NIE MOŻE stać się nagłówkiem dokumentu ani rozpocząć sekcji (rozszerzenie FR-047).
- **FR-087**: W dokumencie zawierającym tabelę-dokument nagłówkami dokumentu od początku pierwszej
  tabeli-dokumentu do końca dokumentu są wyłącznie nazwy sekcji (FR-083) oraz jednostki redakcyjne
  wg FR-043 („Art. N.”, „§ N.”, „Rozdział …”), jeśli występują. Tekst po ostatnim wierszu
  tabeli-dokumentu (np. „MOJE OŚWIADCZENIA” z listą oświadczeń i miejscem na podpis) jest treścią
  ostatniej sekcji; pogrubione linie stają się pogrubionymi akapitami. Treść przed tabelą (okładka)
  jest przetwarzana wg dotychczasowych reguł (FR-040 – FR-043a, z FR-088).

#### Okładka i podpisy grafik

- **FR-088**: Krótki tekst (≤ 2 linie) leżący w obrysie obrazu albo bezpośrednio pod nim (odstęp
  pionowy ≤ 3 wysokości linii, zakres poziomy mieszczący się w szerokości obrazu powiększonej o 10%)
  jest podpisem grafiki: NIE MOŻE stać się nagłówkiem typograficznym (FR-041) i jest renderowany jako
  zwykły akapit z oryginalnym tekstem. Reguła dotyczy wszystkich dokumentów i jest wyłączalna (FR-005).
- **FR-093**: Linia bloku tytułowego (pierwsza strona, pod tytułem dokumentu, przed pierwszą sekcją)
  zaczynająca się od „Obowiązuje od” / „obowiązuje od” (wielkość liter bez znaczenia; z datą lub
  zakresem dat) NIE MOŻE stać się nagłówkiem: jest renderowana jako zwykły akapit z oryginalnym tekstem
  bezpośrednio pod tytułem i należy do części tytułowej (przed pierwszą sekcją). Reguła dotyczy
  wszystkich dokumentów (zastępuje decyzję domyślną feature 001, w której podtytuł był `##`) i jest
  wyłączalna (FR-005).

#### Zgodność z dotychczasowym zachowaniem

- **FR-089**: Obszar rozpoznany jako schemat kroków (FR-067) NIE MOŻE wchodzić do tabeli-dokumentu;
  rozpoznanie schematu ma pierwszeństwo. Tabele niebędące tabelą-dokumentem (taryfy opłat, tabele
  definicji o krótkich wierszach) zachowują dotychczasowy rendering (FR-060 – FR-066) bez zmian.
- **FR-090**: Raport (FR-070) MUSI zawierać dla każdej tabeli-dokumentu: zakres stron, liczbę sekcji
  i liczbę pominiętych wystąpień wiersza nazw kolumn wraz z jego tekstem. Model dokumentu MUSI
  oznaczać sekcje pochodzące z tabeli-dokumentu odrębnym rodzajem sekcji.
- **FR-091**: Wynik MUSI zawierać wyłącznie oryginalny tekst PDF: biblioteka nie dopisuje słów,
  etykiet, numeracji ani nazw sekcji (pusta lewa komórka nie otrzymuje nazwy); strukturę wyraża
  wyłącznie formatowanie Markdown. Jedynym pomijanym tekstem są wystąpienia wiersza nazw kolumn
  (FR-082).
- **FR-092**: Rozpoznanie i rendering tabeli-dokumentu MUSZĄ być deterministyczne i niezależne od
  systemu operacyjnego i zainstalowanych czcionek (FR-008, FR-011a).
- **FR-094**: Łącznik kończący linię wewnątrz wyrazu będącego adresem (zawierającego „://”, „www.”
  albo „/”) MUSI zostać zachowany, a część z następnej linii dołączona bez spacji (rozszerzenie FR-012,
  dotyczy wszystkich dokumentów).

### Key Entities

- **Tabela-dokument**: region tabeli z siatką linii spełniający FR-080 — zakres stron, położenie
  granicy kolumn, wiersz nazw kolumn (tekst, wystąpienia), lista wierszy.
- **Wiersz tabeli-dokumentu**: para lewa/prawa komórka między poziomymi liniami siatki na jednej
  stronie; lewa pusta oznacza kontynuację.
- **Sekcja tabeli-dokumentu**: sekcja modelu (odrębny rodzaj) z nazwą z lewej komórki, poziomem pod
  tytułem, zakresem stron i treścią z prawych komórek wszystkich jej wierszy (akapity, listy,
  pogrubione akapity-śródtytuły).
- **Podpis grafiki**: krótki tekst przy obrazie (FR-088), zwykły akapit.

## Success Criteria *(mandatory)*

Kryteria są weryfikowane na syntetycznym odpowiedniku `mbank-reg3` w korpusie repozytorium (z ręcznie
przygotowanym wynikiem oczekiwanym) oraz ręcznie / testem opcjonalnym na prywatnym `mbank-reg3.pdf`.

### Measurable Outcomes

- **SC-010**: 100% słów tekstu PDF — poza wystąpieniami wiersza nazw kolumn — występuje w wyniku,
  w kolejności czytania dokumentu; wynik nie zawiera ani jednego słowa spoza PDF.
- **SC-011**: 100% nazw sekcji z lewej kolumny jest nagłówkami dokumentu poziomu pod tytułem, w
  kolejności dokumentu, każda w całości (żadna nie jest rozbita) i przed treścią swojej komórki.
- **SC-012**: 0 fałszywych nagłówków: w wyniku dla `mbank-reg3` nagłówkami są wyłącznie tytuł `#` i
  nazwy sekcji — podtytuł „Obowiązuje od …” i podpis „mBank.pl” są akapitami; żaden śródtytuł z komórki ani
  „MOJE OŚWIADCZENIA” nie jest nagłówkiem.
- **SC-013**: 0 tabel GFM, 0 tabel awaryjnych, 0 linii z separatorem „ | ” i 0 ostrzeżeń o
  niejednoznacznej siatce w wyniku i raporcie dla `mbank-reg3`.
- **SC-014**: Każda komórka przechodząca przez granicę strony daje jeden ciągły fragment: między
  nagłówkiem jej sekcji a następnym nagłówkiem znajduje się cała jej treść z wszystkich stron, bez
  powtórzonego wiersza nazw kolumn, a akapit przerwany granicą strony jest jednym akapitem.
- **SC-015**: 100% pozycji list z prawych komórek ma poprawne oznaczenie i poziom zagnieżdżenia
  (pozycje „o” pod właściwą pozycją „•”).
- **SC-016**: Brak regresji: 100% istniejących plików wzorcowych `Corpus/acts` i `Corpus/banking`
  przechodzi bez zmian, a wynik dla `mbank-regulamin-pdp.pdf`, `mbank-reg1.pdf` i `mbank-reg2.pdf`
  jest identyczny z wynikiem sprzed zmiany — z jedynym dopuszczalnym wyjątkiem zamierzonej zmiany
  FR-093 (linia „Obowiązuje od …” z `##` na akapit; poza tym diff pusty), o ile dotyczy danego pliku.
- **SC-017**: Wynik dla korpusu jest identyczny na Windows i Linux (CI).

## Assumptions

- Nazwy sekcji otrzymują poziom `##` (dokument ma tytuł `#` z okładki). Okładka pozostaje przetwarzana
  jak w feature 001 z wyjątkiem podpisu grafiki (FR-088) i podtytułu „Obowiązuje od …” (FR-093).
- Wiersz nazw kolumn „Definicje | Wyjaśnienie” jest pomijany przy każdym wystąpieniu, także pierwszym —
  analogicznie do wiersza nazw kolumn schematu kroków (FR-067); jest to jedyny tekst wyłączony z
  kryterium kompletności SC-010.
- „MOJE OŚWIADCZENIA” i linia podpisu na ostatniej stronie należą do ostatniej sekcji (pogrubiony akapit +
  lista + akapity), niezależnie od tego, czy stoją w ostatnim wierszu tabeli, czy pod nią.
- Progi FR-080 (35% szerokości, 50% stron, mediana 40 słów) i FR-088 są punktem startowym, dostrajanym
  na syntetycznym korpusie i prywatnych regulaminach w trakcie implementacji (TDD); ich zmiany są
  dokumentowane.
- Prawdziwych dokumentów bankowych nie wolno commitować (`Corpus/private` w `.gitignore`); testy w
  repozytorium używają syntetycznych PDF-ów generowanych w korpusie bankowym, a `mbank-reg3.pdf` służy
  do weryfikacji ręcznej i opcjonalnego testu prywatnego korpusu (zmienna `LEGALAGENT_PRIVATE_CORPUS`).
- Obowiązuje konstytucja projektu: TDD z osobnymi commitami red/green, test czerwony przed kodem
  produkcyjnym.
- Linki zachowują tylko tekst widoczny w PDF (jak w feature 001); adnotacje linków nie są odczytywane.
- Brak hooka tworzącego gałąź git: specyfikacja powstaje w katalogu `specs/002-table-document-sections`;
  utworzenie gałęzi `002-table-document-sections` jest decyzją użytkownika.
