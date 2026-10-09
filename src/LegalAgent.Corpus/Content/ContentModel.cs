using LegalAgent.Corpus.Composition;

namespace LegalAgent.Corpus.Content;

/// <summary>Document type from <c>typy.yaml</c>.</summary>
public sealed record DocumentTypeDef(string Id, string Prefix, string DesignationPattern, string Name, IReadOnlyList<string> RequiredElements)
{
    /// <summary>Gets the minimum number of base documents of this type per layout id (<c>uklady-min</c>); empty when absent.</summary>
    public IReadOnlyDictionary<string, int> MinLayouts { get; init; } = new Dictionary<string, int>(StringComparer.Ordinal);

    /// <summary>Gets the English name of the type (<c>nazwa-en</c>) used in chunk metadata (spec 004); null when absent.</summary>
    public string? EnglishName { get; init; }
}

/// <summary>Front matter specification of a template.</summary>
public sealed record FrontSpec(bool Cover, bool RecordCard, IReadOnlyList<string> Fields);

/// <summary>Optional-block selection of a section slot.</summary>
public sealed record OptionalSpec(IReadOnlyList<string> Categories, int Min, int Max);

/// <summary>Section slot of a template.</summary>
public sealed record SectionSlot(string Kind, string? Title, IReadOnlyList<string> Required, OptionalSpec? Optional);

/// <summary>Document template from <c>szablony/*.yaml</c>.</summary>
public sealed record DocumentTemplate(
    string Id,
    string Type,
    string Topic,
    IReadOnlyList<string> TitleVariants,
    IReadOnlyList<string> Layouts,
    FrontSpec Front,
    IReadOnlyList<SectionSlot> Sections,
    IReadOnlyDictionary<string, string> Parameters,
    string File);

/// <summary>Element of a source content block.</summary>
public abstract record SourceElement;

/// <summary>Paragraph (<c>akapit</c>).</summary>
public sealed record SourceParagraph(string Text) : SourceElement;

/// <summary>Point of a clause with optional letters.</summary>
public sealed record SourcePoint(string Text, IReadOnlyList<string> Letters);

/// <summary>Clause (<c>ustep</c>) with optional points.</summary>
public sealed record SourceClause(string Text, IReadOnlyList<SourcePoint> Points) : SourceElement;

/// <summary>Heading (<c>naglowek</c>).</summary>
public sealed record SourceHeading(string Text, int Level) : SourceElement;

/// <summary>Table column.</summary>
public sealed record SourceTableColumn(string Header, double Weight);

/// <summary>Table (<c>tabela</c>).</summary>
public sealed record SourceTable(
    IReadOnlyList<SourceTableColumn> Columns,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    bool? Grid,
    IReadOnlyList<string> Notes) : SourceElement;

/// <summary>Tariff item with optional sub-items.</summary>
public sealed record SourceTariffItem(string Service, string Rate, string Mode, IReadOnlyList<SourceTariffItem> Children);

/// <summary>Tariff items (<c>pozycje-taryfy</c>).</summary>
public sealed record SourceTariffItems(IReadOnlyList<SourceTariffItem> Items) : SourceElement;

/// <summary>Procedure step with optional sub-steps.</summary>
public sealed record SourceStep(string Text, IReadOnlyList<SourceStep> Children);

/// <summary>Steps (<c>kroki</c>).</summary>
public sealed record SourceSteps(IReadOnlyList<SourceStep> Steps) : SourceElement;

/// <summary>Step of a step scheme.</summary>
public sealed record SourceSchemeStep(string Name, IReadOnlyList<string> Explanation);

/// <summary>Step scheme (<c>schemat</c>).</summary>
public sealed record SourceScheme(IReadOnlyList<SourceSchemeStep> Steps) : SourceElement;

/// <summary>Checklist (<c>lista-kontrolna</c>).</summary>
public sealed record SourceChecklist(ChecklistForm? Form, IReadOnlyList<string> Items) : SourceElement;

/// <summary>Callout (<c>ramka</c>).</summary>
public sealed record SourceCallout(string Text) : SourceElement;

/// <summary>Record card (<c>metryczka</c>).</summary>
public sealed record SourceRecordCard(IReadOnlyList<KeyValuePair<string, string>> Pairs) : SourceElement;

/// <summary>Content block from <c>bloki/**/*.yaml</c>.</summary>
public sealed record ContentBlock(
    string Id,
    IReadOnlyList<string> Types,
    IReadOnlyList<string> Topics,
    string Category,
    bool Shared,
    string? Unit,
    string? Title,
    IReadOnlyList<SourceElement> Elements,
    IReadOnlyDictionary<string, string> Footnotes,
    string File);

/// <summary>Poison kind from the header of <c>zatrucia/&lt;rodzaj&gt;.yaml</c>.</summary>
public sealed record PoisonKindDef(string Kind, string Abbreviation, string Description, string File);

/// <summary>Poison pattern from <c>zatrucia/*.yaml</c>.</summary>
public sealed record PoisonPattern(
    string Id,
    string Kind,
    IReadOnlyList<string> Types,
    string? Goal,
    string Operation,
    IReadOnlyList<string> Placements,
    IReadOnlyList<string> TextVariants,
    string? Fact,
    FactValue? Value,
    IReadOnlyDictionary<string, string> FrontFields,
    string Description,
    string File);

/// <summary>Source legal act from <c>akty.yaml</c>.</summary>
public sealed record ActSource(
    string Id,
    string Title,
    string Journal,
    DateOnly ConsolidatedDate,
    string Url,
    DateOnly DownloadedOn,
    string File,
    string? Notes);

/// <summary>Everything loaded from the content directory.</summary>
public sealed record ContentLibrary(
    IReadOnlyList<DocumentTypeDef> Types,
    FactCatalog Facts,
    IReadOnlyList<DocumentTemplate> Templates,
    IReadOnlyList<ContentBlock> Blocks,
    IReadOnlyList<PoisonKindDef> PoisonKinds,
    IReadOnlyList<PoisonPattern> PoisonPatterns,
    IReadOnlyList<string> ForbiddenNames,
    IReadOnlyList<ActSource> Acts,
    string ContentHash);
