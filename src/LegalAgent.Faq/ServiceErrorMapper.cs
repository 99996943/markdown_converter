using LegalAgent.Faq.Model;

namespace LegalAgent.Faq;

/// <summary>Maps exceptions of the chat service to <see cref="FaqServiceException"/> (research R7).</summary>
internal static class ServiceErrorMapper
{
    /// <summary>The mapped exception, or <c>null</c> for a cancellation requested by the caller (passed on as is).</summary>
    public static FaqServiceException? Map(Exception exception, FaqStep step, string? documentId, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
