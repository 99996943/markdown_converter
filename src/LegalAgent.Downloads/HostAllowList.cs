namespace LegalAgent.Downloads;

/// <summary>Allowed hosts: a host matches an entry when it equals it or is its subdomain (research R3).</summary>
internal sealed class HostAllowList(IReadOnlyList<string> hosts)
{
    /// <summary>The configured entries, as given.</summary>
    public IReadOnlyList<string> Hosts { get; } = hosts;

    /// <summary>Whether the host of the address is allowed.</summary>
    public bool IsAllowed(Uri address) => throw new NotImplementedException();
}
