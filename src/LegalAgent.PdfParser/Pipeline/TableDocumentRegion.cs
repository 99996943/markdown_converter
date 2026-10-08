namespace LegalAgent.PdfParser.Pipeline;

/// <summary>A table-document (spec 002, FR-080) recognised by <see cref="Stages.TableDocumentStage"/>.</summary>
/// <param name="Index">Document-wide index, also stored in <see cref="Layout.LayoutAnnotations.TableDocumentIndex"/>.</param>
/// <param name="FirstPage">First page of the table.</param>
/// <param name="LastPage">Last page of the table.</param>
/// <param name="Top">Top edge (pt, Y down) of the table frame on its first page.</param>
/// <param name="Divider">X of the ruling between the left and the right column.</param>
/// <param name="ContentLeft">Leftmost word edge of the right column over the whole table.</param>
/// <param name="ContentRight">Rightmost word edge of the right column over the whole table.</param>
/// <param name="SectionCount">Number of sections (rows with a name in the left cell).</param>
/// <param name="HeaderRowText">Text of the dropped column-name row, cells joined with „ | ”; null when there is none.</param>
/// <param name="DroppedHeaderRows">Number of dropped occurrences of the column-name row.</param>
internal sealed record TableDocumentRegion(
    int Index,
    int FirstPage,
    int LastPage,
    double Top,
    double Divider,
    double ContentLeft,
    double ContentRight,
    int SectionCount,
    string? HeaderRowText,
    int DroppedHeaderRows);
