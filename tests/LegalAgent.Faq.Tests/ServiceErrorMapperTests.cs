using System.Net;
using LegalAgent.Faq.Model;
using Microsoft.SemanticKernel;

namespace LegalAgent.Faq.Tests;

public sealed class ServiceErrorMapperTests
{
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "", FaqServiceErrorKind.Authentication)]
    [InlineData(HttpStatusCode.Forbidden, "", FaqServiceErrorKind.Authentication)]
    [InlineData(HttpStatusCode.NotFound, "", FaqServiceErrorKind.DeploymentNotFound)]
    [InlineData(HttpStatusCode.TooManyRequests, "", FaqServiceErrorKind.RateLimited)]
    [InlineData(HttpStatusCode.BadRequest, "{\"error\":{\"code\":\"content_filter\"}}", FaqServiceErrorKind.ContentFiltered)]
    [InlineData(HttpStatusCode.BadRequest, "{\"error\":{\"code\":\"context_length_exceeded\"}}", FaqServiceErrorKind.InputTooLong)]
    [InlineData(HttpStatusCode.BadRequest, "{\"error\":{\"code\":\"invalid_request\"}}", FaqServiceErrorKind.Other)]
    [InlineData(HttpStatusCode.InternalServerError, "", FaqServiceErrorKind.ServiceUnavailable)]
    [InlineData(HttpStatusCode.ServiceUnavailable, "", FaqServiceErrorKind.ServiceUnavailable)]
    public void HttpStatus_MapsToKind(HttpStatusCode status, string body, FaqServiceErrorKind kind)
    {
        FaqServiceException mapped = Map(new HttpOperationException(status, body, "błąd", null), FaqStep.Candidates, "D3");

        Assert.Equal(kind, mapped.Kind);
        Assert.Equal((int)status, mapped.StatusCode);
        Assert.Equal(FaqStep.Candidates, mapped.Step);
        Assert.Equal("D3", mapped.DocumentId);
    }

    [Theory]
    [MemberData(nameof(TransportFailures))]
    public void TransportFailure_MapsToKind(Exception exception, FaqServiceErrorKind kind)
    {
        FaqServiceException mapped = Map(exception, FaqStep.Selection, null);

        Assert.Equal(kind, mapped.Kind);
        Assert.Null(mapped.StatusCode);
        Assert.Equal(FaqStep.Selection, mapped.Step);
        Assert.Null(mapped.DocumentId);
        Assert.Same(exception, mapped.InnerException);
    }

    public static TheoryData<Exception, FaqServiceErrorKind> TransportFailures => new()
    {
        { new TaskCanceledException("timeout"), FaqServiceErrorKind.Timeout },
        { new TimeoutException("timeout"), FaqServiceErrorKind.Timeout },
        { new HttpRequestException("Nie można rozpoznać nazwy hosta."), FaqServiceErrorKind.Network },
        { new HttpOperationException(null, null, "wrapped", new HttpRequestException("dns")), FaqServiceErrorKind.Network },
        { new HttpOperationException(null, null, "wrapped", new TaskCanceledException("timeout")), FaqServiceErrorKind.Timeout },
        { new InvalidOperationException("coś innego"), FaqServiceErrorKind.Other },
    };

    [Fact]
    public void CancelledByUser_IsNotMapped()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Null(ServiceErrorMapper.Map(new TaskCanceledException(), FaqStep.Candidates, "D1", cancellation.Token));
        Assert.Null(ServiceErrorMapper.Map(new OperationCanceledException(), FaqStep.Candidates, "D1", cancellation.Token));
    }

    [Fact]
    public void Message_IsPolish_WithStepAndShortExcerpt()
    {
        string body = "{\"error\":\"" + new string('x', 1000) + "\"}";

        FaqServiceException mapped = Map(new HttpOperationException(HttpStatusCode.TooManyRequests, body, "Too Many Requests", null), FaqStep.Candidates, "D3");

        Assert.Contains("krok kandydatów, D3", mapped.Message, StringComparison.Ordinal);
        Assert.Contains("429", mapped.Message, StringComparison.Ordinal);
        Assert.Contains("{\"error\":\"xxx", mapped.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('x', 301), mapped.Message, StringComparison.Ordinal);
    }

    private static FaqServiceException Map(Exception exception, FaqStep step, string? documentId) =>
        ServiceErrorMapper.Map(exception, step, documentId, CancellationToken.None)
        ?? throw new InvalidOperationException("not mapped");
}
