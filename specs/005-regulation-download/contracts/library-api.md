# Kontrakt: publiczne API `LegalAgent.Downloads`

Biblioteka ogólna: dowolne adresy PDF i dowolna lista hostów; bez konsoli, konfiguracji i stanu globalnego.
Przestrzeń nazw `LegalAgent.Downloads`. Wszystkie publiczne typy mają komentarze XML. Modele:
`../data-model.md`.

```csharp
public sealed record DownloadOptions { /* pola z data-model.md */ }

public static class AddressValidator
{
    /// Trim + reguły FR-302; previous = adresy już przyjęte (do wykrycia duplikatu).
    public static AddressCheck Check(string? input, IReadOnlyCollection<Uri> previous, DownloadOptions options);

    /// Sprawdza całą listę (tryb argumentów/konfiguracji); pierwszy błąd z numerem pozycji.
    public static IReadOnlyList<AddressCheck> CheckAll(IReadOnlyList<string> inputs, DownloadOptions options);
}

public static class FileNamePlanner
{
    /// Deterministyczne nazwy plików (R7); wynik w kolejności adresów.
    public static IReadOnlyList<PlannedDownload> Plan(IReadOnlyList<Uri> addresses);
}

public sealed class DocumentDownloader
{
    /// httpClient: bez automatycznych przekierowań (AllowAutoRedirect = false), Timeout = Infinite.
    /// Rzuca ArgumentException przy niepoprawnych opcjach.
    public DocumentDownloader(HttpClient httpClient, DownloadOptions options);

    /// Tworzy katalog, pobiera równolegle, zapisuje atomowo, zapisuje manifest, sprząta gdy wszystko pobrane.
    /// Błędy pojedynczych adresów → DownloadResult.Failed (bez wyjątku).
    /// Rzuca: ArgumentException (pusta lista, adres niepoprawny wg AddressValidator),
    ///        DownloadDirectoryException (katalog/manifest/sprzątanie — kod 4 w aplikacji),
    ///        OperationCanceledException tylko gdy cancellationToken anulowano przed rozpoczęciem;
    ///        anulowanie w trakcie → wyniki Cancelled + zapisany manifest, potem OperationCanceledException.
    public Task<DownloadRun> DownloadAllAsync(
        IReadOnlyList<Uri> addresses,
        IProgress<DownloadEvent>? progress = null,
        CancellationToken cancellationToken = default);
}

public static class DownloadManifestJson
{
    public const int SchemaVersion = 1;
    public static string Serialize(IReadOnlyList<DownloadResult> results);   // contracts/download-manifest.md
}

public sealed class DownloadDirectoryException : IOException { }
```

Wersja biblioteki 1.0.0; zmiany łamiące powyższe sygnatury → MAJOR (konstytucja).
