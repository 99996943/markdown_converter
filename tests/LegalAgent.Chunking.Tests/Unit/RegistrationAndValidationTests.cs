using LegalAgent.Chunking.Model;
using LegalAgent.Chunking.Tests.Fixtures;
using LegalAgent.PdfParser;
using LegalAgent.PdfParser.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LegalAgent.Chunking.Tests.Unit;

/// <summary>T004: dependency injection, options validation and metadata validation (FR-201, FR-206, FR-210).</summary>
public sealed class RegistrationAndValidationTests
{
    private static readonly PdfConversionResult Empty = Doc.Result(Doc.Document(null, null));

    [Fact]
    public void AddLegalAgentChunking_RegistersChunkerAsSingletonAndTheParser()
    {
        using ServiceProvider provider = new ServiceCollection().AddLegalAgentChunking().BuildServiceProvider();

        IDocumentChunker? first = provider.GetService<IDocumentChunker>();
        Assert.NotNull(first);
        Assert.Same(first, provider.GetService<IDocumentChunker>());
        Assert.NotNull(provider.GetService<IPdfMarkdownConverter>());
    }

    [Fact]
    public void AddLegalAgentChunking_IsIdempotentAndConfigureAccumulates()
    {
        var services = new ServiceCollection();
        services.AddLegalAgentChunking(o => o.MaxChunkLength = 1500);
        services.AddLegalAgentChunking(o => o.MaxChunkLength += 1);

        Assert.Single(services, d => d.ServiceType == typeof(IDocumentChunker));
        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.Equal(1501, provider.GetRequiredService<IOptions<ChunkingOptions>>().Value.MaxChunkLength);
    }

    [Fact]
    public async Task ChunkAsync_ConfiguredLimitBelowMinimum_ThrowsOptionsValidationException()
    {
        IDocumentChunker chunker = Chunker(o => o.MaxChunkLength = 199);

        await Assert.ThrowsAsync<OptionsValidationException>(() => chunker.ChunkAsync(Empty, new DocumentMetadata("REG-01"), cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ChunkAsync_RequestOptionsChangeOnlyThatCall()
    {
        using ServiceProvider provider = new ServiceCollection().AddLegalAgentChunking().BuildServiceProvider();
        IDocumentChunker chunker = provider.GetRequiredService<IDocumentChunker>();
        var metadata = new DocumentMetadata("REG-01");
        var tooSmall = new ChunkingRequest { ConfigureOptions = o => o.MaxChunkLength = 199 };

        await Assert.ThrowsAsync<OptionsValidationException>(() => chunker.ChunkAsync(Empty, metadata, tooSmall, TestContext.Current.CancellationToken));

        ChunkedDocument result = await chunker.ChunkAsync(Empty, metadata, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Empty(result.Chunks);
        Assert.Equal(2000, provider.GetRequiredService<IOptions<ChunkingOptions>>().Value.MaxChunkLength);
    }

    [Theory]
    [InlineData("")]
    [InlineData("-REG")]
    [InlineData(".REG")]
    [InlineData("REG 05")]
    [InlineData("REG/05")]
    [InlineData("rachunek-płatniczy")]
    public async Task ChunkAsync_InvalidDocumentId_ThrowsArgumentException(string id)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => Chunker().ChunkAsync(Empty, new DocumentMetadata(id), cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ChunkAsync_DocumentIdLongerThan100Characters_ThrowsArgumentException()
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => Chunker().ChunkAsync(Empty, new DocumentMetadata(new string('a', 101)), cancellationToken: TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ChunkAsync_VersionNotPositive_ThrowsArgumentException(int version)
    {
        var metadata = new DocumentMetadata("REG-01") { Version = version };

        await Assert.ThrowsAnyAsync<ArgumentException>(() => Chunker().ChunkAsync(Empty, metadata, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ChunkAsync_ValidToBeforeValidFrom_ThrowsArgumentException()
    {
        var metadata = new DocumentMetadata("REG-01") { ValidFrom = new DateOnly(2025, 6, 1), ValidTo = new DateOnly(2025, 5, 31) };

        await Assert.ThrowsAnyAsync<ArgumentException>(() => Chunker().ChunkAsync(Empty, metadata, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("REG-05-w1")]
    [InlineData("dz-u-2019-1781-ochrona-danych")]
    [InlineData("ZAT_01.v2")]
    public async Task ChunkAsync_ValidMetadata_ReturnsHeaderFromMetadata(string id)
    {
        var metadata = new DocumentMetadata(id)
        {
            Version = 1,
            ValidFrom = new DateOnly(2025, 6, 1),
            ValidTo = new DateOnly(2025, 6, 1),
        };

        ChunkedDocument result = await Chunker().ChunkAsync(Empty, metadata, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Same(metadata, result.Header.Metadata);
        Assert.Equal(id, result.Header.SeriesKey);
        Assert.Empty(result.Chunks);
    }

    private static IDocumentChunker Chunker(Action<ChunkingOptions>? configure = null) =>
        new ServiceCollection().AddLegalAgentChunking(configure).BuildServiceProvider().GetRequiredService<IDocumentChunker>();
}
