using LegalAgent.Faq.Model;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace MBank.FaqGenerator;

/// <summary>Creates the Azure OpenAI chat service and the request settings of each step (research R4, R7).</summary>
internal static class ChatServiceFactory
{
    /// <summary>The connector over an <c>AzureOpenAIClient</c> with the key, a per-request time limit and no retries.</summary>
    /// <param name="settings">Section AzureOpenAI.</param>
    /// <param name="apiKey">The API key; kept only in the credential.</param>
    /// <param name="handler">HTTP handler (tests); <c>null</c> uses the network.</param>
    public static IChatCompletionService Create(AzureOpenAiSettings settings, string apiKey, HttpMessageHandler? handler) =>
        throw new NotImplementedException();

    /// <summary>Settings of one request: structured output with the step's schema, temperature, seed, output limit.</summary>
    public static PromptExecutionSettings ExecutionSettings(AzureOpenAiSettings settings, FaqStep step, string schema) =>
        throw new NotImplementedException();
}
