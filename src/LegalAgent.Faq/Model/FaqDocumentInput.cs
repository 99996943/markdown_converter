namespace LegalAgent.Faq.Model;

/// <summary>One source document of the FAQ.</summary>
/// <param name="Name">Display name: the title or the file name; not empty.</param>
/// <param name="Resource">Absolute address of the source.</param>
/// <param name="Markdown">Full Markdown of the document; not empty.</param>
/// <param name="Units">Unit designations and heading texts of the document; a cited unit must match one of them.</param>
public sealed record FaqDocumentInput(string Name, Uri Resource, string Markdown, IReadOnlyList<string> Units);
