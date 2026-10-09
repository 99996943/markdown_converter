using System.Globalization;
using LegalAgent.Corpus.Content;
using LegalAgent.Corpus.Random;

namespace LegalAgent.Corpus.Planning;

/// <summary>Plans the base documents of a corpus run.</summary>
public static class CorpusPlanner
{
    /// <summary>Plans the documents: the base documents, their versions, outdated documents and contradiction pairs.</summary>
    /// <param name="content">The loaded content.</param>
    /// <param name="parameters">The run parameters.</param>
    /// <returns>The plan: documents ordered by type (typy.yaml order) then id.</returns>
    /// <exception cref="CorpusGenerationException">The content cannot satisfy the parameters.</exception>
    public static CorpusPlan Plan(ContentLibrary content, RunParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(parameters);

        var requested = parameters.Types;
        if (requested is not null)
        {
            foreach (string id in requested)
            {
                if (!content.Types.Any(t => string.Equals(t.Id, id, StringComparison.Ordinal)))
                {
                    throw new CorpusGenerationException($"Nieznany typ dokumentu '{id}' w parametrach przebiegu.");
                }
            }
        }

        var requiredBlocks = content.Templates
            .SelectMany(t => t.Sections)
            .SelectMany(s => s.Required)
            .ToHashSet(StringComparer.Ordinal);

        var types = new List<List<DocumentPlan>>();
        foreach (DocumentTypeDef type in content.Types)
        {
            if (requested is not null && !requested.Contains(type.Id, StringComparer.Ordinal))
            {
                continue;
            }

            types.Add(PlanType(content, parameters, type, requiredBlocks));
        }

        List<DocumentPlan> documents = DocumentStates.Apply(content, parameters, types);
        documents.AddRange(PoisonPlanner.Plan(content, parameters, documents));
        return new CorpusPlan(documents);
    }

    /// <summary>
    /// Facts a document of <paramref name="template"/> certainly states: used by its required blocks outside variant
    /// groups (<c>{a|b}</c>), in ordinal order.
    /// </summary>
    public static IReadOnlyList<string> GuaranteedFacts(ContentLibrary content, DocumentTemplate template)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(template);
        return DocumentStates.GuaranteedFacts(content, template);
    }

    private static List<DocumentPlan> PlanType(
        ContentLibrary content,
        RunParameters parameters,
        DocumentTypeDef type,
        HashSet<string> requiredBlocks)
    {
        ulong seed = parameters.Seed;
        int count = parameters.DocumentsPerType;
        int width = Math.Max(2, count.ToString(CultureInfo.InvariantCulture).Length);

        var templates = content.Templates
            .Where(t => string.Equals(t.Type, type.Id, StringComparison.Ordinal))
            .OrderBy(t => t.Id, StringComparer.Ordinal)
            .ToList();
        if (templates.Count == 0)
        {
            throw new CorpusGenerationException($"Brak szablonów dla typu '{type.Id}'.");
        }

        DeterministicRandom.Derive(seed, "szablony", type.Id).Shuffle(templates);

        var ids = new string[count];
        var docTemplates = new DocumentTemplate[count];
        for (int i = 0; i < count; i++)
        {
            ids[i] = type.Prefix + "-" + (i + 1).ToString(CultureInfo.InvariantCulture).PadLeft(width, '0');
            docTemplates[i] = templates[i % templates.Count];
        }

        string[] layouts = AssignLayouts(type, seed, ids, docTemplates);
        List<string>[] pools = DealPools(content, type, seed, ids, docTemplates, requiredBlocks);

        var result = new List<DocumentPlan>(count);
        for (int i = 0; i < count; i++)
        {
            string id = ids[i];
            string nn = id[(type.Prefix.Length + 1)..];
            var pages = parameters.Pages;
            int targetPages = pages.Min + DeterministicRandom.Derive(seed, "strony", id).Next(pages.Max - pages.Min + 1);
            DateOnly shifted = parameters.ReferenceDate.AddMonths(-(1 + DeterministicRandom.Derive(seed, "od", id).Next(18)));
            pools[i].Sort(StringComparer.Ordinal);
            result.Add(new DocumentPlan
            {
                Id = id,
                Type = type.Id,
                Prefix = type.Prefix,
                Designation = type.DesignationPattern
                    .Replace("{prefiks}", type.Prefix, StringComparison.Ordinal)
                    .Replace("{nn}", nn, StringComparison.Ordinal),
                Template = docTemplates[i].Id,
                Layout = layouts[i],
                ValidFrom = new DateOnly(shifted.Year, shifted.Month, 1),
                BlockPool = pools[i],
                TargetPages = targetPages,
                Seed = DeterministicRandom.DeriveSeed(seed, "dokument", id),
            });
        }

        return result;
    }

    private static string[] AssignLayouts(DocumentTypeDef type, ulong seed, string[] ids, DocumentTemplate[] docTemplates)
    {
        var layouts = new string?[ids.Length];
        foreach (var (layout, k) in type.MinLayouts.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            int taken = 0;
            for (int i = 0; i < ids.Length && taken < k; i++)
            {
                if (layouts[i] is null && docTemplates[i].Layouts.Contains(layout, StringComparer.Ordinal))
                {
                    layouts[i] = layout;
                    taken++;
                }
            }

            if (taken < k)
            {
                throw new CorpusGenerationException(
                    $"Typ '{type.Id}': szablony nie pozwalają na {k.ToString(CultureInfo.InvariantCulture)} dokumentów z układem '{layout}' "
                    + $"(możliwe tylko {taken.ToString(CultureInfo.InvariantCulture)}).");
            }
        }

        for (int i = 0; i < ids.Length; i++)
        {
            if (layouts[i] is null)
            {
                var allowed = docTemplates[i].Layouts;
                layouts[i] = allowed[DeterministicRandom.Derive(seed, "uklad", ids[i]).Next(allowed.Count)];
            }
        }

        return layouts!;
    }

    private static List<string>[] DealPools(
        ContentLibrary content,
        DocumentTypeDef type,
        ulong seed,
        string[] ids,
        DocumentTemplate[] docTemplates,
        HashSet<string> requiredBlocks)
    {
        var pools = new List<string>[ids.Length];
        for (int i = 0; i < pools.Length; i++)
        {
            pools[i] = [];
        }

        var categories = docTemplates
            .Select(t => t.Sections
                .Where(s => s.Optional is not null)
                .SelectMany(s => s.Optional!.Categories)
                .ToHashSet(StringComparer.Ordinal))
            .ToArray();

        var candidates = content.Blocks
            .Where(b => !b.Shared
                && b.Types.Contains(type.Id, StringComparer.Ordinal)
                && !requiredBlocks.Contains(b.Id))
            .OrderBy(b => b.Id, StringComparer.Ordinal)
            .ToList();

        var dealt = new HashSet<string>(StringComparer.Ordinal);
        var topics = docTemplates.Select(t => t.Topic).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        foreach (string topic in topics)
        {
            var group = Enumerable.Range(0, ids.Length)
                .Where(i => string.Equals(docTemplates[i].Topic, topic, StringComparison.Ordinal))
                .ToList();
            var blocks = candidates.Where(b => b.Topics.Contains(topic, StringComparer.Ordinal) && !dealt.Contains(b.Id)).ToList();
            Deal(blocks, group, categories, pools, dealt, DeterministicRandom.Derive(seed, "pula", type.Id + "/" + topic));
        }

        var typeWide = candidates.Where(b => b.Topics.Count == 0).ToList();
        Deal(typeWide, [.. Enumerable.Range(0, ids.Length)], categories, pools, dealt, DeterministicRandom.Derive(seed, "pula", type.Id + "/*"));
        return pools;
    }

    private static void Deal(
        List<ContentBlock> blocks,
        List<int> group,
        HashSet<string>[] categories,
        List<string>[] pools,
        HashSet<string> dealt,
        DeterministicRandom random)
    {
        random.Shuffle(blocks);
        int next = 0;
        foreach (ContentBlock block in blocks)
        {
            for (int step = 0; step < group.Count; step++)
            {
                int pos = (next + step) % group.Count;
                int doc = group[pos];
                if (categories[doc].Contains(block.Category))
                {
                    pools[doc].Add(block.Id);
                    dealt.Add(block.Id);
                    next = pos + 1;
                    break;
                }
            }
        }
    }
}
