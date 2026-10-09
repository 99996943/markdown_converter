using System.Globalization;

namespace LegalAgent.Downloads;

/// <summary>Allowed hosts: a host matches an entry when it equals it or is its subdomain (research R3).</summary>
internal sealed class HostAllowList
{
    private static readonly IdnMapping Idn = new();
    private readonly string[] asciiHosts;

    /// <summary>Creates the list.</summary>
    /// <param name="hosts">Host names (Unicode or punycode).</param>
    public HostAllowList(IReadOnlyList<string> hosts)
    {
        Hosts = hosts;
        asciiHosts = [.. hosts.Select(Normalize)];
    }

    /// <summary>The configured entries, as given.</summary>
    public IReadOnlyList<string> Hosts { get; }

    /// <summary>Whether the host of the address is allowed.</summary>
    public bool IsAllowed(Uri address)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (address.HostNameType != UriHostNameType.Dns)
        {
            return false;
        }

        string host = Normalize(address.IdnHost);
        return asciiHosts.Any(allowed =>
            host.Equals(allowed, StringComparison.Ordinal)
            || host.EndsWith("." + allowed, StringComparison.Ordinal));
    }

    private static string Normalize(string host)
    {
        string trimmed = host.Trim().TrimEnd('.');
        return Idn.GetAscii(trimmed).ToLowerInvariant();
    }
}
