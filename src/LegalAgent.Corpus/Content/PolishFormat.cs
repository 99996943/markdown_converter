using System.Globalization;

namespace LegalAgent.Corpus.Content;

/// <summary>Polish formatting of fact values (amounts, percentages, terms, dates).</summary>
public static class PolishFormat
{
    private static readonly string[] MonthsGenitive =
    [
        "stycznia", "lutego", "marca", "kwietnia", "maja", "czerwca",
        "lipca", "sierpnia", "września", "października", "listopada", "grudnia",
    ];

    /// <summary>"1 234,50 zł": two decimals, decimal comma, thousands grouped with a normal space.</summary>
    public static string Amount(decimal value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        decimal rounded = Math.Round(value, 2, MidpointRounding.AwayFromZero);
        string text = rounded.ToString("#,##0.00", CultureInfo.InvariantCulture);
        return text.Replace(',', ' ').Replace('.', ',') + " zł";
    }

    /// <summary>"1,5%": decimal comma, no trailing zeros, no space before the sign.</summary>
    public static string Percent(decimal value) =>
        value.ToString("0.############################", CultureInfo.InvariantCulture).Replace('.', ',') + "%";

    /// <summary>"1 dzień", otherwise "{n} dni".</summary>
    public static string Days(int value) =>
        value == 1 ? "1 dzień" : value.ToString(CultureInfo.InvariantCulture) + " dni";

    /// <summary>"1 stycznia 2027 r.".</summary>
    public static string Date(DateOnly value) =>
        string.Create(CultureInfo.InvariantCulture, $"{value.Day} {MonthsGenitive[value.Month - 1]} {value.Year} r.");

    /// <summary>Formats a fact value according to its kind; a missing value for the kind throws <see cref="ContentException"/>.</summary>
    public static string Format(FactKind kind, FactValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return kind switch
        {
            FactKind.Kwota => Amount(RequireNumber(kind, value)),
            FactKind.Procent => Percent(RequireNumber(kind, value)),
            FactKind.Termin => Days(decimal.ToInt32(RequireNumber(kind, value))),
            FactKind.Tekst => value.Text ?? throw Missing(kind, "tekst"),
            FactKind.Data => Date(value.Date ?? throw Missing(kind, "data")),
            _ => throw new ContentException($"Nieznany rodzaj faktu: {kind}."),
        };
    }

    private static decimal RequireNumber(FactKind kind, FactValue value) =>
        value.Number ?? throw Missing(kind, "liczba");

    private static ContentException Missing(FactKind kind, string what) =>
        new($"Fakt rodzaju {kind} nie ma wartości ({what}).");
}
