using System.Globalization;

namespace LegalAgent.Globalization;

/// <summary>The pl-PL culture for numbers in user messages (linked into each project that needs it).</summary>
internal static class PolishCulture
{
    /// <summary>pl-PL from the system.</summary>
    public static readonly CultureInfo Culture = Create(CultureInfo.GetCultureInfo);

    /// <summary>pl-PL found by <paramref name="lookup"/>.</summary>
    internal static CultureInfo Create(Func<string, CultureInfo> lookup) => lookup("pl-PL");
}
