using System.Globalization;
using System.Text.Json;
using LegalAgent.Faq.Model;

namespace LegalAgent.Faq;

/// <summary>A parsed value or the problem that prevented parsing.</summary>
/// <typeparam name="T">Type of the value.</typeparam>
/// <param name="Value">The value; <c>null</c> when <paramref name="Problem"/> is set.</param>
/// <param name="Problem">Why the text was rejected.</param>
internal sealed record Parsed<T>(T? Value, string? Problem)
    where T : class;

/// <summary>An item of the selection response before validation.</summary>
/// <param name="Question">Question as returned.</param>
/// <param name="Answer">Answer as returned.</param>
/// <param name="BasedOn">Candidate identifiers as returned.</param>
internal sealed record ParsedItem(string Question, string Answer, IReadOnlyList<string> BasedOn);

/// <summary>
/// Parses the JSON responses of both steps (contracts/model-exchange.md) without trusting the service schema: the whole
/// text must be one JSON object of exactly the expected shape (required properties, types, no extra properties).
/// </summary>
internal static class FaqResponseParser
{
    /// <summary>Problem reported for any text that is not JSON of the expected shape.</summary>
    public const string NotJson = "odpowiedź nie jest poprawnym JSON-em zgodnym ze schematem";

    /// <summary>Parses a candidate response; candidates get identifiers <c>&lt;documentId&gt;-K1</c>… in order.</summary>
    public static Parsed<IReadOnlyList<FaqCandidate>> ParseCandidates(string text, string documentId) =>
        Parse<IReadOnlyList<FaqCandidate>>(text, root =>
        {
            JsonElement array = Property(root, "candidates", JsonValueKind.Array, ["candidates"]);
            var candidates = new List<FaqCandidate>();
            foreach (JsonElement element in array.EnumerateArray())
            {
                Shape(element, ["question", "answer", "unit", "quote"]);
                candidates.Add(new FaqCandidate(
                    string.Create(CultureInfo.InvariantCulture, $"{documentId}-K{candidates.Count + 1}"),
                    documentId,
                    String(element, "question"),
                    String(element, "answer"),
                    Unit(String(element, "unit")),
                    String(element, "quote")));
            }

            return candidates;
        });

    /// <summary>Parses a selection response.</summary>
    public static Parsed<IReadOnlyList<ParsedItem>> ParseSelection(string text) =>
        Parse<IReadOnlyList<ParsedItem>>(text, root =>
        {
            JsonElement array = Property(root, "items", JsonValueKind.Array, ["items"]);
            var items = new List<ParsedItem>();
            foreach (JsonElement element in array.EnumerateArray())
            {
                Shape(element, ["question", "answer", "basedOn"]);
                var basedOn = new List<string>();
                foreach (JsonElement id in Property(element, "basedOn", JsonValueKind.Array).EnumerateArray())
                {
                    basedOn.Add(id.ValueKind == JsonValueKind.String ? id.GetString()! : throw new FormatException());
                }

                items.Add(new ParsedItem(String(element, "question"), String(element, "answer"), basedOn));
            }

            return items;
        });

    private static Parsed<T> Parse<T>(string text, Func<JsonElement, T> read)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new Parsed<T>(null, NotJson);
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(text);
            return new Parsed<T>(read(document.RootElement), null);
        }
        catch (Exception e) when (e is JsonException or FormatException or InvalidOperationException)
        {
            return new Parsed<T>(null, NotJson);
        }
    }

    /// <summary>The element is an object with exactly the given properties.</summary>
    private static void Shape(JsonElement element, string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException();
        }

        int count = 0;
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!names.Contains(property.Name, StringComparer.Ordinal))
            {
                throw new FormatException();
            }

            count++;
        }

        if (count != names.Length)
        {
            throw new FormatException();
        }
    }

    private static JsonElement Property(JsonElement element, string name, JsonValueKind kind, string[]? shape = null)
    {
        if (shape is not null)
        {
            Shape(element, shape);
        }

        return element.TryGetProperty(name, out JsonElement value) && value.ValueKind == kind ? value : throw new FormatException();
    }

    private static string String(JsonElement element, string name) =>
        Property(element, name, JsonValueKind.String).GetString()!;

    private static string? Unit(string unit) => string.IsNullOrWhiteSpace(unit) ? null : unit.Trim();
}
