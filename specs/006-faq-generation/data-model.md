# Data Model: Konwersja i generowanie FAQ (spec 006)

Typy w bibliotece `LegalAgent.Faq` (publiczne, zob. `contracts/library-api.md`) i w aplikacji
`mBank.FaqGenerator` (wewnętrzne). Rekordy niemutowalne.

## Biblioteka — konwersja (`LegalAgent.Faq.Conversion`)

### PdfSource (wejście)
`Index` (1–n), `Address` (Uri, adres źródła z manifestu), `PdfPath` (pełna ścieżka pliku PDF). Aplikacja buduje
listę z `DownloadRun.Results`.

### ConvertedDocument
| Pole | Typ | Opis / reguły |
|------|-----|---------------|
| `Index` | int | 1–5, pozycja adresu (z `DownloadResult.Index`) |
| `Address` | Uri | adres źródła podany przez użytkownika (manifest) |
| `PdfFileName` | string | nazwa pliku PDF w katalogu pobrań |
| `MarkdownFileName` | string | `<PdfFileName bez .pdf>.md` |
| `Title` | string? | `LegalDocument.Title` z parsera |
| `Markdown` | string | treść zapisana do pliku |
| `Units` | IReadOnlyList<string> | `Designation` i `HeadingText` wszystkich sekcji (rekurencyjnie), bez duplikatów, w kolejności dokumentu |
| `PageCount` | int | `ConversionReport.PageCount` |
| `Warnings` | IReadOnlyList<ConversionWarning> | ostrzeżenia parsera |

### ConversionFailure
`Index`, `PdfFileName`, `Reason` (tekst dla użytkownika). Powstaje z `PdfParserException`, z
`IsComplete == false` albo z pustego Markdown (bez tekstu poza `<!-- page: N -->`).

### ConversionRun
`Documents` (pomyślne, w kolejności indeksu), `Failures`, `RemovedMarkdownFiles`. Właściwość `AllSucceeded`
oznacza `Failures.Count == 0`. Sprzątanie `*.md` następuje tylko przy `AllSucceeded`.

### ConversionEvent
`Index`, `FileName`, `Kind` (`Started` | `Converted` | `Failed`), `Document?`, `Failure?`. Zdarzenia są
przekazywane do `ConsoleReport`.

## Biblioteka `LegalAgent.Faq`

### FaqDocumentInput (wejście)
| Pole | Typ | Reguły |
|------|-----|--------|
| `Name` | string | wyświetlana nazwa: tytuł albo nazwa pliku; niepusta |
| `Resource` | Uri | bezwzględny adres źródła |
| `Markdown` | string | niepusty |
| `Units` | IReadOnlyList<string> | może być pusta (wtedy każda wskazana jednostka jest odrzucana) |

### FaqSourceDocument (wynik)
`Id` (`D1`…`Dn`, nadawane przez `FaqGenerator` według kolejności listy), `Name`, `Resource`.

### FaqSource
`DocumentId` (np. `D2`), `Unit` (string? — po przycięciu; `null` lub pusty = brak jednostki).

### FaqCandidate
`Id` (`D2-K3`; K numerowane od 1 w kolejności odpowiedzi modelu), `DocumentId`, `Question`, `Answer`,
`Unit` (string?), `Quote` (string? — dosłowny fragment dokumentu, T067d). Odrzucony kandydat zostawia lukę w
numeracji (np. D1-K1, D1-K3).

### FaqItem (wynik)
`Number` (1–10), `Question`, `Answer`, `Sources` (1..n `FaqSource`, z kandydatów `BasedOn`), `BasedOn`
(1..n identyfikatorów kandydatów).

### FaqResult
| Pole | Opis |
|------|------|
| `Items` | dokładnie 10 `FaqItem` |
| `Documents` | `FaqSourceDocument` w kolejności wejścia |
| `Candidates` | wszyscy kandydaci (do diagnostyki, nie są zapisywani do pliku FAQ) |
| `Usage` | `FaqUsage`: suma `InputTokens`/`OutputTokens` z metadanych odpowiedzi (null, gdy usługa ich nie podała) |

### FaqGeneratorOptions
| Pole | Domyślnie | Walidacja |
|------|-----------|-----------|
| `CandidatesPerDocument` | 10 | 1–30 |
| `ItemCount` | 10 | stała z wymagań; w aplikacji 10 |
| `MaxDocumentTokens` | 100 000 | > 0 |
| `CharactersPerToken` | 3.0 | > 0 (szacunek, research R3) |

### FaqProgress (zdarzenia `IProgress<FaqEvent>`)
`Kind`: `CandidatesStarted(docId, chars, estTokens)` | `CandidateDropped(docId, detail)` |
`CandidatesFinished(docId, count, usage?)` |
`SelectionStarted(candidateCount, chars, estTokens)` | `SelectionCorrection(problems)` | `SelectionFinished(usage?)`
(zużycie wyboru obejmuje poprawkę).

### Błędy (wyjątki biblioteki)
| Typ | Kiedy | Pola |
|-----|-------|------|
| `FaqInputTooLongException` | `CheckInput`: szacunek tokenów dokumentu > `MaxDocumentTokens` | `DocumentName`, `Characters`, `EstimatedTokens`, `Limit` |
| `FaqServiceException` | wyjątek usługi modelu (research R7) | `Kind` (`FaqServiceErrorKind`), `Step` (`Candidates` z `DocumentId` / `Selection`), `StatusCode?`, komunikat |
| `FaqResponseException` | odpowiedź niezgodna z regułami (research R5) | `Step`, `DocumentId?`, `Problems` (lista tekstów, np. „pozycja 4: dokument D9 nie istnieje”) |

Komunikaty wyjątków nie zawierają treści zapytania. Wyjątki usługi mogą zawierać fragment odpowiedzi usługi, więc
aplikacja przepuszcza każdy komunikat przez `SecretRedactor` (FR-411).

## Reguły sprawdzania (FR-430, research R5)

**Kandydaci (na dokument)**:
1. JSON `{"candidates":[{"question","answer","unit","quote"}]}`.
2. 1 ≤ liczba ≤ `CandidatesPerDocument`.
3. `question`, `answer` niepuste po przycięciu.
4. Pytania niepowtarzające się (normalizacja: małe litery w kulturze niezmiennej, zwinięte białe znaki, bez
   końcowego `?`).
5. `unit` pusty albo pasuje do `Units` dokumentu.

Reguły 1–5 odrzucają całą odpowiedź. Potem ugruntowanie (T067d, `FaqGrounding`) sprawdza każdego kandydata osobno:
- tekst jednostki = sekcje Markdown, których nagłówek pasuje do `unit` (`UnitMatcher`), każda do następnego
  nagłówka wyższego poziomu albo tego samego poziomu; sekcja z numerem lub oznaczeniem („6. …”, „§ 5”, „Art. 3”,
  „Rozdział 2”) obejmuje też następujące po niej nagłówki bez numeru tego samego poziomu („Dodatkowe
  wyjaśnienia”, T067h); bez `unit` albo bez pasującego nagłówka — cały dokument;
- normalizacja: komentarze `<!-- … -->` usunięte, tylko litery i cyfry małymi literami, reszta jako jedna spacja;
- cytat ma co najmniej 3 słowa; dzielony na fragmenty po wielokropkach; co najmniej 80% trójek kolejnych słów
  fragmentów występuje w tekście jednostki (T067h: drobne zmiany przechodzą, parafraza nie);
- każda liczba (ciąg cyfr) odpowiedzi występuje w tekście jednostki.

Kandydat, który nie spełnia tych warunków, odpada (zdarzenie `CandidateDropped` z jedną linią „kandydat Dn-Kk:
powód; powód”, w której jest początek cytatu; ostrzeżenie w konsoli). Odpowiedź
jest odrzucana (`FaqResponseException`, kod 7) tylko wtedy, gdy z dokumentu nie zostanie żaden kandydat.

**Wybór**:
1. JSON `{"items":[{"question","answer","basedOn":[...]}]}`.
2. Dokładnie `ItemCount` pozycji.
3. Pola niepuste; `basedOn` ma ≥ 1 element.
4. Pytania niepowtarzające się.
5. Każde `basedOn` to istniejący kandydat.
6. Każda liczba odpowiedzi występuje w odpowiedziach lub cytatach kandydatów `basedOn` (T067d).
Źródła pozycji nie pochodzą od modelu (T067b): to dokument i jednostka każdego kandydata `basedOn`, w kolejności
`basedOn`, bez powtórzeń; źródło bez jednostki odpada, gdy ten sam dokument jest też źródłem z jednostką.
Jednostki kandydatów są już sprawdzone w kroku kandydatów.

Wszystkie problemy są zbierane, nie tylko pierwszy. Każdy problem skutkuje odrzuceniem odpowiedzi.

**Dopasowanie jednostki**: `UnitMatcher.Matches(cited, units)`:
- normalizacja: NBSP → spacja, zwinięte białe znaki, przycięcie, bez końcowej kropki, porównanie
  `OrdinalIgnoreCase` po `ToLowerInvariant`;
- warunek: równość z dowolnym elementem albo `cited` zaczyna się od elementu, po którym jest koniec, spacja lub
  `,`.
- numer nagłówka numerowanego też jest oznaczeniem jednostki (T067a): z „6. Jakie …” liczy się „6”, z „2.1. …”
  „2.1”, z tym samym warunkiem końca.

Przykłady:
- „§ 12 ust. 3” pasuje do „§ 12.”;
- „Art. 5a” nie pasuje do „Art. 5”;
- „Rozdział 2” pasuje do „Rozdział 2. Otwarcie rachunku”, bo oznaczenie „Rozdział 2” jest w zbiorze.
- „6”, „6.” i „6 ust. 2” pasują do „6. Jakie informacje …”; „§ 6” i „16” nie; „2” nie pasuje do „2.1. …”.

## Plik FAQ

`FaqMarkdownRenderer.Render(FaqResult, FaqFileHeader)` zwraca tekst zgodny z `contracts/faq-file.md`.
`FaqFileHeader` zawiera: `Title`, `Description`, `Timestamp` (DateTimeOffset UTC), `Model`, `Deployment`.
`Resource` pochodzi z `FaqResult.Documents`. Tytuł i opis podaje aplikacja (słowo „mBank” nie trafia do
biblioteki).

## Konfiguracja aplikacji (nowe sekcje)

### AzureOpenAI
| Pole | Domyślnie | Reguły |
|------|-----------|--------|
| `Endpoint` | `""` | wymagany; bezwzględny `https` |
| `Deployment` | `"gpt-4o-mini"` | wymagany, niepusty |
| `Model` | `"gpt-4o-mini"` | tylko informacyjnie (nagłówek FAQ) |
| `TimeoutSeconds` | 300 | > 0 |
| `Temperature` | 0 | null = nie wysyłaj; 0–2 |
| `Seed` | 42 | null = nie wysyłaj |
| `MaxOutputTokens` | 4096 | > 0 |

Klucz **nie jest** polem konfiguracji. Jeśli w konfiguracji lub zmiennych pojawi się `AzureOpenAI:ApiKey`,
aplikacja kończy się kodem 2 z komunikatem, że klucz podaje się tylko w konsoli lub potokiem. To zabezpieczenie
przed odruchowym wpisaniem klucza do pliku.

### Faq
| Pole | Domyślnie | Reguły |
|------|-----------|--------|
| `OutputDirectory` | `"faq"` | katalog OKF z `FAQ_mBank.md`; względny od katalogu roboczego; tworzony, jeśli brak |
| `CandidatesPerDocument` | 10 | 1–30 |
| `MaxDocumentTokens` | 100000 | > 0 |

## Przejścia stanów uruchomienia

```text
start → konfiguracja OK? ─nie→ 2
      → adresy → pobieranie ─niepełne→ 3
      → konwersja ─błąd→ 5
      → CheckInput ─za długi→ 6
      → klucz ─brak wejścia→ 2
      → kandydaci D1..D5 (kolejno) ─błąd usługi→ 6 / ─zła odpowiedź→ 7
      → wybór ─błąd usługi→ 6 / ─zła odpowiedź→ 7
      → zapis FAQ ─błąd→ 4
      → 0
(Ctrl+C w dowolnym miejscu → 130; poprzedni FAQ_mBank.md nietknięty poza udanym zapisem)
```
