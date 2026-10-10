namespace LegalAgent.Faq.Model;

/// <summary>Front matter values supplied by the caller (contracts/faq-file.md).</summary>
/// <param name="Title">Title.</param>
/// <param name="Description">Description.</param>
/// <param name="Timestamp">Moment of writing; rendered in UTC.</param>
/// <param name="Model">Model name.</param>
/// <param name="Deployment">Deployment name.</param>
public sealed record FaqFileHeader(string Title, string Description, DateTimeOffset Timestamp, string Model, string Deployment);
