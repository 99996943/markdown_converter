using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;

namespace LegalAgent.Downloads.Tests.Fakes;

/// <summary>A request the fake received.</summary>
/// <param name="Address">Requested address.</param>
/// <param name="UserAgent">The <c>User-Agent</c> header, if any.</param>
internal sealed record ReceivedRequest(Uri Address, string? UserAgent);

/// <summary>Scripted HTTP responses for offline tests; unknown addresses answer 404.</summary>
internal sealed class FakeHttpHandler : HttpMessageHandler
{
    private readonly ConcurrentDictionary<string, Func<CancellationToken, Task<HttpResponseMessage>>> routes = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<ReceivedRequest> requests = new();
    private TaskCompletionSource? barrier;
    private int barrierTarget;
    private int barrierCount;

    /// <summary>Requests received so far, in arrival order.</summary>
    public IReadOnlyList<ReceivedRequest> Requests => [.. requests];

    /// <summary>Answers with a PDF body.</summary>
    public FakeHttpHandler Pdf(string url, byte[]? content = null, DateTimeOffset? lastModified = null) =>
        Respond(url, () =>
        {
            HttpResponseMessage response = Ok(content ?? PdfBytes.Sample(url));
            if (lastModified is not null)
            {
                response.Content.Headers.LastModified = lastModified;
            }

            return response;
        });

    /// <summary>Answers with an HTML page and status 200.</summary>
    public FakeHttpHandler Html(string url) => Respond(url, () => Ok(PdfBytes.Html, "text/html"));

    /// <summary>Answers with a status code and an empty body.</summary>
    public FakeHttpHandler Status(string url, HttpStatusCode code) =>
        Respond(url, () => new HttpResponseMessage(code) { Content = new ByteArrayContent([]) });

    /// <summary>Answers with a redirect.</summary>
    public FakeHttpHandler Redirect(string url, string location, HttpStatusCode code = HttpStatusCode.Found) =>
        Respond(url, () =>
        {
            var response = new HttpResponseMessage(code) { Content = new ByteArrayContent([]) };
            response.Headers.Location = new Uri(location, UriKind.RelativeOrAbsolute);
            return response;
        });

    /// <summary>Throws the exception instead of answering (e.g. a name resolution failure).</summary>
    public FakeHttpHandler Throw(string url, Exception exception) =>
        Route(url, _ => Task.FromException<HttpResponseMessage>(exception));

    /// <summary>Never answers; the request ends only when cancelled.</summary>
    public FakeHttpHandler Hang(string url) =>
        Route(url, async ct =>
        {
            await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
            throw new InvalidOperationException("unreachable");
        });

    /// <summary>Answers 200 with a body produced by the stream factory (no <c>Content-Length</c> unless given).</summary>
    public FakeHttpHandler Body(string url, Func<Stream> body, long? contentLength = null) =>
        Respond(url, () =>
        {
            var content = new StreamContent(body());
            content.Headers.ContentLength = contentLength;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });

    /// <summary>Custom route.</summary>
    public FakeHttpHandler Route(string url, Func<CancellationToken, Task<HttpResponseMessage>> respond)
    {
        routes[new Uri(url).AbsoluteUri] = respond;
        return this;
    }

    /// <summary>Holds every response until <paramref name="count"/> requests arrived (fails after 5 s).</summary>
    public FakeHttpHandler HoldUntil(int count)
    {
        barrierTarget = count;
        barrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        return this;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Uri address = request.RequestUri!;
        requests.Enqueue(new ReceivedRequest(address, request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null));

        if (barrier is not null)
        {
            if (Interlocked.Increment(ref barrierCount) >= barrierTarget)
            {
                barrier.TrySetResult();
            }

            await barrier.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
        }

        if (routes.TryGetValue(address.AbsoluteUri, out Func<CancellationToken, Task<HttpResponseMessage>>? respond))
        {
            HttpResponseMessage response = await respond(cancellationToken).ConfigureAwait(false);
            response.RequestMessage = request;
            return response;
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new ByteArrayContent([]), RequestMessage = request };
    }

    private static HttpResponseMessage Ok(byte[] body, string mediaType = "application/pdf")
    {
        var content = new ByteArrayContent(body);
        content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    private FakeHttpHandler Respond(string url, Func<HttpResponseMessage> response) =>
        Route(url, _ => Task.FromResult(response()));
}
