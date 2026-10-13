# Research: Oryginalne etykiety przypisów w Markdownie

## R1. Etykiety przypisów w korpusie (stan 1.3.1)

Pomiar tymczasową sondą po etapie `FootnoteDetection` (etykiety `FootnoteDraft.Label`, 40 dokumentów: `Corpus/acts`,
prywatny korpus, `publish/mBank.FaqGenerator-win-x64/downloads` z 2026-10-13, dwa PDF-y z katalogu głównego,
`Corpus/banking`).

| Grupa | Etykiety | Powtórzenia w dokumencie |
|---|---|---|
| prawa-konsumenta, kredyt-konsumencki, uslugi-platnicze, ustawa-o-sluzbie-cywilnej (i D20081505Lj) | „1)”…„N)” | brak |
| prawo-bankowe | „1)”, „1)”…„7)”, „2)”…„19)” | „1)”–„7)” ×2 (dwie serie: tekst jednolity i obwieszczenie) |
| obwieszczenie-msz | „1)”, „1)”…„4)”, „*)”, „**)”, „***)” ×2 | „1)”, „*)”, „**)”, „***)” ×2 |
| regulaminy (corp, firm, ind, downloads) | „1”, „2”… (bez nawiasu; luki tam, gdzie definicji nie wykryto) | brak |
| reg1, regulamin-pdp (i „Regulamin podstawowego…”) | „1”…„6”, „1)”, „2)”, „7”…„12” | brak („1)” ≠ „1”); „1)”, „2)” to punkty listy w przypisie (spec 011 K3) |
| `Corpus/banking/regulamin-rachunku` | jeden przypis z pustą etykietą (odnośnik „¹” pełnej wielkości) | — |

**Wniosek**: dopisek `-2` (FR-903) pojawi się tylko w 2 aktach publicznych, identyfikator zastępczy (FR-904) tylko w
jednym dokumencie syntetycznym. Regulaminy bankowe dostaną czyste etykiety z PDF-u.

## R2. Dlaczego numeracja „przeskakuje”

`DocumentBuildStage.NumberFootnotes` numeruje w dwóch przejściach: (1) przypisy, do których prowadzi rozpoznany
odnośnik, w kolejności czytania (akapity, listy z zagnieżdżeniami, tabele — `InlinesOf` obejmuje wszystkie trzy);
(2) przypisy-sieroty w kolejności stron, przypięte do najgłębszej sekcji otwartej na ich stronie.

W regulaminie kredytu gotówkowego (`downloads`, 25 przypisów) rozpoznane są odnośniki do 8 przypisów
(17 × FTN001). Odnośniki „BOK13.”, „umowy16,”, „tabeli20,” mają za podniesioną liczbą pełnowymiarową interpunkcję —
to klasa K1/K2 ze spec 011 (znane ograniczenie, README 1.3.1). Numery 1–6 dostały więc odnośniki z list (s. 13, 15)
i tabeli (s. 21), a 7–25 sieroty po kolei stron. Hipoteza „tabele numerowane przed tekstem” jest fałszywa.

**Decyzja**: kolejność definicji zostaje regułą (FR-905; dzisiejszy kod ją spełnia), a przeskakiwanie znika, bo
identyfikatory to etykiety z PDF-u. Rozpoznawanie odnośników z interpunkcją zostaje osobnym kandydatem (największy
zysk jakości: w tym dokumencie 17 z 25 przypisów jest bez odnośnika) — poza zakresem spec 012.

## R3. Znaki dozwolone w identyfikatorze przypisu GFM

Specyfikacja GFM (cmark-gfm, rozszerzenie footnotes) parsuje `[^label]` regułami etykiety odnośnika: dowolne znaki
poza nieucieczonymi `[` `]`, bez samych białych znaków, do 999 znaków; GitHub dodatkowo odrzuca spacje i tabulatory.
Etykiety z korpusu („21”, „1)”, „*)”, „**)”, „***)”, z dopiskiem „1)-2”) spełniają te reguły. Etykieta jest parsowana
przed wyróżnieniami, więc `*` w `[^*)]` nie otwiera kursywy.

**Decyzja**: identyfikator = etykieta dosłownie; parser odrzuca (zastępuje identyfikatorem zastępczym, FR-904)
etykietę pustą, ze spacją, tabulatorem lub nawiasem kwadratowym. **Weryfikacja**: zadanie w tasks — podgląd
Markdownu z `[^1)]`, `[^*)]`, `[^1)-2]` na GitHubie (gist/PR) i w VS Code przed commitem green.

**Alternatywy**: rdzeń etykiety (`[^1]`) — odrzucone przez właściciela (P1); ucieczki znaków (`[^\*)]`) — zbędne
według R3.

## R4. Gdzie powstaje identyfikator (model i API)

Konsumenci numerów: `MarkdownRenderer` (`[^{Number}]`, `[^{FootnoteNumber}]`), chunker (`FootnoteSelector`,
`UnitSplitter` — wybór i kolejność definicji po `Number`), `NumberCheck` w testach. Fragment chunka renderuje tylko
część przypisów, więc dopisku `-2` nie da się policzyć w rendererze — identyfikator musi być w modelu.

**Decyzja**: `DocumentBuildStage` wylicza identyfikator raz dla dokumentu i zapisuje go w nowych właściwościach
`init`: `Footnote.Identifier` (identyfikator Markdownu; `OriginalLabel` zostaje etykietą z PDF-u) i `FootnoteRef.Identifier`. `Number` zostaje (kolejność, wybór w
chunkerze, zgodność API). Renderer pisze `Identifier`, a gdy jest pusty (model zbudowany ręcznie przez klienta biblioteki) —
`Number`. Zmiana addytywna: publiczne API zgodne wstecz → biblioteka **1.4.0** (MINOR).

**Alternatywy**: zmiana `FootnoteRef(int)` na `FootnoteRef(string)` — łamie API (MAJOR), odrzucone; mapa
numer→identyfikator w rendererze — nie działa dla fragmentów chunkera.

## R5. Kolejność wystąpień dla dopisku (FR-903)

Kolejność = definicje w kolejności stron, potem identyfikatora szkicu (ta sama, w której dziś powstają szkice).
W prawo-bankowe pierwsza seria „1)”–„7)” (tekst jednolity) zostaje bez dopisku, druga (obwieszczenie) dostaje „-2”.
Gdy etykieta z dopiskiem („1)-2”) byłaby równa innej etykiecie dokumentu, kolejny numer dopisku jest zwiększany
(FR-903, ostatnie zdanie); w korpusie nie występuje.

## R6. Wpływ na testy i korpusy

- Goldeny parsera: zmienią się wszystkie dokumenty z przypisami (6 aktów, `Corpus/banking/regulamin-rachunku` —
  bez zmiany identyfikatora zastępczego „1”, prywatne z przypisami: corp-5, firm-1/2/3/5/6, ind-1/2/4, reg1, pdp).
  Diff do akceptacji właściciela (FR-908), sprawdzany skryptem: po zamianie identyfikatorów na numery 1.3.1 tekst
  identyczny (SC-133).
- Goldeny chunków (`tests/LegalAgent.Chunking.Tests/Golden`) i korpus syntetyczny `corpus/` (`refresh`): przypisy w
  dokumentach syntetycznych mają etykiety z generatora — sprawdzić w `refresh`, czy zmienia się tylko `[^…]`.
- `NumberCheck` (`tests/…/Fixtures/NumberCheck.cs`): usuwa z Markdownu definicje i odnośniki według wzorca `[^n]`
  i dopuszcza cyfry etykiet po `Footnote.Number` (`FootnoteLabels`). Zadanie: wzorce dla identyfikatorów z
  etykietą i dopiskiem (`[^1)-2]`, `[^*)]`), żeby nie liczyły się jako nadmiar (`Liczby +`); bramka
  `RegulationNumbers_HaveNoExcess` bez zmian `number-check.txt`.
- Bramka FTN001 (spec 011): liczby bez zmian (FR-909).
