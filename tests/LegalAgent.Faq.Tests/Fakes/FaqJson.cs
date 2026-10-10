using System.Globalization;
using System.Text.Json;

namespace LegalAgent.Faq.Tests.Fakes;

/// <summary>
/// A candidate in a scripted model response; the default quote is <see cref="FaqJson.QuoteMarker"/>, which the fake
/// model replaces with a line of the document it was sent.
/// </summary>
internal sealed record CandidateJson(string Question, string Answer, string Unit = "", string Quote = FaqJson.QuoteMarker);

/// <summary>An item in a scripted selection response.</summary>
internal sealed record ItemJson(string Question, string Answer, IReadOnlyList<string> BasedOn);

/// <summary>Builds model responses in the shape of contracts/model-exchange.md.</summary>
internal static class FaqJson
{
    /// <summary>Replaced by the fake model with <see cref="QuoteFrom"/> of the request.</summary>
    public const string QuoteMarker = "@cytat@";

    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>A candidate response.</summary>
    public static string Candidates(params CandidateJson[] candidates) =>
        JsonSerializer.Serialize(new { candidates }, Options);

    /// <summary>A valid candidate response with <paramref name="count"/> distinct questions without units.</summary>
    public static string Candidates(string documentId, int count) =>
        Candidates([.. Enumerable.Range(1, count).Select(k => Candidate(documentId, k))]);

    /// <summary>The k-th generated candidate of a document; the answer has no digits (grounding checks numbers).</summary>
    public static CandidateJson Candidate(string documentId, int k) =>
        new(
            string.Create(CultureInfo.InvariantCulture, $"Pytanie {k} o dokument {documentId}?"),
            string.Create(CultureInfo.InvariantCulture, $"Odpowiedź z dokumentu, wariant {Letter(k)}."));

    /// <summary>
    /// The first line of the document in a candidate request with at least three words that is not a heading or a
    /// page marker; empty when there is none.
    /// </summary>
    public static string QuoteFrom(string user)
    {
        int start = user.IndexOf("\n\n", StringComparison.Ordinal);
        int end = user.IndexOf("\nJednostki dokumentu ", StringComparison.Ordinal);
        string document = start < 0 ? string.Empty : user[(start + 2)..(end > start ? end : user.Length)];
        return document.Split('\n').FirstOrDefault(line =>
            !line.StartsWith('#')
            && !line.StartsWith("<!--", StringComparison.Ordinal)
            && line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length >= 3) ?? string.Empty;
    }

    /// <summary>The text with <see cref="QuoteMarker"/> replaced by the JSON-escaped quote from the request.</summary>
    public static string ReplaceQuote(string text, string user) =>
        text.Contains(QuoteMarker, StringComparison.Ordinal)
            ? text.Replace(QuoteMarker, JsonSerializer.Serialize(QuoteFrom(user))[1..^1], StringComparison.Ordinal)
            : text;

    private static char Letter(int k) => (char)('a' + ((k - 1) % 26));

    /// <summary>
    /// A valid answer to any request of a run: three candidates for „Dokument Dn: …”, otherwise a selection of
    /// <paramref name="itemCount"/> items over the documents listed in the message.
    /// </summary>
    public static string ValidAnswer(ChatCall call, int itemCount = 10)
    {
        string user = call.User;
        if (user.StartsWith("Dokument D", StringComparison.Ordinal))
        {
            return Candidates(user["Dokument ".Length..user.IndexOf(':', StringComparison.Ordinal)], 3);
        }

        int documents = user.Split('\n').Skip(1).TakeWhile(l => l.Length > 0).Count();
        return Selection(itemCount, documents);
    }

    /// <summary>A selection response.</summary>
    public static string Selection(params ItemJson[] items) =>
        JsonSerializer.Serialize(new { items }, Options);

    /// <summary>
    /// A valid selection of <paramref name="count"/> items: item i is based on candidate K1 of document D((i-1) mod n + 1)
    /// (or Kk when a document has more).
    /// </summary>
    public static string Selection(int count, int documentCount) =>
        Selection([.. SelectionItems(count, documentCount)]);

    /// <summary>The items of <see cref="Selection(int, int)"/>.</summary>
    public static IEnumerable<ItemJson> SelectionItems(int count, int documentCount) =>
        Enumerable.Range(1, count).Select(i =>
        {
            int d = ((i - 1) % documentCount) + 1;
            int k = ((i - 1) / documentCount) + 1;
            string doc = string.Create(CultureInfo.InvariantCulture, $"D{d}");
            return new ItemJson(
                string.Create(CultureInfo.InvariantCulture, $"Pytanie końcowe {i}?"),
                string.Create(CultureInfo.InvariantCulture, $"Odpowiedź końcowa, wariant {Letter(i)}."),
                [string.Create(CultureInfo.InvariantCulture, $"{doc}-K{k}")]);
        });
}
