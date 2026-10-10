using System.Globalization;
using System.Text;
using LegalAgent.Faq.Model;

namespace LegalAgent.Faq;

/// <summary>Checks parsed responses against the rules of data-model.md („Reguły sprawdzania”, research R5).</summary>
internal static class FaqResponseValidator
{
    /// <summary>
    /// Checks the candidates of one document: count, texts, repeated questions. Units are checked per candidate by
    /// <see cref="FaqGrounding"/> (T067n).
    /// </summary>
    /// <exception cref="FaqResponseException">With all problems found.</exception>
    public static void ValidateCandidates(
        IReadOnlyList<FaqCandidate> candidates,
        string documentId,
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
        }

        if (problems.Count > 0)
        {
            throw new FaqResponseException(FaqStep.Candidates, documentId, problems);
        }
    }

    /// <summary>
    /// Checks the ranked selection pool and chooses the final items (T067l): invalid items are skipped; every document
    /// first gets its best-ranked items up to <paramref name="minPerDocument"/>, then items are taken in the model's
    /// order while no document exceeds <paramref name="maxPerDocument"/>. The chosen items keep the model's order and are
    /// numbered 1…<paramref name="itemCount"/>; their sources come from their <c>basedOn</c> candidates (T067b).
    /// </summary>
    /// <exception cref="FaqResponseException">Fewer than <paramref name="itemCount"/> items can be chosen, or a document lacks its minimum.</exception>
    public static SelectionResult ValidateSelection(
        IReadOnlyList<ParsedItem> items,
        IReadOnlyList<FaqCandidate> candidates,
        int itemCount,
        int minPerDocument = 0,
        int maxPerDocument = int.MaxValue)
    {
        var candidatesById = candidates.ToDictionary(c => c.Id, StringComparer.Ordinal);
        string[] documents = [.. candidates.Select(c => c.DocumentId).Distinct(StringComparer.Ordinal)];
        var questions = new Dictionary<string, int>(StringComparer.Ordinal);
        var valid = new List<(FaqItem Item, string[] Documents)>();
        var skipped = new List<string>();
        for (int i = 0; i < items.Count; i++)
        {
            int position = i + 1;
            List<string> itemProblems = Check(items[i], position, candidatesById, questions, out List<FaqCandidate> basedOn);
            if (itemProblems.Count > 0)
            {
                skipped.AddRange(itemProblems);
                continue;
            }

            questions[NormalizeQuestion(items[i].Question)] = position;
            var item = new FaqItem(position, items[i].Question.Trim(), items[i].Answer.Trim(), SourcesOf(basedOn), items[i].BasedOn);
            valid.Add((item, [.. basedOn.Select(c => c.DocumentId).Distinct(StringComparer.Ordinal)]));
        }

        var problems = new List<string>();
        if (valid.Count < itemCount)
        {
            problems.Add(Invariant($"liczba poprawnych pozycji {valid.Count}, potrzeba co najmniej {itemCount}"));
        }
        else
        {
            bool[] chosen = Choose(valid, documents, itemCount, minPerDocument, maxPerDocument, problems);
            if (problems.Count == 0)
            {
                FaqItem[] result =
                [
                    .. valid.Where((_, k) => chosen[k]).Select((v, k) => v.Item with { Number = k + 1 }),
                ];
                return new SelectionResult(result, skipped);
            }
        }

        problems.AddRange(skipped);
        throw new FaqResponseException(FaqStep.Selection, null, problems);
    }

    /// <summary>Problems of one pool item; the duplicate check uses the questions of the valid items before it.</summary>
    private static List<string> Check(
        ParsedItem item,
        int position,
        Dictionary<string, FaqCandidate> candidatesById,
        Dictionary<string, int> questions,
        out List<FaqCandidate> basedOn)
    {
        var problems = new List<string>();
        if (string.IsNullOrWhiteSpace(item.Question))
        {
            problems.Add(Invariant($"pozycja {position}: puste pytanie"));
        }
        else if (questions.TryGetValue(NormalizeQuestion(item.Question), out int first))
        {
            problems.Add(Invariant($"pozycja {position}: powtórzone pytanie (jak w pozycji {first})"));
        }

        if (string.IsNullOrWhiteSpace(item.Answer))
        {
            problems.Add(Invariant($"pozycja {position}: pusta odpowiedź"));
        }

        if (item.BasedOn.Count == 0)
        {
            problems.Add(Invariant($"pozycja {position}: puste basedOn"));
        }

        basedOn = new List<FaqCandidate>(item.BasedOn.Count);
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

        return problems;
    }

    /// <summary>Which valid items are chosen; adds a problem for every document below its minimum and when N cannot be reached.</summary>
    private static bool[] Choose(
        List<(FaqItem Item, string[] Documents)> valid,
        string[] documents,
        int itemCount,
        int minPerDocument,
        int maxPerDocument,
        List<string> problems)
    {
        bool[] chosen = new bool[valid.Count];
        var counts = documents.ToDictionary(d => d, _ => 0, StringComparer.Ordinal);
        int total = 0;
        bool CanTake(int k) => !chosen[k] && total < itemCount && valid[k].Documents.All(d => counts[d] < maxPerDocument);
        void Take(int k)
        {
            chosen[k] = true;
            total++;
            foreach (string d in valid[k].Documents)
            {
                counts[d]++;
            }
        }

        foreach (string document in documents)
        {
            for (int k = 0; k < valid.Count && counts[document] < minPerDocument; k++)
            {
                if (valid[k].Documents.Contains(document, StringComparer.Ordinal) && CanTake(k))
                {
                    Take(k);
                }
            }

            if (counts[document] < minPerDocument)
            {
                string found = counts[document] == 0 ? "brak pozycji" : Items(counts[document]);
                problems.Add(Invariant($"dokument {document}: {found} (co najmniej {minPerDocument})"));
            }
        }

        for (int k = 0; k < valid.Count && total < itemCount; k++)
        {
            if (CanTake(k))
            {
                Take(k);
            }
        }

        if (total < itemCount)
        {
            problems.Add(Invariant($"po zastosowaniu limitu {maxPerDocument} pozycji na dokument zostają {total} z {itemCount} pozycji"));
        }

        return chosen;
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

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
