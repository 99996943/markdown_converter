using System.Globalization;
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
        options.Validate();
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
    public static IReadOnlyList<FaqInputEstimate> CheckInput(IReadOnlyList<FaqDocumentInput> documents, FaqGeneratorOptions options)
    {
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        if (documents.Count == 0)
        {
            throw new ArgumentException("Lista dokumentów jest pusta.", nameof(documents));
        }

        var estimates = new List<FaqInputEstimate>(documents.Count);
        foreach (FaqDocumentInput document in documents)
        {
            ArgumentNullException.ThrowIfNull(document, nameof(documents));
            if (string.IsNullOrWhiteSpace(document.Name))
            {
                throw new ArgumentException("Dokument nie ma nazwy.", nameof(documents));
            }

            if (document.Resource is null || !document.Resource.IsAbsoluteUri)
            {
                throw new ArgumentException($"Adres dokumentu {document.Name} nie jest bezwzględny.", nameof(documents));
            }

            if (string.IsNullOrWhiteSpace(document.Markdown))
            {
                throw new ArgumentException($"Dokument {document.Name} jest pusty.", nameof(documents));
            }

            int characters = document.Markdown.Length;
            int tokens = TokenEstimator.Estimate(characters, options.CharactersPerToken);
            if (tokens > options.MaxDocumentTokens)
            {
                throw new FaqInputTooLongException(document.Name, characters, tokens, options.MaxDocumentTokens);
            }

            estimates.Add(new FaqInputEstimate(document.Name, characters, tokens));
        }

        return estimates;
    }

    /// <summary>Requests candidates for D1…Dn in turn, then the selection; validates every response.</summary>
    /// <param name="documents">Documents in order; they become D1…Dn.</param>
    /// <param name="progress">Receives progress events.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The validated result.</returns>
    /// <exception cref="FaqInputTooLongException">A document is too long.</exception>
    /// <exception cref="FaqServiceException">The service failed.</exception>
    /// <exception cref="FaqResponseException">A response was rejected.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<FaqResult> GenerateAsync(
        IReadOnlyList<FaqDocumentInput> documents,
        IProgress<FaqEvent>? progress = null,
        CancellationToken cancellationToken = default)
    {
        CheckInput(documents, options);
        FaqSourceDocument[] sources =
        [
            .. documents.Select((d, i) => new FaqSourceDocument(string.Create(CultureInfo.InvariantCulture, $"D{i + 1}"), d.Name, d.Resource)),
        ];

        var candidates = new List<FaqCandidate>();
        FaqUsage? total = null;
        for (int i = 0; i < documents.Count; i++)
        {
            FaqSourceDocument source = sources[i];
            string user = FaqPrompts.CandidatesUser(source, documents[i].Markdown, documents[i].Units);
            progress?.Report(new FaqEvent(FaqEventKind.CandidatesStarted, source.Id, user.Length, Estimate(user), null, null));

            (string text, FaqUsage? usage) = await AskAsync(FaqStep.Candidates, source.Id, FaqPrompts.CandidatesSystem(options.CandidatesPerDocument), user, [], cancellationToken)
                .ConfigureAwait(false);
            Parsed<IReadOnlyList<FaqCandidate>> parsed = FaqResponseParser.ParseCandidates(text, source.Id);
            IReadOnlyList<FaqCandidate> accepted = parsed.Value ?? throw new FaqResponseException(FaqStep.Candidates, source.Id, [parsed.Problem!]);
            FaqResponseValidator.ValidateCandidates(accepted, source.Id, documents[i].Units, options.CandidatesPerDocument);
            List<FaqCandidate> grounded = Ground(accepted, source.Id, documents[i].Markdown, progress);
            candidates.AddRange(grounded);
            total = UsageReader.Add(total, usage, first: i == 0);
            progress?.Report(new FaqEvent(FaqEventKind.CandidatesFinished, source.Id, 0, 0, grounded.Count, usage));
        }

        string selectionUser = FaqPrompts.SelectionUser(sources, candidates);
        progress?.Report(new FaqEvent(FaqEventKind.SelectionStarted, null, selectionUser.Length, Estimate(selectionUser), candidates.Count, null));
        (int minPerDocument, int maxPerDocument) = ItemsPerDocument(documents.Count);
        string selectionSystem = FaqPrompts.SelectionSystem(options.ItemCount, minPerDocument, maxPerDocument);
        (string selectionText, FaqUsage? selectionUsage) = await AskAsync(FaqStep.Selection, null, selectionSystem, selectionUser, [], cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<FaqItem> result;
        try
        {
            result = Select(selectionText, candidates, minPerDocument, maxPerDocument);
        }
        catch (FaqResponseException rejected)
        {
            // One correction (T067i): the same conversation, the rejected answer and its problems.
            progress?.Report(new FaqEvent(FaqEventKind.SelectionCorrection, null, 0, 0, null, null, string.Join("; ", rejected.Problems)));
            ChatMessageContent[] correction =
            [
                new(AuthorRole.Assistant, selectionText),
                new(AuthorRole.User, FaqPrompts.SelectionCorrection(rejected.Problems, options.ItemCount)),
            ];
            (string correctedText, FaqUsage? correctedUsage) = await AskAsync(FaqStep.Selection, null, selectionSystem, selectionUser, correction, cancellationToken)
                .ConfigureAwait(false);
            selectionUsage = UsageReader.Add(selectionUsage, correctedUsage, first: false);
            result = Select(correctedText, candidates, minPerDocument, maxPerDocument);
        }

        total = UsageReader.Add(total, selectionUsage, first: false);
        progress?.Report(new FaqEvent(FaqEventKind.SelectionFinished, null, 0, 0, result.Count, selectionUsage));
        return new FaqResult(result, sources, candidates, total);
    }

    /// <summary>
    /// Candidates grounded in their units (T067d); the others are dropped and reported. The response is rejected when
    /// none is left.
    /// </summary>
    private static List<FaqCandidate> Ground(IReadOnlyList<FaqCandidate> candidates, string documentId, string markdown, IProgress<FaqEvent>? progress)
    {
        var grounded = new List<FaqCandidate>(candidates.Count);
        var problems = new List<string>();
        foreach (FaqCandidate candidate in candidates)
        {
            if (FaqGrounding.Problem(candidate, markdown) is not { } problem)
            {
                grounded.Add(candidate);
                continue;
            }

            problems.Add(problem);
            progress?.Report(new FaqEvent(FaqEventKind.CandidateDropped, documentId, 0, 0, null, null, problem));
        }

        return grounded.Count > 0 ? grounded : throw new FaqResponseException(FaqStep.Candidates, documentId, problems);
    }

    private int Estimate(string text) => TokenEstimator.Estimate(text.Length, options.CharactersPerToken);

    /// <summary>The parsed and validated selection.</summary>
    /// <exception cref="FaqResponseException">The text is not JSON of the schema or breaks the rules.</exception>
    private IReadOnlyList<FaqItem> Select(string text, IReadOnlyList<FaqCandidate> candidates, int minPerDocument, int maxPerDocument)
    {
        Parsed<IReadOnlyList<ParsedItem>> selection = FaqResponseParser.ParseSelection(text);
        IReadOnlyList<ParsedItem> items = selection.Value ?? throw new FaqResponseException(FaqStep.Selection, null, [selection.Problem!]);
        return FaqResponseValidator.ValidateSelection(items, candidates, options.ItemCount, minPerDocument, maxPerDocument);
    }

    /// <summary>
    /// Effective limits of items per document (T067j): the maximum covers ItemCount, the minimum is dropped when the
    /// documents cannot all get it.
    /// </summary>
    private (int Min, int Max) ItemsPerDocument(int documentCount)
    {
        int max = Math.Max(options.MaxItemsPerDocument, (options.ItemCount + documentCount - 1) / documentCount);
        int min = documentCount * options.MinItemsPerDocument <= options.ItemCount ? options.MinItemsPerDocument : 0;
        return (min, max);
    }

    /// <summary>
    /// One request: system and user message, then <paramref name="followUp"/> (the correction exchange), settings for the
    /// step; returns the text and usage of the first message.
    /// </summary>
    private async Task<(string Text, FaqUsage? Usage)> AskAsync(
        FaqStep step,
        string? documentId,
        string system,
        string user,
        IReadOnlyList<ChatMessageContent> followUp,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var history = new ChatHistory();
        history.AddSystemMessage(system);
        history.AddUserMessage(user);
        history.AddRange(followUp);
        PromptExecutionSettings settings = executionSettings(step, step == FaqStep.Candidates ? FaqSchemas.Candidates : FaqSchemas.Selection);
        IReadOnlyList<ChatMessageContent> messages;
        try
        {
            messages = await chat.GetChatMessageContentsAsync(history, settings, null, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (ServiceErrorMapper.Map(e, step, documentId, cancellationToken) is { } mapped)
        {
            // No retries (FR-424): the first failure ends the generation.
            throw mapped;
        }
        return messages.Count > 0 ? (messages[0].Content ?? string.Empty, UsageReader.Read(messages[0].Metadata)) : (string.Empty, null);
    }
}
