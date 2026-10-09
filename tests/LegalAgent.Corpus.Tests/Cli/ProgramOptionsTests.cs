using System.Globalization;
using System.Reflection;
using LegalAgent.Corpus.Cli;
using LegalAgent.Corpus.Planning;

namespace LegalAgent.Corpus.Tests.Cli;

/// <summary>Every CLI option overrides the value from --params (contracts/cli.md, "Opcje").</summary>
public sealed class ProgramOptionsTests : IDisposable
{
    private readonly string baseDirectory = Path.Combine(Path.GetTempPath(), "cliopt-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

    public ProgramOptionsTests()
    {
        Directory.CreateDirectory(baseDirectory);
        new RunParameters
        {
            Seed = 7,
            ReferenceDate = new DateOnly(2025, 1, 2),
            Types = ["a", "b"],
            DocumentsPerType = 10,
            Pages = new PageRange(5, 6),
            VersionedShare = 30,
            OutdatedPerType = 2,
            ContradictionPairsPerType = 4,
            CrossTypeContradictionPairs = 5,
            Poison = [new PoisonQuota("stary", 9)],
            StrictUniqueness = true,
            OutputDirectory = "out-file",
            ContentDirectory = "content-file",
        }.Save(Path.Combine(baseDirectory, "przebieg.json"));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(baseDirectory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void NoOptions_KeepsFileValues()
    {
        RunParameters p = Resolve();

        Assert.Equal(7UL, p.Seed);
        Assert.Equal(new DateOnly(2025, 1, 2), p.ReferenceDate);
        Assert.Equal(["a", "b"], p.Types);
        Assert.Equal(10, p.DocumentsPerType);
        Assert.Equal(new PageRange(5, 6), p.Pages);
        Assert.Equal(30, p.VersionedShare);
        Assert.Equal(2, p.OutdatedPerType);
        Assert.Equal(4, p.ContradictionPairsPerType);
        Assert.Equal(5, p.CrossTypeContradictionPairs);
        Assert.Equal([new PoisonQuota("stary", 9)], p.Poison);
        Assert.True(p.StrictUniqueness);
        Assert.Equal("out-file", p.OutputDirectory);
        Assert.Equal("content-file", p.ContentDirectory);
    }

    [Fact]
    public void Seed_Overrides() => Assert.Equal(42UL, Resolve("--seed", "42").Seed);

    [Fact]
    public void Types_Overrides() => Assert.Equal(["regulaminy", "procedury"], Resolve("--types", "regulaminy,procedury").Types);

    [Fact]
    public void Count_Overrides() => Assert.Equal(15, Resolve("--count", "15").DocumentsPerType);

    [Fact]
    public void Pages_Overrides() => Assert.Equal(new PageRange(40, 50), Resolve("--pages", "40-50").Pages);

    [Fact]
    public void ReferenceDate_Overrides() =>
        Assert.Equal(new DateOnly(2026, 3, 15), Resolve("--reference-date", "2026-03-15").ReferenceDate);

    [Fact]
    public void Versioned_IsPercent_AndOverridesVersionedShare()
    {
        RunParameters p = Resolve("--versioned", "50");

        Assert.Equal(50, p.VersionedShare);
        Assert.Equal(5, p.VersionedCount);
    }

    [Fact]
    public void Outdated_Overrides() => Assert.Equal(4, Resolve("--outdated", "4").OutdatedPerType);

    [Fact]
    public void Contradictions_TwoValues_SetsPerTypeAndCrossType()
    {
        RunParameters p = Resolve("--contradictions", "2,3");

        Assert.Equal(2, p.ContradictionPairsPerType);
        Assert.Equal(3, p.CrossTypeContradictionPairs);
    }

    [Fact]
    public void Contradictions_OneValue_SetsPerTypeAndKeepsCrossType()
    {
        RunParameters p = Resolve("--contradictions", "0");

        Assert.Equal(0, p.ContradictionPairsPerType);
        Assert.Equal(5, p.CrossTypeContradictionPairs);
    }

    [Fact]
    public void Poison_List_ReplacesFileQuotas() =>
        Assert.Equal(
            [new PoisonQuota("polecenia-dla-ai", 3), new PoisonQuota("podszywanie", 2)],
            Resolve("--poison", "polecenia-dla-ai=3,podszywanie=2").Poison);

    [Fact]
    public void Poison_Repeated_Accumulates() =>
        Assert.Equal(
            [new PoisonQuota("x", 1), new PoisonQuota("y", 2)],
            Resolve("--poison", "x=1", "--poison", "y=2").Poison);

    [Fact]
    public void Poison_None_ClearsQuotas() => Assert.Empty(Resolve("--poison", "none").Poison);

    [Fact]
    public void NoStrictUniqueness_TurnsItOff() => Assert.False(Resolve("--no-strict-uniqueness").StrictUniqueness);

    [Fact]
    public void Out_And_Content_Override()
    {
        RunParameters p = Resolve("--out", "o2", "--content", "c2");

        Assert.Equal("o2", p.OutputDirectory);
        Assert.Equal("c2", p.ContentDirectory);
    }

    [Fact]
    public void Params_ChoosesTheFile()
    {
        new RunParameters { Seed = 99 }.Save(Path.Combine(baseDirectory, "inny.json"));

        Assert.Equal(99UL, Resolve("--params", "inny.json").Seed);
    }

    [Theory]
    [InlineData("--count", "abc")]
    [InlineData("--count", "0")]
    [InlineData("--count", "501")]
    [InlineData("--pages", "10")]
    [InlineData("--pages", "10-5")]
    [InlineData("--pages", "0-5")]
    [InlineData("--reference-date", "2026-13-40")]
    [InlineData("--reference-date", "jutro")]
    [InlineData("--versioned", "101")]
    [InlineData("--versioned", "x")]
    [InlineData("--outdated", "-1")]
    [InlineData("--outdated", "9")]
    [InlineData("--contradictions", "a")]
    [InlineData("--contradictions", "1,b")]
    [InlineData("--contradictions", "1,2,3")]
    [InlineData("--poison", "bez-liczby")]
    [InlineData("--poison", "x=abc")]
    [InlineData("--poison", "x=-1")]
    [InlineData("--poison", "x=1,x=2")]
    [InlineData("--poison", "none,x=1")]
    [InlineData("--types", "a,,b")]
    [InlineData("--seed", "-1")]
    public void InvalidValue_Exit2_WithPolishMessage(string option, string value)
    {
        var (code, err) = InvokeStderr("verify", "--params", "przebieg.json", option, value);

        Assert.Equal(2, code);
        Assert.StartsWith("błąd:", err, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidValue_MessageNamesTheOption()
    {
        var (_, err) = InvokeStderr("verify", "--params", "przebieg.json", "--reference-date", "jutro");

        Assert.Contains("--reference-date", err, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingValue_Exit2()
    {
        var (code, err) = InvokeStderr("verify", "--params", "przebieg.json", "--versioned");

        Assert.Equal(2, code);
        Assert.Contains("--versioned", err, StringComparison.Ordinal);
    }

    [Fact]
    public void Help_MentionsEveryOption()
    {
        var stdout = new StringWriter(CultureInfo.InvariantCulture);
        Assert.Equal(0, Program.Run(["--help"], stdout, new StringWriter(CultureInfo.InvariantCulture), baseDirectory));

        foreach (string option in new[] { "--types", "--count", "--pages", "--seed", "--reference-date", "--versioned", "--outdated", "--contradictions", "--poison", "--no-strict-uniqueness", "--out", "--content", "--truth", "--save-params", "--params" })
        {
            Assert.Contains(option, stdout.ToString(), StringComparison.Ordinal);
        }
    }

    private RunParameters Resolve(params string[] options)
    {
        MethodInfo? method = typeof(Program).GetMethod("ResolveParameters", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        string[] args = ["--params", "przebieg.json", .. options];
        try
        {
            return (RunParameters)method.Invoke(null, [args, baseDirectory])!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }

    private (int Code, string Error) InvokeStderr(params string[] args)
    {
        var stderr = new StringWriter(CultureInfo.InvariantCulture);
        int code = Program.Run(args, new StringWriter(CultureInfo.InvariantCulture), stderr, baseDirectory);
        return (code, stderr.ToString());
    }
}
