using System.Globalization;
using System.Text;
using LegalAgent.Corpus.Content;

namespace LegalAgent.Corpus.Validation;

/// <summary>A failed corpus check.</summary>
/// <param name="Check">Name of the check.</param>
/// <param name="Message">Human readable description.</param>
/// <param name="DocumentId">Document concerned, if any.</param>
/// <param name="BlockId">Block concerned, if any.</param>
/// <param name="Template">Template concerned, if any.</param>
public sealed record CheckViolation(string Check, string Message, string? DocumentId = null, string? BlockId = null, string? Template = null);

/// <summary>Rendered plain text of a document (or one of its blocks).</summary>
/// <param name="DocumentId">Document id.</param>
/// <param name="BlockId">Block id or null.</param>
/// <param name="Text">Plain text.</param>
public sealed record RenderedText(string DocumentId, string? BlockId, string Text);

/// <summary>Rendered block of a base document.</summary>
/// <param name="DocumentId">Document id.</param>
/// <param name="BlockId">Block id.</param>
/// <param name="Shared">Whether the block is shared.</param>
/// <param name="Text">Plain text.</param>
public sealed record RenderedBlock(string DocumentId, string BlockId, bool Shared, string Text);

/// <summary>Word counts of a document.</summary>
/// <param name="DocumentId">Document id.</param>
/// <param name="TotalWords">All words.</param>
/// <param name="SharedWords">Words in shared blocks.</param>
public sealed record DocumentWords(string DocumentId, int TotalWords, int SharedWords);

/// <summary>Reference the composer could not resolve.</summary>
/// <param name="DocumentId">Document id.</param>
/// <param name="BlockId">Block id.</param>
/// <param name="Target">Unresolved target.</param>
public sealed record UnresolvedReference(string DocumentId, string BlockId, string Target);

/// <summary>Corpus-level validation checks (FR-103a, FR-103b, FR-105, FR-110-FR-112, FR-114).</summary>
public static partial class CorpusChecks
{
    private const string TemplateStructureCheck = "TemplateStructure";

    /// <summary>FR-105: forbidden names, case- and diacritic-insensitive.</summary>
    public static IReadOnlyList<CheckViolation> ForbiddenNames(IEnumerable<RenderedText> texts, IReadOnlyList<string> forbiddenNames)
    {
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(forbiddenNames);
        var names = forbiddenNames.Select(n => (Name: n, Key: FoldDiacritics(n))).Where(n => n.Key.Length > 0).ToList();
        var result = new List<CheckViolation>();
        foreach (var text in texts)
        {
            var folded = FoldDiacritics(text.Text);
            foreach (var (name, key) in names)
            {
                if (ContainsWord(folded, key))
                {
                    result.Add(new CheckViolation(
                        "ForbiddenNames",
                        $"dokument {text.DocumentId}, blok {text.BlockId ?? "-"}: zabroniona nazwa '{name}'",
                        text.DocumentId,
                        text.BlockId));
                }
            }
        }

        return Sort(result);
    }

    /// <summary>Whether <paramref name="key"/> occurs in <paramref name="text"/> not glued to letters or digits.</summary>
    private static bool ContainsWord(string text, string key)
    {
        for (int i = text.IndexOf(key, StringComparison.Ordinal); i >= 0; i = text.IndexOf(key, i + 1, StringComparison.Ordinal))
        {
            bool before = i == 0 || !char.IsLetterOrDigit(text[i - 1]);
            int end = i + key.Length;
            bool after = end == text.Length || !char.IsLetterOrDigit(text[end]);
            if (before && after)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>FR-103b: non-shared block text must not repeat across documents.</summary>
    public static IReadOnlyList<CheckViolation> Uniqueness(IEnumerable<RenderedBlock> blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        var result = new List<CheckViolation>();
        var groups = blocks.Where(b => !b.Shared)
            .Select(b => (Block: b, Key: Normalize(b.Text)))
            .Where(x => x.Key.Length > 0)
            .GroupBy(x => x.Key, StringComparer.Ordinal);
        foreach (var group in groups)
        {
            var docs = group.Select(x => x.Block.DocumentId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

            // FR-120: versions of one document („REG-03-w1”, „REG-03”) repeat its text by design.
            if (docs.Select(BaseId).Distinct(StringComparer.Ordinal).Count() < 2)
            {
                continue;
            }

            var ids = group.Select(x => x.Block.BlockId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
            result.Add(new CheckViolation(
                "Uniqueness",
                $"powtorzony tekst bloku {string.Join(", ", ids)} w dokumentach {string.Join(", ", docs)}",
                docs[0],
                ids[0]));
        }

        return Sort(result);
    }

    private static string BaseId(string documentId) => VersionSuffix().Replace(documentId, string.Empty);

    [System.Text.RegularExpressions.GeneratedRegex(@"-w\d+$", System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex VersionSuffix();

    /// <summary>FR-103a: share of shared words per document.</summary>
    public static IReadOnlyList<CheckViolation> SharedShare(IEnumerable<DocumentWords> documents, double maxShare)
    {
        ArgumentNullException.ThrowIfNull(documents);
        var result = new List<CheckViolation>();
        foreach (var doc in documents)
        {
            if (doc.TotalWords <= 0)
            {
                continue;
            }

            var share = (double)doc.SharedWords / doc.TotalWords;
            if (share > maxShare)
            {
                result.Add(new CheckViolation(
                    "SharedShare",
                    $"dokument {doc.DocumentId}: udzial blokow wspolnych {share.ToString("0.000", CultureInfo.InvariantCulture)} przekracza {maxShare.ToString("0.000", CultureInfo.InvariantCulture)}",
                    doc.DocumentId));
            }
        }

        return Sort(result);
    }

    /// <summary>FR-114: unresolved references.</summary>
    public static IReadOnlyList<CheckViolation> References(IEnumerable<UnresolvedReference> unresolved)
    {
        ArgumentNullException.ThrowIfNull(unresolved);
        return Sort(unresolved.Select(u => new CheckViolation(
            "References",
            $"dokument {u.DocumentId}, blok {u.BlockId}: nierozwiazane odwolanie '{u.Target}'",
            u.DocumentId,
            u.BlockId)).ToList());
    }

    /// <summary>FR-110-FR-112: required elements of each template's type.</summary>
    public static IReadOnlyList<CheckViolation> TemplateStructure(ContentLibrary library)
    {
        ArgumentNullException.ThrowIfNull(library);
        var result = new List<CheckViolation>();
        var blocksById = library.Blocks.GroupBy(b => b.Id, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        foreach (var template in library.Templates)
        {
            var type = library.Types.FirstOrDefault(t => string.Equals(t.Id, template.Type, StringComparison.Ordinal));
            if (type is null)
            {
                continue;
            }

            var required = template.Sections
                .SelectMany(s => s.Required)
                .Select(id => blocksById.GetValueOrDefault(id))
                .OfType<ContentBlock>()
                .ToList();
            foreach (var entry in type.RequiredElements)
            {
                var (name, count, error) = ParseEntry(entry);
                if (error is not null)
                {
                    result.Add(new CheckViolation(TemplateStructureCheck, $"typ {type.Id}: wymagany element '{entry}': {error}", Template: template.Id));
                    continue;
                }

                var have = Count(name, template, required);
                if (have is null)
                {
                    result.Add(new CheckViolation(TemplateStructureCheck, $"typ {type.Id}: nieznany wymagany element '{name}'", Template: template.Id));
                }
                else if (have < count)
                {
                    result.Add(new CheckViolation(
                        TemplateStructureCheck,
                        $"szablon {template.Id} (typ {type.Id}): brak elementu '{entry}' (jest {have})",
                        Template: template.Id));
                }
            }
        }

        return Sort(result);
    }

    private static (string Name, int Count, string? Error) ParseEntry(string entry)
    {
        var idx = entry.IndexOf(':', StringComparison.Ordinal);
        if (idx < 0)
        {
            return (entry.Trim(), 1, null);
        }

        var name = entry[..idx].Trim();
        if (!int.TryParse(entry[(idx + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var n) || n < 1)
        {
            return (name, 0, "niepoprawna liczba");
        }

        return (name, n, null);
    }

    private static int? Count(string name, DocumentTemplate template, List<ContentBlock> blocks)
    {
        var elements = blocks.SelectMany(b => b.Elements).ToList();
        switch (name)
        {
            case "okladka": return template.Front.Cover ? 1 : 0;
            case "metryczka": return template.Front.RecordCard ? 1 : 0;
            case "rozdzialy": return Sections(template, "rozdzial");
            case "segmenty": return Sections(template, "sekcja-taryfy");
            case "zalacznik": return Sections(template, "zalacznik");
            case "paragrafy": return blocks.Count(b => b.Unit is not null && b.Unit.StartsWith('§'));
            case "ustepy": return elements.OfType<SourceClause>().Count();
            case "punkty": return elements.OfType<SourceClause>().Count(c => c.Points.Count > 0);
            case "litery": return elements.OfType<SourceClause>().SelectMany(c => c.Points).Count(p => p.Letters.Count > 0);
            case "definicje": return blocks.Count(b => b.Category is "definicje" or "definicja");
            case "przypis": return blocks.Sum(b => b.Footnotes.Count) + elements.OfType<SourceTable>().Sum(t => t.Notes.Count);
            case "tabela": return elements.Count(e => e is SourceTable or SourceTariffItems);
            case "pozycje-taryfy": return elements.OfType<SourceTariffItems>().Sum(t => t.Items.Sum(CountItems));
            case "kroki": return elements.OfType<SourceSteps>().Count();
            case "kroki-poziomy": return elements.OfType<SourceSteps>().Select(s => Depth(s.Steps)).DefaultIfEmpty(0).Max();
            case "schemat": return elements.OfType<SourceScheme>().Count();
            case "lista-kontrolna": return elements.OfType<SourceChecklist>().Count();
            case "ramka": return elements.OfType<SourceCallout>().Count();
            case "odwolanie-taryfa": return CountText(blocks, "{{ref:dokument:taryfa");
            case "odwolanie-akt": return CountText(blocks, "{{ref:akt:");
            case "odwolanie-procedura": return CountText(blocks, "{{ref:dokument:procedura");
            default: return null;
        }
    }

    private static int Sections(DocumentTemplate template, string kind)
        => template.Sections.Count(s => string.Equals(s.Kind, kind, StringComparison.Ordinal));

    private static int CountItems(SourceTariffItem item) => 1 + item.Children.Sum(CountItems);

    private static int Depth(IReadOnlyList<SourceStep> steps)
        => steps.Count == 0 ? 0 : 1 + steps.Max(s => Depth(s.Children));

    private static int CountText(List<ContentBlock> blocks, string needle)
        => blocks.Sum(b => Texts(b).Count(t => t.Contains(needle, StringComparison.Ordinal)));

    private static IEnumerable<string> Texts(ContentBlock block)
    {
        if (block.Title is not null)
        {
            yield return block.Title;
        }

        foreach (var footnote in block.Footnotes.Values)
        {
            yield return footnote;
        }

        foreach (var element in block.Elements)
        {
            foreach (var t in ElementTexts(element))
            {
                yield return t;
            }
        }
    }

    private static IEnumerable<string> ElementTexts(SourceElement element)
    {
        switch (element)
        {
            case SourceParagraph p:
                yield return p.Text;
                break;
            case SourceHeading h:
                yield return h.Text;
                break;
            case SourceClause c:
                yield return c.Text;
                foreach (var point in c.Points)
                {
                    yield return point.Text;
                    foreach (var letter in point.Letters)
                    {
                        yield return letter;
                    }
                }

                break;
            case SourceTable t:
                foreach (var col in t.Columns)
                {
                    yield return col.Header;
                }

                foreach (var cell in t.Rows.SelectMany(r => r))
                {
                    yield return cell;
                }

                foreach (var note in t.Notes)
                {
                    yield return note;
                }

                break;
            case SourceTariffItems ti:
                foreach (var item in ti.Items.SelectMany(Flatten))
                {
                    yield return item.Service;
                    yield return item.Rate;
                    yield return item.Mode;
                }

                break;
            case SourceSteps s:
                foreach (var step in s.Steps.SelectMany(FlattenSteps))
                {
                    yield return step.Text;
                }

                break;
            case SourceScheme sc:
                foreach (var step in sc.Steps)
                {
                    yield return step.Name;
                    foreach (var e in step.Explanation)
                    {
                        yield return e;
                    }
                }

                break;
            case SourceChecklist cl:
                foreach (var item in cl.Items)
                {
                    yield return item;
                }

                break;
            case SourceCallout co:
                yield return co.Text;
                break;
            case SourceRecordCard rc:
                foreach (var pair in rc.Pairs)
                {
                    yield return pair.Key;
                    yield return pair.Value;
                }

                break;
            default:
                break;
        }
    }

    private static IEnumerable<SourceTariffItem> Flatten(SourceTariffItem item)
        => new[] { item }.Concat(item.Children.SelectMany(Flatten));

    private static IEnumerable<SourceStep> FlattenSteps(SourceStep step)
        => new[] { step }.Concat(step.Children.SelectMany(FlattenSteps));

    private static string Normalize(string text)
        => string.Join(' ', text.ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string FoldDiacritics(string text)
    {
        var decomposed = text.Replace('Ł', 'L').Replace('ł', 'l').Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(ch);
            }
        }

        return Normalize(sb.ToString());
    }

    private static List<CheckViolation> Sort(List<CheckViolation> list)
    {
        list.Sort((a, b) =>
        {
            var c = string.CompareOrdinal(a.DocumentId, b.DocumentId);
            if (c != 0)
            {
                return c;
            }

            c = string.CompareOrdinal(a.BlockId, b.BlockId);
            if (c != 0)
            {
                return c;
            }

            c = string.CompareOrdinal(a.Template, b.Template);
            return c != 0 ? c : string.CompareOrdinal(a.Message, b.Message);
        });
        return list;
    }
}
