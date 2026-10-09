using LegalAgent.Corpus.Composition;
using LegalAgent.Corpus.Content;
using LegalAgent.Corpus.Planning;

namespace LegalAgent.Corpus.Typesetting;

/// <summary>A composed and typeset document whose page count lies in the requested range.</summary>
/// <param name="Composition">The composition used.</param>
/// <param name="Typeset">Its typesetting.</param>
/// <param name="Iterations">Number of compose-and-typeset rounds it took.</param>
public sealed record FitResult(CompositionResult Composition, TypesetResult Typeset, int Iterations);

/// <summary>
/// Chooses how many optional blocks a document takes so that its page count hits the plan's target within the run's
/// page range (research R6, FR-103): page counts are measured by typesetting, at most <see cref="MaxIterations"/> rounds.
/// </summary>
public static class PageFitter
{
    /// <summary>Upper bound of compose-and-typeset rounds per document.</summary>
    public const int MaxIterations = 8;

    /// <summary>Fits <paramref name="plan"/> into <paramref name="range"/>.</summary>
    public static FitResult Fit(DocumentPlan plan, ContentLibrary content, ulong runSeed, PageRange range)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(range);

        LayoutStyle style = LayoutStyles.Get(plan.Layout);
        var tried = new SortedDictionary<int, (CompositionResult Composition, TypesetResult Typeset)>();
        int Pages(int n)
        {
            if (!tried.TryGetValue(n, out var result))
            {
                CompositionResult composition = DocumentComposer.Compose(plan, content, runSeed, n);
                result = (composition, Typesetter.Typeset(composition.Document, style));
                tried[n] = result;
            }

            return result.Typeset.PageCount;
        }

        int capacity = DocumentComposer.OptionalCapacity(plan, content);
        int most = Pages(capacity);
        if (most < range.Min)
        {
            // Blocks still missing, at the pages one optional block adds on average (FR-103b: a strict run cannot repeat).
            double perBlock = capacity == 0 ? 1 : Math.Max(0.1, (double)(most - Pages(0)) / capacity);
            int missing = (int)Math.Ceiling((range.Min - most) / perBlock);
            throw Unreachable(plan, $"za mało bloków: najwięcej {most} str. przy {capacity} blokach opcjonalnych, wymagane {range.Min}–{range.Max}; brakuje ok. {missing} bloków");
        }

        int least = Pages(0);
        if (least > range.Max)
        {
            throw Unreachable(plan, $"za dużo treści wymaganej: najmniej {least} str., wymagane {range.Min}–{range.Max}");
        }

        int target = Math.Clamp(plan.TargetPages, Math.Max(range.Min, least), Math.Min(range.Max, most));
        int lo = 0;
        int hi = capacity;
        while (tried.Count < MaxIterations && hi - lo > 1 && Pages(lo) != target && Pages(hi) != target)
        {
            // Interpolate between the bracketing counts (pages grow roughly linearly with the blocks added).
            double share = (double)(target - Pages(lo)) / Math.Max(1, Pages(hi) - Pages(lo));
            int n = Math.Clamp(lo + (int)Math.Round(share * (hi - lo)), lo + 1, hi - 1);
            int pages = Pages(n);
            if (pages == target)
            {
                break;
            }

            if (pages < target)
            {
                lo = n;
            }
            else
            {
                hi = n;
            }
        }

        var best = tried
            .Where(t => t.Value.Typeset.PageCount >= range.Min && t.Value.Typeset.PageCount <= range.Max)
            .OrderBy(t => Math.Abs(t.Value.Typeset.PageCount - target))
            .ThenBy(t => t.Key)
            .Select(t => t.Value)
            .FirstOrDefault();
        if (best.Composition is null)
        {
            throw Unreachable(plan, $"po {tried.Count} próbach żadna liczba bloków nie daje {range.Min}–{range.Max} str.");
        }

        return new FitResult(best.Composition, best.Typeset, tried.Count);
    }

    private static CorpusGenerationException Unreachable(DocumentPlan plan, string detail) =>
        new($"Dokument {plan.Id} (szablon {plan.Template}): nieosiągalny zakres stron — {detail}.")
        {
            DocumentId = plan.Id,
            Template = plan.Template,
        };
}
