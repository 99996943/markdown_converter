using System.Globalization;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace LegalAgent.Corpus.Content;

/// <summary>A text found in a source file, scanned later for <c>{{fakt:id}}</c> references.</summary>
internal sealed record TextRef(string File, string Path, string Text);

/// <summary>Typed access to one parsed YAML file with exact YAML paths in errors.</summary>
internal sealed class YamlSource
{
    private readonly List<TextRef> _texts;

    public YamlSource(string file, List<TextRef> texts)
    {
        File = file;
        _texts = texts;
    }

    /// <summary>File path relative to the content directory ('/' separators).</summary>
    public string File { get; }

    public static string Key(string path, string key) => path.Length == 0 ? key : path + "." + key;

    public static string Idx(string path, int index) => path + "[" + index.ToString(CultureInfo.InvariantCulture) + "]";

    /// <summary>Parses <paramref name="text"/> and returns the root node of the single document.</summary>
    public YamlNode ParseRoot(string text)
    {
        try
        {
            var stream = new YamlStream();
            stream.Load(new StringReader(text));
            if (stream.Documents.Count != 1)
            {
                throw Fail(string.Empty, "Expected exactly one YAML document.");
            }

            return stream.Documents[0].RootNode;
        }
        catch (Exception e) when (e is YamlException or ArgumentException)
        {
            var line = e is YamlException ye ? $" (line {ye.Start.Line.ToString(CultureInfo.InvariantCulture)})" : string.Empty;
            throw new ContentException($"{File}: invalid YAML{line}: {e.Message}", e) { File = File, YamlPath = string.Empty };
        }
    }

    public ContentException Fail(string path, string message, YamlNode? at = null)
    {
        var line = at is not null && at.Start.Line > 0 ? $" (line {at.Start.Line.ToString(CultureInfo.InvariantCulture)})" : string.Empty;
        var where = path.Length == 0 ? File : $"{File}: {path}";
        return new ContentException($"{where}{line}: {message}") { File = File, YamlPath = path };
    }

    public MapReader Map(YamlNode node, string path)
    {
        if (node is not YamlMappingNode map)
        {
            throw Fail(path, "Expected a mapping.", node);
        }

        return new MapReader(this, map, path);
    }

    public List<(YamlNode Node, string Path)> Seq(YamlNode node, string path, bool nonEmpty = false)
    {
        if (node is not YamlSequenceNode seq)
        {
            throw Fail(path, "Expected a list.", node);
        }

        if (nonEmpty && seq.Children.Count == 0)
        {
            throw Fail(path, "The list must not be empty.", node);
        }

        var result = new List<(YamlNode, string)>(seq.Children.Count);
        for (var i = 0; i < seq.Children.Count; i++)
        {
            result.Add((seq.Children[i], Idx(path, i)));
        }

        return result;
    }

    public string Scalar(YamlNode node, string path)
    {
        if (node is YamlScalarNode { Value: { } value })
        {
            return value;
        }

        throw Fail(path, "Expected a text value.", node);
    }

    public string Text(YamlNode node, string path)
    {
        var value = Scalar(node, path);
        _texts.Add(new TextRef(File, path, value));
        return value;
    }

    public bool Bool(YamlNode node, string path) =>
        Scalar(node, path) switch
        {
            "true" => true,
            "false" => false,
            _ => throw Fail(path, "Expected true or false.", node),
        };

    public int Int(YamlNode node, string path) =>
        int.TryParse(Scalar(node, path), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
            ? v
            : throw Fail(path, "Expected an integer.", node);

    public decimal Dec(YamlNode node, string path) =>
        decimal.TryParse(Scalar(node, path), NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            ? v
            : throw Fail(path, "Expected a number.", node);

    public DateOnly Date(YamlNode node, string path) =>
        DateOnly.TryParseExact(Scalar(node, path), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var v)
            ? v
            : throw Fail(path, "Expected a date in the form yyyy-MM-dd.", node);

    /// <summary>A list of scalars, or a single scalar when <paramref name="allowScalar"/> is set.</summary>
    public List<string> StrList(YamlNode node, string path, bool text = false, bool nonEmpty = false, bool allowScalar = false)
    {
        if (allowScalar && node is YamlScalarNode)
        {
            return [text ? Text(node, path) : Scalar(node, path)];
        }

        var result = new List<string>();
        foreach (var (item, itemPath) in Seq(node, path, nonEmpty))
        {
            result.Add(text ? Text(item, itemPath) : Scalar(item, itemPath));
        }

        return result;
    }

    /// <summary>A mapping of scalar keys to texts.</summary>
    public Dictionary<string, string> StrMap(YamlNode node, string path, bool text = true)
    {
        if (node is not YamlMappingNode map)
        {
            throw Fail(path, "Expected a mapping.", node);
        }

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (k, v) in map.Children)
        {
            var key = Scalar(k, path);
            var childPath = Key(path, key);
            result[key] = text ? Text(v, childPath) : Scalar(v, childPath);
        }

        return result;
    }
}

/// <summary>Reads the keys of a mapping and rejects the ones that were never asked for.</summary>
internal sealed class MapReader
{
    private readonly YamlSource _src;
    private readonly YamlMappingNode _node;
    private readonly Dictionary<string, YamlNode> _map = new(StringComparer.Ordinal);
    private readonly List<string> _order = [];
    private readonly HashSet<string> _used = new(StringComparer.Ordinal);

    public MapReader(YamlSource src, YamlMappingNode node, string path)
    {
        _src = src;
        _node = node;
        Path = path;
        foreach (var (k, v) in node.Children)
        {
            var key = k is YamlScalarNode { Value: { } s } ? s : throw src.Fail(path, "Mapping keys must be plain text.", k);
            _map[key] = v;
            _order.Add(key);
        }
    }

    public string Path { get; }

    public IReadOnlyList<string> Keys => _order;

    public string P(string key) => YamlSource.Key(Path, key);

    public bool Has(string key) => _map.ContainsKey(key);

    public YamlNode? Opt(string key)
    {
        _used.Add(key);
        return _map.GetValueOrDefault(key);
    }

    public YamlNode Req(string key) =>
        Opt(key) ?? throw _src.Fail(P(key), $"Missing required key '{key}'.", _node);

    public string ReqStr(string key) => _src.Scalar(Req(key), P(key));

    public string? OptStr(string key) => Opt(key) is { } n ? _src.Scalar(n, P(key)) : null;

    public string ReqText(string key) => _src.Text(Req(key), P(key));

    public string? OptText(string key) => Opt(key) is { } n ? _src.Text(n, P(key)) : null;

    /// <summary>Fails on the first key that was not read.</summary>
    public void Finish()
    {
        foreach (var key in _order)
        {
            if (!_used.Contains(key))
            {
                throw _src.Fail(P(key), $"Unknown key '{key}'.", _map[key]);
            }
        }
    }
}
