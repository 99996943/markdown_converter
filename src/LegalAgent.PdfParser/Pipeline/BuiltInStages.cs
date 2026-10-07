using LegalAgent.PdfParser.Stages;

namespace LegalAgent.PdfParser.Pipeline;

/// <summary>The built-in pipeline stages in their default configuration.</summary>
internal static class BuiltInStages
{
    public static IReadOnlyList<IPipelineStage> Create() =>
    [
        new PageExtractionStage(),
        new TextNormalizationStage(),
        new LineAssemblyStage(),
        new ArtifactRemovalStage(),
        new ReadingOrderStage(),
        new BlockAssemblyStage(),
        new DocumentBuildStage(),
    ];
}
