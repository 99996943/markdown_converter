using System.ClientModel;
using System.ClientModel.Primitives;
using Azure.AI.OpenAI;
using LegalAgent.Faq.Model;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using OpenAI.Chat;

namespace MBank.FaqGenerator;

/// <summary>Creates the Azure OpenAI chat service and the request settings of each step (research R4, R7).</summary>
internal static class ChatServiceFactory
{
    /// <summary>The connector over an <c>AzureOpenAIClient</c> with the key, a per-request time limit and no retries.</summary>
    /// <param name="settings">Section AzureOpenAI.</param>
    /// <param name="apiKey">The API key; kept only in the credential.</param>
    /// <param name="handler">HTTP handler (tests); <c>null</c> uses the network.</param>
    public static IChatCompletionService Create(AzureOpenAiSettings settings, string apiKey, HttpMessageHandler? handler)
    {
        var options = new AzureOpenAIClientOptions
        {
            NetworkTimeout = TimeSpan.FromSeconds(settings.TimeoutSeconds),
            RetryPolicy = new ClientRetryPolicy(maxRetries: 0),
        };
        if (handler is not null)
        {
            options.Transport = new HttpClientPipelineTransport(new HttpClient(handler, disposeHandler: false));
        }

        var client = new AzureOpenAIClient(new Uri(settings.Endpoint.Trim()), new ApiKeyCredential(apiKey), options);
        return new AzureOpenAIChatCompletionService(settings.Deployment, client, settings.Model);
    }

    /// <summary>Settings of one request: structured output with the step's schema, temperature, seed, output limit.</summary>
    public static PromptExecutionSettings ExecutionSettings(AzureOpenAiSettings settings, FaqStep step, string schema) =>
        new AzureOpenAIPromptExecutionSettings
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                step == FaqStep.Candidates ? "faq_candidates" : "faq_selection",
                BinaryData.FromString(schema),
                jsonSchemaIsStrict: true),
            Temperature = settings.Temperature,
            Seed = settings.Seed,
            MaxTokens = settings.MaxOutputTokens,
        };
}
