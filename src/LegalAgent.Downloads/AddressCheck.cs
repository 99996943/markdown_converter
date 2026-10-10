namespace LegalAgent.Downloads;

/// <summary>Reason an address was rejected.</summary>
public enum AddressError
{
    /// <summary>Empty input.</summary>
    Empty,

    /// <summary>Not an absolute URL.</summary>
    NotAbsolute,

    /// <summary>Scheme other than https (or http when allowed).</summary>
    SchemeNotAllowed,

    /// <summary>Host not on the allow list.</summary>
    HostNotAllowed,

    /// <summary>The address contains user credentials (<c>user@host</c>).</summary>
    HasUserInfo,

    /// <summary>The address repeats an address given earlier.</summary>
    Duplicate,
}

/// <summary>Result of checking one address.</summary>
/// <param name="IsValid">Whether the address is accepted.</param>
/// <param name="Address">The trimmed address, when valid.</param>
/// <param name="Error">The reason, when invalid.</param>
/// <param name="Message">User-facing message (Polish), when invalid.</param>
public sealed record AddressCheck(bool IsValid, Uri? Address, AddressError? Error, string? Message)
{
    internal static AddressCheck Valid(Uri address) => new(true, address, null, null);

    internal static AddressCheck Invalid(AddressError error, string message) => new(false, null, error, message);
}
