using LegalAgent.Corpus.Composition;

namespace LegalAgent.Corpus.Typesetting;

/// <summary>
/// Step scheme (FR-067) like the reference terms: a bold column-name row „Kolejność działań | Wyjaśnienie”, gray boxes
/// without borders (≤ 50% of the page width, aligned edges) with the step name vertically centred, the explanation
/// to the right of the box and an arrow image between consecutive boxes. A step that does not fit moves to the next
/// page under a repeated column-name row. The step names are bold paragraphs in the truth, never headings; the
/// column-name rows are artifacts.
/// </summary>
internal static class StepSchemeLayout
{
    private const double BoxWidth = 142;
    private const double BoxInset = 8;
    private const double TextGap = 6;
    private const double LineStep = 15;
    private const double BoxPadding = 10;
    private const double BoxGap = 22;
    private const double ArrowSize = 13;
    private const byte Gray = 217;
    private const string NameColumn = "Kolejność działań";
    private const string ExplanationColumn = "Wyjaśnienie";

    public static void Scheme(PageWriter w, StepSchemeElement scheme)
    {
        LayoutStyle s = w.Style;
        double size = s.BodySize;
        double boxX = w.ColumnLeft - BoxInset;
        double textX = boxX + BoxWidth + TextGap;
        double textWidth = w.ColumnLeft + w.ColumnWidth - textX;

        double ColumnRow(double baseline)
        {
            w.Builder.Text(boxX + 6, baseline, NameColumn, size, bold: true);
            w.Builder.Text(textX, baseline, ExplanationColumn, size, bold: true);
            w.Truth.Artifacts.Add(NameColumn + " " + ExplanationColumn);
            return baseline + 6;
        }

        var steps = scheme.Steps.Select(step => (
            Name: TextMeasure.Wrap(TextMeasure.Tokenize([new Inline(step.Name, InlineStyle.Bold)]), BoxWidth - 12, size),
            Text: step.Explanation.Select(p => TextMeasure.Wrap(TextMeasure.Tokenize(p), textWidth, size)).ToList(),
            Source: step)).ToList();

        double Height(int nameLines, List<List<SetLine>> text) =>
            (Math.Max(nameLines * LineStep, (text.Sum(p => p.Count) * LineStep) + ((text.Count - 1) * 7)) + BoxPadding) - 3;

        w.Need((2 * LineStep) + Height(steps[0].Name.Count, steps[0].Text));
        double top = ColumnRow(w.Y);
        for (int i = 0; i < steps.Count; i++)
        {
            (List<SetLine> name, List<List<SetLine>> text, SchemeStep source) = steps[i];
            double h = Height(name.Count, text);
            if (top + h > w.Limit + 4)
            {
                w.Y = top;
                int columns = w.Columns;
                w.NewPage();
                w.StartColumns(columns);
                top = ColumnRow(w.Y);
            }

            w.Builder.FilledRect(boxX, top, BoxWidth, h, Gray);
            w.Words(source.Name);
            double nameY = top + ((h - (name.Count * LineStep)) / 2) + size;
            foreach (SetLine line in name)
            {
                w.Draw(line, boxX + 6, nameY, size, record: false);
                nameY += LineStep;
            }

            double y = top + BoxPadding + size - 3;
            foreach (List<SetLine> paragraph in text)
            {
                foreach (SetLine line in paragraph)
                {
                    w.Draw(line, textX, y, size);
                    y += LineStep;
                }

                y += 7;
            }

            w.Track();
            top += h;
            if (i + 1 < steps.Count)
            {
                w.Builder.Image(boxX + ((BoxWidth - ArrowSize) / 2), top + 4, ArrowSize, ArrowSize);
                top += BoxGap;
            }
        }

        w.Y = top + s.Leading + s.ParagraphGap;
    }
}
