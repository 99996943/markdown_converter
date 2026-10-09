using LegalAgent.Chunking.Model;
using LegalAgent.Chunking.Serialization;

namespace LegalAgent.Chunking.Tests.Serialization;

/// <summary>T030: the JSON contract of chunks (contracts/chunks-json.md, FR-250 – FR-252).</summary>
public sealed class ChunkJsonTests
{
    private static readonly ChunkedDocumentHeader Header = new(
        new DocumentMetadata("REG-05-w1")
        {
            Designation = "BP/REG/05",
            Type = "regulation",
            Title = "Regulamin „Konto”",
            Version = 1,
            ValidFrom = new DateOnly(2024, 9, 1),
            ValidTo = new DateOnly(2025, 8, 31),
            Status = "outdated",
        },
        "Regulamin „Konto”",
        "REGULAMIN",
        "BP/REG/05",
        new ChunkSource(22, "ab12", true, []));

    private static readonly Chunk Paragraph = new(
        "REG-05-w1_a761a1aea4847cbf_2",
        "BP/REG/05 | § 11",
        2,
        2,
        ChunkUnitKind.Paragraph,
        "§ 11",
        ["3."],
        ["Warunki <promocji> & opłaty", "§ 11."],
        new PageSpan(5, 6),
        20,
        false,
        "### § 11.\n\n- 3\\. \"Tak\"");

    [Fact]
    public void ToJsonLines_WritesTheContractFieldsInOrderLeavingOutNulls()
    {
        string json = ChunkJson.ToJsonLines(new ChunkedDocument(Header, [Paragraph]));

        Assert.Equal(
            "{\"schemaVersion\":1,\"document\":{\"id\":\"REG-05-w1\",\"designation\":\"BP/REG/05\",\"type\":\"regulation\","
            + "\"title\":\"Regulamin „Konto”\",\"detectedTitle\":\"REGULAMIN\",\"version\":1,\"validFrom\":\"2024-09-01\","
            + "\"validTo\":\"2025-08-31\",\"status\":\"outdated\",\"source\":{\"pageCount\":22,\"sha256\":\"ab12\",\"isComplete\":true,"
            + "\"skippedPages\":[]}},\"chunk\":{\"id\":\"REG-05-w1_a761a1aea4847cbf_2\",\"unitKey\":\"BP/REG/05 | § 11\",\"part\":2,"
            + "\"partCount\":2,\"unitKind\":\"paragraph\",\"citation\":\"§ 11\",\"listLabels\":[\"3.\"],"
            + "\"sectionPath\":[\"Warunki <promocji> & opłaty\",\"§ 11.\"],\"pages\":{\"first\":5,\"last\":6},\"length\":20,"
            + "\"exceedsLimit\":false,\"content\":\"### § 11.\\n\\n- 3\\\\. \\\"Tak\\\"\"}}\n",
            json);
    }

    [Fact]
    public void ToJsonLines_OneLinePerChunkEachEndingWithLineFeed()
    {
        Chunk preamble = Paragraph with { ChunkId = "REG-05-w1_x_1", UnitKind = ChunkUnitKind.Preamble, Citation = null, ListLabels = [], SectionPath = [] };
        Chunk tableDocument = Paragraph with { UnitKind = ChunkUnitKind.TableDocumentSection };

        string json = ChunkJson.ToJsonLines(new ChunkedDocument(Header, [preamble, tableDocument]));

        string[] lines = json.Split('\n');
        Assert.Equal(3, lines.Length);
        Assert.Equal(string.Empty, lines[2]);
        Assert.All(lines.Take(2), l => Assert.StartsWith("{\"schemaVersion\":1,\"document\":{", l, StringComparison.Ordinal));
        Assert.Contains("\"unitKind\":\"preamble\",\"listLabels\":[],\"sectionPath\":[]", lines[0], StringComparison.Ordinal);
        Assert.Contains("\"unitKind\":\"tableDocumentSection\"", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public void ToJsonLines_DocumentWithoutChunksIsEmpty()
    {
        Assert.Equal(string.Empty, ChunkJson.ToJsonLines(new ChunkedDocument(Header, [])));
    }

    [Fact]
    public void ToJsonLines_MinimalMetadataWritesOnlyRequiredDocumentFields()
    {
        var header = new ChunkedDocumentHeader(new DocumentMetadata("dz-u-2024-30"), null, null, "dz-u-2024-30", new ChunkSource(3, "cd", false, [2]));

        string json = ChunkJson.ToJsonLines(new ChunkedDocument(header, [Paragraph]));

        Assert.StartsWith(
            "{\"schemaVersion\":1,\"document\":{\"id\":\"dz-u-2024-30\",\"designation\":\"dz-u-2024-30\",\"source\":{\"pageCount\":3,\"sha256\":\"cd\",\"isComplete\":false,\"skippedPages\":[2]}},\"chunk\":",
            json,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ReadLines_ReadsBackWhatWasWritten()
    {
        string json = ChunkJson.ToJsonLines(new ChunkedDocument(Header, [Paragraph, Paragraph with { Part = 1, ChunkId = "REG-05-w1_a761a1aea4847cbf_1" }]));

        IReadOnlyList<ChunkRecord> records = ChunkJson.ReadLines(json);

        Assert.Equal(2, records.Count);
        ChunkRecord record = records[0];
        Assert.Equal(Header.Metadata, record.Document.Metadata);
        Assert.Equal(Header.Title, record.Document.Title);
        Assert.Equal(Header.DetectedTitle, record.Document.DetectedTitle);
        Assert.Equal(Header.SeriesKey, record.Document.SeriesKey);
        Assert.Equal((22, "ab12", true), (record.Document.Source.PageCount, record.Document.Source.Sha256, record.Document.Source.IsComplete));
        Assert.Empty(record.Document.Source.SkippedPages);
        Chunk chunk = record.Chunk;
        Assert.Equal(
            (Paragraph.ChunkId, Paragraph.UnitKey, Paragraph.Part, Paragraph.PartCount, Paragraph.UnitKind, Paragraph.Citation, Paragraph.Pages, Paragraph.Length, Paragraph.ExceedsLimit, Paragraph.Content),
            (chunk.ChunkId, chunk.UnitKey, chunk.Part, chunk.PartCount, chunk.UnitKind, chunk.Citation, chunk.Pages, chunk.Length, chunk.ExceedsLimit, chunk.Content));
        Assert.Equal(Paragraph.ListLabels, chunk.ListLabels);
        Assert.Equal(Paragraph.SectionPath, chunk.SectionPath);
        Assert.Equal(1, records[1].Chunk.Part);
        Assert.Equal(json, ChunkJson.ToJsonLines(new ChunkedDocument(records[0].Document, records.Select(r => r.Chunk).ToList())));
    }

    [Fact]
    public void ReadLines_IgnoresUnknownFieldsAndBlankLines()
    {
        string line = ChunkJson.ToJsonLines(new ChunkedDocument(Header, [Paragraph])).TrimEnd('\n');
        string extended = line.Replace("\"schemaVersion\":1,", "\"schemaVersion\":1,\"producer\":\"x\",", StringComparison.Ordinal);

        IReadOnlyList<ChunkRecord> records = ChunkJson.ReadLines(extended + "\n\n");

        Assert.Equal(Paragraph.Content, Assert.Single(records).Chunk.Content);
    }

    [Theory]
    [InlineData("\"schemaVersion\":1,", "", "schemaVersion")]
    [InlineData("\"schemaVersion\":1,", "\"schemaVersion\":2,", "schemaVersion")]
    [InlineData(",\"content\":", ",\"text\":", "content")]
    [InlineData("\"part\":2,", "\"part\":\"2\",", "part")]
    [InlineData("\"unitKind\":\"paragraph\"", "\"unitKind\":\"\"", "unitKind")]
    [InlineData("\"unitKind\":\"paragraph\"", "\"unitKind\":\"3\"", "unitKind")]
    [InlineData("\"unitKind\":\"paragraph\"", "\"unitKind\":\"Paragraph\"", "unitKind")]
    public void ReadLines_InvalidRecordRaisesFormatExceptionWithTheLineNumber(string from, string to, string field)
    {
        string line = ChunkJson.ToJsonLines(new ChunkedDocument(Header, [Paragraph])).TrimEnd('\n');
        string broken = line.Replace(from, to, StringComparison.Ordinal);

        FormatException error = Assert.Throws<FormatException>(() => ChunkJson.ReadLines(line + "\n" + broken + "\n"));

        Assert.Contains("2", error.Message, StringComparison.Ordinal);
        Assert.Contains(field, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadLines_LineThatIsNotJsonRaisesFormatException()
    {
        Assert.Throws<FormatException>(() => ChunkJson.ReadLines("{nie json"));
    }
}
