# Korpus syntetyczny „Bank Przykładowy S.A.”

Szkic — pełna instrukcja (generowanie, `verify`, `refresh`, rozbudowa) powstaje w zadaniu T117. Poniżej
zasady pisania treści w `corpus/zrodla/` (format plików: `specs/003-synthetic-bank-corpus/contracts/content-format.md`).

## Pisanie treści

### Pliki

| Plik | Zawartość |
|------|-----------|
| `zrodla/typy.yaml` | typy dokumentów, prefiksy, elementy obowiązkowe (`wymagane-elementy`), minimalne liczby układów (`uklady-min`) |
| `zrodla/fakty.yaml`, `zrodla/fakty/<temat>.yaml` | fakty banku: stawki, terminy, jednostki, dane kontaktowe; jeden fakt = jedno źródło wartości dla wszystkich dokumentów |
| `zrodla/szablony/<szablon>.yaml` | szablon jednego dokumentu: tytuł, układy, czoło, sekcje z blokami wymaganymi i opcjonalnymi |
| `zrodla/bloki/<katalog>/<temat>.yaml` | bloki treści; `bloki/wspolne/` — bloki `wspolny: true` |
| `zrodla/zabronione.yaml` | nazwy i znaki towarowe prawdziwych banków — nie mogą wystąpić w treści |

### Jak kompozytor składa dokument

- **Sekcje szablonu** (`sekcje[].rodzaj`): `rozdzial` → nagłówek „Rozdział N” + tytuł; `sekcja` → „N.” + tytuł
  (procedury); `sekcja-taryfy` → „I.”, „II.” + tytuł (segmenty taryfy); `zalacznik` → nowa strona i „Załącznik nr N”
  + tytuł. W sekcji najpierw bloki `wymagane` (w podanej kolejności), potem bloki opcjonalne dobrane z puli
  dokumentu (`opcjonalne.kategorie`, `min`, `max`).
- **Jednostka** bloku `jednostka: "§ {n}."` → nagłówek „§ N.” numerowany w całym dokumencie. Paragraf z dwoma lub
  więcej `ustep` → „1.”, „2.” z punktami „1)” i literami „a)”; paragraf z jednym `ustep` → zwykły akapit, punkty od
  „1)”. Nie dawaj tytułów paragrafom (`tytul` bloku z jednostką jest pomijany).
- **Kroki** (`kroki`) w sekcji „N.” → „N.1.”, „N.1.1.” (numeracja ciągła w sekcji, przez kolejne bloki); maksymalnie
  dwa poziomy (`podkroki` jednego poziomu).
- **Pozycje taryfy** (`pozycje-taryfy`) → jedna tabela na sekcję taryfy: „Lp. | Wyszczególnienie czynności | Tryb
  pobierania | Stawka”; numeracja pozycji „1.”, „2.” ciągła w całym dokumencie, `podpozycje` → „2.1.”, „2.2.”.
  Przypis `[^n]` w pozycji → znacznik „n)” w komórce i treść pod tabelą.
- **Przypisy** `[^n]` w tekście → numer przypisu w dokumencie, treść u dołu strony; treść w `przypisy: {n: "…"}` bloku.
- **Odwołania** `{{ref:blok:<id>}}` → „§ 14” (tylko do bloków z jednostką, które na pewno są w dokumencie, czyli
  wymaganych); `{{ref:zalacznik:<id-bloku-w-zalaczniku>}}` → „Załącznik nr 2”; `{{ref:dokument:<id-szablonu>}}` →
  tytuł dokumentu z tego szablonu; `{{ref:akt:<id>}}` → tytuł aktu z `akty.yaml` i publikator. Nierozwiązane
  odwołanie = błąd przebiegu.
- **Parametry** `{{param:…}}`: `bank`, `oznaczenie`, `wersja`, `od`, `do`, `produkt`, `jednostka`/`wlasciciel`
  (z `parametry` szablonu) i każdy inny klucz z `parametry` szablonu.
- **Fakty** `{{fakt:<id>}}` → wartość obowiązująca w dniu początku obowiązywania dokumentu, w formacie rodzaju:
  `kwota` 25 → „25,00 zł”, `procent` 1.5 → „1,5%”, `termin` 14 → „14 dni”, `data` → „1 stycznia 2027 r.”, `tekst`
  dosłownie. Każda stawka, termin, limit, nazwa jednostki, adres, telefon = fakt (spójność dokumentów, wersje i
  sprzeczności w manifeście powstają z faktów).
- **Warianty** `{a|b|c}` — każdy blok MUSI mieć warianty brzmienia (FR-103b): co najmniej kilka grup w bloku, w
  różnych miejscach zdań. Warianty muszą być merytorycznie równoważne.
- Elementy specjalne: `schemat` (2–6 kroków z nazwą ≤ 4 słowa i wyjaśnieniem), `lista-kontrolna`
  (`forma: wektor|tekst|tabela`), `ramka`, `metryczka`, `tabela`, `naglowek`.

### Zasady treści

- Wyłącznie fikcyjny **Bank Przykładowy S.A.** Adresy: ul. Przykładowa 1, 00-001 Warszawa; domeny `example.com`,
  `bank.example`, `przyklad.invalid`; telefony 800 000 000 – 800 000 099; KRS 0000000000, NIP 000-000-00-00,
  REGON 000000000; numery rachunków z samych zer. Żadnych nazw prawdziwych banków, marek ani produktów
  (`zabronione.yaml`).
- Teksty pisane od nowa — nie kopiuj regulaminów prawdziwych banków. Polszczyzna prawnicza/bankowa, merytorycznie
  spójna z typem i tematem dokumentu (FR-114).
- Blok niewspólny występuje w jednym dokumencie korpusu; bloki wspólne to ≤ 20% słów dokumentu.
- Nie używaj w treści znaków `{`, `}`, `|`, `*` bez ucieczki (`\{`, `\}`, `\|`, `\*`).
- Objętość: dokument ma 20–30 stron; strona to ok. 400–450 słów tekstu ciągłego (mniej w tabelach i schematach).
  Szablon potrzebuje bloków wymaganych na ok. 15 stron i puli bloków opcjonalnych (kategorie z `opcjonalne`) na
  kolejne ≥ 20 stron — generator dobiera bloki opcjonalne, aż trafi w docelową liczbę stron.
