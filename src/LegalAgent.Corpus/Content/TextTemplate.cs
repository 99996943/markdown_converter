using LegalAgent.Corpus.Composition;
using LegalAgent.Corpus.Random;

namespace LegalAgent.Corpus.Content;

/// <summary>What a template needs from the document being composed.</summary>
public interface ITemplateContext
{
    /// <summary>Kind and value of fact <paramref name="id"/> for this document; throws <see cref="ContentException"/> when unknown.</summary>
    (FactKind Kind, FactValue Value) Fact(string id);

    /// <summary>Document parameter <paramref name="name"/> (<c>bank</c>, <c>oznaczenie</c>, …); throws <see cref="ContentException"/> when unknown.</summary>
    string Param(string name);
}

/// <summary>
/// Text template syntax of the content files (contracts/content-format.md): variants <c>{a|b}</c> (nested),
/// <c>{{fakt:id}}</c>, <c>{{param:name}}</c>, <c>{{ref:target}}</c> (deferred), <c>**bold**</c>, <c>*italic*</c>,
/// <c>[^n]</c> footnote references and the escapes <c>\{</c>, <c>\}</c>, <c>\|</c>.
/// </summary>
public static class TextTemplate
{
    /// <summary>Renders <paramref name="template"/> to inline runs, choosing variants with <paramref name="random"/>.</summary>
    public static IReadOnlyList<Inline> Render(string template, ITemplateContext context, DeterministicRandom random) =>
        throw new NotImplementedException();
}
