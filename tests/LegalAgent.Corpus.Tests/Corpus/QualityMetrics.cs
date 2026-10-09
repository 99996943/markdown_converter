using LegalAgent.Corpus.Truth;

namespace LegalAgent.Corpus.Tests.Corpus;

/// <summary>Quality measurements of one document: library Markdown against the reference truth (SC-022 – SC-026).</summary>
internal sealed record QualityReport(
    string DocumentId,
    double WordCompleteness,
    int ExtraWords,
    IReadOnlyList<string> ExtraWordSamples,
    double ReadingOrder,
    double HeadingRecall,
    double FalseHeadingShare,
    double ListRecall,
    double RowsIntact,
    double TablesAsSingleGfm,
    double CellAgreement,
    IReadOnlyList<string> Failures);

internal static class QualityMetrics
{
    public static QualityReport Measure(string documentId, DocumentTruth truth, string markdown) =>
        throw new NotImplementedException();
}
