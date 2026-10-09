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
/// <c>[^n]</c> footnote references and the escapes <c>\{</c>, <c>\}</c>, <c>\|</c>, <c>\*</c>, <c>\\</c>.
/// </summary>
public static class TextTemplate
{
    /// <summary>Renders <paramref name="template"/> to inline runs, choosing variants with <paramref name="random"/>.</summary>
    public static IReadOnlyList<Inline> Render(string template, ITemplateContext context, DeterministicRandom random)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(random);
        var renderer = new Renderer(template, context, random);
        renderer.RenderRange(0, template.Length, InlineStyle.Regular);
        return renderer.Output;
    }

    private sealed class Renderer(string text, ITemplateContext context, DeterministicRandom random)
    {
        public List<Inline> Output { get; } = [];

        public void RenderRange(int start, int end, InlineStyle style)
        {
            int i = start;
            while (i < end)
            {
                char c = text[i];
                if (c == '\\' && i + 1 < end && text[i + 1] is '{' or '}' or '|' or '*' or '\\')
                {
                    AddText(text[i + 1].ToString(), style);
                    i += 2;
                }
                else if (c == '{' && i + 1 < end && text[i + 1] == '{')
                {
                    i = RenderDirective(i, end, style);
                }
                else if (c == '{')
                {
                    i = RenderVariants(i, end, style);
                }
                else if (c is '}' or '|')
                {
                    throw Error(i, $"Nieoczekiwany znak '{c}' (użyj \\{c}, aby wstawić go dosłownie).");
                }
                else if (c == '*')
                {
                    i = RenderEmphasis(i, end);
                }
                else if (c == '[' && i + 1 < end && text[i + 1] == '^')
                {
                    i = RenderFootnote(i, end);
                }
                else if (char.IsWhiteSpace(c))
                {
                    i = RenderWhitespace(i, end, style);
                }
                else
                {
                    int j = i + 1;
                    while (j < end && !IsSpecial(text[j]) && !char.IsWhiteSpace(text[j]))
                    {
                        j++;
                    }

                    AddText(text[i..j], style);
                    i = j;
                }
            }
        }

        private static bool IsSpecial(char c) => c is '\\' or '{' or '}' or '|' or '*' or '[';

        private static ContentException Error(int position, string message) =>
            new($"{message} (pozycja {position} w szablonie)") { Position = position };

        private int RenderWhitespace(int i, int end, InlineStyle style)
        {
            int j = i;
            bool newline = false;
            while (j < end && char.IsWhiteSpace(text[j]))
            {
                newline |= text[j] is '\n' or '\r';
                j++;
            }

            AddText(newline ? " " : text[i..j], style);
            return j;
        }

        private int RenderDirective(int i, int end, InlineStyle style)
        {
            int close = text.IndexOf("}}", i + 2, end - (i + 2), StringComparison.Ordinal);
            if (close < 0)
            {
                throw Error(i, "Niezamknięte '{{'.");
            }

            string body = text[(i + 2)..close];
            int colon = body.IndexOf(':', StringComparison.Ordinal);
            if (colon < 0)
            {
                throw Error(i, $"Nieprawidłowa konstrukcja '{{{{{body}}}}}': oczekiwano prefiksu fakt:, param: lub ref:.");
            }

            string prefix = body[..colon];
            string arg = body[(colon + 1)..];
            switch (prefix)
            {
                case "fakt":
                    {
                        (FactKind kind, FactValue value) = Wrap(i, () => context.Fact(arg));
                        AddText(Wrap(i, () => PolishFormat.Format(kind, value)), style);
                        break;
                    }

                case "param":
                    AddText(Wrap(i, () => context.Param(arg)), style);
                    break;
                case "ref":
                    if (arg.Length == 0)
                    {
                        throw Error(i, "Puste odwołanie {{ref:}}.");
                    }

                    Output.Add(new Inline(arg, style, InlineKind.Reference));
                    break;
                default:
                    throw Error(i, $"Nieznany prefiks '{prefix}:' w '{{{{{body}}}}}'.");
            }

            return close + 2;
        }

        private int RenderVariants(int i, int end, InlineStyle style)
        {
            var starts = new List<int> { i + 1 };
            int depth = 0;
            int j = i;
            int close = -1;
            while (j < end)
            {
                char c = text[j];
                if (c == '\\' && j + 1 < end)
                {
                    j += 2;
                    continue;
                }

                if (c == '{')
                {
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        close = j;
                        break;
                    }
                }
                else if (c == '|' && depth == 1)
                {
                    starts.Add(j + 1);
                }

                j++;
            }

            if (close < 0)
            {
                throw Error(i, "Niezamknięte '{'.");
            }

            int choice = random.Next(starts.Count);
            int to = choice + 1 < starts.Count ? starts[choice + 1] - 1 : close;
            RenderRange(starts[choice], to, style);
            return close + 1;
        }

        private int RenderEmphasis(int i, int end)
        {
            bool bold = i + 1 < end && text[i + 1] == '*';
            int markerLength = bold ? 2 : 1;
            int contentStart = i + markerLength;
            int depth = 0;
            int j = contentStart;
            int close = -1;
            while (j < end)
            {
                char c = text[j];
                if (c == '\\' && j + 1 < end)
                {
                    j += 2;
                    continue;
                }

                if (c == '{')
                {
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                }
                else if (c == '*' && depth == 0 && (!bold || (j + 1 < end && text[j + 1] == '*')))
                {
                    close = j;
                    break;
                }

                j++;
            }

            if (close < 0)
            {
                throw Error(i, bold ? "Niezamknięte '**'." : "Niezamknięte '*'.");
            }

            RenderRange(contentStart, close, bold ? InlineStyle.Bold : InlineStyle.Italic);
            return close + markerLength;
        }

        private int RenderFootnote(int i, int end)
        {
            int close = text.IndexOf(']', i + 2, end - (i + 2));
            if (close < 0 || close == i + 2)
            {
                throw Error(i, "Nieprawidłowy przypis: oczekiwano [^klucz].");
            }

            Output.Add(new Inline(text[(i + 2)..close], InlineStyle.Regular, InlineKind.FootnoteRef));
            return close + 1;
        }

        private void AddText(string value, InlineStyle style)
        {
            if (value.Length == 0)
            {
                return;
            }

            if (Output.Count > 0 && Output[^1] is { Kind: InlineKind.Text } last && last.Style == style)
            {
                Output[^1] = last with { Text = last.Text + value };
            }
            else
            {
                Output.Add(new Inline(value, style));
            }
        }

        private static T Wrap<T>(int position, Func<T> action)
        {
            try
            {
                return action();
            }
            catch (ContentException ex)
            {
                throw new ContentException($"{ex.Message} (pozycja {position} w szablonie)", ex)
                {
                    Position = position,
                    File = ex.File,
                    YamlPath = ex.YamlPath,
                };
            }
        }
    }
}
