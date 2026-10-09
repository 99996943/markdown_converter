# Kontrakt: publiczne API `LegalAgent.Chunking`

Szkic sygnatur (nazwy obowiązujące; szczegóły dokumentacji XML w implementacji). Modele:
`data-model.md`.

```csharp
namespace LegalAgent.Chunking;

/// Dzieli dokument na fragmenty. Bezpieczny przy wywołaniach równoległych na jednej instancji.
public interface IDocumentChunker
{
    /// Dzieli gotowy wynik konwersji parsera.
    /// ArgumentException: błędne metadane lub opcje. OperationCanceledException: anulowanie.
    Task<ChunkedDocument> ChunkAsync(
        PdfConversionResult conversion,
        DocumentMetadata metadata,
        ChunkingRequest? request = null,
        CancellationToken cancellationToken = default);

    /// Konwertuje PDF (od bieżącej pozycji strumienia, strumień nie jest zamykany) i dzieli wynik.
    /// Dodatkowo: wyjątki PdfParserException z konwersji, bez opakowania.
    Task<ChunkedDocument> ChunkAsync(
        Stream pdf,
        DocumentMetadata metadata,
        ChunkingRequest? request = null,
        CancellationToken cancellationToken = default);
}

public sealed record DocumentMetadata(string DocumentId)
{
    public string? Designation { get; init; }
    public string? Type { get; init; }
    public string? Title { get; init; }
    public int? Version { get; init; }
    public DateOnly? ValidFrom { get; init; }
    public DateOnly? ValidTo { get; init; }
    public string? Status { get; init; }
    public string? PreviousVersion { get; init; }
}

public sealed record ChunkingRequest
{
    public Action<ChunkingOptions>? ConfigureOptions { get; init; }
    public PdfConversionRequest? ParserRequest { get; init; }
}

public sealed class ChunkingOptions
{
    public int MaxChunkLength { get; set; } = 2000;
    public RenderingOptions? Rendering { get; set; }
}

public static class ChunkJson
{
    public const int SchemaVersion = 1;
    public static string ToJsonLines(ChunkedDocument document);            // contracts/chunks-json.md
    public static IReadOnlyList<ChunkRecord> ReadLines(string jsonLines);   // FormatException z numerem linii
}

/// Rekord kontraktu odczytany z JSON (metadane dokumentu + fragment).
public sealed record ChunkRecord(ChunkedDocumentHeader Document, Chunk Chunk);
```

```csharp
namespace Microsoft.Extensions.DependencyInjection;

public static class ChunkingServiceCollectionExtensions
{
    /// Rejestruje parser (AddLegalAgentPdfParser), DocumentChunker (singleton) i walidację opcji.
    /// Idempotentne; configure się kumuluje.
    public static IServiceCollection AddLegalAgentChunking(
        this IServiceCollection services,
        Action<ChunkingOptions>? configure = null);
}
```

Zasady:

- Brak I/O plików, konsoli, zmiennych środowiskowych i stanu statycznego mutowalnego (FR-200).
- Wynik zależy tylko od wejścia, metadanych i opcji; kultura procesu nie ma wpływu (FR-205).
- `ChunkAsync` z wynikiem parsera nie wykonuje pracy asynchronicznej; sprawdza token przed pracą i
  między jednostkami.
- Biblioteka nie dodaje żadnego tekstu do `Content` (FR-230).
