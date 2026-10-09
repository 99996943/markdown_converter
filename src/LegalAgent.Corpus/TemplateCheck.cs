using LegalAgent.Corpus.Planning;
using LegalAgent.Corpus.Validation;

namespace LegalAgent.Corpus;

/// <summary>Page range of a template in one layout style.</summary>
/// <param name="Layout">Layout style id.</param>
/// <param name="MinPages">Pages with the required blocks only (and the section minima).</param>
/// <param name="MaxPages">Pages with every optional block of the template's pool.</param>
/// <param name="OptionalCapacity">Number of optional blocks available.</param>
/// <param name="FittedPages">Pages after fitting into the run's range; null when unreachable.</param>
/// <param name="FitError">Why the range is unreachable.</param>
/// <param name="SharedShare">Share of shared-block words in the fitted document.</param>
public sealed record LayoutCheck(string Layout, int MinPages, int MaxPages, int OptionalCapacity, int? FittedPages, string? FitError, double SharedShare);

/// <summary>Authoring report of one template (<c>check --template</c>).</summary>
/// <param name="Template">Template id.</param>
/// <param name="Type">Document type.</param>
/// <param name="RequiredWords">Words of the required blocks (first layout).</param>
/// <param name="OptionalWords">Words of all optional blocks of the pool (first layout).</param>
/// <param name="Layouts">One entry per allowed layout.</param>
/// <param name="Violations">Template structure, forbidden names, unresolved references, shared share.</param>
/// <param name="WrittenFiles">Sample PDF and Markdown files written.</param>
public sealed record TemplateCheckReport(
    string Template,
    string Type,
    int RequiredWords,
    int OptionalWords,
    IReadOnlyList<LayoutCheck> Layouts,
    IReadOnlyList<CheckViolation> Violations,
    IReadOnlyList<string> WrittenFiles);
