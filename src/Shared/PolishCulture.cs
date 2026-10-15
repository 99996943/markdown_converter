using System.Globalization;

namespace LegalAgent.Globalization;

/// <summary>The pl-PL culture for numbers in user messages (linked into each project that needs it).</summary>
internal static class PolishCulture
{
    /// <summary>pl-PL from the system, or its number format when the system has no culture data.</summary>
    public static readonly CultureInfo Culture = Create(CultureInfo.GetCultureInfo);

    /// <summary>
    /// pl-PL found by <paramref name="lookup"/>. Without ICU (globalization-invariant mode, e.g. Linux without libicu)
    /// pl-PL does not exist; the invariant culture with the pl-PL number separators formats numbers the same way.
    /// </summary>
    internal static CultureInfo Create(Func<string, CultureInfo> lookup)
    {
        try
        {
            return lookup("pl-PL");
        }
        catch (CultureNotFoundException)
        {
            var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            culture.NumberFormat.NumberDecimalSeparator = ",";
            culture.NumberFormat.NumberGroupSeparator = " ";
            return CultureInfo.ReadOnly(culture);
        }
    }
}
