namespace LegalAgent.Downloads;

/// <summary>Checks addresses entered by the user or read from configuration (FR-302).</summary>
public static class AddressValidator
{
    /// <summary>Checks one address against the options and the addresses accepted before it.</summary>
    /// <param name="input">Raw input; surrounding white space is ignored.</param>
    /// <param name="previous">Addresses already accepted (for duplicate detection).</param>
    /// <param name="options">Download options (allowed hosts, http).</param>
    /// <returns>The check result.</returns>
    public static AddressCheck Check(string? input, IReadOnlyCollection<Uri> previous, DownloadOptions options) =>
        throw new NotImplementedException();

    /// <summary>Checks a whole list in order; duplicates are detected against earlier positions.</summary>
    /// <param name="inputs">Raw addresses.</param>
    /// <param name="options">Download options.</param>
    /// <returns>One result per input, in order.</returns>
    public static IReadOnlyList<AddressCheck> CheckAll(IReadOnlyList<string> inputs, DownloadOptions options) =>
        throw new NotImplementedException();
}
