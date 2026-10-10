using LegalAgent.Faq.Model;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace LegalAgent.Faq;

/// <summary>
/// Generates an FAQ from a set of documents in two steps (research R3): candidates for every document in turn, then one
/// selection request over all candidates. Every response is parsed and validated; nothing is retried.
/// </summary>
public sealed class FaqGenerator
{
    private readonly IChatCompletionService chat;
    private readonly FaqGeneratorOptions options;
    private readonly Func<FaqStep, string, PromptExecutionSettings> executionSettings;

    /// <summary>Creates the generator.</summary>
    /// <param name="chat">Chat completion service.</param>
    /// <param name="options">Options.</param>
    /// <param name="executionSettings">Builds the request settings for a step and its JSON schema (<see cref="FaqSchemas"/>).</param>
    /// <exception cref="ArgumentException">The options are invalid.</exception>
    public FaqGenerator(
        IChatCompletionService chat,
        FaqGeneratorOptions options,
        Func<FaqStep, string, PromptExecutionSettings> executionSettings)
    {
        ArgumentNullException.ThrowIfNull(chat);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(executionSettings);
        this.chat = chat;
        this.options = options;
        this.executionSettings = executionSettings;
    }

    /// <summary>Checks the size of every document without calling the model (before the key is asked for).</summary>
    /// <param name="documents">Documents in order.</param>
    /// <param name="options">Options.</param>
    /// <returns>The size of every document.</returns>
    /// <exception cref="ArgumentException">The list is empty or a document is invalid.</exception>
    /// <exception cref="FaqInputTooLongException">The first document over <see cref="FaqGeneratorOptions.MaxDocumentTokens"/>.</exception>
    public static IReadOnlyList<FaqInputEstimate> CheckInput(IReadOnlyList<FaqDocumentInput> documents, FaqGeneratorOptions options) =>
        throw new NotImplementedException();

    /// <summary>Requests candidates for D1…Dn in turn, then the selection; validates every response.</summary>
    /// <param name="documents">Documents in order; they become D1…Dn.</param>
    /// <param name="progress">Receives progress events.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The validated result.</returns>
    /// <exception cref="FaqInputTooLongException">A document is too long.</exception>
    /// <exception cref="FaqServiceException">The service failed.</exception>
    /// <exception cref="FaqResponseException">A response was rejected.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public Task<FaqResult> GenerateAsync(
        IReadOnlyList<FaqDocumentInput> documents,
        IProgress<FaqEvent>? progress = null,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}
