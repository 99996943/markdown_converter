using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using LegalAgent.Downloads.Model;

namespace LegalAgent.Downloads;

/// <summary>Serializes the download manifest (contracts/download-manifest.md).</summary>
public static class DownloadManifestJson
{
    /// <summary>Manifest schema version.</summary>
    public const int SchemaVersion = 1;

    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
        IndentSize = 2,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Serializes the results to the manifest text (LF, trailing newline).</summary>
    /// <param name="results">Results in index order.</param>
    /// <returns>The manifest JSON.</returns>
    public static string Serialize(IReadOnlyList<DownloadResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", SchemaVersion);
            writer.WriteStartArray("items");
            foreach (DownloadResult result in results.OrderBy(r => r.Index))
            {
                WriteItem(writer, result);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray()) + "\n";
    }

    /// <summary>Manifest name of an error kind (kebab-case).</summary>
    internal static string KindName(DownloadErrorKind kind) => kind switch
    {
        DownloadErrorKind.HttpStatus => "http-status",
        DownloadErrorKind.Timeout => "timeout",
        DownloadErrorKind.Connection => "connection",
        DownloadErrorKind.NotPdf => "not-pdf",
        DownloadErrorKind.TooLarge => "too-large",
        DownloadErrorKind.RedirectNotAllowed => "redirect-not-allowed",
        DownloadErrorKind.TooManyRedirects => "too-many-redirects",
        DownloadErrorKind.WriteFailed => "write-failed",
        DownloadErrorKind.Cancelled => "cancelled",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    private static void WriteItem(Utf8JsonWriter writer, DownloadResult result)
    {
        writer.WriteStartObject();
        writer.WriteNumber("index", result.Index);
        writer.WriteString("url", result.Address.AbsoluteUri);
        writer.WriteString("status", result.Status == DownloadStatus.Downloaded ? "downloaded" : "failed");
        writer.WriteString("file", result.FileName);
        if (result.SizeBytes is long size)
        {
            writer.WriteNumber("size", size);
        }

        if (result.Sha256 is not null)
        {
            writer.WriteString("sha256", result.Sha256);
        }

        if (result.LastModified is DateTimeOffset modified)
        {
            writer.WriteString(
                "lastModified",
                modified.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
        }

        if (result.Error is DownloadError error)
        {
            writer.WriteStartObject("error");
            writer.WriteString("kind", KindName(error.Kind));
            if (error.HttpStatusCode is int status)
            {
                writer.WriteNumber("httpStatus", status);
            }

            writer.WriteString("message", error.Message);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }
}
