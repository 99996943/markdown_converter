using System.Text;
using LegalAgent.Corpus.Planning;

namespace LegalAgent.Corpus.Tests.Unit.Planning;

public sealed class RunParametersTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "rp-" + Guid.NewGuid().ToString("N"));

    public RunParametersTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string P(string name = "przebieg.json") => Path.Combine(_dir, name);

    [Fact]
    public void Defaults_MatchDataModel()
    {
        var p = new RunParameters();

        Assert.Equal(20261008UL, p.Seed);
        Assert.Equal(new DateOnly(2026, 10, 1), p.ReferenceDate);
        Assert.Null(p.Types);
        Assert.Equal(10, p.DocumentsPerType);
        Assert.Equal(new PageRange(20, 30), p.Pages);
        Assert.Equal(30, p.VersionedShare);
        Assert.Equal(3, p.MaxVersions);
        Assert.Equal(2, p.OutdatedPerType);
        Assert.Equal(1, p.ContradictionPairsPerType);
        Assert.Equal(1, p.CrossTypeContradictionPairs);
        Assert.Empty(p.Poison);
        Assert.True(p.StrictUniqueness);
        Assert.Equal(20, p.MaxSharedShare);
        Assert.Equal("corpus", p.OutputDirectory);
        Assert.Equal("corpus/zrodla", p.ContentDirectory);
        Assert.NotNull(p.ParserOptions);
        p.Validate();
    }

    [Theory]
    [InlineData(10, 30, 3)]
    [InlineData(10, 25, 3)]
    [InlineData(7, 30, 3)]
    [InlineData(10, 0, 0)]
    [InlineData(10, 100, 10)]
    public void VersionedCount_RoundsUp(int docs, int share, int expected)
    {
        var p = new RunParameters { DocumentsPerType = docs, VersionedShare = share };

        Assert.Equal(expected, p.VersionedCount);
    }

    public static TheoryData<string, RunParameters> Invalid => new()
    {
        { "documentsPerType", new RunParameters { DocumentsPerType = 0 } },
        { "documentsPerType", new RunParameters { DocumentsPerType = 501 } },
        { "pages", new RunParameters { Pages = new PageRange(30, 20) } },
        { "pages", new RunParameters { Pages = new PageRange(0, 20) } },
        { "pages", new RunParameters { Pages = new PageRange(20, 501) } },
        { "versionedShare", new RunParameters { VersionedShare = 101 } },
        { "versionedShare", new RunParameters { VersionedShare = -1 } },
        { "maxVersions", new RunParameters { MaxVersions = 1 } },
        { "maxVersions", new RunParameters { MaxVersions = 6 } },
        { "outdatedPerType", new RunParameters { OutdatedPerType = 8 } },
        { "outdatedPerType", new RunParameters { OutdatedPerType = -1 } },
        { "contradictionPairsPerType", new RunParameters { ContradictionPairsPerType = -1 } },
        { "crossTypeContradictionPairs", new RunParameters { CrossTypeContradictionPairs = -1 } },
        { "poison", new RunParameters { Poison = [new PoisonQuota("a", 1), new PoisonQuota("a", 2)] } },
        { "poison", new RunParameters { Poison = [new PoisonQuota("", 1)] } },
        { "poison", new RunParameters { Poison = [new PoisonQuota("a", -1)] } },
        { "maxSharedShare", new RunParameters { MaxSharedShare = 101 } },
    };

    [Theory]
    [MemberData(nameof(Invalid))]
    public void Validate_OutOfRange_ThrowsWithField(string field, RunParameters p)
    {
        var ex = Assert.Throws<RunParametersException>(p.Validate);

        Assert.Equal(field, ex.Field);
    }

    [Fact]
    public void Validate_OutdatedAtLimit_Passes()
    {
        new RunParameters { OutdatedPerType = 7 }.Validate();
    }

    [Fact]
    public void SaveLoad_RoundTrips()
    {
        var p = new RunParameters
        {
            Seed = 42,
            Types = ["regulaminy", "procedury"],
            Pages = new PageRange(5, 9),
            Poison = [new PoisonQuota("polecenia-dla-ai", 3), new PoisonQuota("podszywanie", 2)],
            StrictUniqueness = false,
        };
        p.ParserOptions.Normalization.HyphenationExceptions.Add("test-słowo");
        p.ParserOptions.Limits.MaxPages = null;

        p.Save(P());
        var loaded = RunParameters.Load(P());

        Assert.Equal(p.ToJson(), loaded.ToJson());
        Assert.Equal(["regulaminy", "procedury"], loaded.Types);
        Assert.Equal(3, loaded.ParserOptions.Normalization.HyphenationExceptions.Count);
        Assert.Null(loaded.ParserOptions.Limits.MaxPages);
        Assert.Equal(p.Poison, loaded.Poison);
        Assert.Equal(new PageRange(5, 9), loaded.Pages);
    }

    [Fact]
    public void ParserOptionsDefaults_RoundTrip()
    {
        var p = new RunParameters();

        p.Save(P());
        var loaded = RunParameters.Load(P());

        Assert.Equal(p.ToJson(), loaded.ToJson());
        Assert.Equal(["e-mail", "biało-czerwony"], loaded.ParserOptions.Normalization.HyphenationExceptions);
        Assert.Equal(TimeSpan.FromSeconds(120), loaded.ParserOptions.Limits.MaxDuration);
    }

    [Fact]
    public void Load_MissingFields_KeepDefaults()
    {
        File.WriteAllText(P(), "{ \"seed\": 7, \"pages\": { \"min\": 3, \"max\": 4 } }");

        var p = RunParameters.Load(P());

        Assert.Equal(7UL, p.Seed);
        Assert.Equal(new PageRange(3, 4), p.Pages);
        Assert.Equal(10, p.DocumentsPerType);
        Assert.Equal(2, p.ParserOptions.Normalization.HyphenationExceptions.Count);
    }

    [Fact]
    public void Load_UnknownField_NamesIt()
    {
        File.WriteAllText(P(), "{ \"seeed\": 7 }");

        var ex = Assert.Throws<RunParametersException>(() => RunParameters.Load(P()));

        Assert.Contains("seeed", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_UnknownNestedField_NamesIt()
    {
        File.WriteAllText(P(), "{ \"parserOptions\": { \"nope\": 1 } }");

        var ex = Assert.Throws<RunParametersException>(() => RunParameters.Load(P()));

        Assert.Contains("nope", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_SyntaxError_Throws()
    {
        File.WriteAllText(P(), "{ \"seed\": ");

        Assert.Throws<RunParametersException>(() => RunParameters.Load(P()));
    }

    [Fact]
    public void Save_Format_NoBomLfTrailingNewlineCamelCase()
    {
        new RunParameters().Save(P());

        byte[] bytes = File.ReadAllBytes(P());
        string text = Encoding.UTF8.GetString(bytes);

        Assert.NotEqual(0xEF, bytes[0]);
        Assert.EndsWith("\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\r", text, StringComparison.Ordinal);
        Assert.StartsWith("{\n  \"seed\": 20261008,\n  \"referenceDate\": \"2026-10-01\",\n  \"documentsPerType\": 10,", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\"types\"", text, StringComparison.Ordinal);
        Assert.Contains("\"outputDirectory\": \"corpus\"", text, StringComparison.Ordinal);
        Assert.Contains("\"contentDirectory\": \"corpus/zrodla\"", text, StringComparison.Ordinal);
        Assert.Equal(text, new RunParameters().ToJson());
    }

    [Fact]
    public void Save_WindowsSeparators_AreNormalised()
    {
        var p = new RunParameters { ContentDirectory = "corpus\\zrodla", OutputDirectory = "out\\dir" };

        string json = p.ToJson();

        Assert.Contains("\"contentDirectory\": \"corpus/zrodla\"", json, StringComparison.Ordinal);
        Assert.Contains("\"outputDirectory\": \"out/dir\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\\\\", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("C:\\korpus", "outputDirectory")]
    [InlineData("/tmp/korpus", "outputDirectory")]
    public void Save_AbsoluteOutput_Throws(string path, string field)
    {
        var ex = Assert.Throws<RunParametersException>(() => new RunParameters { OutputDirectory = path }.Save(P()));

        Assert.Equal(field, ex.Field);
    }

    [Fact]
    public void Save_AbsoluteContent_Throws()
    {
        var ex = Assert.Throws<RunParametersException>(() => new RunParameters { ContentDirectory = "D:/zrodla" }.ToJson());

        Assert.Equal("contentDirectory", ex.Field);
    }

    [Fact]
    public void ToJson_KeepsPolishLettersReadable()
    {
        var p = new RunParameters { Poison = [new PoisonQuota("zażółć", 1)] };

        Assert.Contains("zażółć", p.ToJson(), StringComparison.Ordinal);
    }
}
