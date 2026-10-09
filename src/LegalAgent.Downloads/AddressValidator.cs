namespace LegalAgent.Downloads;

/// <summary>Checks addresses entered by the user or read from configuration (FR-302).</summary>
public static class AddressValidator
{
    /// <summary>Checks one address against the options and the addresses accepted before it.</summary>
    /// <param name="input">Raw input; surrounding white space is ignored.</param>
    /// <param name="previous">Addresses already accepted (for duplicate detection).</param>
    /// <param name="options">Download options (allowed hosts, http).</param>
    /// <returns>The check result.</returns>
    public static AddressCheck Check(string? input, IReadOnlyCollection<Uri> previous, DownloadOptions options)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(options);
        return Check(input, previous, options, new HostAllowList(options.AllowedHosts));
    }

    /// <summary>Checks a whole list in order; duplicates are detected against earlier positions.</summary>
    /// <param name="inputs">Raw addresses.</param>
    /// <param name="options">Download options.</param>
    /// <returns>One result per input, in order.</returns>
    public static IReadOnlyList<AddressCheck> CheckAll(IReadOnlyList<string> inputs, DownloadOptions options)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(options);
        var hosts = new HostAllowList(options.AllowedHosts);
        var accepted = new List<Uri>();
        var checks = new List<AddressCheck>(inputs.Count);
        foreach (string input in inputs)
        {
            AddressCheck check = Check(input, accepted, options, hosts);
            if (check.Address is not null)
            {
                accepted.Add(check.Address);
            }

            checks.Add(check);
        }

        return checks;
    }

    /// <summary>Whether the scheme of the address is allowed by the options.</summary>
    internal static bool IsSchemeAllowed(Uri address, DownloadOptions options) =>
        address.Scheme == Uri.UriSchemeHttps || (options.AllowHttp && address.Scheme == Uri.UriSchemeHttp);

    /// <summary>Message for a scheme that is not allowed.</summary>
    internal static string SchemeMessage(DownloadOptions options) =>
        options.AllowHttp
            ? "dozwolone są tylko adresy https:// lub http://."
            : "dozwolone są tylko adresy https://.";

    /// <summary>Message for a host that is not allowed.</summary>
    internal static string HostMessage(string host, HostAllowList hosts) =>
        $"host {host} nie jest na liście dozwolonych ({string.Join(", ", hosts.Hosts)}).";

    private static AddressCheck Check(string? input, IReadOnlyCollection<Uri> previous, DownloadOptions options, HostAllowList hosts)
    {
        string trimmed = input?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return AddressCheck.Invalid(AddressError.Empty, "adres jest pusty.");
        }

        // On Unix "/a.pdf" parses as an absolute file URI; only an explicit file: scheme counts as absolute.
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? address)
            || (address.IsFile && !trimmed.StartsWith("file:", StringComparison.OrdinalIgnoreCase)))
        {
            return AddressCheck.Invalid(AddressError.NotAbsolute, "to nie jest pełny adres URL (oczekiwano https://…).");
        }

        if (!IsSchemeAllowed(address, options))
        {
            return AddressCheck.Invalid(AddressError.SchemeNotAllowed, SchemeMessage(options));
        }

        if (address.UserInfo.Length > 0)
        {
            return AddressCheck.Invalid(AddressError.HasUserInfo, "adres nie może zawierać nazwy użytkownika ani hasła.");
        }

        if (!hosts.IsAllowed(address))
        {
            return AddressCheck.Invalid(AddressError.HostNotAllowed, HostMessage(address.Host, hosts));
        }

        // Uri.Equals ignores the fragment and the case of the host.
        if (previous.Any(p => p.Equals(address)))
        {
            return AddressCheck.Invalid(AddressError.Duplicate, "ten adres został już podany (powtórzony adres).");
        }

        return AddressCheck.Valid(address);
    }
}
