using System.Text;

namespace LegalAgent.PdfParser.Rendering;

/// <summary>Escapes text for safe inclusion in the Markdown output (contracts/markdown-output.md).</summary>
internal static class MarkdownEscaper
{
    /// <summary>
    /// Escapes inline special characters (<c>\ * _ [ ] &lt; &gt; `</c>, and <c>|</c> in tables).
    /// At the start of a line it additionally escapes characters that would start a block:
    /// <c># + - &gt; =</c> and ordered list markers such as <c>2024.</c> or <c>12)</c>.
    /// </summary>
    /// <param name="text">Text to escape.</param>
    /// <param name="atLineStart">True when the text begins a Markdown line.</param>
    /// <param name="inTable">True inside a table cell.</param>
    public static string EscapeText(string text, bool atLineStart = false, bool inTable = false)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0)
        {
            return text;
        }

        var sb = new StringBuilder(text.Length + 8);
        int digitsEnd = 0;
        if (atLineStart)
        {
            while (digitsEnd < text.Length && text[digitsEnd] is >= '0' and <= '9')
            {
                digitsEnd++;
            }
        }

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            bool escape = c switch
            {
                '\\' or '*' or '_' or '[' or ']' or '<' or '>' or '`' => true,
                '|' => inTable,
                '#' or '+' or '-' or '=' => atLineStart && i == 0,
                '.' or ')' => atLineStart && digitsEnd > 0 && i == digitsEnd,
                _ => false,
            };

            if (escape)
            {
                sb.Append('\\');
            }

            sb.Append(c);
        }

        return sb.ToString();
    }

    /// <summary>Escapes a list label: a trailing <c>)</c> or <c>.</c> gets a backslash (<c>1)</c> becomes <c>1\)</c>).</summary>
    /// <param name="label">Literal label from the source.</param>
    public static string EscapeListLabel(string label)
    {
        ArgumentNullException.ThrowIfNull(label);
        if (label.Length == 0)
        {
            return label;
        }

        char last = label[^1];
        string head = label[..^1];
        return last is ')' or '.'
            ? EscapeText(head) + "\\" + last
            : EscapeText(label);
    }
}
