# LegalAgent.Corpus

Deterministyczny generator **syntetycznego korpusu dokumentów polskiego banku** „Bank Przykładowy S.A.” (spec 003):
regulaminów, taryf i procedur wewnętrznych w PDF, razem z ich Markdown (z parsera), fragmentami dla RAG
(`*.chunks.jsonl`) i manifestem. Gotowy korpus jest zacommitowany w [`corpus/`](../../corpus/) — jego instrukcja dla
użytkownika to [`corpus/README.md`](../../corpus/README.md). Ten plik opisuje, **jak działa generator**.

Korpus służy do dwóch rzeczy:

- **testowania parsera** — każdy PDF ma „prawdę referencyjną” zapisaną podczas składu, więc jakość Markdown da się
  zmierzyć liczbowo;
- **testowania aplikacji RAG** — manifest opisuje wersje dokumentów, zmiany między nimi, sprzeczności i dokumenty
  zatrute (np. z wstrzykniętymi poleceniami dla AI).

Specyfikacja: [`specs/003-synthetic-bank-corpus/`](../../specs/003-synthetic-bank-corpus/).

## Użycie

Aplikacja konsolowa `src/LegalAgent.Corpus.Cli` (`legalagent-corpus`), uruchamiana z katalogu głównego repozytorium.
Bez opcji czyta parametry z `corpus/przebieg.json` i pisze do `corpus/`.

| Polecenie | Działanie |
|-----------|-----------|
| `generate` | planuje, składa PDF, konwertuje do Markdown, dzieli na fragmenty, zapisuje manifest; usuwa nieaktualne pliki |
| `refresh` | bez składania: konwertuje PDF-y z manifestu (także akty prawne) od nowa i przepisuje Markdown, fragmenty i manifest — **po zmianie parsera lub chunkingu** |
| `verify` | odtwarza cały korpus w pamięci i porównuje z dyskiem; nic nie zapisuje (kod 1 = różnice) — **krok CI** |
| `check --template <id>` | sprawdza jeden szablon w każdym dozwolonym układzie (zakres stron, naruszenia reguł) |

```bash
dotnet run --project src/LegalAgent.Corpus.Cli -c Release -- generate
dotnet run --project src/LegalAgent.Corpus.Cli -c Release -- refresh
dotnet run --project src/LegalAgent.Corpus.Cli -c Release -- verify
dotnet run --project src/LegalAgent.Corpus.Cli -c Release -- check --template <id>
```

Opcje `generate`/`verify` nadpisują parametry z pliku (`--seed`, `--types`, `--count`, `--pages`, `--versioned`,
`--outdated`, `--contradictions`, `--poison`, `--truth <katalog>` …; pełna lista: `--help`). Kody wyjścia: 0 sukces,
1 różnice (`verify`), 2 błędne parametry lub źródła, 3 naruszenie reguł korpusu, 4 nazwa zabroniona, 5 błąd
konwersji, 6 błąd wejścia/wyjścia.

API: `CorpusGenerator.GenerateAsync`, `RefreshAsync`, `VerifyAsync`, `CheckTemplateAsync` z `RunParameters`.

## Zasada działania

```text
corpus/zrodla/*.yaml ─► Content ─► Planning ─► Composition ─► Typesetting ─► PDF ─► Parser ─► Chunking
   (szablony, bloki,     (ładowanie)  (ziarno:      (elementy     (układ + zapis    (bajt w  (Markdown) (*.chunks.jsonl)
    fakty, zatrucia)                  wersje,       dokumentu)    PRAWDY)           bajt)
                                      sprzeczności,                                        │
                                      zatrucia)                         Validation ◄───────┘ ─► Manifest
```

### 1. Treść (`Content/`)

Źródła w `corpus/zrodla/` (YAML, po polsku):

- `typy.yaml` — typy dokumentów (`regulaminy`, `taryfy`, `procedury`), prefiksy, wzorce oznaczeń, angielskie nazwy
  typów dla metadanych fragmentów (`nazwa-en`);
- `szablony/` — szablony dokumentów: sekcje z blokami obowiązkowymi i kategoriami bloków opcjonalnych, dozwolone układy;
- `bloki/` — bloki treści z wariantami zdań (`{a|b}`) i parametrami;
- `fakty*.yaml`, `fakty/` — fakty (kwoty, stawki, terminy) z historią wartości i wartościami alternatywnymi;
- `zatrucia/` — wzorce dokumentów zatrutych; `zabronione.yaml` — nazwy, które nie mogą pojawić się w korpusie;
- `akty.yaml` — akty prawne dodane ręcznie jako PDF (tylko konwertowane, nie generowane).

### 2. Plan (`Planning/`)

`CorpusPlanner` z `RunParameters` i ziarna wybiera dokumenty bazowe każdego typu (szablon, układ, pulę bloków
opcjonalnych, docelową liczbę stron). `DocumentStates` dokłada **wersje** (wcześniejsze wersje dzielą z najnowszą
bloki opcjonalne i ich kolejność, więc numery paragrafów i klucze jednostek się zgadzają), **dokumenty nieaktualne**
i **pary sprzeczności** (dwa dokumenty podające inną wartość tego samego faktu). `PoisonPlanner` tworzy **dokumenty
zatrute**: kopię planu dokumentu, który udają (ten sam szablon, układ, oznaczenie, daty), z jednym wzorcem zatrucia
(`falszywe-stawki`, `polecenia-dla-ai`, `podszywanie`, `nieaktualny-jako-obowiazujacy`, `sprzecznosc-z-oryginalem`).

### 3. Kompozycja (`Composition/`)

`DocumentComposer` zamienia plan w drzewo elementów: renderuje sekcje szablonu z blokami obowiązkowymi i wybraną
liczbą opcjonalnych, numeruje jednostki (`§ N.`, pozycje taryfy, kroki procedury, załączniki), rozwiązuje odsyłacze i
przypisy, wypełnia metryczkę i okładkę. Zapisuje też, gdzie użyto każdego faktu i gdzie jest zatrucie (do manifestu).

### 4. Skład i prawda (`Typesetting/`, `Truth/`)

`Typesetter` układa elementy na stronach przez `PageWriter` według **stylu układu** (`LayoutStyles`):

| Układ | Charakter |
|-------|-----------|
| `jedna-kolumna` | klasyczny regulamin |
| `dwie-kolumny` | tekst w dwóch kolumnach |
| `tabela-dokument` | cały dokument jako tabela sekcja–treść (spec 002) |
| `taryfa-siatka`, `taryfa-bez-siatki` | taryfy opłat z liniami siatki i bez nich |
| `procedura` | procedura z krokami, schematami i listami kontrolnymi |

Podczas składu zapisywana jest **prawda referencyjna** (`DocumentTruth`): słowa treści w kolejności czytania,
nagłówki z poziomami, punkty list z etykietami i głębokością, tabele (po scaleniu stron, bez powtórzonych nagłówków),
osobno nagłówki/stopki/numery stron (artefakty) i dosłowne teksty zatruć. Skład jest dwuprzebiegowy (pierwszy liczy
strony do stopki „Strona n z N”). `PageFitter` dobiera liczbę bloków opcjonalnych tak, by dokument trafił w zakres
stron (maks. 8 rund skład–pomiar).

### 5. PDF (`Pdf/`)

`SyntheticPdfBuilder` zapisuje PDF z osadzonymi fontami Noto Sans (polskie znaki). `PdfIdNormalizer` zastępuje
losowy `/ID` w trailerze skrótem SHA-256 pliku, więc **ten sam plan daje ten sam PDF bajt w bajt**.

### 6. Markdown i fragmenty

Każdy PDF jest konwertowany parserem (`LegalAgent.PdfParser`, opcje z `parserOptions` w parametrach przebiegu), a
wynik dzielony przez `LegalAgent.Chunking` na `<id>.chunks.jsonl`. Metadane fragmentów pochodzą z wpisu manifestu;
typ i status są tłumaczone na angielski (`regulation`, `in-force`/`outdated`), manifest zachowuje polskie wartości.

### 7. Walidacja (`Validation/CorpusChecks`)

Przed zapisem generator sprawdza reguły korpusu: brak nazw zabronionych (bez względu na wielkość liter i znaki
diakrytyczne — m.in. nazwy prawdziwych banków), unikalność bloków między dokumentami, udział słów z bloków wspólnych,
rozwiązane odsyłacze, obowiązkowe elementy szablonów. Naruszenie przerywa przebieg (kody 3–4) i nie zostawia
częściowego korpusu.

### 8. Manifest (`Manifest/`)

`ManifestWriter` zapisuje `corpus/manifest.json`: parametry przebiegu, wersje parsera i generatora, skrót treści
źródłowej i dla każdego dokumentu — pliki, oznaczenie, wersję, daty, status, poprzednią wersję, zmiany między
wersjami (jednostka, strona, przed/po), sprzeczności i opis zatrucia. Bez znacznika czasu (powtarzalność).

## Determinizm

Każda decyzja losowa pochodzi z `DeterministicRandom` (SplitMix64) ze strumieni wyprowadzanych z (ziarno, cel, id)
skrótem FNV-1a — wynik nie zależy od wersji .NET, systemu ani kolejności równoległego przetwarzania. Razem z
`PdfIdNormalizer` i stałym formatem Markdown/JSON daje to korpus odtwarzalny bajt w bajt, co sprawdza `verify` w CI:
jeśli zacommitowany `corpus/` różni się od odtworzonego, build nie przechodzi.

## Po co prawda referencyjna

Testy porównują Markdown parsera z prawdą zapisaną przy składzie (`tests/LegalAgent.Corpus.Tests/Corpus/QualityMetrics.cs`):

| Miara | Próg (pełny korpus) |
|-------|---------------------|
| kompletność słów | ≥ 99,5% |
| odnalezione nagłówki | ≥ 98% |
| fałszywe nagłówki | ≤ 1% |
| odnalezione punkty list | ≥ 98% |
| zgodność komórek tabel | ≥ 98% |

Mierzone są też kolejność czytania, nadmiarowe słowa i całość wierszy tabel. Zwykły `dotnet test` sprawdza stałą
próbkę dokumentów; `LEGALAGENT_CORPUS_FULL=1` uruchamia miary na całym korpusie i sprawdza aktualność Markdown, a
`LEGALAGENT_CORPUS_REPORT` zapisuje tabele pomiarów. `--truth <katalog>` zapisuje prawdę każdego dokumentu jako JSON.

## Zasady przy zmianach

- **Zmiana składu, która zmienia to, co jest drukowane, musi zaktualizować zapis prawdy** — inaczej miary zaczną
  zgłaszać różnice, których w PDF nie ma.
- Po zmianie generatora uruchom `generate`, po zmianie parsera lub chunkingu — `refresh`; zacommituj zmienione pliki
  `corpus/` (CI uruchamia `verify`).
- W korpusie nie mogą występować nazwy prawdziwych banków — pilnuje tego `zabronione.yaml` i `CorpusChecks.ForbiddenNames`.
- Do eksperymentów generuj poza `corpus/` (`--out <katalog>`), żeby nie zmieniać zacommitowanego korpusu.
