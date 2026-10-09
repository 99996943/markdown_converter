namespace LegalAgent.Corpus.Content;

/// <summary>Polish formatting of fact values (amounts, percentages, terms, dates).</summary>
public static class PolishFormat
{
    /// <summary>"1 234,50 zł".</summary>
    public static string Amount(decimal value) => throw new NotImplementedException();

    /// <summary>"1,5%".</summary>
    public static string Percent(decimal value) => throw new NotImplementedException();

    /// <summary>"1 dzień", "14 dni".</summary>
    public static string Days(int value) => throw new NotImplementedException();

    /// <summary>"1 stycznia 2027 r.".</summary>
    public static string Date(DateOnly value) => throw new NotImplementedException();

    /// <summary>Formats a fact value according to its kind.</summary>
    public static string Format(FactKind kind, FactValue value) => throw new NotImplementedException();
}
