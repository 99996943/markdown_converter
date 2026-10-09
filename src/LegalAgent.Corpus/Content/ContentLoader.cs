using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using LegalAgent.Corpus.Composition;
using YamlDotNet.RepresentationModel;

namespace LegalAgent.Corpus.Content;

/// <summary>Loads and validates the content source files (<c>corpus/zrodla/</c>).</summary>
public static class ContentLoader
{
    /// <summary>Loads the whole content directory; throws <see cref="ContentException"/> on any error.</summary>
    public static ContentLibrary Load(string contentDirectory)
    {
        ArgumentNullException.ThrowIfNull(contentDirectory);
        return new Loader(contentDirectory).Run();
    }

    private static readonly Regex FactReference = new(@"\{\{fakt:([^}]+)\}\}", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5));
    private static readonly Regex TypeIdPattern = new("^[a-z-]+$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5));
    private static readonly Regex PrefixPattern = new("^[A-Z]{2,4}$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5));
    private static readonly Regex AbbreviationPattern = new("^[A-Z]{2,6}$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5));

    private static readonly HashSet<string> Layouts = new(StringComparer.Ordinal)
    {
        "jedna-kolumna", "dwie-kolumny", "tabela-dokument", "taryfa-siatka", "taryfa-bez-siatki", "procedura",
    };

    private static readonly HashSet<string> SectionKinds = new(StringComparer.Ordinal)
    {
        "rozdzial", "sekcja", "sekcja-taryfy", "zalacznik",
    };

    private static readonly HashSet<string> Goals = new(StringComparer.Ordinal)
    {
        "zmiana-odpowiedzi", "ignorowanie-zrodel", "ukrycie-zrodla", "dzialanie-poza-zakresem", "podszycie-pod-polecenie",
    };

    private static readonly HashSet<string> Operations = new(StringComparer.Ordinal)
    {
        "wstaw", "nadpisz-fakt", "zmien-czolo", "przesun-daty",
    };

    private static readonly HashSet<string> Placements = new(StringComparer.Ordinal)
    {
        "akapit", "przypis", "komorka-tabeli", "metryczka", "okladka", "ramka",
    };

    private static readonly HashSet<string> ElementKinds = new(StringComparer.Ordinal)
    {
        "akapit", "ustep", "naglowek", "tabela", "pozycje-taryfy", "kroki", "schemat", "lista-kontrolna", "ramka", "metryczka",
    };

    private const int MaxStepDepth = 3;

    private sealed class Loader
    {
        private readonly string _dir;
        private readonly List<TextRef> _texts = [];
        private readonly List<(string Id, string File, string Path, string Kind)> _blockTypeRefs = [];

        public Loader(string dir) => _dir = dir;

        public ContentLibrary Run()
        {
            if (!Directory.Exists(_dir))
            {
                throw new ContentException($"Content directory not found: {_dir}");
            }

            var files = ReadFiles();
            var hash = ComputeHash(files);

            var types = LoadTypes(Required(files, "typy.yaml"));
            var facts = LoadFacts(Required(files, "fakty.yaml"));
            var forbidden = files.TryGetValue("zabronione.yaml", out var zab) ? LoadForbidden(("zabronione.yaml", zab)) : [];
            var acts = files.TryGetValue("akty.yaml", out var akty) ? LoadActs(("akty.yaml", akty)) : [];

            var templates = new List<DocumentTemplate>();
            var blocks = new List<ContentBlock>();
            var kinds = new List<PoisonKindDef>();
            var patterns = new List<PoisonPattern>();
            foreach (var (rel, text) in files)
            {
                if (!rel.EndsWith(".yaml", StringComparison.Ordinal))
                {
                    continue;
                }

                var segments = rel.Split('/');
                if (segments.Length == 2 && segments[0] == "szablony")
                {
                    templates.Add(LoadTemplate(rel, text));
                }
                else if (segments.Length >= 2 && segments[0] == "bloki")
                {
                    blocks.AddRange(LoadBlocks(rel, text));
                }
                else if (segments.Length == 2 && segments[0] == "zatrucia")
                {
                    LoadPoison(rel, text, facts, kinds, patterns);
                }
            }

            var library = new ContentLibrary(types, facts, templates, blocks, kinds, patterns, forbidden, acts, hash);
            Validate(library);
            return library;
        }

        private SortedDictionary<string, string> ReadFiles()
        {
            var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
            var utf8 = new UTF8Encoding(false);
            foreach (var path in Directory.EnumerateFiles(_dir, "*", SearchOption.AllDirectories))
            {
                var rel = System.IO.Path.GetRelativePath(_dir, path).Replace('\\', '/');
                if (string.Equals(System.IO.Path.GetFileName(path), ".gitkeep", StringComparison.Ordinal))
                {
                    continue;
                }

                var text = utf8.GetString(System.IO.File.ReadAllBytes(path));
                result[rel] = text.Length > 0 && text[0] == '﻿' ? text[1..] : text;
            }

            return result;
        }

        private string ComputeHash(SortedDictionary<string, string> files)
        {
            using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            foreach (var rel in files.Keys)
            {
                var path = System.IO.Path.Combine(_dir, rel.Replace('/', System.IO.Path.DirectorySeparatorChar));
                sha.AppendData(Encoding.UTF8.GetBytes(rel));
                sha.AppendData([0]);
                sha.AppendData(System.IO.File.ReadAllBytes(path));
                sha.AppendData([0]);
            }

            return Convert.ToHexStringLower(sha.GetHashAndReset());
        }

        private static (string Rel, string Text) Required(SortedDictionary<string, string> files, string rel) =>
            files.TryGetValue(rel, out var text)
                ? (rel, text)
                : throw new ContentException($"Required content file missing: {rel}") { File = rel };

        private YamlSource Source(string rel) => new(rel, _texts);

        // ---------------------------------------------------------------- typy.yaml

        private List<DocumentTypeDef> LoadTypes((string Rel, string Text) file)
        {
            var src = Source(file.Rel);
            var root = src.Map(src.ParseRoot(file.Text), string.Empty);
            var list = new List<DocumentTypeDef>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var prefixes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (node, path) in src.Seq(root.Req("typy"), root.P("typy")))
            {
                var m = src.Map(node, path);
                var id = m.ReqStr("id");
                if (!TypeIdPattern.IsMatch(id))
                {
                    throw src.Fail(m.P("id"), $"Type id '{id}' must match ^[a-z-]+$.");
                }

                var prefix = m.ReqStr("prefiks");
                if (!PrefixPattern.IsMatch(prefix))
                {
                    throw src.Fail(m.P("prefiks"), $"Prefix '{prefix}' must be 2-4 uppercase ASCII letters.");
                }

                if (!ids.Add(id))
                {
                    throw src.Fail(m.P("id"), $"Duplicate type id '{id}'.");
                }

                if (!prefixes.Add(prefix))
                {
                    throw src.Fail(m.P("prefiks"), $"Duplicate type prefix '{prefix}'.");
                }

                var pattern = m.ReqStr("oznaczenie");
                var name = m.ReqStr("nazwa");
                var required = m.Opt("wymagane-elementy") is { } r ? src.StrList(r, m.P("wymagane-elementy")) : [];
                var minLayouts = new Dictionary<string, int>(StringComparer.Ordinal);
                if (m.Opt("uklady-min") is { } minNode)
                {
                    var minPath = m.P("uklady-min");
                    if (minNode is not YamlMappingNode minMap)
                    {
                        throw src.Fail(minPath, "Expected a mapping of layout id to a minimum count.", minNode);
                    }

                    foreach (var (k, v) in minMap.Children)
                    {
                        var layout = src.Scalar(k, minPath);
                        var layoutPath = minPath + "." + layout;
                        if (!Layouts.Contains(layout))
                        {
                            throw src.Fail(layoutPath, $"Unknown layout style '{layout}'.", k);
                        }

                        var count = src.Int(v, layoutPath);
                        if (count < 1)
                        {
                            throw src.Fail(layoutPath, $"The minimum count for layout '{layout}' must be at least 1.", v);
                        }

                        minLayouts[layout] = count;
                    }
                }

                m.Finish();
                list.Add(new DocumentTypeDef(id, prefix, pattern, name, required) { MinLayouts = minLayouts });
            }

            root.Finish();
            return list;
        }

        // ---------------------------------------------------------------- fakty.yaml

        private FactCatalog LoadFacts((string Rel, string Text) file)
        {
            var src = Source(file.Rel);
            var root = src.Map(src.ParseRoot(file.Text), string.Empty);
            var list = new List<Fact>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (node, path) in src.Seq(root.Req("fakty"), root.P("fakty")))
            {
                var m = src.Map(node, path);
                var id = m.ReqStr("id");
                if (!ids.Add(id))
                {
                    throw src.Fail(m.P("id"), $"Duplicate fact id '{id}' (also in {file.Rel}).");
                }

                var kind = ParseKind(src, m);
                var entries = new List<FactEntry>();
                DateOnly? previous = null;
                foreach (var (vNode, vPath) in src.Seq(m.Req("wartosci"), m.P("wartosci"), nonEmpty: true))
                {
                    var v = src.Map(vNode, vPath);
                    DateOnly? from = v.Opt("od") is { } odNode ? src.Date(odNode, v.P("od")) : null;
                    if (entries.Count == 0 && from is not null)
                    {
                        throw src.Fail(v.P("od"), "The first value is the base value and must have no 'od'.", v.Opt("od"));
                    }

                    if (entries.Count > 0)
                    {
                        if (from is null)
                        {
                            throw src.Fail(v.P("od"), "Every value after the first needs 'od'.", vNode);
                        }

                        if (previous is { } p && from <= p)
                        {
                            throw src.Fail(v.P("od"), "'od' dates must be strictly increasing.", v.Opt("od"));
                        }
                    }

                    previous = from ?? previous;
                    entries.Add(new FactEntry(from, ParseValue(src, kind, v.Req("wartosc"), v.P("wartosc"))));
                    v.Finish();
                }

                var alternatives = new List<FactValue>();
                if (m.Opt("alternatywy") is { } altNode)
                {
                    foreach (var (aNode, aPath) in src.Seq(altNode, m.P("alternatywy")))
                    {
                        var alt = ParseValue(src, kind, aNode, aPath);
                        if (entries.Any(e => e.Value == alt))
                        {
                            throw src.Fail(aPath, "An alternative must differ from every value of the fact.", aNode);
                        }

                        alternatives.Add(alt);
                    }
                }

                m.Finish();
                list.Add(new Fact(id, kind, entries, alternatives));
            }

            root.Finish();
            return new FactCatalog(list);
        }

        private static FactKind ParseKind(YamlSource src, MapReader m)
        {
            var text = m.ReqStr("rodzaj");
            return text switch
            {
                "kwota" => FactKind.Kwota,
                "procent" => FactKind.Procent,
                "termin" => FactKind.Termin,
                "tekst" => FactKind.Tekst,
                "data" => FactKind.Data,
                _ => throw src.Fail(m.P("rodzaj"), $"Unknown fact kind '{text}' (kwota, procent, termin, tekst, data)."),
            };
        }

        private static FactValue ParseValue(YamlSource src, FactKind kind, YamlNode node, string path) =>
            kind switch
            {
                FactKind.Tekst => new FactValue(Text: src.Scalar(node, path)),
                FactKind.Data => new FactValue(Date: src.Date(node, path)),
                _ => new FactValue(Number: src.Dec(node, path)),
            };

        // ---------------------------------------------------------------- zabronione.yaml, akty.yaml

        private List<string> LoadForbidden((string Rel, string Text) file)
        {
            var src = Source(file.Rel);
            var root = src.Map(src.ParseRoot(file.Text), string.Empty);
            var list = src.StrList(root.Req("zabronione"), root.P("zabronione"));
            root.Finish();
            return list;
        }

        private List<ActSource> LoadActs((string Rel, string Text) file)
        {
            var src = Source(file.Rel);
            var root = src.Map(src.ParseRoot(file.Text), string.Empty);
            var list = new List<ActSource>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (node, path) in src.Seq(root.Req("akty"), root.P("akty")))
            {
                var m = src.Map(node, path);
                var id = m.ReqStr("id");
                if (!ids.Add(id))
                {
                    throw src.Fail(m.P("id"), $"Duplicate act id '{id}'.");
                }

                var title = m.ReqStr("tytul");
                var journal = m.ReqStr("publikator");
                var consolidated = src.Date(m.Req("tekst-jednolity"), m.P("tekst-jednolity"));
                var url = m.ReqStr("url");
                var downloaded = src.Date(m.Req("pobrano"), m.P("pobrano"));
                var fileName = m.OptStr("plik") ?? id + ".pdf";
                var notes = m.OptStr("uwagi");
                m.Finish();
                list.Add(new ActSource(id, title, journal, consolidated, url, downloaded, fileName, notes));
            }

            root.Finish();
            return list;
        }

        // ---------------------------------------------------------------- szablony

        private DocumentTemplate LoadTemplate(string rel, string text)
        {
            var src = Source(rel);
            var m = src.Map(src.ParseRoot(text), string.Empty);
            var id = m.ReqStr("id");
            var type = m.ReqStr("typ");
            var topic = m.ReqStr("temat");
            var titles = src.StrList(m.Req("tytul"), m.P("tytul"), text: true, nonEmpty: true, allowScalar: true);

            var layouts = new List<string>();
            foreach (var (node, path) in src.Seq(m.Req("uklady"), m.P("uklady"), nonEmpty: true))
            {
                var layout = src.Scalar(node, path);
                if (!Layouts.Contains(layout))
                {
                    throw src.Fail(path, $"Unknown layout style '{layout}'.", node);
                }

                layouts.Add(layout);
            }

            var f = src.Map(m.Req("czolo"), m.P("czolo"));
            var cover = f.Opt("okladka") is { } c && src.Bool(c, f.P("okladka"));
            var card = f.Opt("metryczka") is { } k && src.Bool(k, f.P("metryczka"));
            var fields = f.Opt("pola") is { } p ? src.StrList(p, f.P("pola")) : [];
            f.Finish();

            var sections = new List<SectionSlot>();
            foreach (var (node, path) in src.Seq(m.Req("sekcje"), m.P("sekcje"), nonEmpty: true))
            {
                var s = src.Map(node, path);
                var kind = s.ReqStr("rodzaj");
                if (!SectionKinds.Contains(kind))
                {
                    throw src.Fail(s.P("rodzaj"), $"Unknown section kind '{kind}'.", node);
                }

                var title = s.OptText("tytul");
                var required = s.Opt("wymagane") is { } rq ? src.StrList(rq, s.P("wymagane")) : [];
                OptionalSpec? optional = null;
                if (s.Opt("opcjonalne") is { } on)
                {
                    var o = src.Map(on, s.P("opcjonalne"));
                    var categories = src.StrList(o.Req("kategorie"), o.P("kategorie"), nonEmpty: true);
                    var min = src.Int(o.Req("min"), o.P("min"));
                    var max = src.Int(o.Req("max"), o.P("max"));
                    if (min < 0)
                    {
                        throw src.Fail(o.P("min"), "'min' must not be negative.");
                    }

                    if (min > max)
                    {
                        throw src.Fail(o.P("min"), "'min' must not exceed 'max'.");
                    }

                    o.Finish();
                    optional = new OptionalSpec(categories, min, max);
                }

                s.Finish();
                sections.Add(new SectionSlot(kind, title, required, optional));
            }

            var parameters = m.Opt("parametry") is { } pn ? src.StrMap(pn, m.P("parametry")) : [];
            m.Finish();
            return new DocumentTemplate(id, type, topic, titles, layouts, new FrontSpec(cover, card, fields), sections, parameters, rel);
        }

        // ---------------------------------------------------------------- bloki

        private List<ContentBlock> LoadBlocks(string rel, string text)
        {
            var src = Source(rel);
            var root = src.Map(src.ParseRoot(text), string.Empty);
            var list = new List<ContentBlock>();
            foreach (var (node, path) in src.Seq(root.Req("bloki"), root.P("bloki")))
            {
                var m = src.Map(node, path);
                var id = m.ReqStr("id");
                var types = src.StrList(m.Req("typy"), m.P("typy"), nonEmpty: true);
                var topics = m.Opt("tematy") is { } tn ? src.StrList(tn, m.P("tematy")) : [];
                var category = m.ReqStr("kategoria");
                var shared = m.Opt("wspolny") is { } sn && src.Bool(sn, m.P("wspolny"));
                var unit = m.OptText("jednostka");
                var title = m.OptText("tytul");
                var elements = new List<SourceElement>();
                foreach (var (eNode, ePath) in src.Seq(m.Req("elementy"), m.P("elementy"), nonEmpty: true))
                {
                    elements.Add(ParseElement(src, eNode, ePath));
                }

                var footnotes = m.Opt("przypisy") is { } fn ? src.StrMap(fn, m.P("przypisy")) : [];
                m.Finish();
                list.Add(new ContentBlock(id, types, topics, category, shared, unit, title, elements, footnotes, rel));
            }

            root.Finish();
            return list;
        }

        private static SourceElement ParseElement(YamlSource src, YamlNode node, string path)
        {
            var m = src.Map(node, path);
            var kinds = m.Keys.Where(ElementKinds.Contains).ToList();
            if (kinds.Count != 1)
            {
                throw src.Fail(path, kinds.Count == 0
                    ? "An element needs exactly one kind key (" + string.Join(", ", ElementKinds.Order(StringComparer.Ordinal)) + ")."
                    : "An element must have exactly one kind key, found: " + string.Join(", ", kinds) + ".", node);
            }

            var kind = kinds[0];
            var kp = m.P(kind);
            var body = m.Req(kind);
            SourceElement result;
            switch (kind)
            {
                case "akapit":
                    result = new SourceParagraph(src.Text(body, kp));
                    break;
                case "ustep":
                    var points = new List<SourcePoint>();
                    if (m.Opt("punkty") is { } pn)
                    {
                        foreach (var (pNode, pPath) in src.Seq(pn, m.P("punkty"), nonEmpty: true))
                        {
                            points.Add(ParsePoint(src, pNode, pPath));
                        }
                    }

                    result = new SourceClause(src.Text(body, kp), points);
                    break;
                case "naglowek":
                    var level = 3;
                    if (m.Opt("poziom") is { } ln)
                    {
                        level = src.Int(ln, m.P("poziom"));
                        if (level is < 1 or > 6)
                        {
                            throw src.Fail(m.P("poziom"), "Heading level must be 1-6.", ln);
                        }
                    }

                    result = new SourceHeading(src.Text(body, kp), level);
                    break;
                case "tabela":
                    result = ParseTable(src, body, kp);
                    break;
                case "pozycje-taryfy":
                    result = new SourceTariffItems(ParseTariffItems(src, body, kp));
                    break;
                case "kroki":
                    result = new SourceSteps(ParseSteps(src, body, kp, 1));
                    break;
                case "schemat":
                    result = ParseScheme(src, body, kp);
                    break;
                case "lista-kontrolna":
                    result = ParseChecklist(src, body, kp);
                    break;
                case "ramka":
                    result = new SourceCallout(src.Text(body, kp));
                    break;
                default:
                    result = ParseRecordCard(src, body, kp);
                    break;
            }

            m.Finish();
            return result;
        }

        private static SourcePoint ParsePoint(YamlSource src, YamlNode node, string path)
        {
            if (node is YamlScalarNode)
            {
                return new SourcePoint(src.Text(node, path), []);
            }

            var m = src.Map(node, path);
            var text = m.ReqText("tekst");
            var letters = m.Opt("litery") is { } ln ? src.StrList(ln, m.P("litery"), text: true, nonEmpty: true) : [];
            m.Finish();
            return new SourcePoint(text, letters);
        }

        private static SourceTable ParseTable(YamlSource src, YamlNode body, string path)
        {
            var m = src.Map(body, path);
            var columns = new List<SourceTableColumn>();
            foreach (var (cNode, cPath) in src.Seq(m.Req("kolumny"), m.P("kolumny"), nonEmpty: true))
            {
                var c = src.Map(cNode, cPath);
                var header = c.ReqText("naglowek");
                var weight = 1.0;
                if (c.Opt("waga") is { } wn)
                {
                    weight = (double)src.Dec(wn, c.P("waga"));
                    if (weight <= 0)
                    {
                        throw src.Fail(c.P("waga"), "Column weight must be positive.", wn);
                    }
                }

                c.Finish();
                columns.Add(new SourceTableColumn(header, weight));
            }

            var rows = new List<IReadOnlyList<string>>();
            foreach (var (rNode, rPath) in src.Seq(m.Req("wiersze"), m.P("wiersze"), nonEmpty: true))
            {
                var cells = src.StrList(rNode, rPath, text: true);
                if (cells.Count != columns.Count)
                {
                    throw src.Fail(rPath, $"Row has {cells.Count.ToString(CultureInfo.InvariantCulture)} cells, expected {columns.Count.ToString(CultureInfo.InvariantCulture)}.", rNode);
                }

                rows.Add(cells);
            }

            bool? grid = m.Opt("siatka") is { } gn ? src.Bool(gn, m.P("siatka")) : null;
            var notes = m.Opt("przypisy") is { } nn ? src.StrList(nn, m.P("przypisy"), text: true) : [];
            m.Finish();
            return new SourceTable(columns, rows, grid, notes);
        }

        private static List<SourceTariffItem> ParseTariffItems(YamlSource src, YamlNode body, string path)
        {
            var list = new List<SourceTariffItem>();
            foreach (var (node, p) in src.Seq(body, path, nonEmpty: true))
            {
                var m = src.Map(node, p);
                var service = m.ReqText("usluga");
                var rate = m.ReqText("stawka");
                var mode = m.ReqText("tryb");
                var children = m.Opt("podpozycje") is { } cn ? ParseTariffItems(src, cn, m.P("podpozycje")) : [];
                m.Finish();
                list.Add(new SourceTariffItem(service, rate, mode, children));
            }

            return list;
        }

        private static List<SourceStep> ParseSteps(YamlSource src, YamlNode body, string path, int depth)
        {
            if (depth > MaxStepDepth)
            {
                throw src.Fail(path, $"Steps may be nested at most {MaxStepDepth.ToString(CultureInfo.InvariantCulture)} levels deep.", body);
            }

            var list = new List<SourceStep>();
            foreach (var (node, p) in src.Seq(body, path, nonEmpty: true))
            {
                var m = src.Map(node, p);
                var text = m.ReqText("tekst");
                var children = m.Opt("podkroki") is { } cn ? ParseSteps(src, cn, m.P("podkroki"), depth + 1) : [];
                m.Finish();
                list.Add(new SourceStep(text, children));
            }

            return list;
        }

        private static SourceScheme ParseScheme(YamlSource src, YamlNode body, string path)
        {
            var steps = new List<SourceSchemeStep>();
            foreach (var (node, p) in src.Seq(body, path))
            {
                var m = src.Map(node, p);
                var name = m.ReqText("nazwa");
                var explanation = src.StrList(m.Req("wyjasnienie"), m.P("wyjasnienie"), text: true, nonEmpty: true, allowScalar: true);
                m.Finish();
                steps.Add(new SourceSchemeStep(name, explanation));
            }

            if (steps.Count < 2)
            {
                throw src.Fail(path, "A step scheme needs at least 2 steps.", body);
            }

            return new SourceScheme(steps);
        }

        private static SourceChecklist ParseChecklist(YamlSource src, YamlNode body, string path)
        {
            var m = src.Map(body, path);
            ChecklistForm? form = null;
            if (m.Opt("forma") is { } fn)
            {
                var text = src.Scalar(fn, m.P("forma"));
                form = text switch
                {
                    "wektor" => ChecklistForm.Vector,
                    "tekst" => ChecklistForm.Text,
                    "tabela" => ChecklistForm.Table,
                    _ => throw src.Fail(m.P("forma"), $"Unknown checklist form '{text}' (wektor, tekst, tabela).", fn),
                };
            }

            var items = src.StrList(m.Req("pozycje"), m.P("pozycje"), text: true, nonEmpty: true);
            m.Finish();
            return new SourceChecklist(form, items);
        }

        private static SourceRecordCard ParseRecordCard(YamlSource src, YamlNode body, string path)
        {
            var pairs = new List<KeyValuePair<string, string>>();
            foreach (var (node, p) in src.Seq(body, path, nonEmpty: true))
            {
                var m = src.Map(node, p);
                var key = m.ReqText("klucz");
                var value = m.ReqText("wartosc");
                m.Finish();
                pairs.Add(new KeyValuePair<string, string>(key, value));
            }

            return new SourceRecordCard(pairs);
        }

        // ---------------------------------------------------------------- zatrucia

        private void LoadPoison(string rel, string text, FactCatalog facts, List<PoisonKindDef> kinds, List<PoisonPattern> patterns)
        {
            var src = Source(rel);
            var m = src.Map(src.ParseRoot(text), string.Empty);
            var kind = m.ReqStr("rodzaj");
            var expected = System.IO.Path.GetFileNameWithoutExtension(rel);
            if (!string.Equals(kind, expected, StringComparison.Ordinal))
            {
                throw src.Fail(m.P("rodzaj"), $"'rodzaj' ('{kind}') must equal the file name ('{expected}').");
            }

            var abbreviation = m.ReqStr("skrot");
            if (!AbbreviationPattern.IsMatch(abbreviation))
            {
                throw src.Fail(m.P("skrot"), $"Abbreviation '{abbreviation}' must be 2-6 uppercase ASCII letters.");
            }

            var description = m.ReqText("opis");
            kinds.Add(new PoisonKindDef(kind, abbreviation, description, rel));

            foreach (var (node, path) in src.Seq(m.Req("wzorce"), m.P("wzorce"), nonEmpty: true))
            {
                patterns.Add(ParsePattern(src, node, path, kind, facts, rel));
            }

            m.Finish();
        }

        private PoisonPattern ParsePattern(YamlSource src, YamlNode node, string path, string kind, FactCatalog facts, string rel)
        {
            var m = src.Map(node, path);
            var id = m.ReqStr("id");
            var types = src.StrList(m.Req("typy"), m.P("typy"), nonEmpty: true);
            foreach (var (tNode, tPath) in src.Seq(m.Req("typy"), m.P("typy")))
            {
                _blockTypeRefs.Add((src.Scalar(tNode, tPath), rel, tPath, "pattern"));
            }

            var goal = m.OptStr("cel");
            if (goal is null && string.Equals(kind, "polecenia-dla-ai", StringComparison.Ordinal))
            {
                throw src.Fail(m.P("cel"), "Patterns of kind 'polecenia-dla-ai' require 'cel'.", node);
            }

            if (goal is not null && !Goals.Contains(goal))
            {
                throw src.Fail(m.P("cel"), $"Unknown goal '{goal}'.", m.Opt("cel"));
            }

            var operation = m.ReqStr("operacja");
            if (!Operations.Contains(operation))
            {
                throw src.Fail(m.P("operacja"), $"Unknown operation '{operation}'.", m.Opt("operacja"));
            }

            var placements = new List<string>();
            if (m.Opt("miejsca") is { } pn)
            {
                foreach (var (pNode, pPath) in src.Seq(pn, m.P("miejsca")))
                {
                    var placement = src.Scalar(pNode, pPath);
                    if (!Placements.Contains(placement))
                    {
                        throw src.Fail(pPath, $"Unknown placement '{placement}'.", pNode);
                    }

                    placements.Add(placement);
                }
            }

            var variants = m.Opt("tekst") is { } tn ? src.StrList(tn, m.P("tekst"), text: true, allowScalar: true) : [];
            var fact = m.OptStr("fakt");
            FactValue? value = null;
            if (m.Opt("wartosc") is { } vn)
            {
                if (fact is null || !facts.Contains(fact))
                {
                    throw src.Fail(m.P("wartosc"), "'wartosc' needs 'fakt' naming an existing fact.", vn);
                }

                value = ParseValue(src, facts.Get(fact).Kind, vn, m.P("wartosc"));
            }

            var fields = m.Opt("pola") is { } fn ? src.StrMap(fn, m.P("pola")) : [];
            var patternDescription = m.ReqText("opis");

            switch (operation)
            {
                case "wstaw":
                    if (variants.Count == 0 || variants.Any(string.IsNullOrWhiteSpace))
                    {
                        throw src.Fail(m.P("tekst"), "Operation 'wstaw' requires non-empty 'tekst'.", node);
                    }

                    if (placements.Count == 0)
                    {
                        throw src.Fail(m.P("miejsca"), "Operation 'wstaw' requires 'miejsca'.", node);
                    }

                    break;
                case "nadpisz-fakt":
                    if (fact is null)
                    {
                        throw src.Fail(m.P("fakt"), "Operation 'nadpisz-fakt' requires 'fakt'.", node);
                    }

                    if (!facts.Contains(fact))
                    {
                        throw src.Fail(m.P("fakt"), $"Unknown fact '{fact}'.", m.Opt("fakt"));
                    }

                    break;
                case "zmien-czolo":
                    if (fields.Count == 0)
                    {
                        throw src.Fail(m.P("pola"), "Operation 'zmien-czolo' requires non-empty 'pola'.", node);
                    }

                    break;
            }

            m.Finish();
            return new PoisonPattern(id, kind, types, goal, operation, placements, variants, fact, value, fields, patternDescription, rel);
        }

        // ---------------------------------------------------------------- cross-file validation

        private void Validate(ContentLibrary lib)
        {
            var typeIds = new HashSet<string>(lib.Types.Select(t => t.Id), StringComparer.Ordinal);

            CheckDuplicates(lib.Templates, t => t.Id, t => t.File, "template");
            CheckDuplicates(lib.Blocks, b => b.Id, b => b.File, "block");
            CheckDuplicates(lib.PoisonPatterns, p => p.Id, p => p.File, "poison pattern");
            CheckDuplicates(lib.Acts, a => a.Id, _ => "akty.yaml", "act");

            var blockIds = new HashSet<string>(lib.Blocks.Select(b => b.Id), StringComparer.Ordinal);
            foreach (var t in lib.Templates)
            {
                if (!typeIds.Contains(t.Type))
                {
                    throw Fail(t.File, "typ", $"Template '{t.Id}' has type '{t.Type}' which is not defined in typy.yaml.");
                }

                for (var i = 0; i < t.Sections.Count; i++)
                {
                    for (var j = 0; j < t.Sections[i].Required.Count; j++)
                    {
                        var required = t.Sections[i].Required[j];
                        if (!blockIds.Contains(required))
                        {
                            throw Fail(t.File, $"sekcje[{i.ToString(CultureInfo.InvariantCulture)}].wymagane[{j.ToString(CultureInfo.InvariantCulture)}]", $"Required block '{required}' does not exist.");
                        }
                    }
                }
            }

            foreach (var group in lib.Blocks.GroupBy(b => b.File, StringComparer.Ordinal))
            {
                var index = 0;
                foreach (var b in group)
                {
                    for (var j = 0; j < b.Types.Count; j++)
                    {
                        if (!typeIds.Contains(b.Types[j]))
                        {
                            throw Fail(b.File, $"bloki[{index.ToString(CultureInfo.InvariantCulture)}].typy[{j.ToString(CultureInfo.InvariantCulture)}]", $"Block '{b.Id}' uses type '{b.Types[j]}' which is not defined in typy.yaml.");
                        }
                    }

                    index++;
                }
            }

            foreach (var (type, file, path, _) in _blockTypeRefs)
            {
                if (!typeIds.Contains(type))
                {
                    throw Fail(file, path, $"Poison pattern uses type '{type}' which is not defined in typy.yaml.");
                }
            }

            foreach (var text in _texts)
            {
                foreach (Match match in FactReference.Matches(text.Text))
                {
                    var id = match.Groups[1].Value;
                    if (!lib.Facts.Contains(id))
                    {
                        throw Fail(text.File, text.Path, $"Unknown fact '{id}' referenced in text.");
                    }
                }
            }
        }

        private static ContentException Fail(string file, string path, string message) =>
            new($"{file}: {path}: {message}") { File = file, YamlPath = path };

        private static void CheckDuplicates<T>(IEnumerable<T> items, Func<T, string> id, Func<T, string> file, string what)
        {
            var seen = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var item in items)
            {
                var key = id(item);
                var currentFile = file(item);
                if (!seen.TryAdd(key, currentFile))
                {
                    throw new ContentException($"Duplicate {what} id '{key}' in {seen[key]} and {currentFile}.") { File = currentFile, YamlPath = "id" };
                }
            }
        }
    }
}
