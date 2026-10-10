using System.Globalization;

namespace MBank.FaqGenerator;

/// <summary>Numbers and plural forms for Polish messages.</summary>
internal static class PolishText
{
    /// <summary>The pl-PL culture used for every number shown to the user.</summary>
    public static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("pl-PL");

    /// <summary>The number with digit grouping (pl-PL).</summary>
    public static string Number(long value) => value.ToString("N0", Culture);

    /// <summary>„1 strona”, „2 strony”, „5 stron”: the number followed by the form that agrees with it.</summary>
    public static string Count(long value, string one, string few, string many) => Number(value) + " " + Form(value, one, few, many);

    /// <summary>The form that agrees with the number.</summary>
    public static string Form(long value, string one, string few, string many)
    {
        if (value == 1)
        {
            return one;
        }

        long lastDigit = value % 10;
        long lastTwo = value % 100;
        return lastDigit is >= 2 and <= 4 && lastTwo is < 12 or > 14 ? few : many;
    }
}
