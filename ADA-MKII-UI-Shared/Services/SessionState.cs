using ADA_MKII_Core.Contracts;

namespace ADA_MKII_UI_Shared.Services;

/// <summary>
/// Who is signed in, for the current UI session. Components subscribe to
/// <see cref="Changed"/> so the shell can react to a login or logout without
/// every page polling for it.
/// </summary>
public sealed class SessionState
{
    private AccountDto? _account;

    public event Action? Changed;

    public AccountDto? Account
    {
        get => _account;
        private set
        {
            _account = value;
            Changed?.Invoke();
        }
    }

    public bool IsSignedIn => _account is not null;

    public void SignedIn(AccountDto account) => Account = account;

    public void SignedOut() => Account = null;
}
