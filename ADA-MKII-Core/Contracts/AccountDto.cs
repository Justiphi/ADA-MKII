namespace ADA_MKII_Core.Contracts;

/// <summary>
/// A user account. Never carries the password hash - this type is returned to
/// clients, and the hash has no business leaving the server or the DataManager.
/// </summary>
public sealed record AccountDto(
    Guid Id,
    string Username,
    string DisplayName,
    DateTimeOffset CreatedUtc,
    DateTimeOffset? DisabledUtc)
{
    public bool IsDisabled => DisabledUtc is not null;
}

/// <summary>Credentials presented at login.</summary>
public sealed record LoginRequest(string Username, string Password);

/// <summary>
/// A successful login. The token is shown exactly once - only its hash is stored -
/// and is what every subsequent request carries as a bearer credential.
/// </summary>
public sealed record LoginResponse(string Token, AccountDto Account);
