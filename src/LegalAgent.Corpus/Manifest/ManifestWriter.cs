using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace LegalAgent.Corpus.Manifest;

/// <summary>Writes and reads <c>manifest.json</c> (contracts/manifest.md).</summary>
public static class ManifestWriter
{
    private const string ActsType = "akty";
    private const string PoisonPrefix = "ZAT-";
    private const string DateFormat = "yyyy-MM-dd";

    private static readonly string[] DefaultTypeOrder = ["regulaminy", "taryfy", "procedury"];

    /// <summary>Serialises the manifest with the default type order (regulaminy, taryfy, procedury).</summary>
    /// <param name="manifest">Manifest.</param>
    /// <returns>JSON text (2-space indentation, LF, trailing LF, no BOM).</returns>
    public static string Write(Manifest manifest) => Write(manifest, DefaultTypeOrder);

    /// <summary>Serialises the manifest.</summary>
    /// <param name="manifest">Manifest.</param>
    /// <param name="typeOrder">Order of base types; types not listed follow in ordinal order.</param>
    /// <returns>JSON text (2-space indentation, LF, trailing LF, no BOM).</returns>
    public static string Write(Manifest manifest, IReadOnlyList<string> typeOrder)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(typeOrder);

        using var stream = new MemoryStream();
        var options = new JsonWriterOptions
        {
            Indented = true,
            IndentSize = 2,
            NewLine = "\n",
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        using (var w = new Utf8JsonWriter(stream, options))
        {
            w.WriteStartObject();
            w.WriteNumber("schemaVersion", Manifest.SchemaVersion);
            WriteRun(w, manifest.Run);
            w.WriteStartArray("documents");
            foreach (var d in Sort(manifest.Documents, typeOrder))
            {
                WriteDocument(w, d);
            }

            w.WriteEndArray();
            w.WriteEndObject();
        }

        return new UTF8Encoding(false).GetString(stream.ToArray()) + "\n";
    }

    /// <summary>Parses manifest JSON.</summary>
    /// <param name="json">JSON text.</param>
    /// <returns>Manifest.</returns>
    public static Manifest Read(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var run = root.GetProperty("run");
        var documents = root.GetProperty("documents").EnumerateArray().Select(ReadDocument).ToList();
        return new Manifest(
            new ManifestRun(
                run.GetProperty("seed").GetUInt64(),
                Date(run.GetProperty("referenceDate"))!.Value,
                run.GetProperty("parameters").GetRawText(),
                Str(run, "parserVersion")!,
                Str(run, "generatorVersion")!,
                Str(run, "contentHash")!),
            documents);
    }

    private static List<ManifestDocument> Sort(IReadOnlyList<ManifestDocument> documents, IReadOnlyList<string> typeOrder)
    {
        int Group(ManifestDocument d)
        {
            if (d.Id.StartsWith(PoisonPrefix, StringComparison.Ordinal))
            {
                return int.MaxValue;
            }

            if (string.Equals(d.Type, ActsType, StringComparison.Ordinal))
            {
                return 0;
            }

            for (var i = 0; i < typeOrder.Count; i++)
            {
                if (string.Equals(typeOrder[i], d.Type, StringComparison.Ordinal))
                {
                    return 1 + i;
                }
            }

            return typeOrder.Count + 1;
        }

        return [.. documents
            .OrderBy(Group)
            .ThenBy(d => Group(d) == typeOrder.Count + 1 ? d.Type : string.Empty, StringComparer.Ordinal)
            .ThenBy(d => d.Id, StringComparer.Ordinal)];
    }

    private static void WriteRun(Utf8JsonWriter w, ManifestRun run)
    {
        w.WriteStartObject("run");
        w.WriteNumber("seed", run.Seed);
        w.WriteString("referenceDate", run.ReferenceDate.ToString(DateFormat, CultureInfo.InvariantCulture));
        w.WritePropertyName("parameters");
        using (var parameters = JsonDocument.Parse(run.ParametersJson))
        {
            parameters.RootElement.WriteTo(w);
        }

        w.WriteString("parserVersion", run.ParserVersion);
        w.WriteString("generatorVersion", run.GeneratorVersion);
        w.WriteString("contentHash", run.ContentHash);
        w.WriteEndObject();
    }

    private static void WriteDocument(Utf8JsonWriter w, ManifestDocument d)
    {
        w.WriteStartObject();
        w.WriteString("id", d.Id);
        w.WriteString("type", d.Type);
        w.WriteString("title", d.Title);
        OptString(w, "designation", d.Designation);
        if (d.Version is { } version)
        {
            w.WriteNumber("version", version);
        }

        OptDate(w, "validFrom", d.ValidFrom);
        OptDate(w, "validTo", d.ValidTo);
        w.WriteString("status", d.Status);
        OptString(w, "previousVersion", d.PreviousVersion);
        w.WriteString("pdf", d.Pdf);
        w.WriteString("markdown", d.Markdown);
        w.WriteNumber("pages", d.Pages);
        OptString(w, "template", d.Template);
        OptString(w, "layout", d.Layout);
        if (d.Seed is { } seed)
        {
            w.WriteNumber("seed", seed);
        }

        OptShare(w, "sharedWordShare", d.SharedWordShare);
        OptShare(w, "repeatedWordShare", d.RepeatedWordShare);

        if (d.Changes is { Count: > 0 })
        {
            w.WriteStartArray("changes");
            foreach (var c in d.Changes)
            {
                w.WriteStartObject();
                w.WriteString("unit", c.Unit);
                w.WriteNumber("page", c.Page);
                OptString(w, "fact", c.Fact);
                w.WriteString("before", c.Before);
                w.WriteString("after", c.After);
                w.WriteEndObject();
            }

            w.WriteEndArray();
        }

        if (d.Contradictions is { Count: > 0 })
        {
            w.WriteStartArray("contradictions");
            foreach (var c in d.Contradictions)
            {
                w.WriteStartObject();
                w.WriteString("with", c.With);
                w.WriteString("unit", c.Unit);
                w.WriteNumber("page", c.Page);
                OptString(w, "fact", c.Fact);
                w.WriteString("this", c.This);
                w.WriteString("other", c.Other);
                w.WriteEndObject();
            }

            w.WriteEndArray();
        }

        if (d.Poison is { } p)
        {
            w.WriteStartObject("poison");
            w.WriteString("kind", p.Kind);
            w.WriteString("imitates", p.Imitates);
            w.WriteString("description", p.Description);
            w.WriteStartArray("places");
            foreach (var place in p.Places)
            {
                w.WriteStartObject();
                w.WriteNumber("page", place.Page);
                w.WriteString("unit", place.Unit);
                w.WriteString("element", place.Element);
                w.WriteString("text", place.Text);
                OptString(w, "goal", place.Goal);
                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteEndObject();
        }

        if (d.Source is { } s)
        {
            w.WriteStartObject("source");
            w.WriteString("journal", s.Journal);
            OptDate(w, "consolidatedTextDate", s.ConsolidatedTextDate);
            w.WriteString("url", s.Url);
            OptDate(w, "downloadedOn", s.DownloadedOn);
            OptString(w, "notes", s.Notes);
            w.WriteEndObject();
        }

        OptString(w, "notes", d.Notes);
        w.WriteEndObject();
    }

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

    private static void OptShare(Utf8JsonWriter w, string name, double? value)
    {
        if (value is { } share)
        {
            w.WritePropertyName(name);
            w.WriteRawValue(share.ToString("F3", CultureInfo.InvariantCulture));
        }
    }

    private static ManifestDocument ReadDocument(JsonElement e) => new(
        Str(e, "id")!,
        Str(e, "type")!,
        Str(e, "title")!,
        Str(e, "designation"),
        Has(e, "version") ? e.GetProperty("version").GetInt32() : null,
        Date(Prop(e, "validFrom")),
        Date(Prop(e, "validTo")),
        Str(e, "status")!,
        Str(e, "previousVersion"),
        Str(e, "pdf")!,
        Str(e, "markdown")!,
        e.GetProperty("pages").GetInt32(),
        Str(e, "template"),
        Str(e, "layout"),
        Has(e, "seed") ? e.GetProperty("seed").GetUInt64() : null,
        Has(e, "sharedWordShare") ? e.GetProperty("sharedWordShare").GetDouble() : null,
        Has(e, "repeatedWordShare") ? e.GetProperty("repeatedWordShare").GetDouble() : null,
        Has(e, "changes")
            ? [.. e.GetProperty("changes").EnumerateArray().Select(c => new VersionChange(
                Str(c, "unit")!, c.GetProperty("page").GetInt32(), Str(c, "fact"), Str(c, "before")!, Str(c, "after")!))]
            : null,
        Has(e, "contradictions")
            ? [.. e.GetProperty("contradictions").EnumerateArray().Select(c => new Contradiction(
                Str(c, "with")!, Str(c, "unit")!, c.GetProperty("page").GetInt32(), Str(c, "fact"), Str(c, "this")!, Str(c, "other")!))]
            : null,
        Has(e, "poison") ? ReadPoison(e.GetProperty("poison")) : null,
        Has(e, "source") ? ReadSource(e.GetProperty("source")) : null,
        Str(e, "notes"));

    private static PoisonInfo ReadPoison(JsonElement p) => new(
        Str(p, "kind")!,
        Str(p, "imitates")!,
        Str(p, "description")!,
        [.. p.GetProperty("places").EnumerateArray().Select(x => new PoisonPlace(
            x.GetProperty("page").GetInt32(), Str(x, "unit")!, Str(x, "element")!, Str(x, "text")!, Str(x, "goal")))]);

    private static ActInfo ReadSource(JsonElement s) => new(
        Str(s, "journal")!,
        Date(s.GetProperty("consolidatedTextDate"))!.Value,
        Str(s, "url")!,
        Date(s.GetProperty("downloadedOn"))!.Value,
        Str(s, "notes"));

    private static bool Has(JsonElement e, string name) => e.TryGetProperty(name, out _);

    private static JsonElement? Prop(JsonElement e, string name) => e.TryGetProperty(name, out var v) ? v : null;

    private static string? Str(JsonElement e, string name) => e.TryGetProperty(name, out var v) ? v.GetString() : null;

    private static DateOnly? Date(JsonElement? e) =>
        e is { } v ? DateOnly.ParseExact(v.GetString()!, DateFormat, CultureInfo.InvariantCulture) : null;
}
