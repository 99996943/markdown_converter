namespace LegalAgent.Faq.Model;

/// <summary>A source document as identified in the result.</summary>
/// <param name="Id">Identifier <c>D1</c>…<c>Dn</c>, in input order.</param>
/// <param name="Name">Display name.</param>
/// <param name="Resource">Address of the source.</param>
public sealed record FaqSourceDocument(string Id, string Name, Uri Resource);
