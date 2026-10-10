using System.Globalization;
using System.Text.Json;

namespace LegalAgent.Faq.Tests.Fakes;

/// <summary>A candidate in a scripted model response.</summary>
internal sealed record CandidateJson(string Question, string Answer, string Unit = "");

/// <summary>A source in a scripted selection response.</summary>
internal sealed record SourceJson(string DocumentId, string Unit = "");

/// <summary>An item in a scripted selection response.</summary>
internal sealed record ItemJson(string Question, string Answer, IReadOnlyList<string> BasedOn, IReadOnlyList<SourceJson> Sources);

/// <summary>Builds model responses in the shape of contracts/model-exchange.md.</summary>
internal static class FaqJson
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>A candidate response.</summary>
    public static string Candidates(params CandidateJson[] candidates) =>
        JsonSerializer.Serialize(new { candidates }, Options);

    /// <summary>A valid candidate response with <paramref name="count"/> distinct questions without units.</summary>
    public static string Candidates(string documentId, int count) =>
        Candidates([.. Enumerable.Range(1, count).Select(k => Candidate(documentId, k))]);

    /// <summary>The k-th generated candidate of a document.</summary>
    public static CandidateJson Candidate(string documentId, int k) =>
        new(
            string.Create(CultureInfo.InvariantCulture, $"Pytanie {k} o dokument {documentId}?"),
            string.Create(CultureInfo.InvariantCulture, $"Odpowiedź {k} z dokumentu {documentId}."));

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

        int documents = user.Split('\n').TakeWhile(l => l.Length > 0).Count(l => l.StartsWith('D'));
        return Selection(itemCount, documents);
    }

    /// <summary>A selection response.</summary>
    public static string Selection(params ItemJson[] items) =>
        JsonSerializer.Serialize(new { items }, Options);

    /// <summary>
    /// A valid selection of <paramref name="count"/> items: item i is based on candidate K1 of document D((i-1) mod n + 1)
    /// (or Kk when a document has more), with that document as the only source.
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
                string.Create(CultureInfo.InvariantCulture, $"Odpowiedź końcowa {i}."),
                [string.Create(CultureInfo.InvariantCulture, $"{doc}-K{k}")],
                [new SourceJson(doc)]);
        });
}
