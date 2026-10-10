using System.Globalization;
using System.Text;
using LegalAgent.Faq.Model;

namespace LegalAgent.Faq;

/// <summary>
/// Renders the FAQ file: OKF front matter written by a small emitter (research R10) and one section per item
/// (contracts/faq-file.md).
/// </summary>
public static class FaqMarkdownRenderer
{
    /// <summary>Renders the result; the same input always gives the same text.</summary>
    /// <param name="result">The FAQ.</param>
    /// <param name="header">Front matter values.</param>
    /// <returns>Text with LF line endings, ending with one LF.</returns>
    public static string Render(FaqResult result, FaqFileHeader header)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(header);
        var documents = result.Documents.ToDictionary(d => d.Id, StringComparer.Ordinal);

        var text = new StringBuilder();
        text.Append("---\n");
        text.Append("type: faq\n");
        text.Append("title: ").Append(Quote(header.Title)).Append('\n');
        text.Append("description: ").Append(Quote(header.Description)).Append('\n');
        text.Append("resource:\n");
        foreach (FaqSourceDocument document in result.Documents)
        {
            text.Append("  - ").Append(Quote(document.Resource.OriginalString)).Append('\n');
        }

        string timestamp = header.Timestamp.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        text.Append("timestamp: ").Append(Quote(timestamp)).Append('\n');
        text.Append("model: ").Append(Quote(header.Model)).Append('\n');
        text.Append("deployment: ").Append(Quote(header.Deployment)).Append('\n');
        text.Append("---\n");

        foreach (FaqItem item in result.Items)
        {
            text.Append("\n## ").Append(Heading(item.Question)).Append('\n');
            text.Append('\n').Append(Body(item.Answer)).Append('\n');
            text.Append('\n').Append(Sources(item.Sources, documents)).Append('\n');
        }

        return text.ToString();
    }

    /// <summary>A double-quoted YAML scalar: <c>\</c> and <c>"</c> escaped, line breaks replaced by spaces.</summary>
    private static string Quote(string value)
    {
        string single = OneLine(value);
        return "\"" + single.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

    /// <summary>The question on one line; a leading <c>#</c> is escaped so it is not read as part of the heading marker.</summary>
    private static string Heading(string question)
    {
        string single = OneLine(question).Trim();
        return single.StartsWith('#') ? "\\" + single : single;
    }

    private static string OneLine(string value) => string.Join(' ', value.Split(["\r\n", "\n", "\r"], StringSplitOptions.None));

    /// <summary>The answer with LF endings, without trailing spaces and without runs of blank lines.</summary>
    private static string Body(string answer)
    {
        var lines = new List<string>();
        foreach (string line in answer.Trim().Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n'))
        {
            string trimmed = line.TrimEnd();
            if (trimmed.Length == 0 && lines.Count > 0 && lines[^1].Length == 0)
            {
                continue;
            }

            lines.Add(trimmed);
        }

        return string.Join('\n', lines);
    }

    private static string Sources(IReadOnlyList<FaqSource> sources, Dictionary<string, FaqSourceDocument> documents)
    {
        IEnumerable<string> parts = sources.Select(source =>
        {
            FaqSourceDocument document = documents[source.DocumentId];
            string link = $"[{document.Name}]({document.Resource.OriginalString})";
            return source.Unit is { Length: > 0 } unit ? $"{link}, {unit}" : link;
        });
        return (sources.Count == 1 ? "Źródło: " : "Źródła: ") + string.Join("; ", parts);
    }
}
