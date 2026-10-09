# Contract: publiczne API biblioteki `LegalAgent.PdfParser`

Wersja kontraktu: **1.0.0** (SemVer; zmiana łamiąca = MAJOR — konstytucja). Sygnatury poniżej
są wiążące co do nazw, typów i semantyki; ciała metod — nie. Typy modelu: [data-model.md](../data-model.md).

> **Dodatki 1.1.0** (spec 002: `SectionKind.TableDocumentSection`, `ConversionReport.TableDocuments`, opcje `Tables.DetectTableDocuments`
> i progi, `Headings.DetectImageCaptions`, `Headings.ValidityLineAsParagraph`) opisuje
> [002-table-document-sections/contracts/public-api.md](../../002-table-document-sections/contracts/public-api.md).
> Zmiany są addytywne; poniższy kontrakt 1.0.0 obowiązuje bez zmian.

## Rejestracja (namespace `Microsoft.Extensions.DependencyInjection`)

```csharp
public static class PdfParserServiceCollectionExtensions
{
    // Rejestruje fasadę, renderer, wszystkie wbudowane etapy (Singleton) i walidację opcji.
    // Wielokrotne wywołanie jest idempotentne (TryAdd); configure jest kumulowane.
    public static IServiceCollection AddLegalAgentPdfParser(
        this IServiceCollection services,
        Action<PdfParserOptions>? configure = null);

    // Dodaje własny etap (Singleton). Kolejność wg IPipelineStage.Order.
    public static IServiceCollection AddPdfParserStage<TStage>(this IServiceCollection services)
        where TStage : class, IPipelineStage;

    // Zastępuje wbudowany lub wcześniej dodany etap.
    public static IServiceCollection ReplacePdfParserStage<TExisting, TReplacement>(this IServiceCollection services)
        where TExisting : class, IPipelineStage
        where TReplacement : class, IPipelineStage;

    // Usuwa etap (np. wyłączenie wykrywania tabel bez zmiany opcji).
    public static IServiceCollection RemovePdfParserStage<TStage>(this IServiceCollection services)
        where TStage : class, IPipelineStage;
}
```

Wiązanie z konfiguracją aplikacji (po stronie aplikacji, standardowo):
`services.Configure<PdfParserOptions>(configuration.GetSection("PdfParser"))`.

## Fasada (namespace `LegalAgent.PdfParser`)

```csharp
public interface IPdfMarkdownConverter
{
    /// Konwertuje PDF ze strumienia (od bieżącej pozycji do końca). Strumień nie jest zamykany.
    /// Bezpieczne do równoległego wywoływania na jednej instancji.
    Task<PdfConversionResult> ConvertAsync(
        Stream pdf,
        PdfConversionRequest? request = null,
        CancellationToken cancellationToken = default);
}

public sealed record PdfConversionRequest
{
    public string? SourceId { get; init; }                         // trafia do SourceInfo.SourceId
    public Action<PdfParserOptions>? ConfigureOptions { get; init; } // nadpisanie per wywołanie (na kopii opcji)
    public IProgress<ConversionProgress>? Progress { get; init; }  // FR-072
}

public readonly record struct ConversionProgress(int PageNumber, int PageCount, string Stage);

public interface IMarkdownRenderer
{
    string Render(LegalDocument document, RenderingOptions? options = null);
}
```

Dla scenariuszy bez kontenera DI: `PdfMarkdownConverter.CreateDefault(Action<PdfParserOptions>? configure = null)`
zwraca skonfigurowaną instancję (wewnętrznie buduje tę samą listę etapów).

## Rozszerzalność potoku (namespace `LegalAgent.PdfParser.Pipeline`, API zaawansowane)

```csharp
public interface IPipelineStage
{
    int Order { get; }                      // wbudowane: 100, 200, …, 1100 (data-model.md §4)
    void Execute(PipelineContext context);  // bez I/O; wyjątek = błąd konwersji (PdfParserException lub przepakowany)
}
```

`PipelineContext` udostępnia: `Options` (tylko odczyt), `Source`, `Pages` (model układu),
`Report` (dodawanie ostrzeżeń), `CancellationToken`. Etap MUSI być bezstanowy (stan wyłącznie w
kontekście).

## Wyjątki (namespace `LegalAgent.PdfParser`)

Wszystkie dziedziczą po `PdfParserException : Exception` i mają czytelny komunikat po polsku.

| Typ | Kiedy | Dodatkowe właściwości |
|-----|-------|----------------------|
| `InvalidPdfException` | pusty strumień, brak nagłówka `%PDF`, uszkodzona struktura (FR-009) | `Reason: InvalidPdfReason { Empty, NotPdf, Corrupted }` |
| `PdfEncryptedException` | wymagane hasło otwarcia | — |
| `PdfPageReadException` | błąd odczytu strony przy `AllowPartialResult = false` lub wszystkie strony pominięte (FR-009a) | `PageNumber` |
| `PdfLimitExceededException` | przekroczony limit (FR-009b) | `Limit: PdfLimit { InputSize, PageCount, Duration }`, `Configured`, `Measured` |
| `PdfNoTextException` | brak tekstu w całym dokumencie (FR-071) | — |

`ArgumentNullException` dla `pdf == null`; `ArgumentException` dla strumienia nieczytelnego
(`!CanRead`); `OptionsValidationException` przy niepoprawnych opcjach;
`OperationCanceledException` wyłącznie przy anulowaniu przez `cancellationToken` wywołującego.

## Gwarancje

- **Determinizm**: dla identycznych bajtów i opcji `Markdown`, `Document` i `Report` (poza
  `Elapsed`) są identyczne (FR-008).
- **Kompletność**: wynik zwrócony bez wyjątku z `IsComplete == false` występuje tylko gdy
  `AllowPartialResult == true` lub istnieją strony bez warstwy tekstowej (FR-071).
- **Brak efektów ubocznych**: brak I/O poza czytaniem przekazanego strumienia; brak stanu
  globalnego; brak sieci.
- **Anulowanie**: sprawdzane przed każdą stroną w każdym etapie przetwarzającym strony.
