# Kontrakt: kontrola liczb w prywatnym korpusie (spec 009, US2)

Interfejs testów parsera dla właściciela; kontrakt Markdown biblioteki (1.3.0) się nie zmienia.

## Plik `number-check.txt`

Położenie: katalog `LEGALAGENT_PRIVATE_CORPUS` (obok PDF-ów, jak `layout-labels.txt`); nieśledzony w git.

```text
# Regulaminy z zestawów FAQ (spec 009, FR-658): nazwa[;dopuszczone]
mbank-corp-1
mbank-corp-5
mbank-corp-2;0,1,2      # przykład wyjątku — tylko gdy US3 wycofano (FR-661); uzasadnienie w komentarzu
```

- Jeden dokument w wierszu: nazwa pliku PDF bez rozszerzenia; puste wiersze i tekst po `#` ignorowane.
- Opcjonalne pole po `;`: lista liczb (rozdzielona przecinkami) dopuszczonych jako nadmiarowe — każde wystąpienie na
  liście dopuszcza jedno nadmiarowe (`0,0` = „0” dwa razy); używane wyłącznie dla udokumentowanego ograniczenia.
- Początkowa zawartość: 15 regulaminów z zestawów FAQ — corp-1, 2, 3, 5, 6, ind-1…5, firm-2…6; bez `mbank-firm-1`,
  `mbank-corp-4`, reg1–3 i regulamin-pdp (te są tylko raportowane).
- Brak pliku → test pomijany (`Assert.SkipUnless`); dokument z listy bez PDF → błąd testu.

## Test `PrivateCorpusTests.RegulationNumbers_HaveNoExcess`

Logika (parsowanie listy, odejmowanie dopuszczonych, komunikaty) jest w czystej funkcji `RegulationGate.Evaluate`
w `Fixtures/NumberCheck.cs`, testowanej offline w `NumberCheckTests` (SC-102); test prywatny tylko ją wywołuje.

- Dla każdego dokumentu z listy: `NumberDiff.Excess` minus dopuszczone; niepuste → porażka.
- Komunikat: jeden wiersz na dokument — `mbank-corp-5: nadmiarowe 661×1, 682×1`.
- Liczby brakujące nie powodują porażki.
- Kategoria testu: bez kategorii (uruchamiany z resztą prywatnego korpusu).

## Raport (`LEGALAGENT_CORPUS_REPORT`)

- Tabela miar: dwie nowe kolumny `Liczby +` (suma `Excess`) i `Liczby −` (suma `Missing`) dla każdego dokumentu.
- Sekcja `## Liczby nadmiarowe i brakujące`: dla dokumentów z niezerową miarą — wartości z krotnościami
  (`+ 661×1, 682×1; − 66×2, 68×1`).
- Sekcja zbiorów słów porównuje z `baseline-009/*.expected.md`.
