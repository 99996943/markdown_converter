using System.Globalization;

namespace LegalAgent.PdfParser.Layout;

/// <summary>Well-known keys of <see cref="LayoutLine.Annotations"/> shared between pipeline stages.</summary>
public static class LayoutAnnotations
{
    /// <summary>Left edge (pt) of the text column the line belongs to; set by reading order on multi-column pages.</summary>
    public const string ColumnLeft = "column.left";

    /// <summary>Right edge (pt) of the text column the line belongs to; set by reading order on multi-column pages.</summary>
    public const string ColumnRight = "column.right";

    /// <summary>Literal label of a list item; set by list detection on <see cref="LineRole.ListItem"/> lines.</summary>
    public const string ListLabel = "list.label";

    /// <summary><see cref="Model.ListLabelKind"/> name of a list item label.</summary>
    public const string ListKind = "list.kind";

    /// <summary>Document-wide identifier of a list item (invariant integer), set on its first line.</summary>
    public const string ListItemId = "list.id";

    /// <summary>Identifier of the parent item of a list item; empty for a top-level item.</summary>
    public const string ListParent = "list.parent";

    /// <summary>Identifier of the item a <see cref="LineRole.ListContinuation"/> line belongs to.</summary>
    public const string ListOwner = "list.owner";

    /// <summary>
    /// Set to <c>"1"</c> on continuation lines forming the common part (FR-054) of <see cref="ListOwner"/>: a paragraph
    /// placed after the owner's nested list instead of being appended to the owner's text.
    /// </summary>
    public const string ListCommonPart = "list.common";

    /// <summary>Index into <see cref="Pipeline.PipelineContext.Tables"/> of the table a <see cref="LineRole.Table"/> line belongs to.</summary>
    public const string TableIndex = "table.index";

    /// <summary>
    /// Document-wide index (invariant integer) of the step scheme (FR-067) a line belongs to: its step titles, its
    /// explanation lines and its dropped column-name rows.
    /// </summary>
    public const string StepIndex = "step.scheme";

    /// <summary>
    /// Document-wide index (invariant integer) of the table-document (spec 002, FR-080) a line belongs to: its section names,
    /// its content lines and its dropped column-name rows.
    /// </summary>
    public const string TableDocumentIndex = "tabledoc.index";

    /// <summary>Number of the step (invariant integer, from 1 within a scheme) on <see cref="LineRole.StepTitle"/> lines.</summary>
    public const string StepNumber = "step.number";

    /// <summary>Stores a coordinate annotation in invariant round-trip format.</summary>
    public static void SetNumber(LayoutLine line, string key, double value)
    {
        ArgumentNullException.ThrowIfNull(line);
        line.Annotations[key] = value.ToString("R", CultureInfo.InvariantCulture);
    }

    /// <summary>Reads a coordinate annotation, or <c>null</c> when absent.</summary>
    public static double? GetNumber(LayoutLine line, string key)
    {
        ArgumentNullException.ThrowIfNull(line);
        return line.Annotations.TryGetValue(key, out string? text)
            && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                ? value
                : null;
    }
}
