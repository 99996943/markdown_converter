using System.Globalization;
using LegalAgent.Corpus.Content;
using LegalAgent.Corpus.Planning;
using LegalAgent.Corpus.Random;
using LegalAgent.Corpus.Validation;

namespace LegalAgent.Corpus.Composition;

/// <summary>Result of composing one document.</summary>
/// <param name="Document">The element tree ready for typesetting.</param>
/// <param name="Blocks">Rendered plain text of every block used (uniqueness check, FR-103b).</param>
/// <param name="Unresolved">Cross references that point to no unit of the document (FR-114).</param>
/// <param name="FactElements">Fact id → ids of the elements whose text uses it (manifest changes and contradictions).</param>
/// <param name="OptionalBlocks">Number of optional blocks actually used.</param>
public sealed record CompositionResult(
    ComposedDocument Document,
    IReadOnlyList<RenderedBlock> Blocks,
    IReadOnlyList<UnresolvedReference> Unresolved,
    IReadOnlyDictionary<string, IReadOnlyList<string>> FactElements,
    int OptionalBlocks)
{
    /// <summary>Gets the places of the document's poison (FR-142); empty for documents that are not poisoned.</summary>
    public IReadOnlyList<ComposedPoison> Poison { get; init; } = [];

    /// <summary>Gets, per fact id, its uses in document order: the element and, in a tariff row, the position („poz. 4.7”).</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<FactUse>> FactUses { get; init; } = new Dictionary<string, IReadOnlyList<FactUse>>(StringComparer.Ordinal);
}

/// <summary>A place of a poison in the composed document.</summary>
/// <param name="ElementId">The element holding the text; <c>okladka</c> or <c>metryczka</c> for the front matter.</param>
/// <param name="Element">The kind of place: <c>akapit</c>, <c>przypis</c>, <c>komorka-tabeli</c>, <c>metryczka</c>, <c>okladka</c>, <c>ramka</c>.</param>
/// <param name="Text">The verbatim poison text as printed.</param>
public sealed record ComposedPoison(string ElementId, string Element, string Text);

/// <summary>A use of a fact in the composed document.</summary>
/// <param name="ElementId">The element whose text states the fact (empty: the front matter).</param>
/// <param name="Unit">The tariff position stating it, or null to take the element's unit.</param>
public sealed record FactUse(string ElementId, string? Unit);

/// <summary>
/// Turns a <see cref="DocumentPlan"/> into a <see cref="ComposedDocument"/>: renders the template sections with their
/// required blocks and a number of optional blocks from the document's pool, numbers the units („§ N.”, tariff
/// positions, procedure steps, annexes), resolves cross references and footnotes, and fills the front matter.
/// </summary>
/// <remarks>
/// Units: a block with <c>jednostka</c> („§ {n}.”) is a level-3 heading numbered continuously in the document; a unit
/// with two or more clauses lists them „1.”, „2.” with points „1)” and letters „a)”; a single clause is a paragraph
/// whose points start at depth 0. Sections: <c>rozdzial</c> → „Rozdział N”, <c>sekcja</c> → „N.” (procedure steps
/// inside are „N.k.”, „N.k.m.”), <c>sekcja-taryfy</c> → „I.”, „II.” with one tariff table per section (positions
/// „1.”, „1.1.” numbered through the document; footnotes of a table are notes „1) …” under it), <c>zalacznik</c> →
/// a new page and „Załącznik nr N”. Variants of a block are drawn from a stream keyed by the document seed and the
/// block id, so a block reads the same whatever else the document contains.
/// </remarks>
public static class DocumentComposer
{
    /// <summary>The fictional bank of the corpus (FR-105).</summary>
    public const string BankName = "Bank Przykładowy S.A.";

    private const string DefaultApprover = "Zarząd Banku Przykładowego S.A.";

    /// <summary>Title of documents made from <paramref name="template"/> (the same in every document of the run).</summary>
    public static string Title(DocumentTemplate template, ContentLibrary content, ulong runSeed)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(content);
        var random = DeterministicRandom.Derive(runSeed, "tytul", template.Id);
        string variant = template.TitleVariants[random.Next(template.TitleVariants.Count)];
        return Inline.PlainText(TextTemplate.Render(variant, new TitleContext(template, content), random)).Trim();
    }

    /// <summary>The largest number of optional blocks the document can take.</summary>
    public static int OptionalCapacity(DocumentPlan plan, ContentLibrary content)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(content);
        return Select(plan, content, TemplateOf(plan, content), int.MaxValue).Sum(s => s.Count);
    }

    /// <summary>Composes <paramref name="plan"/> with <paramref name="optionalBlocks"/> optional blocks (at least the section minima).</summary>
    public static CompositionResult Compose(DocumentPlan plan, ContentLibrary content, ulong runSeed, int optionalBlocks)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(content);
        DocumentTemplate template = TemplateOf(plan, content);
        List<List<string>> optional = Select(plan, content, template, optionalBlocks);
        return new Composition(plan, content, template, runSeed, optional).Run();
    }

    internal static string Roman(int n)
    {
        (int Value, string Symbol)[] map = [(1000, "M"), (900, "CM"), (500, "D"), (400, "CD"), (100, "C"), (90, "XC"), (50, "L"), (40, "XL"), (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I")];
        var sb = new System.Text.StringBuilder();
        foreach ((int value, string symbol) in map)
        {
            while (n >= value)
            {
                sb.Append(symbol);
                n -= value;
            }
        }

        return sb.ToString();
    }

    private static DocumentTemplate TemplateOf(DocumentPlan plan, ContentLibrary content) =>
        content.Templates.FirstOrDefault(t => t.Id == plan.Template)
        ?? throw new CorpusGenerationException("Nieznany szablon: " + plan.Template) { DocumentId = plan.Id, Template = plan.Template };

    /// <summary>Optional blocks per section: the minima first, then round-robin up to the maxima while the budget lasts.</summary>
    private static List<List<string>> Select(DocumentPlan plan, ContentLibrary content, DocumentTemplate template, int budget)
    {
        var byId = content.Blocks.ToDictionary(b => b.Id, StringComparer.Ordinal);
        var pool = plan.BlockPool.Where(byId.ContainsKey).ToList();
        DeterministicRandom.Derive(plan.Seed, "opcjonalne", plan.Id).Shuffle(pool);

        var result = template.Sections.Select(_ => new List<string>()).ToList();
        var used = new HashSet<string>(StringComparer.Ordinal);
        int total = 0;

        bool Take(int slot)
        {
            OptionalSpec? spec = template.Sections[slot].Optional;
            if (spec is null || result[slot].Count >= spec.Max)
            {
                return false;
            }

            string? block = pool.FirstOrDefault(id => !used.Contains(id) && spec.Categories.Contains(byId[id].Category, StringComparer.Ordinal));
            if (block is null)
            {
                return false;
            }

            used.Add(block);
            result[slot].Add(block);
            total++;
            return true;
        }

        for (int slot = 0; slot < template.Sections.Count; slot++)
        {
            int min = template.Sections[slot].Optional?.Min ?? 0;
            while (result[slot].Count < min && Take(slot))
            {
            }
        }

        bool progress = true;
        while (total < budget && progress)
        {
            progress = false;
            for (int slot = 0; slot < template.Sections.Count && total < budget; slot++)
            {
                progress |= Take(slot);
            }
        }

        return result;
    }

    /// <summary>Template context for titles: the bank and the template parameters.</summary>
    private sealed class TitleContext(DocumentTemplate template, ContentLibrary content) : ITemplateContext
    {
        public (FactKind Kind, FactValue Value) Fact(string id)
        {
            Fact fact = content.Facts.Get(id);
            return (fact.Kind, fact.Values[0].Value);
        }

        public string Param(string name) =>
            name == "bank" ? BankName
            : template.Parameters.TryGetValue(name, out string? value) ? value
            : throw new ContentException("Nieznany parametr w tytule: " + name);
    }

    /// <summary>One composition run.</summary>
    private sealed class Composition : ITemplateContext
    {
        private readonly DocumentPlan _plan;
        private readonly ContentLibrary _content;
        private readonly DocumentTemplate _template;
        private readonly ulong _runSeed;
        private readonly List<List<string>> _optional;
        private readonly Dictionary<string, ContentBlock> _blocks;
        private readonly Dictionary<string, FactValue> _overrides;
        private readonly List<Element> _elements = [];
        private readonly Dictionary<int, IReadOnlyList<Inline>> _footnotes = [];
        private readonly Dictionary<(string Block, string Key), int> _footnoteNumbers = [];
        private readonly Dictionary<string, string> _unitLabels = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _annexLabels = new(StringComparer.Ordinal);
        private readonly List<RenderedBlock> _rendered = [];
        private readonly List<UnresolvedReference> _unresolved = [];
        private readonly Dictionary<string, List<string>> _factElements = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<FactUse>> _factUses = new(StringComparer.Ordinal);
        private string? _currentPosition;

        private int _elementCounter;
        private string _currentElement = string.Empty;
        private ContentBlock? _currentBlock;
        private DeterministicRandom _random;
        private int _position;
        private string? _sectionNumber;
        private int _stepCounter;
        private TariffTable? _tariff;
        private readonly System.Text.StringBuilder _blockExtra = new();

        public Composition(DocumentPlan plan, ContentLibrary content, DocumentTemplate template, ulong runSeed, List<List<string>> optional)
        {
            _plan = plan;
            _content = content;
            _template = template;
            _runSeed = runSeed;
            _optional = optional;
            _blocks = content.Blocks.ToDictionary(b => b.Id, StringComparer.Ordinal);
            _overrides = plan.FactOverrides.GroupBy(o => o.FactId, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Last().Value, StringComparer.Ordinal);
            _random = new DeterministicRandom(plan.Seed);
        }

        public CompositionResult Run()
        {
            Number();
            for (int slot = 0; slot < _template.Sections.Count; slot++)
            {
                Section(slot);
            }

            FlushTariff();
            FrontMatter front = Front();
            var poison = new List<ComposedPoison>();
            if (_plan.Poison is { } plan)
            {
                front = ApplyPoison(plan, front, poison);
            }

            var document = new ComposedDocument(_plan.Id, _plan.Layout, front, _elements, _footnotes) { Poison = poison };
            return new CompositionResult(
                document,
                _rendered,
                _unresolved,
                _factElements.ToDictionary(p => p.Key, p => (IReadOnlyList<string>)p.Value, StringComparer.Ordinal),
                _optional.Sum(s => s.Count))
            {
                FactUses = _factUses.ToDictionary(p => p.Key, p => (IReadOnlyList<FactUse>)p.Value, StringComparer.Ordinal),
                Poison = poison,
            };
        }

        // ------------------------------------------------------------ ITemplateContext

        public (FactKind Kind, FactValue Value) Fact(string id)
        {
            Fact fact = _content.Facts.Get(id);
            if (!_factElements.TryGetValue(id, out List<string>? elements))
            {
                elements = [];
                _factElements[id] = elements;
            }

            if (_currentElement.Length > 0 && (elements.Count == 0 || elements[^1] != _currentElement))
            {
                elements.Add(_currentElement);
            }

            var use = new FactUse(_currentElement, _currentPosition);
            if (!_factUses.TryGetValue(id, out List<FactUse>? uses))
            {
                uses = [];
                _factUses[id] = uses;
            }

            if (!uses.Contains(use))
            {
                uses.Add(use);
            }

            return (fact.Kind, _content.Facts.ValueAt(id, _plan.ValidFrom, _overrides));
        }

        public string Param(string name) => name switch
        {
            "bank" => BankName,
            "oznaczenie" => _plan.Designation,
            "wersja" => _plan.Version.ToString(CultureInfo.InvariantCulture),
            "od" => PolishFormat.Date(_plan.ValidFrom),
            "do" => _plan.ValidTo is { } to ? PolishFormat.Date(to) : throw new ContentException("Parametr 'do' w dokumencie bez daty końca: " + _plan.Id),
            "jednostka" => Parameter("jednostka") ?? Parameter("wlasciciel") ?? throw new ContentException("Szablon nie ma parametru 'jednostka': " + _template.Id),
            _ => Parameter(name) ?? throw new ContentException($"Nieznany parametr '{name}' (szablon {_template.Id})"),
        };

        private string? Parameter(string name) => _template.Parameters.TryGetValue(name, out string? value) ? value : null;

        // ------------------------------------------------------------ numbering pre-pass (for references)

        private IEnumerable<string> BlocksOf(int slot) =>
            _template.Sections[slot].Required.Concat(_optional[slot]);

        private void Number()
        {
            int unit = 0;
            int annex = 0;
            for (int slot = 0; slot < _template.Sections.Count; slot++)
            {
                bool isAnnex = _template.Sections[slot].Kind == "zalacznik";
                if (isAnnex)
                {
                    annex++;
                }

                foreach (string id in BlocksOf(slot))
                {
                    ContentBlock block = _blocks[id];
                    if (block.Unit is { } pattern)
                    {
                        unit++;
                        _unitLabels[id] = pattern.Replace("{n}", unit.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
                    }

                    if (isAnnex)
                    {
                        _annexLabels[id] = "Załącznik nr " + annex.ToString(CultureInfo.InvariantCulture);
                    }
                }
            }
        }

        // ------------------------------------------------------------ sections and blocks

        private int _chapter;
        private int _section;
        private int _segment;
        private int _annex;

        private void Section(int slot)
        {
            FlushTariff();
            SectionSlot spec = _template.Sections[slot];
            _random = DeterministicRandom.Derive(_plan.Seed, "sekcja", slot.ToString(CultureInfo.InvariantCulture));
            _currentBlock = null;
            IReadOnlyList<Inline> title = spec.Title is null ? [] : Resolve(TextTemplate.Render(spec.Title, this, _random));
            _sectionNumber = null;
            _stepCounter = 0;
            string label;
            switch (spec.Kind)
            {
                case "rozdzial":
                    label = "Rozdział " + (++_chapter).ToString(CultureInfo.InvariantCulture);
                    break;
                case "sekcja":
                    _sectionNumber = (++_section).ToString(CultureInfo.InvariantCulture);
                    label = _sectionNumber + ".";
                    break;
                case "sekcja-taryfy":
                    label = Roman(++_segment) + ".";
                    break;
                case "zalacznik":
                    Add(new PageBreakElement());
                    label = "Załącznik nr " + (++_annex).ToString(CultureInfo.InvariantCulture);
                    break;
                default:
                    throw new ContentException("Nieznany rodzaj sekcji: " + spec.Kind);
            }

            Add(new HeadingElement(2, label, title) { Unit = label.TrimEnd('.') });
            foreach (string id in BlocksOf(slot))
            {
                Block(_blocks[id]);
            }
        }

        private void Block(ContentBlock block)
        {
            _currentBlock = block;
            _random = DeterministicRandom.Derive(_plan.Seed, "blok", block.Id);
            int first = _elements.Count;
            string? unit = null;
            if (_unitLabels.TryGetValue(block.Id, out string? label))
            {
                unit = label.TrimEnd('.');
                Add(new HeadingElement(3, label, []) { BlockId = block.Id, Unit = unit });
            }
            else if (block.Title is { } title)
            {
                Add(new HeadingElement(3, null, Render(title)) { BlockId = block.Id });
            }

            int clauses = block.Elements.OfType<SourceClause>().Count();
            int clause = 0;
            foreach (SourceElement element in block.Elements)
            {
                if (element is not SourceTariffItems)
                {
                    FlushTariff();
                }

                switch (element)
                {
                    case SourceParagraph p:
                        AddText(id => new ParagraphElement(Render(p.Text)) { Id = id, BlockId = block.Id, Unit = unit });
                        break;
                    case SourceClause c:
                        clause++;
                        Clause(block, c, unit, clauses >= 2 ? clause : 0);
                        break;
                    case SourceHeading h:
                        AddText(id => new HeadingElement(Math.Clamp(h.Level, 2, 3), null, Render(h.Text)) { Id = id, BlockId = block.Id });
                        break;
                    case SourceTable t:
                        Table(block, t, unit);
                        break;
                    case SourceTariffItems items:
                        Tariff(block, items);
                        break;
                    case SourceSteps steps:
                        Steps(block, steps.Steps, _sectionNumber is null ? string.Empty : _sectionNumber + ".", 0, ref _stepCounter);
                        break;
                    case SourceScheme scheme:
                        AddText(id => new StepSchemeElement(scheme.Steps.Select(s => new SchemeStep(
                            Inline.PlainText(Render(s.Name)),
                            s.Explanation.Select(Render).ToList())).ToList()) { Id = id, BlockId = block.Id, Unit = unit });
                        break;
                    case SourceChecklist list:
                        ChecklistForm form = list.Form ?? (ChecklistForm)DeterministicRandom.Derive(_plan.Seed, "lista", block.Id).Next(3);
                        AddText(id => new ChecklistElement(form, list.Items.Select(Render).ToList()) { Id = id, BlockId = block.Id, Unit = unit });
                        break;
                    case SourceCallout callout:
                        AddText(id => new CalloutElement(Render(callout.Text)) { Id = id, BlockId = block.Id, Unit = unit });
                        break;
                    case SourceRecordCard card:
                        AddText(id => new KeyValueTableElement(card.Pairs.Select(p => new KeyValuePair<string, IReadOnlyList<Inline>>(
                            Inline.PlainText(Render(p.Key)),
                            Render(p.Value))).ToList()) { Id = id, BlockId = block.Id, Unit = unit });
                        break;
                    default:
                        throw new ContentException("Nieobsługiwany element treści: " + element.GetType().Name);
                }
            }

            string text = BlockText(first);
            if (_blockExtra.Length > 0)
            {
                text = (text + " " + _blockExtra).Trim();
                _blockExtra.Clear();
            }

            _rendered.Add(new RenderedBlock(_plan.Id, block.Id, block.Shared, text));
        }

        private void Clause(ContentBlock block, SourceClause clause, string? unit, int number)
        {
            int depth = 0;
            string? clauseUnit = unit;
            if (number > 0)
            {
                string n = number.ToString(CultureInfo.InvariantCulture);
                clauseUnit = unit is null ? null : unit + " ust. " + n;
                AddText(id => new ListItemElement(n + ".", 0, Render(clause.Text)) { Id = id, BlockId = block.Id, Unit = clauseUnit });
                depth = 1;
            }
            else
            {
                AddText(id => new ParagraphElement(Render(clause.Text)) { Id = id, BlockId = block.Id, Unit = unit });
            }

            for (int p = 0; p < clause.Points.Count; p++)
            {
                string pn = (p + 1).ToString(CultureInfo.InvariantCulture);
                string? pointUnit = clauseUnit is null ? null : clauseUnit + " pkt " + pn;
                SourcePoint point = clause.Points[p];
                AddText(id => new ListItemElement(pn + ")", depth, Render(point.Text)) { Id = id, BlockId = block.Id, Unit = pointUnit });
                for (int l = 0; l < point.Letters.Count; l++)
                {
                    string letter = ((char)('a' + l)).ToString();
                    string? letterUnit = pointUnit is null ? null : pointUnit + " lit. " + letter;
                    string text = point.Letters[l];
                    AddText(id => new ListItemElement(letter + ")", depth + 1, Render(text)) { Id = id, BlockId = block.Id, Unit = letterUnit });
                }
            }
        }

        private void Steps(ContentBlock block, IReadOnlyList<SourceStep> steps, string prefix, int depth, ref int counter)
        {
            foreach (SourceStep step in steps)
            {
                counter++;
                string number = prefix + counter.ToString(CultureInfo.InvariantCulture);
                AddText(id => new ListItemElement(number + ".", depth, Render(step.Text)) { Id = id, BlockId = block.Id, Unit = "krok " + number });
                int children = 0;
                Steps(block, step.Children, number + ".", depth + 1, ref children);
            }
        }

        private void Table(ContentBlock block, SourceTable table, string? unit)
        {
            string id = NextId();
            _currentElement = id;
            var notes = new NoteCollector(this, block);
            var rows = table.Rows.Select(r => (IReadOnlyList<TableCell>)r.Select(c => new TableCell(notes.Markers(Render(c)))).ToList()).ToList();
            var allNotes = table.Notes.Select(Render).Concat(notes.Notes).ToList();
            AddElement(new TableElement(table.Columns.Select(c => new TableColumn(c.Header, c.Weight)).ToList(), rows, table.Grid, allNotes)
            {
                Id = id,
                BlockId = block.Id,
                Unit = unit,
            });
        }

        private void Tariff(ContentBlock block, SourceTariffItems items)
        {
            if (_tariff is null)
            {
                _tariff = new TariffTable(NextId(), block.Id);
            }

            _currentElement = _tariff.Id;
            var notes = new NoteCollector(this, block, _tariff.Notes.Count);
            void Rows(IReadOnlyList<SourceTariffItem> list, string prefix)
            {
                int k = 0;
                foreach (SourceTariffItem item in list)
                {
                    string number;
                    if (prefix.Length == 0)
                    {
                        number = (++_position).ToString(CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        number = prefix + (++k).ToString(CultureInfo.InvariantCulture);
                    }

                    _currentPosition = "poz. " + number;
                    IReadOnlyList<TableCell> row =
                    [
                        TableCell.Of(number + "."),
                        new TableCell(notes.Markers(Render(item.Service))),
                        new TableCell(notes.Markers(Render(item.Mode))),
                        new TableCell(notes.Markers(Render(item.Rate))),
                    ];
                    _currentPosition = null;
                    _tariff.Rows.Add(row);

                    // Positions are flushed into the table later; their text still belongs to this block (checks).
                    _blockExtra.Append(' ').Append(string.Join(" ", row.Skip(1).Select(c => Inline.PlainText(c.Text))));
                    Rows(item.Children, number + ".");
                }
            }

            Rows(items.Items, string.Empty);
            _tariff.Notes.AddRange(notes.Notes);
        }

        private void FlushTariff()
        {
            if (_tariff is null)
            {
                return;
            }

            TableColumn[] columns = [new("Lp.", 0.7), new("Wyszczególnienie czynności", 4.2), new("Tryb pobierania", 1.7), new("Stawka", 1.6)];
            _elements.Add(new TableElement(columns, _tariff.Rows, null, _tariff.Notes) { Id = _tariff.Id, BlockId = _tariff.Block, Unit = "poz." });
            _tariff = null;
        }

        // ------------------------------------------------------------ elements and text

        // ------------------------------------------------------------ poison (US4, research R8)

        /// <summary>
        /// Applies the poison pattern of the plan: <c>wstaw</c> puts the text at its place in the style of that place,
        /// <c>nadpisz-fakt</c> is already in the plan's overrides (the place is where the document first states the fact),
        /// <c>zmien-czolo</c> replaces record-card fields, <c>przesun-daty</c> drops the end date from the cover.
        /// </summary>
        private FrontMatter ApplyPoison(PoisonPlan plan, FrontMatter front, List<ComposedPoison> places)
        {
            PoisonPattern pattern = _content.PoisonPatterns.First(p => string.Equals(p.Id, plan.PatternId, StringComparison.Ordinal));
            switch (pattern.Operation)
            {
                case "wstaw":
                    _currentBlock = null;
                    _currentElement = string.Empty;
                    _random = PoisonRandom("tekst");
                    return Insert(plan.Placement, Render(pattern.TextVariants[plan.Variant]), front, places);
                case "nadpisz-fakt":
                    {
                        string fact = pattern.Fact!;
                        FactUse use = _factUses.TryGetValue(fact, out List<FactUse>? uses) && uses.Count > 0
                            ? uses[0]
                            : throw new CorpusGenerationException($"Dokument {_plan.Id} nie zawiera faktu '{fact}' wzorca {pattern.Id}.") { DocumentId = _plan.Id };
                        string printed = PolishFormat.Format(_content.Facts.Get(fact).Kind, _content.Facts.ValueAt(fact, _plan.ValidFrom, _overrides));
                        Element? host = _elements.FirstOrDefault(e => string.Equals(e.Id, use.ElementId, StringComparison.Ordinal));
                        string kind = host switch
                        {
                            null => "metryczka",
                            TableElement => "komorka-tabeli",
                            CalloutElement => "ramka",
                            _ => "akapit",
                        };
                        places.Add(new ComposedPoison(host is null ? "metryczka" : use.ElementId, kind, printed));
                        return front;
                    }

                case "zmien-czolo":
                    foreach ((string field, string value) in pattern.FrontFields.OrderBy(p => p.Key, StringComparer.Ordinal))
                    {
                        front = field switch
                        {
                            "zatwierdzil" => front with { ApprovedBy = value },
                            "wlasciciel" => front with { Owner = value },
                            _ => throw new CorpusGenerationException($"Wzorzec {pattern.Id}: nieznane pole czoła '{field}'.") { DocumentId = _plan.Id },
                        };
                        places.Add(new ComposedPoison("metryczka", "metryczka", value));
                    }

                    return front;
                case "przesun-daty":
                    places.Add(new ComposedPoison("okladka", "okladka", "Obowiązuje od " + front.ValidFrom));
                    return front with { ValidTo = null };
                default:
                    throw new CorpusGenerationException($"Wzorzec {pattern.Id}: nieznana operacja '{pattern.Operation}'.") { DocumentId = _plan.Id };
            }
        }

        private FrontMatter Insert(string placement, IReadOnlyList<Inline> text, FrontMatter front, List<ComposedPoison> places)
        {
            string plain = Inline.PlainText(text);
            switch (placement)
            {
                case "metryczka":
                    places.Add(new ComposedPoison("metryczka", placement, plain));
                    return front with { ExtraFields = [.. front.ExtraFields, new KeyValuePair<string, string>("Uwagi", plain)] };
                case "okladka":
                    places.Add(new ComposedPoison("okladka", placement, plain));
                    return front with { CoverNote = text };
                case "komorka-tabeli":
                    {
                        var tables = Enumerable.Range(0, _elements.Count).Where(i => _elements[i] is TableElement { Rows.Count: > 0 }).ToList();
                        int index = tables.Count > 0
                            ? tables[PoisonRandom("tabela").Next(tables.Count)]
                            : throw new CorpusGenerationException($"Dokument {_plan.Id} nie ma tabeli na zatrucie.") { DocumentId = _plan.Id };
                        var table = (TableElement)_elements[index];
                        int row = PoisonRandom("wiersz").Next(table.Rows.Count);
                        int cell = table.Rows[row].Count > 1 ? 1 : 0;
                        var rows = table.Rows.Select(r => r.ToList()).ToList();
                        TableCell target = rows[row][cell];
                        rows[row][cell] = target with { Text = [.. target.Text, new Inline(" "), .. Restyled(text, target.Text)] };
                        _elements[index] = table with { Rows = rows.Select(r => (IReadOnlyList<TableCell>)r).ToList() };
                        places.Add(new ComposedPoison(table.Id, placement, plain));
                        return front;
                    }

                default:
                    {
                        // akapit, ramka, przypis: a paragraph or clause of the body (for a footnote, one after every
                        // existing footnote reference, so the numbers stay in reading order).
                        int lastNote = _elements.FindLastIndex(e => TextOf(e)?.Any(r => r.Kind == InlineKind.FootnoteRef) == true);
                        var hosts = Enumerable.Range(0, _elements.Count)
                            .Where(i => TextOf(_elements[i]) is { Count: > 0 } && (placement != "przypis" || i > lastNote))
                            .ToList();
                        if (hosts.Count == 0)
                        {
                            hosts = Enumerable.Range(0, _elements.Count).Where(i => TextOf(_elements[i]) is { Count: > 0 }).ToList();
                        }

                        int index = hosts.Count > 0
                            ? hosts[PoisonRandom("akapit").Next(hosts.Count)]
                            : throw new CorpusGenerationException($"Dokument {_plan.Id} nie ma akapitu na zatrucie.") { DocumentId = _plan.Id };
                        Element host = _elements[index];
                        IReadOnlyList<Inline> hostText = TextOf(host)!;
                        if (placement == "ramka")
                        {
                            string id = NextId();
                            _elements.Insert(index + 1, new CalloutElement(text) { Id = id, BlockId = host.BlockId, Unit = host.Unit });
                            places.Add(new ComposedPoison(id, placement, plain));
                            return front;
                        }

                        IReadOnlyList<Inline> appended;
                        if (placement == "przypis")
                        {
                            int number = _footnotes.Count == 0 ? 1 : _footnotes.Keys.Max() + 1;
                            _footnotes[number] = text;
                            appended = [.. hostText, new Inline(number.ToString(CultureInfo.InvariantCulture), hostText[^1].Style, InlineKind.FootnoteRef)];
                        }
                        else
                        {
                            appended = [.. hostText, new Inline(" ", hostText[^1].Style), .. Restyled(text, hostText)];
                        }

                        _elements[index] = host switch
                        {
                            ParagraphElement p => p with { Text = appended },
                            ListItemElement l => l with { Text = appended },
                            _ => host,
                        };
                        places.Add(new ComposedPoison(host.Id, placement, plain));
                        return front;
                    }
            }
        }

        /// <summary>The poison text in the style of the host text it joins (FR-131: no visual difference).</summary>
        private static IEnumerable<Inline> Restyled(IReadOnlyList<Inline> text, IReadOnlyList<Inline> host)
        {
            InlineStyle style = host.Count > 0 ? host[^1].Style : InlineStyle.Regular;
            return text.Select(r => r with { Style = style });
        }

        private static IReadOnlyList<Inline>? TextOf(Element element) => element switch
        {
            ParagraphElement p => p.Text,
            ListItemElement l => l.Text,
            _ => null,
        };

        private DeterministicRandom PoisonRandom(string purpose) => DeterministicRandom.Derive(_plan.Seed, "zatrucie-" + purpose, _plan.Id);


        private string NextId() => "e" + (++_elementCounter).ToString(CultureInfo.InvariantCulture);

        private void Add(Element element) => AddElement(element with { Id = NextId() });

        private void AddElement(Element element) => _elements.Add(element);

        private void AddText(Func<string, Element> create)
        {
            string id = NextId();
            _currentElement = id;
            AddElement(create(id));
        }

        private IReadOnlyList<Inline> Render(string template)
        {
            List<Inline> runs = SinglePeriods(Resolve(TextTemplate.Render(template, this, _random)));
            if (_currentBlock is null)
            {
                return runs;
            }

            // Footnote keys of the block → document-wide numbers (the footnote text is rendered on first use).
            var result = new List<Inline>(runs.Count);
            foreach (Inline run in runs)
            {
                if (run.Kind != InlineKind.FootnoteRef)
                {
                    result.Add(run);
                    continue;
                }

                result.Add(run with { Text = FootnoteNumber(_currentBlock, run.Text).ToString(CultureInfo.InvariantCulture) });
            }

            return result;
        }

        /// <summary>
        /// A value ending with a period („1 maja 2026 r.”, „Bank Przykładowy S.A.”) before the sentence's own period gives
        /// one period, not two (an ellipsis of three periods is kept).
        /// </summary>
        private static System.Text.RegularExpressions.Regex DoublePeriod() => DoublePeriodRegex;

        private static readonly System.Text.RegularExpressions.Regex DoublePeriodRegex = new(@"(?<!\.)\.\.(?!\.)", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        private static List<Inline> SinglePeriods(IReadOnlyList<Inline> runs)
        {
            var result = new List<Inline>(runs.Count);
            foreach (Inline run in runs)
            {
                if (run.Kind != InlineKind.Text)
                {
                    result.Add(run);
                    continue;
                }

                string text = DoublePeriod().Replace(run.Text, ".");
                if (text.StartsWith('.') && !text.StartsWith("..", StringComparison.Ordinal)
                    && result.Count > 0 && result[^1].Kind == InlineKind.Text && result[^1].Text.EndsWith('.') && !result[^1].Text.EndsWith("..", StringComparison.Ordinal))
                {
                    text = text[1..];
                }

                if (text.Length > 0)
                {
                    result.Add(run with { Text = text });
                }
            }

            return result;
        }

        private int FootnoteNumber(ContentBlock block, string key)
        {
            if (_footnoteNumbers.TryGetValue((block.Id, key), out int number))
            {
                return number;
            }

            if (!block.Footnotes.TryGetValue(key, out string? text))
            {
                throw new ContentException($"Blok '{block.Id}' odwołuje się do nieistniejącego przypisu [^{key}]") { File = block.File };
            }

            number = _footnotes.Count + 1;
            _footnoteNumbers[(block.Id, key)] = number;
            _footnotes[number] = Resolve(TextTemplate.Render(text, this, _random));
            return number;
        }

        private IReadOnlyList<Inline> Resolve(IReadOnlyList<Inline> runs)
        {
            if (!runs.Any(r => r.Kind == InlineKind.Reference))
            {
                return runs;
            }

            var result = new List<Inline>(runs.Count);
            bool dropPeriod = false;
            foreach (Inline run in runs)
            {
                Inline current = run;
                if (dropPeriod && current.Kind == InlineKind.Text && current.Text.StartsWith('.'))
                {
                    // A referenced title ending with „S.A.” before the sentence's own period: one period only.
                    current = current with { Text = current.Text[1..] };
                }

                dropPeriod = false;
                if (current.Kind == InlineKind.Reference)
                {
                    current = new Inline(Reference(current.Text), current.Style);
                    dropPeriod = current.Text.EndsWith('.');
                }

                if (current.Text.Length > 0)
                {
                    result.Add(current);
                }
            }

            // Merge adjacent plain runs of one style.
            var merged = new List<Inline>(result.Count);
            foreach (Inline run in result)
            {
                if (merged.Count > 0 && merged[^1].Kind == InlineKind.Text && run.Kind == InlineKind.Text && merged[^1].Style == run.Style)
                {
                    merged[^1] = merged[^1] with { Text = merged[^1].Text + run.Text };
                }
                else
                {
                    merged.Add(run);
                }
            }

            return merged;
        }

        private string Reference(string target)
        {
            int colon = target.IndexOf(':', StringComparison.Ordinal);
            string kind = colon < 0 ? target : target[..colon];
            string id = colon < 0 ? string.Empty : target[(colon + 1)..];
            string? text = kind switch
            {
                "blok" when _unitLabels.TryGetValue(id, out string? unit) => unit.TrimEnd('.'),
                "blok" when _annexLabels.TryGetValue(id, out string? annex) => annex,
                "zalacznik" when _annexLabels.TryGetValue(id, out string? annex) => annex,
                "dokument" => _content.Templates.FirstOrDefault(t => t.Id == id) is { } t ? DocumentComposer.Title(t, _content, _runSeed) : null,
                "akt" => _content.Acts.FirstOrDefault(a => a.Id == id) is { } act ? LowerFirst(act.Title) + " (" + act.Journal + ")" : null,
                _ => null,
            };

            if (text is null)
            {
                _unresolved.Add(new UnresolvedReference(_plan.Id, _currentBlock?.Id ?? "(sekcja)", target));
                return target;
            }

            return text;
        }

        private static string LowerFirst(string text) => text.Length == 0 ? text : char.ToLowerInvariant(text[0]) + text[1..];

        private string BlockText(int first)
        {
            var parts = new List<string>();
            foreach (Element e in _elements.Skip(first))
            {
                parts.Add(e switch
                {
                    HeadingElement h => Inline.PlainText(h.Text),
                    ParagraphElement p => Inline.PlainText(p.Text),
                    ListItemElement li => Inline.PlainText(li.Text),
                    TableElement t => string.Join(" ", t.Rows.SelectMany(r => r).Select(c => Inline.PlainText(c.Text))),
                    StepSchemeElement s => string.Join(" ", s.Steps.Select(x => x.Name + " " + string.Join(" ", x.Explanation.Select(Inline.PlainText)))),
                    ChecklistElement c => string.Join(" ", c.Items.Select(Inline.PlainText)),
                    CalloutElement c => Inline.PlainText(c.Text),
                    KeyValueTableElement kv => string.Join(" ", kv.Pairs.Select(p => p.Key + " " + Inline.PlainText(p.Value))),
                    _ => string.Empty,
                });
            }

            return string.Join(" ", parts.Where(p => p.Length > 0));
        }

        // ------------------------------------------------------------ front matter

        private FrontMatter Front()
        {
            string title = DocumentComposer.Title(_template, _content, _runSeed);
            bool card = _template.Front.RecordCard;
            DateOnly approval = _plan.ValidFrom.AddDays(-21);
            return new FrontMatter(
                BankName,
                title,
                _plan.Designation,
                _plan.Version,
                PolishFormat.Date(_plan.ValidFrom),
                _plan.ValidTo is { } to ? PolishFormat.Date(to) : null)
            {
                Cover = _template.Front.Cover,
                RecordCard = card,
                Owner = card ? Parameter("wlasciciel") ?? Parameter("jednostka") : null,
                ApprovedBy = card ? Parameter("zatwierdzil") ?? DefaultApprover : null,
                ApprovalDate = card ? PolishFormat.Date(approval) : null,
                History = card
                    ? _plan.EarlierVersionStarts.Append(_plan.ValidFrom)
                        .Select((from, i) => new HistoryEntry(
                            (i + 1).ToString(CultureInfo.InvariantCulture),
                            PolishFormat.Date(from.AddDays(-21)),
                            i == 0 ? "Wydanie pierwsze." : "Aktualizacja postanowień procedury."))
                        .ToList()
                    : [],
            };
        }

        /// <summary>Turns footnote references inside table cells into „n)” markers and collects the notes.</summary>
        private sealed class NoteCollector(Composition owner, ContentBlock block, int offset = 0)
        {
            private readonly Dictionary<string, int> _numbers = new(StringComparer.Ordinal);

            public List<IReadOnlyList<Inline>> Notes { get; } = [];

            public IReadOnlyList<Inline> Markers(IReadOnlyList<Inline> runs)
            {
                if (!runs.Any(r => r.Kind == InlineKind.FootnoteRef))
                {
                    return runs;
                }

                var result = new List<Inline>();
                foreach (Inline run in runs)
                {
                    if (run.Kind != InlineKind.FootnoteRef)
                    {
                        result.Add(run);
                        continue;
                    }

                    // Render() already replaced the key with a document footnote number; take the footnote back out.
                    int documentNumber = int.Parse(run.Text, CultureInfo.InvariantCulture);
                    string key = owner._footnoteNumbers.First(p => p.Value == documentNumber).Key.Key;
                    if (!_numbers.TryGetValue(key, out int n))
                    {
                        n = offset + Notes.Count + 1;
                        _numbers[key] = n;
                        string marker = n.ToString(CultureInfo.InvariantCulture) + ") ";
                        Notes.Add([new Inline(marker), .. owner._footnotes[documentNumber]]);
                    }

                    owner._footnotes.Remove(documentNumber);
                    owner._footnoteNumbers.Remove((block.Id, key));
                    result.Add(new Inline(" " + n.ToString(CultureInfo.InvariantCulture) + ")"));
                }

                return result;
            }
        }

        private sealed class TariffTable(string id, string block)
        {
            public string Id { get; } = id;

            public string Block { get; } = block;

            public List<IReadOnlyList<TableCell>> Rows { get; } = [];

            public List<IReadOnlyList<Inline>> Notes { get; } = [];
        }
    }
}
