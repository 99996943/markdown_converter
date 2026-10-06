# Konwerter Regulaminów Constitution

## Core Principles

### I. Test-First / TDD (NON-NEGOTIABLE)
Projekt jest rozwijany w schemacie TDD (Red-Green-Refactor). Test MUSI zostać napisany przed
kodem produkcyjnym i MUSI najpierw zakończyć się niepowodzeniem; dopiero potem pisze się
minimalny kod, który go spełnia, a następnie refaktoryzuje. Każda zmiana zachowania MUSI być
pokryta testami automatycznymi. Testy MUSZĄ działać offline i deterministycznie (przykładowe
pliki, atrapy sieci i usług zewnętrznych) oraz MUSZĄ przechodzić przed scaleniem zmiany. Kod
MUSI być podzielony na małe jednostki, które można testować niezależnie.
Uzasadnienie: testy napisane przed kodem wymuszają jasne wymagania i pozwalają wiarygodnie
sprawdzić pozostałe zasady.

### II. Wierność źródłu
Treści generowane z dokumentów źródłowych MUSZĄ opierać się wyłącznie na tych dokumentach i
wskazywać swoje źródło. Niedozwolone jest dodawanie informacji spoza źródła ani zgadywanie; gdy
źródło nie rozstrzyga sprawy, wynik MUSI to jawnie stwierdzać.
Uzasadnienie: dokumenty prawne i bankowe nie tolerują błędnych lub zmyślonych informacji.

### III. Powtarzalność
Ten sam kod uruchomiony na tych samych danych wejściowych MUSI dawać ten sam wynik, a ponowne
uruchomienie nie może uszkadzać ani dublować wcześniejszych wyników. Uruchomienie MUSI
wymagać jednego polecenia, bez ręcznych kroków.
Uzasadnienie: pozwala śledzić zmiany przez diff w git i ufać wynikom.

### IV. Odporność na błędy zewnętrzne
Każde wywołanie zewnętrzne (sieć, system plików, usługi) MUSI mieć jawne timeouty i obsługę
błędów. Błędy MUSZĄ być zgłaszane czytelnym komunikatem i niezerowym kodem wyjścia; niepełny
wynik nie może być po cichu uznany za poprawny.
Uzasadnienie: przewidywalne zachowanie przy awariach i brak ukrytych, błędnych danych.

### V. Bezpieczeństwo i konfiguracja
W repozytorium NIE WOLNO przechowywać sekretów (klucze, tokeny, hasła); wrażliwa konfiguracja
MUSI pochodzić ze zmiennych środowiskowych, a repozytorium MOŻE zawierać jedynie plik
`.env.example`. Dane zmieniające się (np. adresy źródeł) MUSZĄ być zapisane w konfiguracji, a nie
w kodzie.
Uzasadnienie: ochrona poufnych danych i łatwa zmiana źródeł bez edycji logiki.

### VI. Prostota i minimalne zależności
Rozwiązanie MUSI być najprostsze, które spełnia wymagania (YAGNI). Każda nowa zależność wymaga
uzasadnienia w opisie zmiany i MUSI mieć przypiętą wersję.
Uzasadnienie: mniej zależności to łatwiejsze uruchomienie, audyt i utrzymanie.

### VII. Dokumentacja
Repozytorium MUSI zawierać aktualne README: cel projektu, instalację, konfigurację, uruchomienie
i uruchamianie testów. Zmiana, która wpływa na sposób użycia, MUSI aktualizować README.
Uzasadnienie: projekt musi być możliwy do uruchomienia przez nową osobę bez pomocy autora.

## Ograniczenia techniczne

### Platforma
- Projekt MUSI być napisany w C# na .NET 9.
- Program MUSI się budować, uruchamiać i testować na Linuxie oraz NIE MOŻE zależeć od API
  specyficznych dla Windows.

### Architektura: biblioteka i aplikacja
- Rozwiązanie MUSI składać się z osobnej biblioteki klas, zawierającej całą logikę i
  przeznaczonej do ponownego użycia w innych projektach, oraz osobnej aplikacji wykonawczej
  (konsolowej), która jest cienką warstwą nad biblioteką (argumenty, konfiguracja, kody wyjścia).
- Biblioteka MUSI być ogólna: przyjmuje dowolny plik PDF i NIE MOŻE zawierać zaszytych źródeł
  (np. adresów mBanku); lista źródeł należy wyłącznie do konfiguracji aplikacji.
- Biblioteka MUSI zwracać ustrukturyzowany model dokumentu (sekcje z numeracją i metadanymi
  źródła), z którego można wyrenderować Markdown i podzielić treść na fragmenty. Ładowanie do baz
  wektorowych jest poza zakresem projektu.
- Biblioteka NIE MOŻE zależeć od aplikacji, konsoli ani globalnego stanu. Jej publiczne API MUSI
  być jawne i udokumentowane, a zmiany łamiące wymagają podniesienia wersji MAJOR.

### Struktura rozwiązania
- Kod MUSI być zorganizowany w jednej solucji w formacie `.slnx` (nowy format XML); pliki `.sln`
  NIE są dozwolone.
- Solucja MUSI zawierać wszystkie projekty: bibliotekę, aplikację wykonawczą i projekt testowy.
- Budowanie i testy MUSZĄ działać z poziomu solucji na Linuxie: `dotnet build` i `dotnet test`
  uruchamiane na pliku `.slnx`.

### Testy
- Biblioteka MUSI mieć osobny projekt testowy uruchamiany poleceniem `dotnet test` na Linuxie.

### Format wiedzy
- Plik FAQ MUSI być zapisywany w Open Knowledge Format (OKF v0.1): katalog plików Markdown z
  nagłówkiem YAML, w którym wymagane jest pole `type`, a zalecane są `title`, `description`,
  `resource` (URL źródła) i `timestamp`. Specyfikacja:
  https://github.com/GoogleCloudPlatform/knowledge-catalog/tree/main/okf
- Szczegółowy układ plików i pól FAQ określa specyfikacja funkcjonalna, nie konstytucja.

## Przepływ pracy

- Zmiany przechodzą przez Spec Kit: specyfikacja → plan → zadania → implementacja.
- Wymagania konkretnej funkcjonalności opisuje specyfikacja, nie konstytucja.
- Zadania implementacyjne MUSZĄ być porządkowane tak, by test poprzedzał kod produkcyjny (TDD).
- Przegląd zmiany MUSI sprawdzić zgodność z zasadami I–VII oraz z ograniczeniami technicznymi;
  odstępstwa wymagają pisemnego uzasadnienia.

## Governance

Konstytucja ma pierwszeństwo nad innymi praktykami w projekcie. Zmiana wymaga opisu i
uzasadnienia oraz aktualizacji numeru wersji. Wersjonowanie semantyczne: MAJOR — usunięcie lub
niezgodna redefinicja zasady; MINOR — dodanie zasady/sekcji lub istotne rozszerzenie wytycznych;
PATCH — doprecyzowania i poprawki redakcyjne. Zgodność z konstytucją MUSI być weryfikowana przy
każdym przeglądzie zmian.

**Version**: 1.3.0 | **Ratified**: 2026-10-06 | **Last Amended**: 2026-10-06
