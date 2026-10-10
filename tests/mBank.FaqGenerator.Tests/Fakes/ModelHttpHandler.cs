using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;

namespace MBank.FaqGenerator.Tests.Fakes;

/// <summary>A request the fake Azure OpenAI service received.</summary>
/// <param name="Address">Requested address.</param>
/// <param name="ApiKey">The <c>api-key</c> header, if any.</param>
/// <param name="Body">Request body.</param>
internal sealed record ModelRequest(Uri Address, string? ApiKey, string Body)
{
    /// <summary>The body as JSON.</summary>
    public JsonElement Json => JsonDocument.Parse(Body).RootElement;
}

/// <summary>Scripted chat-completions endpoint for the real connector; every request takes the next step.</summary>
internal sealed class ModelHttpHandler : HttpMessageHandler
{
    private readonly ConcurrentQueue<Func<CancellationToken, Task<HttpResponseMessage>>> steps = new();
    private readonly ConcurrentQueue<ModelRequest> requests = new();

    /// <summary>Requests received so far.</summary>
    public IReadOnlyList<ModelRequest> Requests => [.. requests];

    /// <summary>Answers 200 with a chat completion whose message content is <paramref name="content"/>.</summary>
    public ModelHttpHandler Completion(string content, int promptTokens = 100, int completionTokens = 10, string finishReason = "stop")
    {
        string body = JsonSerializer.Serialize(new
        {
            id = "chatcmpl-test",
            @object = "chat.completion",
            created = 1_760_000_000,
            model = "gpt-4o-mini",
            choices = new[]
            {
                new { index = 0, message = new { role = "assistant", content }, finish_reason = finishReason },
            },
            usage = new { prompt_tokens = promptTokens, completion_tokens = completionTokens, total_tokens = promptTokens + completionTokens },
        });
        return Status(HttpStatusCode.OK, body);
    }

    /// <summary>Answers with the status and a JSON body.</summary>
    public ModelHttpHandler Status(HttpStatusCode code, string body = "{}")
    {
        steps.Enqueue(_ => Task.FromResult(new HttpResponseMessage(code)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        }));
        return this;
    }

    /// <summary>Answers with an Azure OpenAI error object.</summary>
    public ModelHttpHandler Error(HttpStatusCode code, string errorCode, string message) =>
        Status(code, JsonSerializer.Serialize(new { error = new { code = errorCode, message } }));

    /// <summary>Throws instead of answering.</summary>
    public ModelHttpHandler Throw(Exception exception)
    {
        steps.Enqueue(_ => Task.FromException<HttpResponseMessage>(exception));
        return this;
    }

    /// <summary>Waits (honouring cancellation) before answering with a completion.</summary>
    public ModelHttpHandler Delay(TimeSpan delay, string content)
    {
        steps.Enqueue(async ct =>
        {
            await Task.Delay(delay, ct).ConfigureAwait(false);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(content) };
        });
        return this;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        string? key = request.Headers.TryGetValues("api-key", out IEnumerable<string>? values) ? string.Join(",", values) : null;
        requests.Enqueue(new ModelRequest(request.RequestUri!, key, body));
        if (!steps.TryDequeue(out Func<CancellationToken, Task<HttpResponseMessage>>? step))
        {
            return new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent(string.Create(CultureInfo.InvariantCulture, $"no scripted response for request {requests.Count}")),
            };
        }

        HttpResponseMessage response = await step(cancellationToken).ConfigureAwait(false);
        response.RequestMessage = request;
        return response;
    }
}
