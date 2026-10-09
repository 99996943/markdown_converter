# Kontrakt: publiczne API `LegalAgent.Faq` 1.0.0

Biblioteka ogólna: nie zna mBanku, Azure ani konsoli. Zależności: `Microsoft.SemanticKernel.Abstractions` 1.80.1 i
projekt `LegalAgent.PdfParser`. Na plikach operuje tylko część `Conversion` (Markdown obok podanych PDF-ów). Wszystkie typy publiczne mają komentarze XML. Przestrzeń nazw: `LegalAgent.Faq` (modele w
`LegalAgent.Faq.Model`).

## Konwersja (`LegalAgent.Faq.Conversion`)

```csharp
public sealed record PdfSource(int Index, Uri Address, string PdfPath);

public sealed class DocumentSetConverter
{
    public DocumentSetConverter(IPdfMarkdownConverter converter);

    /// Konwertuje kolejno; zapis <nazwa>.md atomowo obok PDF; błąd pliku → ConversionFailure (dalsze pliki
    /// konwertowane); przy AllSucceeded usuwa *.md spoza bieżącego zestawu z katalogów PDF-ów.
    /// <exception cref="IOException">Zapis lub usuwanie się nie powiodło.</exception>
    /// <exception cref="OperationCanceledException"/>
    public Task<ConversionRun> ConvertAllAsync(
        IReadOnlyList<PdfSource> sources,
        IProgress<ConversionEvent>? progress = null,
        CancellationToken cancellationToken = default);
}

public static class UnitExtractor
{
    /// Designation i HeadingText wszystkich sekcji (rekurencyjnie), bez duplikatów, w kolejności dokumentu.
    public static IReadOnlyList<string> FromDocument(LegalDocument document);
}

public sealed record ConvertedDocument(int Index, Uri Address, string PdfFileName, string MarkdownFileName, string? Title,
    string Markdown, IReadOnlyList<string> Units, int PageCount, IReadOnlyList<ConversionWarning> Warnings);
public sealed record ConversionFailure(int Index, string PdfFileName, string Reason);
public sealed record ConversionRun(IReadOnlyList<ConvertedDocument> Documents, IReadOnlyList<ConversionFailure> Failures,
    IReadOnlyList<string> RemovedMarkdownFiles) { public bool AllSucceeded { get; } }
public enum ConversionEventKind { Started, Converted, Failed }
public sealed record ConversionEvent(int Index, string FileName, ConversionEventKind Kind, ConvertedDocument? Document, ConversionFailure? Failure);
```

## Wejście FAQ

```csharp
public sealed record FaqDocumentInput(string Name, Uri Resource, string Markdown, IReadOnlyList<string> Units);

public sealed record FaqGeneratorOptions
{
    public int CandidatesPerDocument { get; init; } = 10;   // 1–30
    public int ItemCount { get; init; } = 10;               // ≥ 1
    public int MaxDocumentTokens { get; init; } = 100_000;  // > 0
    public double CharactersPerToken { get; init; } = 3.0;  // > 0
}

public enum FaqStep { Candidates, Selection }
```

## Generator

```csharp
public sealed class FaqGenerator
{
    /// <exception cref="ArgumentException">Nieprawidłowe opcje.</exception>
    public FaqGenerator(
        IChatCompletionService chat,
        FaqGeneratorOptions options,
        Func<FaqStep, string, PromptExecutionSettings> executionSettings);

    /// Sprawdza rozmiar każdego dokumentu bez wywoływania modelu (przed pytaniem o klucz).
    /// <exception cref="FaqInputTooLongException"/>
    public static IReadOnlyList<FaqInputEstimate> CheckInput(
        IReadOnlyList<FaqDocumentInput> documents, FaqGeneratorOptions options);

    /// Kandydaci dla D1…Dn kolejno, potem wybór; sprawdza każdą odpowiedź.
    /// <exception cref="FaqInputTooLongException"/>
    /// <exception cref="FaqServiceException"/>
    /// <exception cref="FaqResponseException"/>
    /// <exception cref="OperationCanceledException"/>
    public Task<FaqResult> GenerateAsync(
        IReadOnlyList<FaqDocumentInput> documents,
        IProgress<FaqEvent>? progress = null,
        CancellationToken cancellationToken = default);
}

public sealed record FaqInputEstimate(string DocumentName, int Characters, int EstimatedTokens);
```

## Wynik i renderowanie

```csharp
public sealed record FaqSourceDocument(string Id, string Name, Uri Resource);
public sealed record FaqSource(string DocumentId, string? Unit);
public sealed record FaqCandidate(string Id, string DocumentId, string Question, string Answer, string? Unit);
public sealed record FaqItem(int Number, string Question, string Answer, IReadOnlyList<FaqSource> Sources, IReadOnlyList<string> BasedOn);
public sealed record FaqUsage(long InputTokens, long OutputTokens);
public sealed record FaqResult(IReadOnlyList<FaqItem> Items, IReadOnlyList<FaqSourceDocument> Documents, IReadOnlyList<FaqCandidate> Candidates, FaqUsage? Usage);

public sealed record FaqFileHeader(string Title, string Description, DateTimeOffset Timestamp, string Model, string Deployment);

public static class FaqMarkdownRenderer
{
    public static string Render(FaqResult result, FaqFileHeader header);   // contracts/faq-file.md
}

public static class FaqSchemas
{
    public const string Candidates = "…";   // contracts/model-exchange.md
    public const string Selection = "…";
}

public static class UnitMatcher
{
    public static bool Matches(string cited, IReadOnlyCollection<string> units);
}
```

## Postęp

```csharp
public enum FaqEventKind { CandidatesStarted, CandidatesFinished, SelectionStarted, SelectionFinished }

public sealed record FaqEvent(
    FaqEventKind Kind,
    string? DocumentId,      // dla kroków kandydatów
    int Characters,          // rozmiar wysyłanej treści (Started)
    int EstimatedTokens,
    int? Count,              // liczba kandydatów (Finished) / liczba kandydatów wejściowych (SelectionStarted)
    FaqUsage? Usage);        // Finished
```

## Wyjątki

```csharp
public enum FaqServiceErrorKind
{
    Authentication, DeploymentNotFound, RateLimited, ContentFiltered, InputTooLong,
    ServiceUnavailable, Timeout, Network, Other,
}

public sealed class FaqInputTooLongException : Exception
{ public string DocumentName { get; } public int Characters { get; } public int EstimatedTokens { get; } public int Limit { get; } }

public sealed class FaqServiceException : Exception
{ public FaqServiceErrorKind Kind { get; } public FaqStep Step { get; } public string? DocumentId { get; } public int? StatusCode { get; } }

public sealed class FaqResponseException : Exception
{ public FaqStep Step { get; } public string? DocumentId { get; } public IReadOnlyList<string> Problems { get; } }
```

Komunikaty wyjątków są po polsku, bo trafiają do użytkownika aplikacji. Nie zawierają treści dokumentów.
`FaqServiceException.Message` może zawierać skrót odpowiedzi usługi (do 300 znaków); wywołujący odpowiada za
usunięcie z niego sekretów.
