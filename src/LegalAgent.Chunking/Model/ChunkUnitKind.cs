namespace LegalAgent.Chunking.Model;

/// <summary>Kind of the unit a chunk belongs to: the preamble or the kind of the section.</summary>
public enum ChunkUnitKind
{
    /// <summary>Content before the first section.</summary>
    Preamble,

    /// <summary>Document title section.</summary>
    DocumentTitle,

    /// <summary>Book (Księga).</summary>
    Book,

    /// <summary>Part (Część).</summary>
    Part,

    /// <summary>Division (Dział).</summary>
    Division,

    /// <summary>Chapter (Rozdział).</summary>
    Chapter,

    /// <summary>Subchapter (Oddział).</summary>
    Subchapter,

    /// <summary>Article (Art.).</summary>
    Article,

    /// <summary>Paragraph (§).</summary>
    Paragraph,

    /// <summary>Section with a typographic heading (tariff or procedure section and the like).</summary>
    Typographic,

    /// <summary>Section of a table-document (spec 002).</summary>
    TableDocumentSection,
}
