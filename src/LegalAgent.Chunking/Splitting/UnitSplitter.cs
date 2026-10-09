using LegalAgent.Chunking.Model;
using LegalAgent.PdfParser.Model;

namespace LegalAgent.Chunking.Splitting;

/// <summary>One part of a unit: rendered content and its metadata.</summary>
/// <param name="Content">Markdown of the part.</param>
/// <param name="ListLabels">Labels of the list item the part starts with and of its ancestors, outermost first.</param>
/// <param name="Pages">Source pages of the part.</param>
/// <param name="ExceedsLimit">True when a single indivisible element is longer than the limit.</param>
internal sealed record UnitPart(string Content, IReadOnlyList<string> ListLabels, PageSpan Pages, bool ExceedsLimit);

/// <summary>
/// Splits a unit into parts within the length limit (research R3). The unit's blocks are broken into atoms
/// (indivisible pieces: a paragraph, a table row, a list item's own text with its paragraphs, every nested item
/// separately) that
/// are packed greedily in document order; the length of a candidate part is measured by rendering it. A unit that
/// fits is one part. Every part is rendered from blocks rebuilt from its atoms, so a list keeps its nesting and a
/// part starting inside a nested list starts with that list at column 0.
/// </summary>
internal static class UnitSplitter
{
    /// <summary>The parts of <paramref name="unit"/>, in document order.</summary>
    public static IReadOnlyList<UnitPart> Split(Unit unit, FragmentRenderer renderer, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(unit);
        ArgumentNullException.ThrowIfNull(renderer);

        string whole = renderer.Render(unit.Section, unit.Blocks, unit.Footnotes);
        if (whole.Length <= maxLength)
        {
            return [new UnitPart(whole, [], Pages(unit), false)];
        }

        List<Atom> atoms = Atoms(unit.Blocks);
        var groups = new List<List<Atom>>();
        var current = new List<Atom>();
        foreach (Atom atom in atoms)
        {
            if (current.Count > 0 && Render(unit, renderer, [.. current, atom], last: false).Length > maxLength)
            {
                groups.Add(current);
                current = [];
            }

            current.Add(atom);
        }

        if (current.Count > 0 || groups.Count == 0)
        {
            groups.Add(current);
        }

        var parts = new List<UnitPart>(groups.Count);
        for (int g = 0; g < groups.Count; g++)
        {
            string content = Render(unit, renderer, groups[g], last: g == groups.Count - 1);
            IReadOnlyList<string> labels = g > 0 && groups[g].Count > 0 && groups[g][0] is ItemAtom first ? first.Node.Labels() : [];
            parts.Add(new UnitPart(content, labels, Pages(unit), content.Length > maxLength));
        }

        return parts;
    }

    private static string Render(Unit unit, FragmentRenderer renderer, IReadOnlyList<Atom> atoms, bool last) =>
        renderer.Render(unit.Section, Blocks(atoms), last ? unit.Footnotes : []);

    private static List<Atom> Atoms(IReadOnlyList<ContentBlock> blocks)
    {
        var atoms = new List<Atom>();
        foreach (ContentBlock block in blocks)
        {
            if (block is ListBlock list)
            {
                AddItems(list, null, -1, atoms);
            }
            else if (block is TableBlock { Rows.Count: > 0 } table)
            {
                atoms.AddRange(Enumerable.Range(0, table.Rows.Count).Select(r => new RowAtom(table, r)));
            }
            else
            {
                atoms.Add(new BlockAtom(block));
            }
        }

        return atoms;
    }

    // Pre-order: an item's own atom, then the items of its nested lists.
    private static void AddItems(ListBlock list, ItemNode? parent, int childIndex, List<Atom> atoms)
    {
        foreach (ListItem item in list.Items)
        {
            var node = new ItemNode(item, list, parent, childIndex);
            atoms.Add(new ItemAtom(node));
            for (int k = 0; k < item.Children.Count; k++)
            {
                if (item.Children[k] is ListBlock nested)
                {
                    AddItems(nested, node, k, atoms);
                }
            }
        }
    }

    private static List<ContentBlock> Blocks(IReadOnlyList<Atom> atoms)
    {
        var blocks = new List<ContentBlock>();
        int i = 0;
        while (i < atoms.Count)
        {
            if (atoms[i] is BlockAtom block)
            {
                blocks.Add(block.Block);
                i++;
                continue;
            }

            if (atoms[i] is RowAtom first)
            {
                // Rows of one table make one table again, with its header row (FR-223).
                var rows = new List<TableRow>();
                while (i < atoms.Count && atoms[i] is RowAtom row && ReferenceEquals(row.Table, first.Table))
                {
                    rows.Add(row.Table.Rows[row.Row]);
                    i++;
                }

                blocks.Add(first.Table with { Rows = rows });
                continue;
            }

            var run = new List<ItemNode>();
            while (i < atoms.Count && atoms[i] is ItemAtom item)
            {
                run.Add(item.Node);
                i++;
            }

            blocks.AddRange(ListBlocks(run));
        }

        return blocks;
    }

    /// <summary>
    /// Rebuilds lists from a run of item nodes: nodes whose parent is not in the run start a list of their own
    /// (consecutive ones of the same source list share it); the others become nested items of their parent.
    /// </summary>
    private static List<ListBlock> ListBlocks(IReadOnlyList<ItemNode> run)
    {
        var inRun = new HashSet<ItemNode>(run);
        var lists = new List<ListBlock>();
        var roots = new List<ItemNode>();
        foreach (ItemNode node in run.Where(n => n.Parent is null || !inRun.Contains(n.Parent)))
        {
            if (roots.Count > 0 && !(ReferenceEquals(roots[0].Owner, node.Owner) && ReferenceEquals(roots[0].Parent, node.Parent)))
            {
                lists.Add(new ListBlock(roots[0].Owner.Pages, roots.Select(r => Item(r, run)).ToList()));
                roots = [];
            }

            roots.Add(node);
        }

        if (roots.Count > 0)
        {
            lists.Add(new ListBlock(roots[0].Owner.Pages, roots.Select(r => Item(r, run)).ToList()));
        }

        return lists;
    }

    private static ListItem Item(ItemNode node, IReadOnlyList<ItemNode> run)
    {
        var children = new List<ContentBlock>();
        for (int k = 0; k < node.Item.Children.Count; k++)
        {
            ContentBlock child = node.Item.Children[k];
            if (child is not ListBlock nested)
            {
                children.Add(child);
                continue;
            }

            List<ListItem> items = run.Where(n => ReferenceEquals(n.Parent, node) && n.ChildIndex == k).Select(n => Item(n, run)).ToList();
            if (items.Count > 0)
            {
                children.Add(new ListBlock(nested.Pages, items));
            }
        }

        return node.Item with { Children = children };
    }

    private static PageSpan Pages(Unit unit)
    {
        if (unit.Section is { } section)
        {
            return new PageSpan(section.Pages.First, section.Pages.Last);
        }

        IEnumerable<int> firsts = unit.Blocks.Select(b => b.Pages.First).Concat(unit.Footnotes.Select(f => f.Page));
        IEnumerable<int> lasts = unit.Blocks.Select(b => b.Pages.Last).Concat(unit.Footnotes.Select(f => f.Page));
        return new PageSpan(firsts.DefaultIfEmpty(1).Min(), lasts.DefaultIfEmpty(1).Max());
    }

    /// <summary>An indivisible piece of a unit's content.</summary>
    private abstract record Atom;

    /// <summary>A whole block (paragraph, table without body rows).</summary>
    private sealed record BlockAtom(ContentBlock Block) : Atom;

    /// <summary>One body row of a table; rendered under the table's header row.</summary>
    private sealed record RowAtom(TableBlock Table, int Row) : Atom;

    /// <summary>A list item's own text and paragraphs, without its nested lists.</summary>
    private sealed record ItemAtom(ItemNode Node) : Atom;

    /// <summary>A list item in its source tree.</summary>
    /// <param name="item">The item.</param>
    /// <param name="owner">The list holding the item.</param>
    /// <param name="parent">The item whose nested list holds this one; null at the top level.</param>
    /// <param name="childIndex">Index of <paramref name="owner"/> in the parent's children; -1 at the top level.</param>
    private sealed class ItemNode(ListItem item, ListBlock owner, ItemNode? parent, int childIndex)
    {
        public ListItem Item { get; } = item;

        public ListBlock Owner { get; } = owner;

        public ItemNode? Parent { get; } = parent;

        public int ChildIndex { get; } = childIndex;

        /// <summary>Labels of the ancestors and the item, outermost first.</summary>
        public List<string> Labels()
        {
            var labels = new List<string>();
            for (ItemNode? n = this; n is not null; n = n.Parent)
            {
                labels.Insert(0, n.Item.Label);
            }

            return labels;
        }
    }
}
