# Feature Specification: Fałszywe ostrzeżenia FTN001_OrphanFootnote po spec 010 (parser 1.3.1)

**Feature Branch**: `011-ftn001-false-warnings`

**Created**: 2026-10-13

**Status**: Draft

**Input**: User description: "Fałszywe ostrzeżenia FTN001_OrphanFootnote po spec 010 (parser 1.3.1). Problem: parser 1.3.0 zgłasza w raporcie konwersji 5 nowych ostrzeżeń FTN001_OrphanFootnote dla aktu prawnego „Akt prawny - D20081505Lj.pdf” (prywatny plik właściciela w katalogu głównym repozytorium). Ostrzeżenia dotyczą strony 13 („1)”), strony 68 („9)”), strony 70 („10)”) i strony 71 („11)”, „12)”). Parser 1.2.3 nie zgłaszał dla tego dokumentu żadnego takiego ostrzeżenia. Markdown i chunki są bajt w bajt identyczne w obu wersjach, więc różnica dotyczy wyłącznie raportu. Pliki porównawcze: publish/porownanie-1.3.0/akt-D20081505Lj.old/new.{md,report.json,chunks.jsonl}. Podejrzenie: zmiany spec 010 w FootnoteDetectionStage tworzą szkice przypisów, do których nie prowadzi żaden odnośnik w tekście (obszar pod linią w rozmiarze tekstu głównego, LoneLabel, odnośniki przed słowem). Odnośniki typu „z późn. zm.1))” pozostają sklejone w obu wersjach. Oczekiwane: 1) ustalić przyczynę każdego z 5 ostrzeżeń (fałszywe czy zasadne), wynik w research.md; 2) fałszywe — parser ich nie zgłasza, liczba FTN001 wraca do poziomu 1.2.3; zasadne — znane ograniczenie w README parsera albo bezpieczna naprawa rozpoznawania odnośnika; 3) wykrywanie przypisów ze spec 010 działa bez zmian na dokumentach bankowych. Ograniczenia: repliki stron (SyntheticPdfBuilder), commit red potem green; goldeny parsera i chunków bez zmian (FR-163); pełny zestaw z LEGALAGENT_PRIVATE_CORPUS bez nowych ostrzeżeń i różnic Markdown, bramka RegulationNumbers_HaveNoExcess przechodzi; po zmianie w kodzie wersja 1.3.1, refresh/verify korpusu, przebudowany publish/; mały zakres."

## Kontekst

Feature poprawia bibliotekę `LegalAgent.PdfParser` po specyfikacji 010 (numeracja wymagań jest kontynuowana od
FR-800 i SC-120). Raport konwersji (`*.report.json`) jest dla właściciela głównym sygnałem jakości: ostrzeżenie
FTN001_OrphanFootnote („Przypis „N)” na stronie S nie ma odnośnika w tekście”) oznacza, że definicja przypisu nie
ma odnośnika i że w Markdownie może brakować powiązania `[^n]`. Fałszywe ostrzeżenia zasłaniają prawdziwe usterki i
osłabiają porównania wersji parsera.

Po wydaniu 1.3.0 właściciel porównał wynik dla aktu prawnego „Akt prawny - D20081505Lj.pdf” (71 stron, prywatny
plik w katalogu głównym repozytorium, nie w gicie) z wynikiem 1.2.3:

| Wersja | FTN001 | Markdown | Chunki |
|---|---|---|---|
| 1.2.3 | 0 | — | — |
| 1.3.0 | 5: s. 13 „1)”, s. 68 „9)”, s. 70 „10)”, s. 71 „11)”, „12)” | identyczny bajt w bajt | identyczne bajt w bajt |

Pliki porównawcze: `publish/porownanie-1.3.0/akt-D20081505Lj.{old,new}.{md,report.json,chunks.jsonl}` (poza gitem).
Podejrzenie właściciela: nowe ścieżki wykrywania przypisów ze spec 010 (obszar przypisów pod linią w rozmiarze
tekstu głównego, podniesiona etykieta w osobnej linii, odnośnik przed słowem) uznają za definicje przypisów linie,
do których nie prowadzi żaden odnośnik. Odnośniki typu „z późn. zm.1))” (cyfra sklejona z tekstem) są sklejone w
obu wersjach — to dawne, znane zachowanie, a nie przedmiot tej poprawki.

**Wynik diagnozy** ([research.md](research.md) R1–R2): żadne z 5 ostrzeżeń nie jest fałszywe. Są to prawdziwe
przypisy aktu, których odnośnik (podniesiony ciąg, za nim pełnowymiarowa interpunkcja: „zm.¹⁾)”, „ogłoszenia¹²⁾,”)
nie był rozpoznawany ani w 1.2.3, ani w 1.3.0. Parser 1.2.3 ich nie zgłaszał, bo podniesiona etykieta definicji
liczyła się jako odnośnik do samej siebie („samoodnośnik”). Spec 010 usunęło samoodnośnik tylko pod kreską
przypisów, a na stronach bez kreski (s. 67 aktu) działa on dalej. Ten sam mechanizm dodał 45 zasadnych ostrzeżeń w 6
aktach `Corpus/acts` (w tym `ustawa-o-sluzbie-cywilnej.pdf`, ten sam akt w innym pliku), a nowe definicje ze spec 010
dodały ostrzeżenia w regulaminach (reg1, regulamin-pdp, ind-4, firm-1). Tytuł feature'u zostaje dla ciągłości,
choć ostrzeżenia okazały się zasadne.

**Poza zakresem**: rozpoznawanie odnośników z pełnowymiarową interpunkcją za nimi, list odnośników („⁹,¹⁰”) i
odnośników w środku słowa (znane ograniczenie, README); podział przypisu z listą „1)”, „2)” na kilka definicji
(reg1/pdp s. 44, istnieje od 1.2.3); przywracanie samoodnośnika; zmiany Markdownu lub chunków jakiegokolwiek dokumentu.

## Clarifications

### Session 2026-10-13

- Q: Czy akt D20081505Lj ma dostać trwałą ochronę przed powrotem ostrzeżeń, skoro nie ma go w żadnym zestawie
  testowym? → A: Tak, bramka na liczbie FTN001 bez goldena Markdown. (Założenie pytania było błędne: akt jest w
  `Corpus/acts` jako `ustawa-o-sluzbie-cywilnej.pdf`; miejsce bramki zmienione w sesji planu poniżej.)
- Q: Czy porównanie FTN001 1.2.3/1.3.0 obejmuje inne dokumenty niż akt D20081505Lj? → A: Tak — wszystkie dostępne
  dokumenty (`Corpus/acts`, `Corpus/banking`, prywatny korpus, regulaminy z publish/); każde nowe ostrzeżenie
  diagnozowane w research.md i poprawiane tą samą poprawką.

### Session 2026-10-13 (plan)

- Q: Diagnoza wykazała, że 5/5 ostrzeżeń jest zasadnych (samoodnośnik w 1.2.3). Jaki zakres ma spec 011? → A:
  Wariant a′: znane ograniczenie w README (nierozpoznane odnośniki) + usunięcie samoodnośnika także na stronach bez
  kreski (spójność; akt 5 → 11 ostrzeżeń); Markdown bez zmian; wersja 1.3.1; bramka utrwala liczby ostrzeżeń.
- Q: Gdzie działa bramka FTN001 z FR-810? → A: Na akcie publicznym `Corpus/acts/ustawa-o-sluzbie-cywilnej.pdf`
  (w CI); bez kopii D20081505Lj w katalogu prywatnym.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Diagnoza każdego ostrzeżenia (Priority: P1)

Jako właściciel parsera chcę wiedzieć, skąd bierze się każde z 5 nowych ostrzeżeń FTN001 dla aktu D20081505Lj —
czy to regresja spec 010 (fałszywa definicja przypisu), czy prawdziwy przypis bez rozpoznanego odnośnika — żeby
podjąć decyzję o poprawce na podstawie faktów, a nie przypuszczeń.

**Why this priority**: od wyniku diagnozy zależy, czy potrzebna jest zmiana w kodzie (US2), czy tylko opis znanego
ograniczenia; bez niej każda poprawka może ukryć prawdziwe przypisy.

**Independent Test**: dokument research.md zawiera dla każdego z 5 ostrzeżeń (strona, etykieta): co jest
wydrukowane w PDF w miejscu definicji i odnośnika, która ścieżka wykrywania przypisów utworzyła definicję, dlaczego
1.2.3 jej nie tworzył, oraz klasyfikację „fałszywe” / „zasadne” z uzasadnieniem.

**Acceptance Scenarios**:

1. **Given** raporty 1.2.3 i 1.3.0 oraz PDF aktu, **When** diagnoza, **Then** każde z 5 ostrzeżeń ma w research.md
   przyczynę wskazującą konkretną regułę wykrywania przypisów i klasyfikację.
2. **Given** ostrzeżenie zasadne (prawdziwy przypis, odnośnik sklejony z tekstem), **When** diagnoza, **Then**
   research.md mówi, czy rozpoznanie odnośnika da się poprawić bezpiecznie (bez zmian w innych dokumentach), czy
   zostaje znanym ograniczeniem.

---

### User Story 2 - Spójne i wiarygodne ostrzeżenia FTN001 (Priority: P1)

Jako właściciel parsera chcę, żeby ostrzeżenie FTN001 znaczyło zawsze to samo — „do tej definicji przypisu nie
prowadzi żaden rozpoznany odnośnik w tekście” — niezależnie od tego, czy nad definicjami jest kreska, i żeby liczba
ostrzeżeń w aktach była utrwalona w testach, bo dopiero wtedy porównanie raportów dwóch wersji parsera jest
wiarygodnym sygnałem.

**Why this priority**: dziś ta sama definicja z podniesioną etykietą jest „powiązana” ze sobą na stronie bez kreski,
a pod kreską jest sierotą; liczba ostrzeżeń zależy od układu strony, nie od tego, czy odnośnik rozpoznano.

**Independent Test**: syntetyczna replika strony bez kreski z blokiem definicji w małej czcionce na dole strony
(etykiety podniesione „2)”, „3)”), z prawdziwym odnośnikiem tylko do jednej z nich i z odnośnikiem do drugiej
zapisanym tak, że parser go nie rozpoznaje (podniesione „3)” i pełnowymiarowy przecinek za nim). Raport zawiera
FTN001 dla „3)”, a Markdown jest taki sam jak w 1.3.0. Test nie przechodzi na kodzie 1.3.0.

**Acceptance Scenarios**:

1. **Given** definicja przypisu z podniesioną etykietą, do której nie prowadzi żaden rozpoznany odnośnik, **When**
   konwersja, **Then** raport zawiera FTN001 dla tej definicji, na stronie z kreską i bez kreski.
2. **Given** definicja z rozpoznanym odnośnikiem w tekście (na końcu słowa, przed słowem, w komórce tabeli), **When**
   konwersja, **Then** odnośnik i definicja są powiązane jak w 1.3.0, bez FTN001.
3. **Given** akt `ustawa-o-sluzbie-cywilnej` (i prywatny D20081505Lj), **When** konwersja parserem 1.3.1, **Then**
   raport zawiera 11 ostrzeżeń FTN001 (5 z 1.3.0 i 6 definicji s. 67 dotąd maskowanych), a Markdown i chunki są
   identyczne z 1.3.0.
4. **Given** pozostałe dokumenty (akty, regulaminy bankowe, korpus syntetyczny), **When** konwersja parserem 1.3.1,
   **Then** liczba FTN001 i Markdown jak w 1.3.0.

---

### User Story 3 - Wykrywanie przypisów ze spec 010 bez zmian (Priority: P1)

Jako twórca aplikacji RAG chcę, żeby przypisy w regulaminach bankowych (przypisy w komórkach tabel, odnośniki
przed słowem, obszar przypisów pod linią w rozmiarze tekstu, podniesiona etykieta w osobnej linii) były
rozpoznawane jak w 1.3.0, bo powiązanie zapisów z datą obowiązywania było celem spec 010.

**Why this priority**: poprawka ostrzeżeń nie może cofnąć wartości spec 010; to warunek przyjęcia US2.

**Independent Test**: istniejące testy przypisów spec 010 i goldeny prywatnego korpusu przechodzą bez zmian.

**Acceptance Scenarios**:

1. **Given** dokumenty bankowe prywatnego korpusu i testy przypisów spec 010, **When** pełny zestaw testów
   parsera z prywatnym korpusem, **Then** goldeny bez zmian, brak nowych ostrzeżeń, bramka kontroli liczb przechodzi.
2. **Given** korpus syntetyczny, **When** odświeżenie i weryfikacja, **Then** brak różnic poza numerem wersji
   parsera w manifeście.

### Edge Cases

- Punkty listy „1)”, „2)” wewnątrz przypisu pod kreską (research R2, K3: reg1 i regulamin-pdp s. 44) są dziś
  osobnymi definicjami z FTN001. To znane ograniczenie (FR-802), istniejące od 1.2.3: spec 011 go nie naprawia, ale
  liczba takich definicji i ostrzeżeń nie może wzrosnąć (SC-125). Linia treści, która w 1.3.0 nie była definicją,
  nie może się nią stać (FR-807).
- Prawdziwy przypis aktu, którego odnośnik jest sklejony z tekstem („z późn. zm.1))”) — 1.2.3 i 1.3.0 renderują go
  tak samo; poprawka nie może zgubić tekstu definicji ani zmienić Markdownu bez zgody właściciela.
- Kilka ostrzeżeń na jednej stronie (s. 71: „11)”, „12)”) — każde diagnozowane osobno; mogą mieć różne przyczyny.
- Strona, na której etykieta definicji ma odnośnik na innej stronie (przypis przeniesiony) — zachowanie jak w 1.3.0.
- Ostrzeżenie zależne od numeracji (np. „9)”–„12)” jako ciąg) — poprawka nie może opierać się na konkretnych
  numerach ani tekście tego aktu.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-800**: Przyczyna każdego z 5 ostrzeżeń FTN001 aktu D20081505Lj (s. 13 „1)”, s. 68 „9)”, s. 70 „10)”, s. 71
  „11)”, „12)”) MUST być ustalona i zapisana w research.md: wydrukowany układ, reguła wykrywania, która utworzyła
  definicję, różnica względem 1.2.3, klasyfikacja fałszywe/zasadne.
- **FR-800a**: Liczby ostrzeżeń FTN001 parserów 1.2.3 i 1.3.0 MUST być porównane na wszystkich dostępnych
  dokumentach (akty publiczne i bankowe korpusu testowego, prywatny korpus, regulaminy z publish/); każde ostrzeżenie
  obecne w 1.3.0, a nieobecne w 1.2.3, MUST być zdiagnozowane w research.md jak w FR-800 i objęte FR-801/FR-802.
- **FR-801**: Podniesiona etykieta otwierająca linię definicji przypisu MUST NOT być liczona jako odnośnik do
  żadnej definicji, także na stronie bez kreski przypisów (koniec samoodnośnika). Rozpoznawanie obszaru przypisów i
  definicji MUST pozostać bez zmian, więc Markdown się nie zmienia (FR-807).
- **FR-802**: Ostrzeżenia zasadne (R1–R2: odnośnik z pełnowymiarową interpunkcją za nim, lista odnośników „⁹,¹⁰”,
  odnośnik w środku słowa, odnośnik złożony do sąsiedniej linii, punkty listy wewnątrz przypisu jako osobne
  definicje) MUST być opisane w „Znanych ograniczeniach” README parsera, bez zmian rozpoznawania odnośników.
- **FR-803**: Każdy przypadek objęty zmianą w kodzie MUST mieć syntetyczną replikę strony w testach (układ bez
  treści prywatnego aktu), dodaną najpierw w osobnym commicie z nieprzechodzącym testem, a potem w commicie z
  poprawką.
- **FR-804**: Wykrywanie przypisów dodane w spec 010 (przypisy w komórkach tabel, odnośniki przed słowem, obszar
  przypisów pod linią w rozmiarze tekstu, podniesiona etykieta w osobnej linii) MUST działać bez zmian; istniejące
  testy tych reguł MUST przechodzić bez modyfikacji oczekiwań.
- **FR-805**: Goldeny parsera (`*.expected.md`) i goldeny chunków MUST pozostać bez zmian; każda różnica MUST być
  zgłoszona właścicielowi do akceptacji przed commitem (FR-163) i zapisana w handoffie.
- **FR-806**: Pełny zestaw testów parsera z prywatnym korpusem MUST przechodzić bez nowych ostrzeżeń i bez różnic
  Markdownu na pozostałych dokumentach prywatnych; bramka kontroli liczb (brak nadmiarowych liczb w regulaminach)
  MUST przechodzić.
- **FR-807**: Markdown i chunki aktu D20081505Lj i wszystkich dokumentów testowych MUST być identyczne z wynikiem
  1.3.0.
- **FR-808**: Wersja parsera MUST wynosić 1.3.1, korpus syntetyczny MUST być odświeżony i przechodzić
  weryfikację, a wydanie w publish/ MUST być przebudowane.
- **FR-809**: Poprawka MUST NOT dodawać nowych rodzajów wykrywania przypisów ani zależeć od tekstu, numerów
  etykiet czy numerów stron tego aktu.
- **FR-810**: Testy MUST utrwalać liczbę ostrzeżeń FTN001 każdego aktu `Corpus/acts` (bramka uruchamiana w CI,
  bez goldena raportu); dla `ustawa-o-sluzbie-cywilnej` wartość 11. Zmiana liczby MUST wymagać świadomej zmiany
  oczekiwań w teście i wpisu w handoffie.

### Key Entities

- **Ostrzeżenie FTN001**: wpis raportu konwersji (kod, strona, komunikat z etykietą) — definicja przypisu bez
  odnośnika.
- **Definicja przypisu (szkic)**: linia lub linie uznane przez parser za tekst przypisu z etykietą; źródło
  ostrzeżenia, gdy żaden odnośnik do niej nie prowadzi.
- **Odnośnik przypisu**: znak w tekście (podniesiona cyfra, etykieta „N)”) wskazujący definicję.
- **Replika strony**: syntetyczny PDF w testach odtwarzający układ strony aktu bez jego treści.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-120**: 5 z 5 ostrzeżeń aktu i każde inne ostrzeżenie FTN001 nowe względem 1.2.3 (FR-800a) ma w research.md
  przyczynę i klasyfikację.
- **SC-121**: Akt `ustawa-o-sluzbie-cywilnej` i D20081505Lj: 11 ostrzeżeń FTN001 w 1.3.1 (5 + 6 dotąd maskowanych);
  0 definicji powiązanych wyłącznie z własną etykietą w całym zestawie dokumentów; bramka FR-810 przechodzi na 1.3.1 i
  nie przechodzi na 1.3.0.
- **SC-122**: Markdown i chunki aktu D20081505Lj i wszystkich dokumentów testowych są identyczne z 1.3.0.
- **SC-123**: 0 zmian goldenów parsera i chunków, 0 nowych ostrzeżeń i 0 różnic Markdownu w prywatnym korpusie;
  bramka kontroli liczb przechodzi.
- **SC-124**: Wszystkie testy przypisów spec 010 przechodzą bez zmian oczekiwań; weryfikacja korpusu syntetycznego
  przechodzi.
- **SC-125**: Na każdym porównanym dokumencie poza aktem o służbie cywilnej liczba FTN001 w 1.3.1 jest równa
  liczbie z 1.3.0; każde ostrzeżenie nowe względem 1.2.3 ma klasę przyczyny w research.md i opis w README.

## Assumptions

- Prywatny PDF aktu i pliki porównawcze w `publish/porownanie-1.3.0/` są dostępne lokalnie; diagnoza korzysta z
  nich, ale do repozytorium trafiają tylko syntetyczne repliki układu.
- Ostrzeżenie FTN001 zasadne jest akceptowalne jako znane ograniczenie (decyzja właściciela w opisie i w sesji
  planu), więc liczba ostrzeżeń może wzrosnąć tam, gdzie samoodnośnik je maskował.
- Prywatny D20081505Lj nie ma goldena; jego Markdown i chunki porównuje się z 1.3.0 przez pliki w
  `publish/porownanie-1.3.0/`; publiczny odpowiednik ma golden w `Corpus/acts`.
- Do porównania 1.2.3/1.3.0 dostępny jest parser 1.2.3 (tag lub commit z historii gita albo wcześniejsze wydanie);
  ostrzeżenia obecne już w 1.2.3 nie są przedmiotem tej poprawki, ale ich liczba nie może wzrosnąć.
- Handoff „Stan prac i przekazanie” w plan.md i tasks.md jak w poprzednich specyfikacjach.
