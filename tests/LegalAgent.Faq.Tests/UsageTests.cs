using LegalAgent.Faq.Model;
using LegalAgent.Faq.Tests.Fakes;
using Microsoft.SemanticKernel;

namespace LegalAgent.Faq.Tests;

public sealed class UsageTests
{
    [Fact]
    public async Task Usage_SummedAndReportedPerStep()
    {
        var chat = new FakeChatCompletionService()
            .Respond(FaqJson.Candidates("D1", 2), new TestUsage(100, 10))
            .Respond(FaqJson.Candidates("D2", 2), new LegacyTestUsage(200, 20))
            .Respond(FaqJson.Selection(2, 2), new TestUsage(50, 5));
        var events = new List<FaqEvent>();

        FaqResult result = await Generator(chat).GenerateAsync(Documents, new SyncProgress(events), Ct);

        Assert.Equal(new FaqUsage(350, 35), result.Usage);
        Assert.Equal(new FaqUsage(100, 10), events[1].Usage);
        Assert.Equal(new FaqUsage(200, 20), events[3].Usage);
        Assert.Equal(new FaqUsage(50, 5), events[5].Usage);
    }

    [Fact]
    public async Task Usage_MissingOrUnknownShape_IsNull()
    {
        var chat = new FakeChatCompletionService()
            .Respond(FaqJson.Candidates("D1", 2))
            .Respond(FaqJson.Candidates("D2", 2), "nieznany kształt")
            .Respond(FaqJson.Selection(2, 2), new TestUsage(50, 5));
        var events = new List<FaqEvent>();

        FaqResult result = await Generator(chat).GenerateAsync(Documents, new SyncProgress(events), Ct);

        Assert.Null(result.Usage);
        Assert.Null(events[1].Usage);
        Assert.Null(events[3].Usage);
        Assert.Equal(new FaqUsage(50, 5), events[5].Usage);
    }

    [Fact]
    public void Reader_ReadsKnownShapes()
    {
        Assert.Equal(new FaqUsage(3, 4), UsageReader.Read(Metadata(new TestUsage(3, 4))));
        Assert.Equal(new FaqUsage(5, 6), UsageReader.Read(Metadata(new LegacyTestUsage(5, 6))));
        Assert.Null(UsageReader.Read(null));
        Assert.Null(UsageReader.Read(Metadata(null)));
        Assert.Null(UsageReader.Read(Metadata(42)));
    }

    private static readonly FaqDocumentInput[] Documents =
    [
        new("A", new Uri("https://example.test/a.pdf"), "# A\n\nTreść dokumentu testowego.\n", []),
        new("B", new Uri("https://example.test/b.pdf"), "# B\n\nTreść dokumentu testowego.\n", []),
    ];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Dictionary<string, object?> Metadata(object? usage) => new() { ["Usage"] = usage };

    private static FaqGenerator Generator(FakeChatCompletionService chat) =>
        new(chat, new FaqGeneratorOptions { ItemCount = 2 }, (_, _) => new PromptExecutionSettings());

    private sealed class SyncProgress(List<FaqEvent> events) : IProgress<FaqEvent>
    {
        public void Report(FaqEvent value) => events.Add(value);
    }
}
