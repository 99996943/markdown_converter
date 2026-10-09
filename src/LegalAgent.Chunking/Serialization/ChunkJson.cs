using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using LegalAgent.Chunking.Model;

namespace LegalAgent.Chunking.Serialization;

/// <summary>A record of the chunk JSON contract: the document data and one chunk.</summary>
/// <param name="Document">Document-level data.</param>
/// <param name="Chunk">The chunk.</param>
public sealed record ChunkRecord(ChunkedDocumentHeader Document, Chunk Chunk);

/// <summary>
/// JSON form of chunked documents (spec 004, contracts/chunks-json.md): one self-contained record per chunk, written as
/// JSON Lines with a fixed field order, UTF-8 characters written as is, null fields left out and LF line endings.
/// </summary>
public static class ChunkJson
{
    /// <summary>Version of the record schema written and the highest one read.</summary>
    public const int SchemaVersion = 1;

    private const string DateFormat = "yyyy-MM-dd";

    private static readonly Dictionary<string, ChunkUnitKind> UnitKinds =
        Enum.GetValues<ChunkUnitKind>().ToDictionary(UnitKindName, k => k, StringComparer.Ordinal);

    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Indented = false,
    };

    /// <summary>One line per chunk, each ending with LF; empty for a document without chunks.</summary>
    public static string ToJsonLines(ChunkedDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var sb = new StringBuilder();
        foreach (Chunk chunk in document.Chunks)
        {
            sb.Append(ToJson(document.Header, chunk)).Append('\n');
        }

        return sb.ToString();
    }

    /// <summary>Reads JSON Lines written by <see cref="ToJsonLines"/>; blank lines are skipped.</summary>
    /// <exception cref="FormatException">A line is not a valid record; the message gives its number.</exception>
    public static IReadOnlyList<ChunkRecord> ReadLines(string jsonLines)
    {
        ArgumentNullException.ThrowIfNull(jsonLines);

        var records = new List<ChunkRecord>();
        string[] lines = jsonLines.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].TrimEnd('\r');
            if (line.Trim().Length == 0)
            {
                continue;
            }

            try
            {
                using JsonDocument json = JsonDocument.Parse(line);
                records.Add(Read(json.RootElement));
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
            {
                throw new FormatException(
                    string.Create(CultureInfo.InvariantCulture, $"Linia {i + 1}: nieprawidłowy rekord fragmentu: {ex.Message}"),
                    ex);
            }
        }

        return records;
    }

    private static string ToJson(ChunkedDocumentHeader header, Chunk chunk)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, WriterOptions))
        {
            w.WriteStartObject();
            w.WriteNumber("schemaVersion", SchemaVersion);
            WriteDocument(w, header);
            WriteChunk(w, chunk);
            w.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteDocument(Utf8JsonWriter w, ChunkedDocumentHeader header)
    {
        DocumentMetadata m = header.Metadata;
        w.WriteStartObject("document");
        w.WriteString("id", m.DocumentId);
        w.WriteString("designation", header.SeriesKey);
        OptString(w, "type", m.Type);
        OptString(w, "title", header.Title);
        OptString(w, "detectedTitle", header.DetectedTitle);
        if (m.Version is { } version)
        {
            w.WriteNumber("version", version);
        }

        OptDate(w, "validFrom", m.ValidFrom);
        OptDate(w, "validTo", m.ValidTo);
        OptString(w, "status", m.Status);
        OptString(w, "previousVersion", m.PreviousVersion);

        w.WriteStartObject("source");
        w.WriteNumber("pageCount", header.Source.PageCount);
        w.WriteString("sha256", header.Source.Sha256);
        w.WriteBoolean("isComplete", header.Source.IsComplete);
        w.WriteStartArray("skippedPages");
        foreach (int page in header.Source.SkippedPages)
        {
            w.WriteNumberValue(page);
        }

        w.WriteEndArray();
        w.WriteEndObject();
        w.WriteEndObject();
    }

    private static void WriteChunk(Utf8JsonWriter w, Chunk c)
    {
        w.WriteStartObject("chunk");
        w.WriteString("id", c.ChunkId);
        w.WriteString("unitKey", c.UnitKey);
        w.WriteNumber("part", c.Part);
        w.WriteNumber("partCount", c.PartCount);
        w.WriteString("unitKind", UnitKindName(c.UnitKind));
        OptString(w, "citation", c.Citation);
        StringArray(w, "listLabels", c.ListLabels);
        StringArray(w, "sectionPath", c.SectionPath);
        w.WriteStartObject("pages");
        w.WriteNumber("first", c.Pages.First);
        w.WriteNumber("last", c.Pages.Last);
        w.WriteEndObject();
        w.WriteNumber("length", c.Length);
        w.WriteBoolean("exceedsLimit", c.ExceedsLimit);
        w.WriteString("content", c.Content);
        w.WriteEndObject();
    }

    private static ChunkRecord Read(JsonElement root)
    {
        if (!root.TryGetProperty("schemaVersion", out JsonElement version) || version.ValueKind != JsonValueKind.Number)
        {
            throw new FormatException("brak pola schemaVersion.");
        }

        if (version.GetInt32() > SchemaVersion)
        {
            throw new FormatException(string.Create(CultureInfo.InvariantCulture, $"schemaVersion {version.GetInt32()} jest nowsza niż obsługiwana {SchemaVersion}."));
        }

        JsonElement d = Required(root, "document", JsonValueKind.Object);
        JsonElement s = Required(d, "source", JsonValueKind.Object);
        string id = String(d, "id");
        string designation = String(d, "designation");
        string? title = OptionalString(d, "title");
        var metadata = new DocumentMetadata(id)
        {
            Designation = designation,
            Type = OptionalString(d, "type"),
            Title = title,
            Version = d.TryGetProperty("version", out _) ? Int(d, "version") : null,
            ValidFrom = OptionalDate(d, "validFrom"),
            ValidTo = OptionalDate(d, "validTo"),
            Status = OptionalString(d, "status"),
            PreviousVersion = OptionalString(d, "previousVersion"),
        };
        var source = new ChunkSource(
            Int(s, "pageCount"),
            String(s, "sha256"),
            Bool(s, "isComplete"),
            Required(s, "skippedPages", JsonValueKind.Array).EnumerateArray().Select(p => p.GetInt32()).ToList());
        var header = new ChunkedDocumentHeader(metadata, title, OptionalString(d, "detectedTitle"), designation, source);

        JsonElement c = Required(root, "chunk", JsonValueKind.Object);
        JsonElement pages = Required(c, "pages", JsonValueKind.Object);
        var chunk = new Chunk(
            String(c, "id"),
            String(c, "unitKey"),
            Int(c, "part"),
            Int(c, "partCount"),
            UnitKind(String(c, "unitKind")),
            OptionalString(c, "citation"),
            Strings(c, "listLabels"),
            Strings(c, "sectionPath"),
            new PageSpan(Int(pages, "first"), Int(pages, "last")),
            Int(c, "length"),
            Bool(c, "exceedsLimit"),
            String(c, "content"));
        return new ChunkRecord(header, chunk);
    }

    private static string UnitKindName(ChunkUnitKind kind)
    {
        string name = kind.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    private static ChunkUnitKind UnitKind(string name) =>
        UnitKinds.TryGetValue(name, out ChunkUnitKind kind) ? kind : throw new FormatException($"nieznany unitKind „{name}”.");

    private static JsonElement Required(JsonElement e, string name, JsonValueKind kind) =>
        e.TryGetProperty(name, out JsonElement value) && value.ValueKind == kind
            ? value
            : throw new FormatException($"brak pola {name} lub zły typ (oczekiwano {kind}).");

    private static string String(JsonElement e, string name) => Required(e, name, JsonValueKind.String).GetString()!;

    private static int Int(JsonElement e, string name) => Required(e, name, JsonValueKind.Number).GetInt32();

    private static bool Bool(JsonElement e, string name) =>
        e.TryGetProperty(name, out JsonElement value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : throw new FormatException($"brak pola {name} lub zły typ (oczekiwano wartości logicznej).");

    private static string? OptionalString(JsonElement e, string name) =>
        e.TryGetProperty(name, out _) ? String(e, name) : null;

    private static DateOnly? OptionalDate(JsonElement e, string name) =>
        OptionalString(e, name) is { } text
            ? DateOnly.ParseExact(text, DateFormat, CultureInfo.InvariantCulture)
            : null;

    private static List<string> Strings(JsonElement e, string name) =>
        Required(e, name, JsonValueKind.Array).EnumerateArray()
            .Select(v => v.ValueKind == JsonValueKind.String ? v.GetString()! : throw new FormatException($"{name}: element nie jest tekstem."))
            .ToList();

    private static void OptString(Utf8JsonWriter w, string name, string? value)
    {
        if (value is not null)
        {
            w.WriteString(name, value);
        }
    }

    private static void OptDate(Utf8JsonWriter w, string name, DateOnly? value)
    {
        if (value is { } date)
        {
            w.WriteString(name, date.ToString(DateFormat, CultureInfo.InvariantCulture));
        }
    }

    private static void StringArray(Utf8JsonWriter w, string name, IReadOnlyList<string> values)
    {
        w.WriteStartArray(name);
        foreach (string value in values)
        {
            w.WriteStringValue(value);
        }

        w.WriteEndArray();
    }
}
