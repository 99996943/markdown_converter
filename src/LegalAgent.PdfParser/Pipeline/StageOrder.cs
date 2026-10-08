namespace LegalAgent.PdfParser.Pipeline;

/// <summary>Order values of the built-in pipeline stages.</summary>
public static class StageOrder
{
    /// <summary>Glyph, ruling and image extraction.</summary>
    public const int PageExtraction = 100;

    /// <summary>Ligatures, spaces, NFC, superscripts.</summary>
    public const int TextNormalization = 200;

    /// <summary>Words, lines, segments and zones.</summary>
    public const int LineAssembly = 300;

    /// <summary>Running header, footer and page number removal.</summary>
    public const int ArtifactRemoval = 400;

    /// <summary>Footnote detection.</summary>
    public const int FootnoteDetection = 500;

    /// <summary>Step schemes (FR-067).</summary>
    public const int StepSequence = 550;

    /// <summary>Table detection.</summary>
    public const int TableDetection = 600;

    /// <summary>Column reading order.</summary>
    public const int ReadingOrder = 700;

    /// <summary>List detection.</summary>
    public const int ListDetection = 800;

    /// <summary>Heading detection.</summary>
    public const int HeadingDetection = 900;

    /// <summary>Paragraph assembly.</summary>
    public const int BlockAssembly = 1000;

    /// <summary>Section tree construction.</summary>
    public const int DocumentBuild = 1100;
}
