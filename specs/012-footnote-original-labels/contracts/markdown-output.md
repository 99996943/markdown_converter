# Kontrakt: Markdown — uzupełnienie 012 (identyfikatory przypisów)

Uzupełnia `specs/001-legal-pdf-parser/contracts/markdown-output.md` oraz uzupełnienia spec 002, 007, 008 i 010.
Wersja kontraktu publicznego: **2.0.0** (MAJOR — zmienia się postać identyfikatora przypisu; znika reguła
„numeracja ciągła od 1”). Publiczne API biblioteki rozszerzone addytywnie (wersja biblioteki 1.4.0).

## Identyfikator przypisu

- Odnośnik `[^id]` i definicja `[^id]: treść` używają **etykiety przypisu z PDF-u**, zapisanej dosłownie, ze
  wszystkimi znakami (nawias, gwiazdki); cyfry indeksu górnego jako zwykłe cyfry:

  | Etykieta w PDF-ie | Odnośnik | Definicja |
  |---|---|---|
  | „²¹” (regulamin) | `TAK[^21]` | `[^21]: O dacie wprowadzenia…` |
  | „¹⁾” (akt) | `dyrektywę[^1)]` | `[^1)]: Niniejsza ustawa wdraża…` |
  | „*)” (obwieszczenie) | `ustawy[^*)]` | `[^*)]: Zmiany tekstu jednolitego…` |

- **Powtórzona etykieta**: pierwsza definicja w kolejności stron ma identyfikator bez dopisku, kolejne `-2`, `-3`…
  (`[^1)]`, `[^1)-2]`). Gdy tak zbudowany identyfikator jest równy innej etykiecie dokumentu, numer dopisku rośnie.
- **Identyfikator zastępczy**: przypis bez etykiety albo z etykietą pustą, zawierającą spację, tabulator lub `[`/`]`
  dostaje identyfikator `n` = numer kolejny przypisu w dokumencie (jak w kontrakcie 1.x) i ostrzeżenie w raporcie
  (FR-904, kod w planie: `FTN002_FootnoteWithoutLabel`).
- Każdy identyfikator ma dokładnie jedną definicję w dokumencie; wszystkie odnośniki do przypisu mają jego
  identyfikator.
- Identyfikatory są lokalne dla dokumentu; podgląd GFM (np. GitHub) może wyświetlać przypisy własną numeracją — źródło
  Markdownu zawiera etykiety z PDF-u.

## Kolejność i miejsce definicji (bez zmian względem 1.x)

- Definicje sekcji stoją po jej blokach, w kolejności pierwszego odnośnika w dokumencie (akapity, listy, tabele w
  kolejności czytania); przypisy bez odnośnika po nich, w kolejności stron, w najgłębszej sekcji otwartej na ich
  stronie.

## Model (addytywnie)

- `Footnote.Identifier` (string, `init`): identyfikator Markdownu przypisu; `Footnote.Number` zostaje jako numer kolejny.
- `FootnoteRef.Identifier` (string, `init`): identyfikator przypisu, na który wskazuje odnośnik.
- Renderer pisze `Identifier`; dla modelu zbudowanego bez niego (pusty) — `Number` (zgodność z klientami 1.x).

## Chunki (spec 004)

- Fragmenty chunkera używają tych samych identyfikatorów co Markdown dokumentu; wybór i kolejność definicji w
  części — bez zmian (po `Number`).
