namespace LegalAgent.Corpus.Content;

/// <summary>Kind of a bank fact; decides the output format.</summary>
public enum FactKind
{
    /// <summary>Amount in PLN: 25 → "25,00 zł".</summary>
    Kwota,

    /// <summary>Percentage: 1.5 → "1,5%".</summary>
    Procent,

    /// <summary>Term in days: 14 → "14 dni".</summary>
    Termin,

    /// <summary>Free text (unit names, addresses, phone numbers).</summary>
    Tekst,

    /// <summary>Date: 2027-01-01 → "1 stycznia 2027 r.".</summary>
    Data,
}

/// <summary>
/// A fact value as written in <c>fakty.yaml</c>: <see cref="Number"/> for amounts, percentages and terms,
/// <see cref="Text"/> for text, <see cref="Date"/> for dates.
/// </summary>
public sealed record FactValue(decimal? Number = null, string? Text = null, DateOnly? Date = null);
