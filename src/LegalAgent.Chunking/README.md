# LegalAgent.Chunking

Biblioteka dzieli dokument przekonwertowany przez `LegalAgent.PdfParser` na **fragmenty (chunki) z metadanymi** dla
aplikacji RAG (spec 004). Fragment to jedna jednostka redakcyjna dokumentu (paragraf, artykuł, sekcja taryfy,
procedury lub tabeli-dokumentu, wstęp) albo część jednostki, jeśli jest dłuższa niż limit. Biblioteka nie czyta ani
nie zapisuje plików — pracuje na modelach w pamięci; zapis JSON Lines robi `ChunkJson`.

Specyfikacja: [`specs/004-document-chunking/`](../../specs/004-document-chunking/), format rekordu:
[`contracts/chunks-json.md`](../../specs/004-document-chunking/contracts/chunks-json.md).

## Użycie

### API

```csharp
var services = new ServiceCollection()
    .AddLegalAgentChunking(o => o.MaxChunkLength = 2000)   // rejestruje też parser
    .BuildServiceProvider();
IDocumentChunker chunker = services.GetRequiredService<IDocumentChunker>();

var metadata = new DocumentMetadata("REG-06")
{
    Designation = "BP/REG/06",      // wspólne dla wszystkich wersji dokumentu
    Type = "regulation",
    Version = 3,
    ValidFrom = new DateOnly(2026, 6, 1),
    Status = "in-force",
    PreviousVersion = "REG-06-w2",
};

await using FileStream pdf = File.OpenRead("REG-06.pdf");
ChunkedDocument result = await chunker.ChunkAsync(pdf, metadata);      // PDF → parser → fragmenty
string jsonl = ChunkJson.ToJsonLines(result);                          // jedna linia na fragment
```

Drugie przeciążenie `ChunkAsync(PdfConversionResult, …)` dzieli wynik parsera, który już masz (tak robi generator
korpusu). `ChunkingRequest` pozwala zmienić opcje dla jednego wywołania (`ConfigureOptions`) i przekazać ustawienia
parsera (`ParserRequest`). Metadane i opcje są sprawdzane przed (wolną) konwersją: `DocumentId` musi pasować do
`^[A-Za-z0-9][A-Za-z0-9._-]{0,99}$`, `Version` > 0, `ValidTo` ≥ `ValidFrom`.

### CLI

```bash
dotnet run --project src/LegalAgent.PdfParser.Cli -c Release -- chunk in.pdf -o out.chunks.jsonl \
    --id REG-06 --designation BP/REG/06 [--type …] [--title …] [--doc-version n] [--valid-from rrrr-mm-dd] \
    [--valid-to rrrr-mm-dd] [--status …] [--previous-version …] [--max-length n]
```

`--id` domyślnie to nazwa pliku bez rozszerzenia. Opcje fragmentów można też ustawić zmiennymi
`CHUNKING__<Pole>`, np. `CHUNKING__MaxChunkLength=1500` (argument `--max-length` ma pierwszeństwo).

### Opcje (`ChunkingOptions`)

| Opcja | Domyślnie | Znaczenie |
|-------|-----------|-----------|
| `MaxChunkLength` | 2000 | maksymalna liczba znaków `content` fragmentu, co najmniej 200 |
| `Rendering` | opcje renderowania parsera | jak renderować treść; znaczniki stron są **zawsze** wyłączone |

## Zasada działania

`DocumentChunker` pracuje na modelu `LegalDocument` z wyniku parsera, **nigdy na Markdown**, i nie ma własnych
heurystyk struktury — jeśli struktura jest błędna, poprawia się ją w parserze.

```text
LegalDocument ──► UnitCollector ──► jednostki ──► UnitSplitter ──► części ──► FragmentRenderer ──► Chunk
                                        │                                          (MarkdownRenderer parsera)
                                        └──► UnitKeyBuilder ──► unitKey ──► ChunkIdBuilder ──► id
```

### 1. Jednostki (`UnitCollector`)

Jednostki zbierane są w kolejności dokumentu:

- **wstęp** — treść przed pierwszą sekcją (i jej przypisy), jeśli nie jest pusta;
- **własna treść każdej sekcji** — bloki i przypisy sekcji bez jej podsekcji; podsekcje są osobnymi jednostkami;
- **sam nagłówek** — sekcja bez treści i bez podsekcji też jest jednostką (zachowuje nagłówek);
- sekcja z podsekcjami, ale bez własnej treści, **nie** tworzy fragmentu.

Tytuł dokumentu trafia tylko do metadanych (`document.title`, `detectedTitle`), nie do treści fragmentów.
Zaślepki pominiętych stron (`SkippedPageBlock`, renderowane jako komentarz) nie są tekstem PDF-u i nie tworzą ani
nie dołączają do jednostek.

Rodzaj jednostki (`unitKind`) pochodzi z rodzaju sekcji parsera: `preamble`, `documentTitle`, `book`, `part`,
`division`, `chapter`, `subchapter`, `article`, `paragraph`, `typographic` (nagłówek typograficzny),
`tableDocumentSection` (sekcja tabeli-dokumentu, spec 002).

### 2. Podział długich jednostek (`UnitSplitter`)

Jednostka, której wyrenderowana treść mieści się w `MaxChunkLength`, jest jednym fragmentem. Dłuższa jest dzielona
na **atomy** — niepodzielne kawałki:

| Atom | Uwagi |
|------|-------|
| akapit (lub inny cały blok) | |
| wiersz tabeli | w każdej części tabela jest odtwarzana **z wierszem nagłówka** |
| własny tekst punktu listy (z jego akapitami) | zagnieżdżone punkty są osobnymi atomami; część zaczynająca się w środku listy odtwarza zagnieżdżenie |
| przypis, do którego jednostka się nie odwołuje | zamyka jednostkę |

Atomy są pakowane **zachłannie** w kolejności dokumentu: kolejny atom dołącza do bieżącej części, dopóki
wyrenderowana część mieści się w limicie (długość mierzy się renderowaniem, nie szacunkiem). **Każda część zaczyna
się nagłówkiem jednostki**, więc fragment jest zrozumiały samodzielnie. Pojedynczy atom dłuższy niż limit zostaje
całą częścią z `exceedsLimit: true` — tekstu się nie tnie.

Przypisy: część niesie przypisy, do których odwołuje się jej treść; przypisy bez odwołania trafiają jako atomy na
koniec jednostki (`FootnoteSelector`). `listLabels` części zaczynającej się w środku listy to dosłowne etykiety
punktu i jego przodków (od zewnętrznego), np. `["3.", "a)"]`.

### 3. Renderowanie (`FragmentRenderer`)

Każda część jest renderowana **tym samym `MarkdownRenderer` co cały dokument** — z małego `LegalDocument` zawierającego
nagłówek jednostki, bloki części i jej przypisy — **bez znaczników stron**. Treść fragmentu jest więc zgodna z
kontraktem Markdown parsera (`specs/001-legal-pdf-parser/contracts/markdown-output.md`) i zawiera wyłącznie tekst z
PDF-u: żadnych dopisanych słów.

### 4. Strony (`PageTracker`)

`pages.first`/`pages.last` to strony źródła części: dla akapitu — strony bloku; dla wiersza tabeli — jego strona
(`TableRow.Page` z parsera) albo strony tabeli; dla punktu listy — strona wyznaczana z podziałów stron napotkanych w
liście. Pierwsza część sekcji zaczyna się na stronie nagłówka; zakres jest przycinany do stron jednostki.

### 5. Klucz jednostki i identyfikator (`UnitKeyBuilder`, `ChunkIdBuilder`)

- **Segment** sekcji to jej oznaczenie („§ 30”, „Art. 5”, „Rozdział 2”) albo — gdy go nie ma — tekst nagłówka, ze
  znormalizowanymi spacjami i bez kropki na końcu. Wstęp ma segment `~preamble`.
- **`unitKey`** = oznaczenie serii (`Designation`, wspólne dla wszystkich wersji; gdy brak — `DocumentId`) + `" | "` +
  **najkrótszy sufiks ścieżki segmentów, który jest unikalny w dokumencie**, np. `BP/REG/06 | § 30`. Paragrafy i
  artykuły są numerowane w całym dokumencie, więc ich klucz nie zależy od rozdziału — **ta sama jednostka ma ten sam
  klucz w każdej wersji**, także gdy w nowej wersji dodano rozdział. Identyczne pełne ścieżki dostają `" #2"`, `" #3"`.
- **`chunk.id`** = `<DocumentId>_<16 pierwszych znaków hex SHA-256(unitKey)>_<numer części>`, np.
  `REG-06_17b142f82d1b92d5_1`. Zależy tylko od dokumentu, klucza i części, więc ponowna konwersja tej samej wersji
  daje te same identyfikatory, a różne wersje — różne (inny `DocumentId`).

`citation` fragmentu to oznaczenie jednostki w postaci z dokumentu („§ 13”) albo jej nagłówek; wstęp nie ma cytatu.
`sectionPath` to teksty nagłówków od korzenia do jednostki.

### 6. Metadane

`DocumentMetadata` podaje wywołujący; biblioteka **kopiuje je do każdego fragmentu i nie interpretuje**. Wartości
przeznaczone dla modelu są po angielsku: `type` (`regulation`, `tariff`, `procedure`, `act`), `status` (`in-force`,
`outdated`), segment wstępu `~preamble`. Tekst dokumentu (treść, tytuł, nagłówki, cytaty) zostaje oryginalny, po
polsku. Z wyniku parsera dochodzą dane źródła: liczba stron, SHA-256 PDF-u, `isComplete`, pominięte strony.

## Format wyjścia (JSON Lines)

`ChunkJson.ToJsonLines` zapisuje jedną linię na fragment (UTF-8 bez BOM, LF, stała kolejność pól, pola `null`
pomijane); każda linia jest samodzielna: `schemaVersion`, `document` i `chunk`. `ChunkJson.ReadLines` czyta plik i
odrzuca rekordy niezgodne ze schematem. Przykład (sformatowany; w pliku jedna linia):

```json
{
  "schemaVersion": 1,
  "document": {
    "id": "REG-06", "designation": "BP/REG/06", "type": "regulation",
    "title": "Regulamin rachunków bankowych dla przedsiębiorców Bank Przykładowy S.A.",
    "version": 3, "validFrom": "2026-06-01", "status": "in-force", "previousVersion": "REG-06-w2",
    "source": { "pageCount": 23, "sha256": "fea5cd4b…", "isComplete": true, "skippedPages": [] }
  },
  "chunk": {
    "id": "REG-06_17b142f82d1b92d5_1", "unitKey": "BP/REG/06 | ~preamble", "part": 1, "partCount": 1,
    "unitKind": "preamble", "listLabels": [], "sectionPath": [], "pages": { "first": 1, "last": 1 },
    "length": 73, "exceedsLimit": false,
    "content": "Bank Przykładowy S.A.\n\nBP/REG/06 Wersja 3 Obowiązuje od 1 czerwca 2026 r."
  }
}
```

Pełny opis pól i reguł zgodności (`schemaVersion`): [`contracts/chunks-json.md`](../../specs/004-document-chunking/contracts/chunks-json.md).

## Wydajność i współbieżność

`DocumentChunker` jest bezstanowy i bezpieczny przy równoległych wywołaniach na jednej instancji (rejestrowany jako
singleton). Opcje są kopiowane przy każdym wywołaniu.

## Testy

Projekt `tests/LegalAgent.Chunking.Tests`:

- testy jednostkowe zbierania jednostek, podziału, stron, kluczy, identyfikatorów i serializacji;
- **goldeny** `tests/LegalAgent.Chunking.Tests/Golden/*.chunks.jsonl` (dokumenty korpusu i akt prawny); przy różnicy
  test zapisuje `*.actual.jsonl`, a `UPDATE_GOLDEN=1` przepisuje goldeny;
- testy determinizmu (te same identyfikatory przy ponownej konwersji, te same klucze w kolejnych wersjach);
- testy wydajności w kategorii `Performance` (poza CI):

```bash
dotnet test tests/LegalAgent.Chunking.Tests -- --filter-class "LegalAgent.Chunking.Tests.PerformanceTests"
```

Pliki `*.chunks.jsonl` korpusu (`corpus/`) tworzy generator korpusu tą biblioteką; po zmianie podziału odśwież je
poleceniem `refresh` generatora (zob. [`LegalAgent.Corpus`](../LegalAgent.Corpus/README.md)).
