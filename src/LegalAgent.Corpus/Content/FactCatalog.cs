namespace LegalAgent.Corpus.Content;

/// <summary>A dated value of a fact; <see cref="From"/> is null for the base value.</summary>
public sealed record FactEntry(DateOnly? From, FactValue Value);

/// <summary>A bank fact from <c>fakty.yaml</c>.</summary>
public sealed record Fact(string Id, FactKind Kind, IReadOnlyList<FactEntry> Values, IReadOnlyList<FactValue> Alternatives);

/// <summary>Catalog of facts ordered by id (ordinal).</summary>
public sealed class FactCatalog
{
    private readonly Dictionary<string, Fact> _byId;

    /// <summary>Creates the catalog; ids must be unique.</summary>
    public FactCatalog(IEnumerable<Fact> facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        All = facts.OrderBy(f => f.Id, StringComparer.Ordinal).ToList();
        _byId = new Dictionary<string, Fact>(StringComparer.Ordinal);
        foreach (var fact in All)
        {
            if (!_byId.TryAdd(fact.Id, fact))
            {
                throw new ContentException($"Duplicate fact id '{fact.Id}'.");
            }
        }
    }

    /// <summary>All facts ordered by id (ordinal).</summary>
    public IReadOnlyList<Fact> All { get; }

    /// <summary>Whether a fact with the id exists.</summary>
    public bool Contains(string id) => _byId.ContainsKey(id);

    /// <summary>The fact with the id; throws <see cref="ContentException"/> naming the id when unknown.</summary>
    public Fact Get(string id) =>
        _byId.TryGetValue(id, out var fact)
            ? fact
            : throw new ContentException($"Unknown fact '{id}'.");

    /// <summary>
    /// Value of fact <paramref name="id"/> on <paramref name="date"/>: the override when present, otherwise the
    /// last entry whose <c>From</c> is null or not after the date.
    /// </summary>
    public FactValue ValueAt(string id, DateOnly date, IReadOnlyDictionary<string, FactValue>? overrides = null) =>
        throw new NotImplementedException();
}
