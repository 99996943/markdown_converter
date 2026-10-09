using LegalAgent.Corpus.Composition;
using LegalAgent.Corpus.Content;
using LegalAgent.Corpus.Random;

namespace LegalAgent.Corpus.Tests.Unit.Content;

public sealed class TextTemplateTests
{
    private sealed class FakeContext : ITemplateContext
    {
        public (FactKind Kind, FactValue Value) Fact(string id) => id switch
        {
            "kwota" => (FactKind.Kwota, new FactValue(Number: 25m)),
            "duza" => (FactKind.Kwota, new FactValue(Number: 1234.5m)),
            "procent" => (FactKind.Procent, new FactValue(Number: 1.5m)),
            "termin" => (FactKind.Termin, new FactValue(Number: 14m)),
            "dzien" => (FactKind.Termin, new FactValue(Number: 1m)),
            "data" => (FactKind.Data, new FactValue(Date: new DateOnly(2027, 1, 1))),
            "tekst" => (FactKind.Tekst, new FactValue(Text: "Biuro Reklamacji")),
            _ => throw new ContentException($"Nieznany fakt '{id}'."),
        };

        public string Param(string name) => name == "bank"
            ? "Bank Przykładowy S.A."
            : throw new ContentException($"Nieznany parametr '{name}'.");
    }

    private static IReadOnlyList<Inline> Render(string template, ulong seed = 1) =>
        TextTemplate.Render(template, new FakeContext(), new DeterministicRandom(seed));

    private static string Plain(string template, ulong seed = 1) => Inline.PlainText(Render(template, seed));

    [Fact]
    public void PlainText_IsOneInline()
    {
        Inline run = Assert.Single(Render("Zwykły tekst."));
        Assert.Equal(new Inline("Zwykły tekst."), run);
    }

    [Fact]
    public void Empty_GivesNoInlines() => Assert.Empty(Render(string.Empty));

    [Fact]
    public void Variants_SameSeed_SameChoice()
    {
        for (ulong seed = 0; seed < 20; seed++)
        {
            Assert.Equal(Plain("{a|b|c}", seed), Plain("{a|b|c}", seed));
        }
    }

    [Fact]
    public void Variants_AllAppearOverManySeeds()
    {
        var seen = new HashSet<string>();
        for (ulong seed = 0; seed < 300; seed++)
        {
            seen.Add(Plain("{a|b|c}", seed));
        }

        Assert.True(seen.SetEquals(["a", "b", "c"]));
    }

    [Fact]
    public void Variants_EmptyBranchAllowed()
    {
        var seen = new HashSet<string>();
        for (ulong seed = 0; seed < 100; seed++)
        {
            seen.Add(Plain("x{a|}y", seed));
        }

        Assert.Contains("xay", seen);
        Assert.Contains("xy", seen);
    }

    [Fact]
    public void Variants_Nested()
    {
        var seen = new HashSet<string>();
        for (ulong seed = 0; seed < 300; seed++)
        {
            seen.Add(Plain("{x {a|b}|y}", seed));
        }

        Assert.True(seen.SetEquals(["x a", "x b", "y"]));
    }

    [Fact]
    public void Variants_UnchosenBranchConsumesNoRandomNumbers()
    {
        // Template: {a|{b|c}} {d|e}. If the nested group is skipped (branch 0 chosen), the second group
        // must use the second draw of the stream; otherwise the third.
        for (ulong seed = 0; seed < 50; seed++)
        {
            var probe = new DeterministicRandom(seed);
            int first = probe.Next(2);
            int second = probe.Next(2);
            int third = probe.Next(2);
            string expected = first == 0
                ? "a " + (second == 0 ? "d" : "e")
                : (second == 0 ? "b" : "c") + " " + (third == 0 ? "d" : "e");
            Assert.Equal(expected, Plain("{a|{b|c}} {d|e}", seed));
        }
    }

    [Fact]
    public void Escapes_AreLiteral() =>
        Assert.Equal("{a|b}*c*\\", Plain("\\{a\\|b\\}\\*c\\*\\\\"));

    [Fact]
    public void Facts_AllKinds()
    {
        Assert.Equal("25,00 zł", Plain("{{fakt:kwota}}"));
        Assert.Equal("1 234,50 zł", Plain("{{fakt:duza}}"));
        Assert.Equal("1,5%", Plain("{{fakt:procent}}"));
        Assert.Equal("14 dni", Plain("{{fakt:termin}}"));
        Assert.Equal("1 dzień", Plain("{{fakt:dzien}}"));
        Assert.Equal("1 stycznia 2027 r.", Plain("{{fakt:data}}"));
        Assert.Equal("Biuro Reklamacji", Plain("{{fakt:tekst}}"));
    }

    [Fact]
    public void Facts_ThousandsSeparatorIsNormalSpace()
    {
        string s = Plain("{{fakt:duza}}");
        Assert.DoesNotContain((char)0xA0, s);
        Assert.Contains(" ", s, StringComparison.Ordinal);
    }

    [Fact]
    public void Param_IsInserted() => Assert.Equal("Bank Przykładowy S.A. pobiera", Plain("{{param:bank}} pobiera"));

    [Fact]
    public void Bold_AndItalic_AreStyled()
    {
        IReadOnlyList<Inline> runs = Render("a **b** c *d* e");
        Assert.Equal(
            [
                new Inline("a "),
                new Inline("b", InlineStyle.Bold),
                new Inline(" c "),
                new Inline("d", InlineStyle.Italic),
                new Inline(" e"),
            ],
            runs);
    }

    [Fact]
    public void Bold_ContainingFact()
    {
        IReadOnlyList<Inline> runs = Render("opłata **{{fakt:kwota}}**");
        Assert.Equal([new Inline("opłata "), new Inline("25,00 zł", InlineStyle.Bold)], runs);
    }

    [Fact]
    public void FootnoteRef_IsSeparateInline()
    {
        IReadOnlyList<Inline> runs = Render("tekst[^1] dalej");
        Assert.Equal(
            [new Inline("tekst"), new Inline("1", InlineStyle.Regular, InlineKind.FootnoteRef), new Inline(" dalej")],
            runs);
    }

    [Fact]
    public void Ref_IsReferenceInline()
    {
        IReadOnlyList<Inline> runs = Render("zgodnie z {{ref:blok:x}}.");
        Assert.Equal(
            [new Inline("zgodnie z "), new Inline("blok:x", InlineStyle.Regular, InlineKind.Reference), new Inline(".")],
            runs);
    }

    [Fact]
    public void AdjacentText_Merges()
    {
        IReadOnlyList<Inline> runs = Render("a {b|b} {{param:bank}} \\{ {{fakt:kwota}}");
        Assert.Equal([new Inline("a b Bank Przykładowy S.A. { 25,00 zł")], runs);
    }

    [Fact]
    public void Newlines_RunOfWhitespaceWithNewline_IsOneSpace()
    {
        Assert.Equal("a b", Plain("a \n  b"));
        Assert.Equal("a  b", Plain("a  b"));
    }

    [Fact]
    public void UnknownFact_Throws_WithPositionAndId()
    {
        var ex = Assert.Throws<ContentException>(() => Render("ab {{fakt:nie-ma}}"));
        Assert.Equal(3, ex.Position);
        Assert.Contains("nie-ma", ex.Message, StringComparison.Ordinal);
        Assert.Contains("3", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownParam_Throws()
    {
        var ex = Assert.Throws<ContentException>(() => Render("{{param:xyz}}"));
        Assert.Equal(0, ex.Position);
        Assert.Contains("xyz", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownPrefix_Throws()
    {
        var ex = Assert.Throws<ContentException>(() => Render("a{{co:x}}"));
        Assert.Equal(1, ex.Position);
    }

    [Fact]
    public void UnclosedBrace_Throws()
    {
        var ex = Assert.Throws<ContentException>(() => Render("ab {a|b"));
        Assert.Equal(3, ex.Position);
        Assert.Contains("3", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnclosedDoubleBrace_Throws()
    {
        var ex = Assert.Throws<ContentException>(() => Render("x {{fakt:kwota"));
        Assert.Equal(2, ex.Position);
    }

    [Fact]
    public void UnclosedBold_Throws()
    {
        var ex = Assert.Throws<ContentException>(() => Render("ab **cd"));
        Assert.Equal(3, ex.Position);
    }

    [Fact]
    public void UnclosedItalic_Throws()
    {
        var ex = Assert.Throws<ContentException>(() => Render("ab *cd"));
        Assert.Equal(3, ex.Position);
    }

    [Fact]
    public void StrayCloseBrace_AndPipe_Throw()
    {
        Assert.Equal(2, Assert.Throws<ContentException>(() => Render("ab}")).Position);
        Assert.Equal(1, Assert.Throws<ContentException>(() => Render("a|b")).Position);
    }
}
