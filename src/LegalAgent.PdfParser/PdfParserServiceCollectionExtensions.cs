using LegalAgent.PdfParser;
using LegalAgent.PdfParser.Pipeline;
using LegalAgent.PdfParser.Rendering;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Dependency-injection registration of the PDF parser and its pipeline stages.</summary>
public static class PdfParserServiceCollectionExtensions
{
    /// <summary>
    /// Registers the converter, renderer, all built-in pipeline stages (singletons) and options validation.
    /// Idempotent: calling it repeatedly registers every service once; <paramref name="configure"/> accumulates.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Adjusts <see cref="PdfParserOptions"/>.</param>
    public static IServiceCollection AddLegalAgentPdfParser(
        this IServiceCollection services,
        Action<PdfParserOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<PdfParserOptions>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<PdfParserOptions>, PdfParserOptionsValidator>());
        services.TryAddSingleton<IMarkdownRenderer, MarkdownRenderer>();
        services.TryAddSingleton<IPdfMarkdownConverter, PdfMarkdownConverter>();

        foreach (Type stageType in BuiltInStages.Create().Select(s => s.GetType()))
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(IPipelineStage), stageType));
        }

        if (configure is not null)
        {
            services.Configure(configure);
        }

        return services;
    }

    /// <summary>Adds a custom pipeline stage (singleton); execution order follows <see cref="IPipelineStage.Order"/>.</summary>
    /// <typeparam name="TStage">The stage type.</typeparam>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddPdfParserStage<TStage>(this IServiceCollection services)
        where TStage : class, IPipelineStage
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPipelineStage, TStage>());
        return services;
    }

    /// <summary>Replaces a built-in or previously added stage with another one.</summary>
    /// <typeparam name="TExisting">The stage to remove.</typeparam>
    /// <typeparam name="TReplacement">The stage to add.</typeparam>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection ReplacePdfParserStage<TExisting, TReplacement>(this IServiceCollection services)
        where TExisting : class, IPipelineStage
        where TReplacement : class, IPipelineStage
    {
        ArgumentNullException.ThrowIfNull(services);
        services.RemovePdfParserStage<TExisting>();
        return services.AddPdfParserStage<TReplacement>();
    }

    /// <summary>Removes a stage, e.g. to disable table detection without changing options.</summary>
    /// <typeparam name="TStage">The stage type.</typeparam>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection RemovePdfParserStage<TStage>(this IServiceCollection services)
        where TStage : class, IPipelineStage
    {
        ArgumentNullException.ThrowIfNull(services);
        for (int i = services.Count - 1; i >= 0; i--)
        {
            ServiceDescriptor d = services[i];
            if (d.ServiceType == typeof(IPipelineStage) && d.ImplementationType == typeof(TStage))
            {
                services.RemoveAt(i);
            }
        }

        return services;
    }
}
