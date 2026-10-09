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
    public static FitResult Fit(DocumentPlan plan, ContentLibrary content, ulong runSeed, PageRange range) =>
        throw new NotImplementedException();
}
