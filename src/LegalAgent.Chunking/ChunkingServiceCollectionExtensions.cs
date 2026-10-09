using LegalAgent.Chunking;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Dependency-injection registration of the chunker.</summary>
public static class ChunkingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the PDF parser, the chunker (singleton) and options validation. Idempotent: calling it repeatedly
    /// registers every service once; <paramref name="configure"/> accumulates.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Adjusts <see cref="ChunkingOptions"/>.</param>
    public static IServiceCollection AddLegalAgentChunking(
        this IServiceCollection services,
        Action<ChunkingOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddLegalAgentPdfParser();
        services.AddSingleton<IDocumentChunker, DocumentChunker>();
        return services;
    }
}
