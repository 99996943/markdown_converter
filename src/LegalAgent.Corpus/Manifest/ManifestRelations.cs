using LegalAgent.Corpus.Content;
using LegalAgent.Corpus.Planning;
using LegalAgent.Corpus.Typesetting;

namespace LegalAgent.Corpus.Manifest;

/// <summary>Changes against the previous version and contradictions with other documents (FR-141).</summary>
public static class ManifestRelations
{
    /// <summary>The facts whose values differ between <paramref name="previous"/> and <paramref name="next"/>.</summary>
    public static IReadOnlyList<VersionChange> Changes(ContentLibrary content, DocumentPlan next, FitResult nextFit, DocumentPlan previous, FitResult previousFit) =>
        throw new NotImplementedException();

    /// <summary>The planted contradictions of <paramref name="doc"/>, with the values here and in the other document.</summary>
    public static IReadOnlyList<Contradiction> Contradictions(ContentLibrary content, DocumentPlan doc, FitResult fit, IReadOnlyList<DocumentPlan> plan) =>
        throw new NotImplementedException();
}
