# Data Model: identyfikatory przypisów

## Footnote (rozszerzony, `src/LegalAgent.PdfParser/Model/Footnote.cs`)

| Pole | Typ | Status | Znaczenie |
|---|---|---|---|
| `Number` | int | bez zmian | numer kolejny w dokumencie (kolejność, wybór w chunkerze) |
| `OriginalLabel` | string | bez zmian | etykieta z PDF-u (`FootnoteDraft.Label`) |
| `Inlines`, `Page`, `IsOrphan` | — | bez zmian | |
| `Identifier` | string, `init`, domyślnie `""` | **nowe** | identyfikator Markdownu (`[^Identifier]`) |

## FootnoteRef (rozszerzony, `src/LegalAgent.PdfParser/Model/Inline.cs`)

| Pole | Typ | Status | Znaczenie |
|---|---|---|---|
| `FootnoteNumber` | int | bez zmian | `Footnote.Number` przypisu |
| `Identifier` | string, `init`, domyślnie `""` | **nowe** | `Footnote.Identifier` przypisu |

## Reguła identyfikatora (FR-900–FR-904)

Wyliczana raz dla dokumentu w `DocumentBuildStage`, po nadaniu numerów:

1. Kandydat = `OriginalLabel`. Jeśli pusty albo zawiera spację, tabulator, `[` lub `]` → identyfikator zastępczy =
   `Number` jako tekst i ostrzeżenie `FTN002_FootnoteWithoutLabel` (strona przypisu).
2. Przypisy z etykietą w kolejności (`Page`, id szkicu): pierwsze wystąpienie etykiety → identyfikator = etykieta;
   k-te (k ≥ 2) → `etykieta-k`; jeśli taki identyfikator jest już zajęty (inna etykieta albo zastępczy), k rośnie.
3. Identyfikatory zastępcze nie mogą zderzyć się z etykietami: przy kolizji zastępczy dostaje kolejny wolny numer.
4. Każdy `FootnoteRef` dostaje `Identifier` przypisu, na który wskazuje (także w komórkach tabel, nagłówku i grupie
   nagłówka; komórki kontynuacji współdzielą inline'y).

Niezmienniki: identyfikatory w dokumencie unikalne; każdy odnośnik ma definicję; liczba przypisów i odnośników jak w
1.3.1.

## Raport

| Kod | Nowy | Znaczenie |
|---|---|---|
| `FTN002_FootnoteWithoutLabel` | tak | przypis bez etykiety z PDF-u dostał identyfikator zastępczy (numer kolejny) |
| `FTN001_OrphanFootnote` | bez zmian | liczby aktów bez zmian (bramka spec 011) |
