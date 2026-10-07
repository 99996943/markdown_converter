using LegalAgent.PdfParser.Model;
using LegalAgent.PdfParser.Pipeline;
using LegalAgent.PdfParser.Rendering;
using LegalAgent.PdfParser.Stages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LegalAgent.PdfParser.Tests.Integration;

/// <summary>US5: registration through <c>AddLegalAgentPdfParser</c> and the stage-customisation helpers.</summary>
public sealed class DependencyInjectionTests
{
    private static readonly int BuiltInCount = BuiltInStages.Create().Count;

    private static async Task<PdfConversionResult> ConvertAsync(
        ServiceProvider provider,
        PdfConversionRequest? request = null)
    {
        using var stream = new MemoryStream(ArtifactCleanupIntegrationTests.BuildUs1Pdf());
        return await provider.GetRequiredService<IPdfMarkdownConverter>()
            .ConvertAsync(stream, request, TestContext.Current.CancellationToken);
    }

    [Fact]
    public void Add_ResolvesConverterAndRendererAsSingletons()
    {
        using ServiceProvider provider = new ServiceCollection().AddLegalAgentPdfParser().BuildServiceProvider();

        Assert.Same(provider.GetRequiredService<IPdfMarkdownConverter>(), provider.GetRequiredService<IPdfMarkdownConverter>());
        Assert.Same(provider.GetRequiredService<IMarkdownRenderer>(), provider.GetRequiredService<IMarkdownRenderer>());
        Assert.IsType<PdfMarkdownConverter>(provider.GetRequiredService<IPdfMarkdownConverter>());
    }

    [Fact]
    public void Add_CalledTwice_RegistersEachBuiltInStageOnce()
    {
        using ServiceProvider provider = new ServiceCollection()
            .AddLegalAgentPdfParser()
            .AddLegalAgentPdfParser()
            .BuildServiceProvider();

        IPipelineStage[] stages = provider.GetServices<IPipelineStage>().ToArray();

        Assert.Equal(BuiltInCount, stages.Length);
        Assert.Equal(BuiltInCount, stages.Select(s => s.GetType()).Distinct().Count());
        Assert.Equal(BuiltInStages.Create().Select(s => s.GetType()).OrderBy(t => t.FullName, StringComparer.Ordinal),
            stages.Select(s => s.GetType()).OrderBy(t => t.FullName, StringComparer.Ordinal));
    }

    [Fact]
    public void Add_ConfigureDelegateAndServicesConfigure_BothApplied()
    {
        var services = new ServiceCollection();
        services.AddLegalAgentPdfParser(o => o.Artifacts.MinPages = 2);
        services.Configure<PdfParserOptions>(o => o.Limits.MaxInputBytes = 12345);
        using ServiceProvider provider = services.BuildServiceProvider();

        PdfParserOptions options = provider.GetRequiredService<IOptions<PdfParserOptions>>().Value;

        Assert.Equal(2, options.Artifacts.MinPages);
        Assert.Equal(12345, options.Limits.MaxInputBytes);
    }

    [Fact]
    public async Task Add_InvalidOptions_ThrowsOptionsValidationExceptionOnFirstConversion()
    {
        var services = new ServiceCollection().AddLegalAgentPdfParser(o => o.Limits.MaxDuration = TimeSpan.Zero);
        using ServiceProvider provider = services.BuildServiceProvider();

        await Assert.ThrowsAsync<OptionsValidationException>(() => ConvertAsync(provider));
    }

    [Fact]
    public async Task AddStage_WithOrder450_RunsBetweenArtifactRemovalAndFootnotes()
    {
        var services = new ServiceCollection();
        services.AddSingleton<List<string>>();
        services.AddLegalAgentPdfParser().AddPdfParserStage<RecordingStage>();
        using ServiceProvider provider = services.BuildServiceProvider();

        IPipelineStage[] ordered = new PipelineRunner(provider.GetServices<IPipelineStage>()).Stages.ToArray();
        int index = Array.FindIndex(ordered, s => s is RecordingStage);
        Assert.IsType<ArtifactRemovalStage>(ordered[index - 1]);
        Assert.True(ordered[index + 1].Order > 450);

        await ConvertAsync(provider);

        Assert.Equal(["recording"], provider.GetRequiredService<List<string>>());
    }

    [Fact]
    public async Task ReplaceStage_TakesEffect()
    {
        using ServiceProvider defaults = new ServiceCollection().AddLegalAgentPdfParser().BuildServiceProvider();
        using ServiceProvider replaced = new ServiceCollection()
            .AddLegalAgentPdfParser()
            .ReplacePdfParserStage<ArtifactRemovalStage, NoOpStage>()
            .BuildServiceProvider();

        PdfConversionResult withRemoval = await ConvertAsync(defaults);
        PdfConversionResult withoutRemoval = await ConvertAsync(replaced);

        Assert.DoesNotContain("Dziennik Ustaw", withRemoval.Markdown, StringComparison.Ordinal);
        Assert.Contains("Dziennik Ustaw", withoutRemoval.Markdown, StringComparison.Ordinal);
        Assert.Equal(BuiltInCount, replaced.GetServices<IPipelineStage>().Count());
        Assert.DoesNotContain(replaced.GetServices<IPipelineStage>(), s => s is ArtifactRemovalStage);
    }

    [Fact]
    public async Task RemoveStage_TakesEffect()
    {
        var services = new ServiceCollection();
        services.AddSingleton<List<string>>();
        services.AddLegalAgentPdfParser()
            .AddPdfParserStage<RecordingStage>()
            .RemovePdfParserStage<RecordingStage>()
            .RemovePdfParserStage<ReadingOrderStage>();
        using ServiceProvider provider = services.BuildServiceProvider();

        IPipelineStage[] stages = provider.GetServices<IPipelineStage>().ToArray();

        Assert.Equal(BuiltInCount - 1, stages.Length);
        Assert.DoesNotContain(stages, s => s is RecordingStage or ReadingOrderStage);

        await ConvertAsync(provider);
        Assert.Empty(provider.GetRequiredService<List<string>>());
    }

    [Fact]
    public async Task ConfigureOptions_AffectsOnlyThatCall()
    {
        using ServiceProvider provider = new ServiceCollection().AddLegalAgentPdfParser().BuildServiceProvider();

        PdfConversionResult noMarkers = await ConvertAsync(
            provider,
            new PdfConversionRequest { ConfigureOptions = o => o.Rendering.PageMarkers = false });
        PdfConversionResult defaults = await ConvertAsync(provider);

        Assert.DoesNotContain("<!--", noMarkers.Markdown, StringComparison.Ordinal);
        Assert.Contains("<!--", defaults.Markdown, StringComparison.Ordinal);
        Assert.True(provider.GetRequiredService<IOptions<PdfParserOptions>>().Value.Rendering.PageMarkers);
    }

    private sealed class RecordingStage(List<string> log) : IPipelineStage
    {
        public int Order => 450;

        public void Execute(PipelineContext context) => log.Add("recording");
    }

    private sealed class NoOpStage : IPipelineStage
    {
        public int Order => StageOrder.ArtifactRemoval;

        public void Execute(PipelineContext context)
        {
        }
    }
}
