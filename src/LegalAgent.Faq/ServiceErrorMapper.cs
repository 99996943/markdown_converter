using System.Globalization;
using System.Net;
using LegalAgent.Faq.Model;
using Microsoft.SemanticKernel;

namespace LegalAgent.Faq;

/// <summary>Maps exceptions of the chat service to <see cref="FaqServiceException"/> (research R7).</summary>
internal static class ServiceErrorMapper
{
    private const int ExcerptLength = 300;

    /// <summary>The mapped exception, or <c>null</c> for a cancellation requested by the caller (passed on as is).</summary>
    public static FaqServiceException? Map(Exception exception, FaqStep step, string? documentId, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        int? status = null;
        string? body = null;
        FaqServiceErrorKind kind;
        if (exception is HttpOperationException { StatusCode: { } code } http)
        {
            status = (int)code;
            body = http.ResponseContent;
            kind = KindOf(code, body);
        }
        else
        {
            kind = Innermost(exception) switch
            {
                TaskCanceledException or TimeoutException => FaqServiceErrorKind.Timeout,
                HttpRequestException => FaqServiceErrorKind.Network,
                _ => FaqServiceErrorKind.Other,
            };
        }

        return new FaqServiceException(kind, step, documentId, status, Message(kind, step, documentId, status, body ?? exception.Message), exception);
    }

    private static FaqServiceErrorKind KindOf(HttpStatusCode code, string? body) => code switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => FaqServiceErrorKind.Authentication,
        HttpStatusCode.NotFound => FaqServiceErrorKind.DeploymentNotFound,
        HttpStatusCode.TooManyRequests => FaqServiceErrorKind.RateLimited,
        HttpStatusCode.BadRequest when Contains(body, "content_filter") => FaqServiceErrorKind.ContentFiltered,
        HttpStatusCode.BadRequest when Contains(body, "context_length_exceeded") => FaqServiceErrorKind.InputTooLong,
        >= HttpStatusCode.InternalServerError => FaqServiceErrorKind.ServiceUnavailable,
        _ => FaqServiceErrorKind.Other,
    };

    /// <summary>The first exception in the chain that says what went wrong on the transport (timeout, connection).</summary>
    private static Exception Innermost(Exception exception)
    {
        for (Exception? e = exception; e is not null; e = e.InnerException)
        {
            if (e is TaskCanceledException or TimeoutException or HttpRequestException)
            {
                return e;
            }
        }

        return exception;
    }

    private static bool Contains(string? text, string value) => text?.Contains(value, StringComparison.Ordinal) == true;

    private static string Message(FaqServiceErrorKind kind, FaqStep step, string? documentId, int? status, string detail)
    {
        string what = kind switch
        {
            FaqServiceErrorKind.Authentication => "usługa odrzuciła klucz",
            FaqServiceErrorKind.DeploymentNotFound => "nie znaleziono wdrożenia",
            FaqServiceErrorKind.RateLimited => "przekroczono limit zapytań",
            FaqServiceErrorKind.ContentFiltered => "zapytanie zablokował filtr treści",
            FaqServiceErrorKind.InputTooLong => "zapytanie przekracza kontekst modelu",
            FaqServiceErrorKind.ServiceUnavailable => "usługa jest niedostępna",
            FaqServiceErrorKind.Timeout => "brak odpowiedzi usługi w wyznaczonym czasie",
            FaqServiceErrorKind.Network => "błąd połączenia z usługą",
            _ => "błąd usługi modelu",
        };
        string http = status is { } s ? string.Create(CultureInfo.InvariantCulture, $"; HTTP {s}") : string.Empty;
        string excerpt = detail.ReplaceLineEndings(" ").Trim();
        if (excerpt.Length > ExcerptLength)
        {
            excerpt = excerpt[..ExcerptLength] + "…";
        }

        return $"Błąd usługi modelu ({FaqMessages.StepName(step, documentId)}{http}): {what}. {excerpt}".TrimEnd();
    }
}
