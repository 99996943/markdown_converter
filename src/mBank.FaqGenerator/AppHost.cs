using Microsoft.SemanticKernel.ChatCompletion;

namespace MBank.FaqGenerator;

/// <summary>
/// External dependencies of <see cref="Program.RunAsync"/> (research R13). The defaults are the production ones; tests
/// replace them with fakes.
/// </summary>
public sealed record AppHost
{
    /// <summary>HTTP handler for the downloads; <c>null</c> uses the network.</summary>
    public HttpMessageHandler? DownloadHandler { get; init; }

    /// <summary>HTTP handler for the Azure OpenAI connector; <c>null</c> uses the network.</summary>
    public HttpMessageHandler? ModelHandler { get; init; }

    /// <summary>Creates the chat service from the API key instead of the Azure OpenAI connector; <c>null</c> uses the connector.</summary>
    public Func<string, IChatCompletionService>? ChatFactory { get; init; }

    /// <summary>Keyboard and redirected input for the API key; <c>null</c> uses the console.</summary>
    public IKeyInput? KeyInput { get; init; }

    /// <summary>Clock for the FAQ timestamp.</summary>
    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;
}
