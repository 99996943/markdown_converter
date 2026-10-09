# Contract: publiczne API — zmiany spec 002

Wersja kontraktu: **1.1.0** (MINOR względem 1.0.0 z `specs/001-legal-pdf-parser/contracts/public-api.md`).
Wszystkie zmiany są addytywne; sygnatury i semantyka z wersji 1.0.0 obowiązują bez zmian. Typy:
[data-model.md](../data-model.md).

## Model (namespace `LegalAgent.PdfParser.Model`)

```csharp
public enum SectionKind
{
    // … wartości 1.0.0 bez zmian (DocumentTitle … Typographic) …

    /// <summary>Section of a table-document: heading = the name in the left cell of a two-column bordered
    /// table that makes up the document (FR-083); content = the right cells, merged across pages.</summary>
    TableDocumentSection,
}

/// <summary>A table-document recognised in the document (FR-090).</summary>
public sealed record TableDocumentSummary(
    int FirstPage,
    int LastPage,
    int SectionCount,
    string? HeaderRowText,     // „Definicje | Wyjaśnienie”; null when the table has no column-name row
    int DroppedHeaderRows);

public sealed record ConversionReport(/* … pola 1.0.0 … */)
{
    /// <summary>Recognised table-documents in page order; empty when none.</summary>
    public IReadOnlyList<TableDocumentSummary> TableDocuments { get; init; } = [];
}
```

Uwaga dla wywołujących: nowa wartość `SectionKind` może trafić do `switch` bez gałęzi `default` —
należy ją obsłużyć jak `Typographic` (nagłówek bez oznaczenia jednostki).

## Opcje (namespace `LegalAgent.PdfParser.Options`)

```csharp
public sealed class TableOptions
{
    // … pola 1.0.0 …
    public bool DetectTableDocuments { get; set; } = true;               // FR-080; false ⇒ zachowanie 001
    public double TableDocumentMaxLeftColumnRatio { get; set; } = 0.35;  // (0, 1]
    public int TableDocumentMinPages { get; set; } = 2;                  // ≥ 2
    public double TableDocumentMinPageRatio { get; set; } = 0.5;         // (0, 1]
    public int TableDocumentMinMedianWords { get; set; } = 40;           // ≥ 1
}

public sealed class HeadingOptions
{
    // … pola 1.0.0 …
    public bool DetectImageCaptions { get; set; } = true;     // FR-088
    public bool ValidityLineAsParagraph { get; set; } = true; // FR-093
}
```

Walidator opcji odrzuca wartości spoza podanych zakresów (`OptionsValidationException` przy pierwszym
użyciu, jak w 1.0.0). Zmienne środowiskowe CLI: automatycznie przez binder
(`PDFPARSER__Tables__DetectTableDocuments=false` itd. — wg `contracts/cli.md` z 001).

## API zaawansowane (etapy, namespace `LegalAgent.PdfParser.Layout` / `.Stages` / `.Pipeline`)

```csharp
public static class StageOrder { public const int TableDocument = 560; /* … */ }

public sealed class TableDocumentStage : IPipelineStage { public int Order => StageOrder.TableDocument; }

public static class LayoutAnnotations
{
    /// <summary>Document-wide index (invariant integer) of the table-document a line belongs to.</summary>
    public const string TableDocumentIndex = "tabledoc.index";
}

public sealed record LayoutGlyph(/* … parametry 1.0.0 … */, string? FontName = null);

public sealed class LayoutPage { public IList<Rect> ImageAreas { get; } /* Y w dół */ }
```

`TableDocumentStage` jest rejestrowany przez `AddPdfParser` razem z pozostałymi etapami wbudowanymi
i może być zastąpiony lub usunięty jak każdy etap (FR-006).

## Gwarancje (uzupełnienie)

- Tabela-dokument nie tworzy `TableBlock`; `Report.TableCount`/`FallbackTableCount` jej nie liczą.
- Przy `Tables.DetectTableDocuments = false` dokument jest przetwarzany jak w 1.0.0 (tabele GFM /
  awaryjne), z wyjątkiem ogólnych poprawek FR-088, FR-093 (własne przełączniki), FR-094 i punktora „o”.
- Wynik pozostaje deterministyczny i niezależny od systemu (FR-008, FR-011a, FR-092).
