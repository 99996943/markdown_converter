using System.Globalization;
using LegalAgent.Faq.Model;
using LegalAgent.Faq.Tests.Fixtures;

namespace LegalAgent.Faq.Tests;

public sealed class RendererTests
{
    private static readonly FaqSourceDocument[] Documents =
    [
        new("D1", "Regulamin A", new Uri("https://example.test/pdf/a.pdf")),
        new("D2", "Taryfa B", new Uri("https://example.test/pdf/b.pdf")),
        new("D3", "Regulamin C", new Uri("https://example.test/pdf/c.pdf")),
    ];

    private static readonly FaqFileHeader Header = new(
        "FAQ — regulaminy \"Banku\" C:\\katalog",
        "Opis\nw dwóch wierszach.",
        new DateTimeOffset(2026, 10, 9, 14, 3, 12, TimeSpan.FromHours(2)),
        "gpt-4o-mini",
        "wdrożenie-1");

    [Fact]
    public void Render_MatchesGolden()
    {
        string text = FaqMarkdownRenderer.Render(Result(), Header);

        GoldenFile.AssertMatches(text, GoldenFile.PathOf("faq.expected.md"));
    }

    [Fact]
    public void Render_LayoutRules()
    {
        string text = FaqMarkdownRenderer.Render(Result(), Header);

        Assert.StartsWith("---\ntype: faq\ntitle: ", text, StringComparison.Ordinal);
        Assert.Contains("title: \"FAQ — regulaminy \\\"Banku\\\" C:\\\\katalog\"\n", text, StringComparison.Ordinal);
        Assert.Contains("description: \"Opis w dwóch wierszach.\"\n", text, StringComparison.Ordinal);
        Assert.Contains("timestamp: \"2026-10-09T12:03:12Z\"\n", text, StringComparison.Ordinal);
        Assert.Equal(10, text.Split('\n').Count(l => l.StartsWith("## ", StringComparison.Ordinal)));
        Assert.DoesNotContain("\n# ", text, StringComparison.Ordinal);
        Assert.Contains("\n## \\# Czy mogę mieć kartę?\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\r", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\n\n\n", text, StringComparison.Ordinal);
        Assert.All(text.Split('\n'), line => Assert.Equal(line.TrimEnd(), line));
        Assert.EndsWith("§ 10\n", text, StringComparison.Ordinal);
        Assert.False(text.EndsWith("\n\n", StringComparison.Ordinal));
    }

    [Fact]
    public void Render_IsStable()
    {
        Assert.Equal(FaqMarkdownRenderer.Render(Result(), Header), FaqMarkdownRenderer.Render(Result(), Header));
    }

    [Fact]
    public void Source_UnitWithKnownPage_NameLinksToPage()
    {
        Assert.Equal(
            "Źródło: [Regulamin A](https://example.test/pdf/a.pdf#page=7), § 12",
            SourceLine(LocatedA(), new FaqSource("D1", "§ 12")));
    }

    [Fact]
    public void Source_LongerCitedUnit_SamePage_TextUnchanged()
    {
        Assert.Equal(
            "Źródło: [Regulamin A](https://example.test/pdf/a.pdf#page=7), § 12 ust. 3",
            SourceLine(LocatedA(), new FaqSource("D1", "§ 12 ust. 3")));
    }

    [Fact]
    public void Source_WithoutUnit_NoPage()
    {
        Assert.Equal("Źródło: [Regulamin A](https://example.test/pdf/a.pdf)", SourceLine(LocatedA(), new FaqSource("D1", null)));
    }

    [Fact]
    public void Source_UnitNotLocated_NoPage()
    {
        Assert.Equal("Źródło: [Regulamin A](https://example.test/pdf/a.pdf), § 99", SourceLine(LocatedA(), new FaqSource("D1", "§ 99")));
    }

    [Fact]
    public void Source_PageUnknown_NoPage()
    {
        FaqSourceDocument document = Documents[0] with { UnitLocations = [new FaqUnitLocation("§ 12", null, null)] };

        Assert.Equal("Źródło: [Regulamin A](https://example.test/pdf/a.pdf), § 12", SourceLine(document, new FaqSource("D1", "§ 12")));
    }

    [Fact]
    public void Source_AddressWithFragment_NoPageAdded()
    {
        FaqSourceDocument document = LocatedA() with { Resource = new Uri("https://example.test/pdf/a.pdf#x") };

        Assert.Equal("Źródło: [Regulamin A](https://example.test/pdf/a.pdf#x), § 12", SourceLine(document, new FaqSource("D1", "§ 12")));
    }

    [Fact]
    public void Sources_EachWithOwnPage()
    {
        FaqSourceDocument b = Documents[1] with { UnitLocations = [new FaqUnitLocation("§ 4.", 3, null)] };
        var item = new FaqItem(1, "Pytanie?", "Odpowiedź.", [new FaqSource("D1", "§ 12"), new FaqSource("D2", "§ 4")], ["D1-K1", "D2-K1"]);

        string text = FaqMarkdownRenderer.Render(new FaqResult([item], [LocatedA(), b], [], null), Header);

        Assert.Contains(
            "\nŹródła: [Regulamin A](https://example.test/pdf/a.pdf#page=7), § 12; [Taryfa B](https://example.test/pdf/b.pdf#page=3), § 4\n",
            text,
            StringComparison.Ordinal);
    }

    [Fact]
    public void FrontMatter_ResourceWithoutPage()
    {
        string text = FaqMarkdownRenderer.Render(
            new FaqResult([new FaqItem(1, "Pytanie?", "Odpowiedź.", [new FaqSource("D1", "§ 12")], ["D1-K1"])], [LocatedA()], [], null),
            Header);

        Assert.Contains("resource:\n  - \"https://example.test/pdf/a.pdf\"\n", text, StringComparison.Ordinal);
    }

    private static FaqSourceDocument LocatedA() =>
        Documents[0] with { UnitLocations = [new FaqUnitLocation("§ 1", 1, null), new FaqUnitLocation("§ 12", 7, null)] };

    private static string SourceLine(FaqSourceDocument document, FaqSource source)
    {
        var item = new FaqItem(1, "Pytanie?", "Odpowiedź.", [source], [document.Id + "-K1"]);
        string text = FaqMarkdownRenderer.Render(new FaqResult([item], [document], [], null), Header);
        return text.Split('\n').Single(l => l.StartsWith("Źród", StringComparison.Ordinal));
    }

    private static FaqResult Result()
    {
        var items = new List<FaqItem>
        {
            new(1, "Ile kosztuje prowadzenie rachunku?", "Prowadzenie rachunku jest bezpłatne.", [new FaqSource("D1", "§ 12")], ["D1-K1"]),
            new(
                2,
                "# Czy mogę mieć kartę?",
                "Tak.\r\n\r\nKarta jest wydawana na wniosek.  \nWniosek składa się w aplikacji.",
                [new FaqSource("D1", "§ 3"), new FaqSource("D2", null)],
                ["D1-K2", "D2-K1"]),
            new(3, "Jak zamknąć rachunek?", "Dokument nie rozstrzyga, jak zamknąć rachunek.", [new FaqSource("D3", null)], ["D3-K1"]),
        };
        for (int n = 4; n <= 10; n++)
        {
            items.Add(new FaqItem(
                n,
                string.Create(CultureInfo.InvariantCulture, $"Pytanie {n}?"),
                string.Create(CultureInfo.InvariantCulture, $"Odpowiedź {n}."),
                [new FaqSource("D2", string.Create(CultureInfo.InvariantCulture, $"§ {n}"))],
                [string.Create(CultureInfo.InvariantCulture, $"D2-K{n}")]));
        }

        return new FaqResult(items, Documents, [], null);
    }
}
