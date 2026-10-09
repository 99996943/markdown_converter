using LegalAgent.Corpus.Content;
using LegalAgent.Corpus.Planning;
using LegalAgent.Corpus.Validation;

namespace LegalAgent.Corpus.Composition;

/// <summary>Result of composing one document.</summary>
/// <param name="Document">The element tree ready for typesetting.</param>
/// <param name="Blocks">Rendered plain text of every block used (uniqueness check, FR-103b).</param>
/// <param name="Unresolved">Cross references that point to no unit of the document (FR-114).</param>
/// <param name="FactElements">Fact id → ids of the elements whose text uses it (manifest changes and contradictions).</param>
/// <param name="OptionalBlocks">Number of optional blocks actually used.</param>
public sealed record CompositionResult(
    ComposedDocument Document,
    IReadOnlyList<RenderedBlock> Blocks,
    IReadOnlyList<UnresolvedReference> Unresolved,
    IReadOnlyDictionary<string, IReadOnlyList<string>> FactElements,
    int OptionalBlocks);

/// <summary>
/// Turns a <see cref="DocumentPlan"/> into a <see cref="ComposedDocument"/>: renders the template sections with their
/// required blocks and a number of optional blocks from the document's pool, numbers the units („§ N.”, tariff
/// positions, procedure steps, annexes), resolves cross references and footnotes, and fills the front matter.
/// </summary>
public static class DocumentComposer
{
    /// <summary>The fictional bank of the corpus (FR-105).</summary>
    public const string BankName = "Bank Przykładowy S.A.";

    /// <summary>Title of documents made from <paramref name="template"/> (the same in every document of the run).</summary>
    public static string Title(DocumentTemplate template, ContentLibrary content, ulong runSeed) => throw new NotImplementedException();

    /// <summary>The largest number of optional blocks the document can take.</summary>
    public static int OptionalCapacity(DocumentPlan plan, ContentLibrary content) => throw new NotImplementedException();

    /// <summary>Composes <paramref name="plan"/> with <paramref name="optionalBlocks"/> optional blocks (at least the section minima).</summary>
    public static CompositionResult Compose(DocumentPlan plan, ContentLibrary content, ulong runSeed, int optionalBlocks) =>
        throw new NotImplementedException();
}
