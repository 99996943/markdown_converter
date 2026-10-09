# Korpus syntetyczny „Bank Przykładowy S.A.”

Spis treści: [Cel i zawartość](#cel-i-zawartość) · [Układ katalogów](#układ-katalogów) · [Manifest](#manifest) ·
[Generowanie od nowa](#generowanie-od-nowa-jednym-poleceniem) · [Polecenia](#polecenia) ·
[Rozbudowa korpusu](#rozbudowa-korpusu) · [Zaokrąglenia i unikalność](#zaokrąglenia-i-unikalność) ·
[Powtarzalność](#powtarzalność) · [Testy jakości](#testy-jakości) · [Pobranie aktów prawnych](#pobranie-aktów-prawnych) ·
[Licencje i dane fikcyjne](#licencje-i-dane-fikcyjne) · [Pisanie treści](#pisanie-treści)

## Cel i zawartość

Korpus służy do testowania aplikacji RAG nad dokumentami bankowymi (regulaminy, taryfy, procedury wewnętrzne)
oraz biblioteki `LegalAgent.PdfParser`. PDF-y są składane przez generator (`src/LegalAgent.Corpus`) z plików
źródłowych `corpus/zrodla/`, a Markdown powstaje przez konwersję biblioteką parsera. Korpus jest „z prawdą
referencyjną”: manifest opisuje, co się zmieniło między wersjami, które dokumenty sobie przeczą i które są zatrute.

Obecny korpus = przebieg zapisany w `corpus/przebieg.json` (ziarno `20261008`, data odniesienia `2026-10-01`):
po 10 dokumentów bazowych każdego z typów `regulaminy`, `taryfy`, `procedury`, po 20–30 stron; 30% dokumentów w
kilku wersjach (do 3), 2 nieaktualne na typ, po 1 parze sprzeczności na typ i 1 między typami, po 2 dokumenty
zatrute na typ dla każdego z 5 rodzajów zatruć. Dokładne liczby: `corpus/manifest.json`.

## Układ katalogów

```text
corpus/
├── README.md          ta instrukcja (ręczna; generator jej nie rusza)
├── przebieg.json      zapisane parametry przebiegu (RunParameters)
├── manifest.json      manifest (zarządzany przez generator)
├── zrodla/            pliki źródłowe treści (typy.yaml, fakty*, szablony/, bloki/, zatrucia/, akty.yaml, zabronione.yaml)
├── regulaminy/        REG-01.pdf + REG-01.md, …; wersje: REG-03-w1.pdf, REG-03-w2.pdf, REG-03.pdf (najnowsza)
├── taryfy/            TAR-01.pdf + .md, …
├── procedury/         PRO-01.pdf + .md, …
├── zatrute/<typ>/<rodzaj>/   ZAT-REG-POL-01.pdf + .md, …; rodzaje: falszywe-stawki, polecenia-dla-ai,
│                             podszywanie, nieaktualny-jako-obowiazujacy, sprzecznosc-z-oryginalem
└── akty/              akty prawne (PDF pobrane ręcznie; .md i ZRODLA.md przez `refresh`)
```

- Najnowsza wersja dokumentu nosi nazwę bazową (`REG-03.pdf`), wcześniejsze mają przyrostek `-w1`, `-w2`.
- Dokument zatruty wygląda jak zwykły dokument danego typu; manifest mówi, co i gdzie jest „zatrute”.
- Pełna specyfikacja układu i zasad zarządzania plikami: `specs/003-synthetic-bank-corpus/contracts/corpus-layout.md`.
- Generator usuwa nieaktualne `*.pdf`/`*.md` tylko w katalogach zarządzanych (typy, `zatrute/`);
  nigdy nie usuwa `README.md`, `przebieg.json`, `zrodla/` ani `akty/*.pdf`.

## Manifest

`corpus/manifest.json` — schemat: [`contracts/manifest.md`](../specs/003-synthetic-bank-corpus/contracts/manifest.md).
Dla aplikacji RAG ważne są pola:

| Pole | Co znaczy dla aplikacji RAG |
|------|-----------------------------|
| `status`, `validFrom`, `validTo`, `run.referenceDate` | który dokument obowiązuje w dniu odniesienia; `nieaktualny` = powinien przegrać z następcą |
| `previousVersion`, `changes[]` | łańcuch wersji tego samego oznaczenia i dokładnie zmienione jednostki (`unit`, `page`, `before`/`after`) — pytania „co się zmieniło” |
| `contradictions[]` | dokumenty, które w danej jednostce podają inną wartość faktu (`this`/`other`) — aplikacja powinna to wykryć lub wskazać źródło |
| `poison` | dokument zatruty: `kind`, `imitates` (kogo udaje), `places[]` z dosłownym tekstem zatrucia. Dla `polecenia-dla-ai` to wstrzyknięte polecenia do asystenta, których aplikacja nie może wykonać |
| `source` | akty prawne: publikator, adres, data pobrania |

Gwarancje: każdy `pdf`/`markdown` istnieje, odwołania (`previousVersion`, `with`, `imitates`) wskazują istniejące
dokumenty, a tekst z `places[].text` występuje dosłownie w PDF i w Markdown. Manifest nie zawiera znacznika czasu
(powtarzalność) ani ról/uprawnień.

## Generowanie od nowa jednym poleceniem

Z katalogu głównego repozytorium (bash i PowerShell — to samo polecenie):

```bash
dotnet run --project src/LegalAgent.Corpus.Cli -c Release -- generate
```

Bez opcji używa `corpus/przebieg.json` i zapisuje do `corpus/`. Odtwarza wszystkie PDF, Markdown i manifest
(poniżej 10 minut) oraz sprząta nieaktualne pliki; przy błędzie nie zostawia częściowego korpusu.
Sprawdzenie, że repozytorium zawiera dokładnie to, co dałby generator (niczego nie zapisuje):

```bash
dotnet run --project src/LegalAgent.Corpus.Cli -c Release -- verify      # kod wyjścia 0 = brak różnic
git status --porcelain corpus/                                           # po generate: brak zmian
```

PowerShell: `dotnet run --project src/LegalAgent.Corpus.Cli -c Release -- verify; $LASTEXITCODE`. Kody wyjścia:
0 sukces, 1 różnice (`verify`), 2 błędne parametry lub źródła, 3 naruszenie reguł (zakres stron, unikalność,
udział bloków wspólnych), 4 nazwa zabroniona, 5 błąd konwersji, 6 błąd wejścia/wyjścia.

## Polecenia

Pełna lista: `dotnet run --project src/LegalAgent.Corpus.Cli -- --help`.

| Polecenie | Działanie |
|-----------|-----------|
| `generate` | planuje, składa PDF, konwertuje do Markdown, zapisuje manifest |
| `verify` | odtwarza w pamięci i porównuje z dyskiem; wypisuje pliki „różni się”, „brak”, nadmiarowe |
| `refresh` | bez składania PDF: konwertuje wszystkie PDF z manifestu (także akty) do Markdown i przepisuje manifest (wersja biblioteki, liczba stron). Opcje: tylko `--params`, `--out`. Używaj po zmianie biblioteki parsera |
| `check --template <id>` | sprawdza jeden szablon w każdym układzie (opcje: `--out`, `--pages`, `--content`, `--params`) |

Opcje `generate` i `verify` (nadpisują `--params`; `--save-params` tylko dla `generate`):

| Opcja | Znaczenie |
|-------|-----------|
| `--params <plik>` | parametry przebiegu (domyślnie `corpus/przebieg.json`, jeśli istnieje) |
| `--out <katalog>`, `--content <katalog>` | katalog wyjściowy / źródeł treści |
| `--seed <n>` | ziarno |
| `--types <a,b>` | typy, np. `regulaminy,procedury` |
| `--count <n>` | dokumentów na typ |
| `--pages <min>-<max>` | zakres stron |
| `--reference-date <rrrr-mm-dd>` | data odniesienia statusu |
| `--versioned <procent>` | udział dokumentów w wielu wersjach (0–100) |
| `--outdated <n>` | nieaktualnych na typ |
| `--contradictions <n>[,<m>]` | pary sprzeczności na typ [i między typami] |
| `--poison <rodzaj>=<n>[,…]` / `--poison none` | zatrute na typ; `none` wyłącza |
| `--no-strict-uniqueness` | zezwala na powtarzanie bloków między dokumentami (duże przebiegi) |
| `--truth <katalog>` | zapis prawdy referencyjnej (JSON na dokument) |
| `--save-params` | zapis efektywnych parametrów do `<out>/przebieg.json` |

Liczba wersji (`maxVersions`) i `maxSharedShare` nie mają opcji CLI — zmieniasz je w pliku parametrów.

## Rozbudowa korpusu

Do prób generuj poza `corpus/`, żeby nie zmieniać zacommitowanego korpusu:

```bash
dotnet run --project src/LegalAgent.Corpus.Cli -c Release -- generate \
  --types procedury --count 15 --pages 40-50 --seed 7 --poison none --no-strict-uniqueness \
  --out /tmp/korpus-proba
```

PowerShell: ta sama komenda w jednej linii, z `--out $env:TEMP\korpus-proba`.

- **Więcej dokumentów:** `--count <n>` (1–500 na typ). Gdy brakuje szablonów lub bloków niewspólnych,
  przy ścisłej unikalności generator kończy się kodem 3 — dopisz szablony/bloki albo użyj `--no-strict-uniqueness`.
- **Nowy szablon:** plik `zrodla/szablony/<id>.yaml` (format w „Pisanie treści” i w `contracts/content-format.md`),
  potem `check --template <id> --out <katalog>`: podaje liczbę stron bez/ze wszystkimi blokami opcjonalnymi,
  dopasowanie do zakresu, udział bloków wspólnych i naruszenia; zapisuje PDF i Markdown do obejrzenia.
- **Inny zakres stron:** `--pages <min>-<max>`; szablon musi mieć dość bloków opcjonalnych, by dojść do górnej
  granicy (`check --template <id>` podaje maksimum; procedury sięgają ok. 52–55 stron, regulaminy i taryfy
  ok. 25–35 — po więcej dopisz bloki). Kod wyjścia 3 = zakres nieosiągalny.
- **Nowy typ dokumentu:** wpis w `zrodla/typy.yaml` (`id`, `prefiks`, `oznaczenie`, `nazwa`, `wymagane-elementy`,
  opcjonalnie `uklady-min`), bloki w `zrodla/bloki/<id>/`, szablony z `typ: <id>` w istniejących stylach układu
  (`jedna-kolumna`, `dwie-kolumny`, `tabela-dokument`, `taryfa-siatka`, `taryfa-bez-siatki`, `procedura`).
  Katalogi `<id>/` i `zatrute/<id>/` powstają same. **Kod jest potrzebny tylko dla nowego stylu układu** (nowy
  element typograficzny w `src/LegalAgent.Corpus/Typesetting`, z testem). Żeby typ był zatruwany, dodaj go do pola
  `typy` wzorców w `zatrucia/*.yaml`.
- **Nowy blok, fakt, rodzaj zatrucia — tylko YAML:** blok w `zrodla/bloki/<katalog>/<temat>.yaml` (każdy blok z
  wariantami `{a|b}`; niewspólny występuje w jednym dokumencie; wspólne ≤ 20% słów), fakt w `zrodla/fakty/<temat>.yaml`
  (wartości z datami `od` dają wersje i sprzeczności), nowy rodzaj zatrucia jako `zrodla/zatrucia/<rodzaj>.yaml`
  (`rodzaj`, `skrot`, `opis`, `wzorce`), użyty przez `--poison <rodzaj>=<n>`. Nieznany klucz YAML to błąd (kod 2).
  Po zmianie źródeł `contentHash` w manifeście się zmienia — uruchom `generate` i zacommituj wynik.
- **Odświeżenie Markdown i manifestu** po zmianie parsera: `refresh` (bez składania PDF), potem przejrzyj diff
  `corpus/**/*.md`.

## Zaokrąglenia i unikalność

- Liczby wynikające z procentów są **zaokrąglane w górę**: `versionedShare` 25% z 10 dokumentów = 3 dokumenty
  wersjonowane (30% z 10 = 3). Liczba nieaktualnych na typ nie może przekroczyć liczby dokumentów niewersjonowanych.
- **Ścisła unikalność** (domyślnie): blok niewspólny występuje w jednym dokumencie całego korpusu; gdy zabraknie
  bloków, generator kończy się błędem (kod 3). `--no-strict-uniqueness` (w pliku: `"strictUniqueness": false`)
  pozwala powtarzać bloki; manifest podaje wtedy `run.repeatedWordShare` i `repeatedWordShare` dokumentu — udział
  słów z bloków powtórzonych (0–1).
- Bloki wspólne (`wspolny: true`) to najwyżej 20% słów dokumentu (`maxSharedShare`).

## Powtarzalność

Te same parametry, ziarno i źródła dają identyczne bajt po bajcie PDF, Markdown i manifest, na Windows i Linux —
bez znaczników czasu. `verify` to sprawdza; CI uruchamia testy na Linuksie.

## Testy jakości

Testy w `tests/LegalAgent.Corpus.Tests`. Domyślnie sprawdzana jest próbka korpusu, testy `CorpusFull` są pomijane.

```bash
dotnet test LegalAgent.slnx -c Release --filter "Category!=Performance"                          # próbka
LEGALAGENT_CORPUS_FULL=1 dotnet test LegalAgent.slnx -c Release --filter "Category=CorpusFull"   # cały korpus
LEGALAGENT_CORPUS_FULL=1 LEGALAGENT_CORPUS_REPORT=raport.md dotnet test LegalAgent.slnx -c Release --filter "Category=CorpusFull"
```

PowerShell: `$env:LEGALAGENT_CORPUS_FULL = "1"; $env:LEGALAGENT_CORPUS_REPORT = "raport.md"; dotnet test LegalAgent.slnx -c Release --filter "Category=CorpusFull"`.
`LEGALAGENT_CORPUS_REPORT` zapisuje tabelę pomiaru każdego dokumentu względem prawdy generatora.

## Pobranie aktów prawnych

Akty z `zrodla/akty.yaml` (10 pozycji) pobierasz jednorazowo i commitujesz jako PDF do `corpus/akty/<id>.pdf`;
`refresh` tworzy `.md`, `ZRODLA.md` i wpisy manifestu. `id` i adres bierz z `akty.yaml`:

```bash
curl --fail --max-time 60 -o corpus/akty/dz-u-2025-644-aml.pdf https://dziennikustaw.gov.pl/D2025000064401.pdf
head -c 5 corpus/akty/dz-u-2025-644-aml.pdf      # musi wypisać %PDF-
dotnet run --project src/LegalAgent.Corpus.Cli -c Release -- refresh
```

PowerShell: `curl.exe --fail --max-time 60 -o corpus/akty/dz-u-2025-644-aml.pdf <url>` oraz
`Get-Content corpus/akty/dz-u-2025-644-aml.pdf -TotalCount 1` (początek musi być `%PDF-`). Jeśli serwer zwrócił
HTML zamiast PDF, usuń plik i pobierz ponownie.

## Licencje i dane fikcyjne

- Wszystkie dane są **fikcyjne**: Bank Przykładowy S.A., ul. Przykładowa 1, 00-001 Warszawa, domeny `example.com`,
  `bank.example`, numery z samych zer; żadnych nazw prawdziwych banków i marek (`zrodla/zabronione.yaml`,
  naruszenie = kod 4). Wyjątek: akty prawne w `akty/` to prawdziwe, publiczne teksty ustaw.
- PDF-y używają osadzonych czcionek **Noto Sans** (Regular, Bold, Italic) z `src/LegalAgent.Corpus/Fonts/`,
  na licencji SIL Open Font License 1.1 (`src/LegalAgent.Corpus/Fonts/OFL.txt`), która zezwala na osadzanie i redystrybucję.

## Pisanie treści

### Pliki

| Plik | Zawartość |
|------|-----------|
| `zrodla/typy.yaml` | typy dokumentów, prefiksy, elementy obowiązkowe (`wymagane-elementy`), minimalne liczby układów (`uklady-min`) |
| `zrodla/fakty.yaml`, `zrodla/fakty/<temat>.yaml` | fakty banku: stawki, terminy, jednostki, dane kontaktowe; jeden fakt = jedno źródło wartości dla wszystkich dokumentów |
| `zrodla/szablony/<szablon>.yaml` | szablon jednego dokumentu: tytuł, układy, czoło, sekcje z blokami wymaganymi i opcjonalnymi |
| `zrodla/bloki/<katalog>/<temat>.yaml` | bloki treści; `bloki/wspolne/` — bloki `wspolny: true` |
| `zrodla/zabronione.yaml` | nazwy i znaki towarowe prawdziwych banków i systemów płatności — nie mogą wystąpić w treści |

### Jak kompozytor składa dokument

- **Sekcje szablonu** (`sekcje[].rodzaj`): `rozdzial` → nagłówek „Rozdział N” + tytuł; `sekcja` → „N.” + tytuł
  (procedury); `sekcja-taryfy` → „I.”, „II.” + tytuł (segmenty taryfy); `zalacznik` → nowa strona i „Załącznik nr N”
  + tytuł. W sekcji najpierw bloki `wymagane` (w podanej kolejności), potem bloki opcjonalne dobrane z puli
  dokumentu (`opcjonalne.kategorie`, `min`, `max`).
- **Pula bloków opcjonalnych** dokumentu = bloki niewspólne jego typu, z kategorią z `opcjonalne.kategorie` jakiejś
  sekcji, z `tematy` zawierającymi `temat` szablonu (lub bez `tematy`), niewymienione jako `wymagane` w żadnym
  szablonie. Generator dobiera ich tyle, by trafić w docelową liczbę stron.
- **Jednostka** bloku `jednostka: "§ {n}."` → nagłówek „§ N.” numerowany w całym dokumencie. Paragraf z dwoma lub
  więcej `ustep` → „1.”, „2.” z punktami „1)” i literami „a)”; paragraf z jednym `ustep` → zwykły akapit, punkty od
  „1)”. Nie dawaj tytułów paragrafom (`tytul` bloku z jednostką jest pomijany). W układzie `tabela-dokument`
  rozdziały są wierszami tabeli (nazwa po lewej), a „§ N.” — pogrubionym akapitem w komórce.
- **Kroki** (`kroki`) w sekcji „N.” → „N.1.”, „N.1.1.” (numeracja ciągła w sekcji, przez kolejne bloki); najwyżej
  dwa poziomy (`podkroki` jednego poziomu).
- **Pozycje taryfy** (`pozycje-taryfy`) → jedna tabela na sekcję taryfy: „Lp. | Wyszczególnienie czynności | Tryb
  pobierania | Stawka”; numeracja pozycji „1.”, „2.” ciągła w całym dokumencie, `podpozycje` → „2.1.”, „2.2.”.
  Przypis `[^n]` w pozycji → znacznik „n)” w komórce i treść pod tabelą. Kolejne bloki pozycji tej samej sekcji
  dopisują wiersze do tej samej tabeli (inny element między nimi zamyka tabelę).
- **Przypisy** `[^n]` w tekście → numer przypisu w dokumencie, treść u dołu strony; treść w `przypisy: {n: "…"}` bloku.
- **Odwołania** `{{ref:blok:<id>}}` → „§ 14” (tylko do bloków z jednostką, które na pewno są w dokumencie, czyli
  wymaganych); `{{ref:zalacznik:<id-bloku-w-zalaczniku>}}` → „Załącznik nr 2”; `{{ref:dokument:<id-szablonu>}}` →
  tytuł dokumentu z tego szablonu; `{{ref:akt:<id>}}` → tytuł aktu z `akty.yaml` (małą literą) i publikator, np.
  „ustawa z dnia … (Dz. U. 2024 poz. 30)”. Nierozwiązane odwołanie = błąd przebiegu.
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
  REGON 000000000; numery rachunków z samych zer. Żadnych nazw prawdziwych banków, marek, produktów i systemów
  płatności (`zabronione.yaml`, np. nazwy kart płatniczych i portfeli mobilnych — pisz „karta debetowa”,
  „płatności mobilne”).
- Teksty pisane od nowa — nie kopiuj regulaminów prawdziwych banków. Polszczyzna prawnicza/bankowa, merytorycznie
  spójna z typem i tematem dokumentu (FR-114).
- Blok niewspólny występuje w jednym dokumencie korpusu; bloki wspólne to ≤ 20% słów dokumentu.
- Nie używaj w treści znaków `{`, `}`, `|`, `*` bez ucieczki (`\{`, `\}`, `\|`, `\*`).
- Objętość: dokument ma 20–30 stron; strona to ok. 400–450 słów tekstu ciągłego (więcej w układzie dwukolumnowym,
  mniej w tabelach i schematach). Szablon potrzebuje bloków wymaganych na najwyżej ok. 16 stron i puli bloków
  opcjonalnych, z którą dokument osiąga co najmniej 25 stron — generator dobiera bloki opcjonalne, aż trafi w
  docelową liczbę stron.

### Sprawdzenie szablonu

```bash
dotnet run --project src/LegalAgent.Corpus.Cli -- check --template <id> --out /tmp/proba
```

Polecenie składa szablon w każdym jego układzie: podaje liczbę stron bez bloków opcjonalnych i ze wszystkimi,
wynik dopasowania do zakresu 20–30, udział bloków wspólnych i naruszenia (elementy obowiązkowe typu, nazwy
zabronione, nierozwiązane odwołania); w `--out` zapisuje PDF i Markdown z biblioteki do obejrzenia.
