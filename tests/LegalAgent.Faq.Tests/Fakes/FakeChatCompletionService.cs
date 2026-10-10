using System.Collections.Concurrent;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace LegalAgent.Faq.Tests.Fakes;

/// <summary>A request the fake model received.</summary>
/// <param name="History">Copy of the chat history.</param>
/// <param name="Settings">The execution settings.</param>
internal sealed record ChatCall(IReadOnlyList<ChatMessageContent> History, PromptExecutionSettings? Settings)
{
    /// <summary>Text of the system message.</summary>
    public string System => History.First(m => m.Role == AuthorRole.System).Content ?? string.Empty;

    /// <summary>Text of the user message.</summary>
    public string User => History.First(m => m.Role == AuthorRole.User).Content ?? string.Empty;
}

/// <summary>Usage metadata in the shape of OpenAI.Chat.ChatTokenUsage.</summary>
internal sealed record TestUsage(int InputTokenCount, int OutputTokenCount);

/// <summary>Usage metadata in the older shape (prompt/completion).</summary>
internal sealed record LegacyTestUsage(int PromptTokens, int CompletionTokens);

/// <summary>
/// Scripted <see cref="IChatCompletionService"/>: every call takes the next step (a response text with optional usage, an
/// exception or a wait for cancellation). Records every call and the highest number of concurrent calls.
/// </summary>
internal sealed class FakeChatCompletionService : IChatCompletionService
{
    private readonly ConcurrentQueue<Func<CancellationToken, Task<ChatMessageContent>>> steps = new();
    private readonly ConcurrentQueue<ChatCall> calls = new();
    private int running;
    private int maxConcurrency;

    /// <summary>Calls received so far, in order.</summary>
    public IReadOnlyList<ChatCall> Calls => [.. calls];

    /// <summary>Highest number of calls running at the same time.</summary>
    public int MaxConcurrency => maxConcurrency;

    /// <summary>Key passed to the factory that created this service (application tests).</summary>
    public string? ReceivedKey { get; set; }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, object?> Attributes { get; } = new Dictionary<string, object?>();

    /// <summary>Answers calls when no step is scripted; <c>null</c> makes such a call fail.</summary>
    public Func<ChatCall, string>? Fallback { get; set; }

    /// <summary>Usage metadata of the fallback answers.</summary>
    public object? FallbackUsage { get; set; }

    /// <summary>Answers the next call with the text and optional usage metadata.</summary>
    public FakeChatCompletionService Respond(string text, object? usage = null)
    {
        steps.Enqueue(async _ =>
        {
            await Task.Yield();
            var metadata = usage is null ? null : new Dictionary<string, object?> { ["Usage"] = usage };
            return new ChatMessageContent(AuthorRole.Assistant, text, "fake-model", null, null, metadata);
        });
        return this;
    }

    /// <summary>Throws the exception on the next call.</summary>
    public FakeChatCompletionService Fail(Exception exception)
    {
        steps.Enqueue(_ => Task.FromException<ChatMessageContent>(exception));
        return this;
    }

    /// <summary>The next call waits until cancelled; <paramref name="started"/> is set when it begins.</summary>
    public FakeChatCompletionService Hang(TaskCompletionSource? started = null)
    {
        steps.Enqueue(async ct =>
        {
            started?.TrySetResult();
            await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
            throw new InvalidOperationException("unreachable");
        });
        return this;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chatHistory);
        calls.Enqueue(new ChatCall([.. chatHistory], executionSettings));
        int now = Interlocked.Increment(ref running);
        InterlockedMax(ref maxConcurrency, now);
        try
        {
            if (!steps.TryDequeue(out Func<CancellationToken, Task<ChatMessageContent>>? step))
            {
                if (Fallback is null)
                {
                    throw new InvalidOperationException("Atrapa modelu: brak zaprogramowanej odpowiedzi.");
                }

                var metadata = FallbackUsage is null ? null : new Dictionary<string, object?> { ["Usage"] = FallbackUsage };
                return [new ChatMessageContent(AuthorRole.Assistant, Fallback(calls.Last()), "fake-model", null, null, metadata)];
            }

            return [await step(cancellationToken).ConfigureAwait(false)];
        }
        finally
        {
            Interlocked.Decrement(ref running);
        }
    }

    /// <inheritdoc />
    public IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        while ((current = Volatile.Read(ref target)) < value)
        {
            if (Interlocked.CompareExchange(ref target, value, current) == current)
            {
                return;
            }
        }
    }
}
