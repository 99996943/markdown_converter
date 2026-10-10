using System.Globalization;
using System.Text;
using LegalAgent.Faq.Model;

namespace LegalAgent.Faq;

/// <summary>Checks parsed responses against the rules of data-model.md („Reguły sprawdzania”, research R5).</summary>
internal static class FaqResponseValidator
{
    /// <summary>Checks the candidates of one document.</summary>
    /// <exception cref="FaqResponseException">With all problems found.</exception>
    public static void ValidateCandidates(
        IReadOnlyList<FaqCandidate> candidates,
        string documentId,
        IReadOnlyCollection<string> units,
        int maxCount)
    {
        var problems = new List<string>();
        if (candidates.Count < 1 || candidates.Count > maxCount)
        {
            problems.Add(Invariant($"liczba kandydatów {candidates.Count} poza zakresem 1–{maxCount}"));
        }

        var questions = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < candidates.Count; i++)
        {
            FaqCandidate candidate = candidates[i];
            int position = i + 1;
            CheckTexts(candidate.Question, candidate.Answer, position, questions, problems);
            if (candidate.Unit is { } unit && !UnitMatcher.Matches(unit, units))
            {
                problems.Add(UnknownUnit(position, unit, documentId));
            }
        }

        if (problems.Count > 0)
        {
            throw new FaqResponseException(FaqStep.Candidates, documentId, problems);
        }
    }

    /// <summary>
    /// Checks the selection and returns the numbered items with trimmed texts; the sources of an item come from its
    /// <c>basedOn</c> candidates (T067b).
    /// </summary>
    /// <exception cref="FaqResponseException">With all problems found.</exception>
    public static IReadOnlyList<FaqItem> ValidateSelection(
        IReadOnlyList<ParsedItem> items,
        IReadOnlyList<FaqCandidate> candidates,
        int itemCount,
        int minPerDocument = 0,
        int maxPerDocument = int.MaxValue)
    {
        var problems = new List<string>();
        if (items.Count != itemCount)
        {
            problems.Add(Invariant($"liczba pozycji {items.Count} zamiast {itemCount}"));
        }

        var candidatesById = candidates.ToDictionary(c => c.Id, StringComparer.Ordinal);
        var itemsPerDocument = new OrderedDictionary<string, int>(StringComparer.Ordinal);
        foreach (string documentId in candidates.Select(c => c.DocumentId).Distinct(StringComparer.Ordinal))
        {
            itemsPerDocument[documentId] = 0;
        }

        var questions = new Dictionary<string, int>(StringComparer.Ordinal);
        var result = new List<FaqItem>(items.Count);
        for (int i = 0; i < items.Count; i++)
        {
            ParsedItem item = items[i];
            int position = i + 1;
            CheckTexts(item.Question, item.Answer, position, questions, problems);
            if (item.BasedOn.Count == 0)
            {
                problems.Add(Invariant($"pozycja {position}: puste basedOn"));
            }

            var basedOn = new List<FaqCandidate>(item.BasedOn.Count);
            foreach (string id in item.BasedOn)
            {
                if (candidatesById.TryGetValue(id, out FaqCandidate? candidate))
                {
                    basedOn.Add(candidate);
                }
                else
                {
                    problems.Add(Invariant($"pozycja {position}: kandydat {id} nie istnieje"));
                }
            }

            if (basedOn.Count > 0)
            {
                var numbers = new HashSet<string>(
                    basedOn.SelectMany(c => FaqGrounding.Numbers(c.Answer).Concat(FaqGrounding.Numbers(c.Quote ?? string.Empty))),
                    StringComparer.Ordinal);
                foreach (string number in FaqGrounding.Numbers(item.Answer).Distinct(StringComparer.Ordinal).Where(n => !numbers.Contains(n)))
                {
                    problems.Add(Invariant($"pozycja {position}: liczba „{number}” nie występuje w kandydatach basedOn"));
                }
            }

            result.Add(new FaqItem(position, item.Question.Trim(), item.Answer.Trim(), SourcesOf(basedOn), item.BasedOn));
            foreach (string documentId in basedOn.Select(c => c.DocumentId).Distinct(StringComparer.Ordinal))
            {
                itemsPerDocument[documentId]++;
            }
        }

        foreach ((string documentId, int count) in itemsPerDocument)
        {
            if (count > maxPerDocument)
            {
                problems.Add(Invariant($"dokument {documentId}: {Items(count)} (najwyżej {maxPerDocument})"));
            }
            else if (count < minPerDocument)
            {
                string found = count == 0 ? "brak pozycji" : Items(count);
                problems.Add(Invariant($"dokument {documentId}: {found} (co najmniej {minPerDocument})"));
            }
        }

        if (problems.Count > 0)
        {
            throw new FaqResponseException(FaqStep.Selection, null, problems);
        }

        return result;
    }

    /// <summary>
    /// Document and unit of each candidate, in order, without duplicates; a source without a unit is dropped when the
    /// same document is also a source with a unit.
    /// </summary>
    private static List<FaqSource> SourcesOf(List<FaqCandidate> candidates)
    {
        var withUnit = new HashSet<string>(candidates.Where(c => c.Unit is not null).Select(c => c.DocumentId), StringComparer.Ordinal);
        var sources = new List<FaqSource>();
        foreach (FaqCandidate candidate in candidates)
        {
            var source = new FaqSource(candidate.DocumentId, candidate.Unit);
            if ((source.Unit is not null || !withUnit.Contains(source.DocumentId)) && !sources.Contains(source))
            {
                sources.Add(source);
            }
        }

        return sources;
    }

    /// <summary>Lower case (invariant), collapsed whitespace, trimmed, without a trailing question mark.</summary>
    internal static string NormalizeQuestion(string question)
    {
        var builder = new StringBuilder(question.Length);
        foreach (string word in question.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(word.ToLowerInvariant());
        }

        return builder.ToString().TrimEnd('?').TrimEnd();
    }

    private static void CheckTexts(string question, string answer, int position, Dictionary<string, int> questions, List<string> problems)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            problems.Add(Invariant($"pozycja {position}: puste pytanie"));
        }
        else
        {
            string key = NormalizeQuestion(question);
            if (questions.TryGetValue(key, out int first))
            {
                problems.Add(Invariant($"pozycja {position}: powtórzone pytanie (jak w pozycji {first})"));
            }
            else
            {
                questions[key] = position;
            }
        }

        if (string.IsNullOrWhiteSpace(answer))
        {
            problems.Add(Invariant($"pozycja {position}: pusta odpowiedź"));
        }
    }

    /// <summary>„1 pozycja”, „3 pozycje”, „5 pozycji”.</summary>
    private static string Items(int count) =>
        count == 1 ? "1 pozycja"
        : count % 10 is >= 2 and <= 4 && count % 100 is < 12 or > 14 ? Invariant($"{count} pozycje")
        : Invariant($"{count} pozycji");

    private static string UnknownUnit(int position, string unit, string documentId) =>
        Invariant($"pozycja {position}: jednostka „{unit}” nie występuje w dokumencie {documentId}");

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
