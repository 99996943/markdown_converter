namespace LegalAgent.Faq;

/// <summary>Heading anchors of a Markdown document as GitHub and VS Code compute them (<c>github-slugger</c>, research R3 of spec 013).</summary>
public static class MarkdownAnchors
{
    /// <summary>The anchor of one heading text, without the counter of repeats.</summary>
    /// <param name="headingText">Text of the heading without Markdown markers.</param>
    /// <returns>The anchor (without <c>#</c>).</returns>
    public static string Slug(string headingText) => throw new NotImplementedException();

    /// <summary>Anchors of all ATX headings of the document in order, repeats suffixed <c>-1</c>, <c>-2</c>….</summary>
    /// <param name="markdown">The Markdown document.</param>
    /// <returns>The anchors (without <c>#</c>).</returns>
    public static IReadOnlyList<string> ForDocument(string markdown) => throw new NotImplementedException();
}
