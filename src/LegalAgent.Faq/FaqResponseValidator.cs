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

    /// <summary>Checks the selection and returns the numbered items with trimmed texts.</summary>
    /// <exception cref="FaqResponseException">With all problems found.</exception>
    public static IReadOnlyList<FaqItem> ValidateSelection(
        IReadOnlyList<ParsedItem> items,
        IReadOnlyList<FaqCandidate> candidates,
        IReadOnlyDictionary<string, IReadOnlyList<string>> unitsByDocument,
        int itemCount) =>
        throw new NotImplementedException();

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

    private static string UnknownUnit(int position, string unit, string documentId) =>
        Invariant($"pozycja {position}: jednostka „{unit}” nie występuje w dokumencie {documentId}");

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
