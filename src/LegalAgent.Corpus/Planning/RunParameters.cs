using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using LegalAgent.PdfParser;

namespace LegalAgent.Corpus.Planning;

/// <summary>How many documents of a poison kind to produce per type.</summary>
/// <param name="Kind">The poison kind id.</param>
/// <param name="PerType">The number of poisoned documents per type.</param>
public sealed record PoisonQuota(string Kind, int PerType);

/// <summary>An inclusive page range.</summary>
/// <param name="Min">The minimum page count.</param>
/// <param name="Max">The maximum page count.</param>
public sealed record PageRange(int Min, int Max);

/// <summary>The parameters of one corpus run (przebieg.json).</summary>
public sealed record RunParameters
{
    private static readonly JsonSerializerOptions JsonOptions = CreateOptions();

    /// <summary>Gets the master seed.</summary>
    public ulong Seed { get; init; } = 20261008;

    /// <summary>Gets the reference date for the validity status.</summary>
    public DateOnly ReferenceDate { get; init; } = new(2026, 10, 1);

    /// <summary>Gets the type ids; null means all types.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Types { get; init; }

    /// <summary>Gets the number of documents per type.</summary>
    public int DocumentsPerType { get; init; } = 10;

    /// <summary>Gets the page range.</summary>
    public PageRange Pages { get; init; } = new(20, 30);

    /// <summary>Gets the percent of versioned documents.</summary>
    public int VersionedShare { get; init; } = 30;

    /// <summary>Gets the maximum number of versions.</summary>
    public int MaxVersions { get; init; } = 3;

    /// <summary>Gets the number of outdated documents per type.</summary>
    public int OutdatedPerType { get; init; } = 2;

    /// <summary>Gets the contradiction pairs per type.</summary>
    public int ContradictionPairsPerType { get; init; } = 1;

    /// <summary>Gets the contradiction pairs across types.</summary>
    public int CrossTypeContradictionPairs { get; init; } = 1;

    /// <summary>Gets the poison quotas.</summary>
    public IReadOnlyList<PoisonQuota> Poison { get; init; } = [];

    /// <summary>Gets a value indicating whether non-shared blocks must not repeat.</summary>
    public bool StrictUniqueness { get; init; } = true;

    /// <summary>Gets the maximum percent of shared blocks.</summary>
    public int MaxSharedShare { get; init; } = 20;

    /// <summary>Gets the output directory (relative, '/' separators).</summary>
    public string OutputDirectory { get; init; } = "corpus";

    /// <summary>Gets the content directory (relative, '/' separators).</summary>
    public string ContentDirectory { get; init; } = "corpus/zrodla";

    /// <summary>Gets the parser options.</summary>
    public PdfParserOptions ParserOptions { get; init; } = new();

    /// <summary>Gets the number of versioned documents per type (rounded up).</summary>
    [JsonIgnore]
    public int VersionedCount => (int)((((long)DocumentsPerType * VersionedShare) + 99) / 100);

    /// <summary>Loads parameters from a JSON file; missing fields keep their defaults.</summary>
    /// <param name="path">The file path.</param>
    /// <returns>The parameters.</returns>
    /// <exception cref="RunParametersException">Unknown field or malformed JSON.</exception>
    public static RunParameters Load(string path)
    {
        string text = File.ReadAllText(path, Encoding.UTF8);
        try
        {
            return JsonSerializer.Deserialize<RunParameters>(text, JsonOptions)
                ?? throw new RunParametersException("Pusty plik parametrów.");
        }
        catch (JsonException ex)
        {
            throw new RunParametersException($"Błędny plik parametrów '{path}': {ex.Message}", ex);
        }
    }

    /// <summary>Validates the parameters; throws on the first invalid field.</summary>
    /// <exception cref="RunParametersException">A field is invalid.</exception>
    public void Validate()
    {
        if (Types is { } types && (types.Count == 0 || types.Any(string.IsNullOrWhiteSpace)))
        {
            throw new RunParametersException("types", "lista typów nie może być pusta ani zawierać pustych identyfikatorów");
        }

        CheckRange("documentsPerType", DocumentsPerType, 1, 500);
        if (Pages is null || Pages.Min < 1 || Pages.Max > 500 || Pages.Min > Pages.Max)
        {
            throw new RunParametersException("pages", "wymagane 1 <= min <= max <= 500");
        }

        CheckRange("versionedShare", VersionedShare, 0, 100);
        CheckRange("maxVersions", MaxVersions, 2, 5);
        CheckRange("outdatedPerType", OutdatedPerType, 0, DocumentsPerType - VersionedCount);
        CheckRange("contradictionPairsPerType", ContradictionPairsPerType, 0, int.MaxValue);
        CheckRange("crossTypeContradictionPairs", CrossTypeContradictionPairs, 0, int.MaxValue);

        var kinds = new HashSet<string>(StringComparer.Ordinal);
        foreach (PoisonQuota quota in Poison ?? [])
        {
            if (string.IsNullOrWhiteSpace(quota?.Kind))
            {
                throw new RunParametersException("poison", "rodzaj nie może być pusty");
            }

            if (quota.PerType < 0)
            {
                throw new RunParametersException("poison", $"liczba dla rodzaju '{quota.Kind}' nie może być ujemna");
            }

            if (!kinds.Add(quota.Kind))
            {
                throw new RunParametersException("poison", $"rodzaj '{quota.Kind}' powtórzony");
            }
        }

        CheckRange("maxSharedShare", MaxSharedShare, 0, 100);
    }

    /// <summary>Saves the parameters (UTF-8 without BOM, LF, trailing LF).</summary>
    /// <param name="path">The file path.</param>
    /// <exception cref="RunParametersException">A directory path is absolute.</exception>
    public void Save(string path)
    {
        string json = ToJson();
        string? dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(path, json, new UTF8Encoding(false));
    }

    /// <summary>Serialises the parameters exactly as <see cref="Save"/> writes them.</summary>
    /// <returns>The JSON text.</returns>
    /// <exception cref="RunParametersException">A directory path is absolute.</exception>
    public string ToJson()
    {
        var normalised = this with
        {
            OutputDirectory = RelativePath("outputDirectory", OutputDirectory),
            ContentDirectory = RelativePath("contentDirectory", ContentDirectory),
        };
        return JsonSerializer.Serialize(normalised, JsonOptions) + "\n";
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            NewLine = "\n",
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new NormalizationOptionsConverter());
        return options;
    }

    private static string RelativePath(string field, string value)
    {
        string path = (value ?? string.Empty).Replace('\\', '/');
        bool absolute = path.StartsWith('/')
            || (path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':');
        if (absolute)
        {
            throw new RunParametersException(field, $"ścieżka musi być względna, otrzymano '{value}'");
        }

        return path;
    }

    private static void CheckRange(string field, int value, int min, int max)
    {
        if (value < min || value > max)
        {
            string upper = max == int.MaxValue ? string.Empty : max.ToString(CultureInfo.InvariantCulture);
            throw new RunParametersException(
                field,
                $"wartość {value.ToString(CultureInfo.InvariantCulture)} poza zakresem {min.ToString(CultureInfo.InvariantCulture)}..{upper}");
        }
    }

    /// <summary>
    /// <see cref="NormalizationOptions.HyphenationExceptions"/> has no setter, so the default deserializer would
    /// append to the defaults; this converter replaces the list instead.
    /// </summary>
    private sealed class NormalizationOptionsConverter : JsonConverter<NormalizationOptions>
    {
        public override NormalizationOptions Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var dto = JsonSerializer.Deserialize<Dto>(ref reader, options) ?? new Dto();
            var result = new NormalizationOptions
            {
                DropRotatedText = dto.DropRotatedText,
                DropInvisibleText = dto.DropInvisibleText,
            };
            if (dto.HyphenationExceptions is not null)
            {
                result.HyphenationExceptions.Clear();
                foreach (string item in dto.HyphenationExceptions)
                {
                    result.HyphenationExceptions.Add(item);
                }
            }

            return result;
        }

        public override void Write(Utf8JsonWriter writer, NormalizationOptions value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(
                writer,
                new Dto
                {
                    HyphenationExceptions = [.. value.HyphenationExceptions],
                    DropRotatedText = value.DropRotatedText,
                    DropInvisibleText = value.DropInvisibleText,
                },
                options);
        }

        private sealed class Dto
        {
            public List<string>? HyphenationExceptions { get; set; }

            public bool DropRotatedText { get; set; } = true;

            public bool DropInvisibleText { get; set; } = true;
        }
    }
}
