using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using ADA_MKII_Core.Contracts;

namespace ADA_MKII_DataManager;

/// <summary>A row in the accounts grid.</summary>
public sealed record AccountRow(AccountDto Account)
{
    public Guid Id => Account.Id;

    public string Username => Account.Username;

    public string DisplayName => Account.DisplayName;

    public DateTimeOffset CreatedUtc => Account.CreatedUtc;

    public string Status => Account.IsDisabled ? "Disabled" : "Active";
}

public partial class MainWindow : Window
{
    private readonly AdminService _admin;
    private readonly ObservableCollection<AccountRow> _accounts = [];
    private readonly ObservableCollection<ConversationSummary> _conversations = [];

    /// <summary>Guards against overlapping operations - see <see cref="RunAsync"/>.</summary>
    private bool _busy;

    public MainWindow(AdminService admin)
    {
        _admin = admin;
        InitializeComponent();

        AccountsGrid.ItemsSource = _accounts;
        ConversationsGrid.ItemsSource = _conversations;

        Loaded += OnLoadedAsync;
    }

    private AccountRow? Selected => AccountsGrid.SelectedItem as AccountRow;

    private async void OnLoadedAsync(object sender, RoutedEventArgs e)
    {
        // Prove the database is reachable and migrated before the operator starts
        // clicking things, rather than failing on the first action.
        await RunAsync(async ct =>
        {
            var state = await _admin.CheckConnectionAsync(ct);
            SetStatus(state);
            await ReloadAccountsAsync(ct);
        });
    }

    private async void OnRefresh(object sender, RoutedEventArgs e) =>
        await RunAsync(ReloadAccountsAsync);

    private async void OnAccountSelected(object sender, SelectionChangedEventArgs e) =>
        await RunAsync(RefreshSelectionAsync);

    private async void OnCreateAccount(object sender, RoutedEventArgs e)
    {
        var username = NewUsername.Text.Trim();
        var display = NewDisplayName.Text.Trim();
        var password = NewPassword.Password;

        if (username.Length == 0)
        {
            Warn("A username is required.");
            return;
        }

        if (password.Length < 12)
        {
            // A long minimum is cheap here: accounts are created by an operator,
            // not by users picking something memorable under time pressure.
            Warn("Use a password of at least 12 characters.");
            return;
        }

        if (password != NewPasswordConfirm.Password)
        {
            Warn("The passwords do not match.");
            return;
        }

        await RunAsync(async ct =>
        {
            var created = await _admin.CreateAccountAsync(username, display, password, ct);

            if (created is null)
            {
                Warn($"The username '{username}' is already taken.");
                return;
            }

            NewUsername.Clear();
            NewDisplayName.Clear();
            NewPassword.Clear();
            NewPasswordConfirm.Clear();

            await ReloadAccountsAsync(ct);
            SetStatus($"Created account '{created.Username}'.");
        });
    }

    private async void OnResetPassword(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } row)
        {
            Warn("Select an account first.");
            return;
        }

        var password = ResetPassword.Password;
        if (password.Length < 12)
        {
            Warn("Use a password of at least 12 characters.");
            return;
        }

        if (!Confirm($"Reset the password for '{row.Username}'?\n\nEvery token this account holds will be revoked."))
        {
            return;
        }

        await RunAsync(async ct =>
        {
            await _admin.SetPasswordAsync(row.Id, password, ct);
            ResetPassword.Clear();
            SetStatus($"Password reset for '{row.Username}'; its tokens were revoked.");
        });
    }

    private async void OnRename(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } row)
        {
            Warn("Select an account first.");
            return;
        }

        var name = RenameBox.Text.Trim();
        if (name.Length == 0)
        {
            Warn("A display name is required.");
            return;
        }

        await RunAsync(async ct =>
        {
            await _admin.RenameAsync(row.Id, name, ct);
            await ReloadAccountsAsync(ct);
            SetStatus($"Renamed '{row.Username}' to '{name}'.");
        });
    }

    private async void OnToggleDisabled(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } row)
        {
            Warn("Select an account first.");
            return;
        }

        var disable = !row.Account.IsDisabled;
        var verb = disable ? "Disable" : "Re-enable";

        if (!Confirm($"{verb} '{row.Username}'?"))
        {
            return;
        }

        await RunAsync(async ct =>
        {
            await _admin.SetDisabledAsync(row.Id, disable, ct);
            await ReloadAccountsAsync(ct);
            SetStatus($"{verb}d '{row.Username}'.");
        });
    }

    private async void OnRevokeTokens(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } row)
        {
            Warn("Select an account first.");
            return;
        }

        if (!Confirm($"Revoke every token for '{row.Username}'?\n\nAll its devices will have to log in again."))
        {
            return;
        }

        await RunAsync(async ct =>
        {
            var revoked = await _admin.RevokeAllTokensAsync(row.Id, ct);
            await RefreshSelectionAsync(ct);
            SetStatus($"Revoked {revoked} token(s) for '{row.Username}'.");
        });
    }

    private async void OnDeleteConversations(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } row)
        {
            Warn("Select an account first.");
            return;
        }

        if (!Confirm($"Delete every conversation belonging to '{row.Username}'?\n\nThis cannot be undone.", danger: true))
        {
            return;
        }

        await RunAsync(async ct =>
        {
            var deleted = await _admin.DeleteAllConversationsAsync(row.Id, ct);
            await RefreshSelectionAsync(ct);
            SetStatus($"Deleted {deleted} conversation(s) for '{row.Username}'.");
        });
    }

    private async void OnDeleteAccount(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } row)
        {
            Warn("Select an account first.");
            return;
        }

        if (!Confirm(
            $"Permanently delete '{row.Username}' and everything it owns?\n\n" +
            "Conversations, settings and tokens all go with it. Disabling is usually the better choice.",
            danger: true))
        {
            return;
        }

        await RunAsync(async ct =>
        {
            await _admin.DeleteAccountAsync(row.Id, ct);
            await ReloadAccountsAsync(ct);
            SetStatus($"Deleted account '{row.Username}'.");
        });
    }

    private async Task ReloadAccountsAsync(CancellationToken cancellationToken)
    {
        var selectedId = Selected?.Id;

        var accounts = await _admin.ListAccountsAsync(cancellationToken);

        _accounts.Clear();
        foreach (var account in accounts)
        {
            _accounts.Add(new AccountRow(account));
        }

        // Keep the operator's place across a refresh.
        if (selectedId is { } id)
        {
            AccountsGrid.SelectedItem = _accounts.FirstOrDefault(a => a.Id == id);
        }

        await RefreshSelectionAsync(cancellationToken);
    }

    private async Task RefreshSelectionAsync(CancellationToken cancellationToken)
    {
        _conversations.Clear();

        if (Selected is not { } row)
        {
            SelectedSummary.Text = "No account selected.";
            RenameBox.Text = string.Empty;
            DisableButton.Content = "Disable";
            return;
        }

        RenameBox.Text = row.DisplayName;
        DisableButton.Content = row.Account.IsDisabled ? "Re-enable" : "Disable";

        var usage = await _admin.GetUsageAsync(row.Id, cancellationToken);

        var last = usage.LastActivityUtc is { } when
            ? when.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC"
            : "never";

        SelectedSummary.Text =
            $"{row.DisplayName} ({row.Username}) — {row.Status}\n" +
            $"{usage.Conversations} conversation(s), {usage.Messages} message(s), {usage.Tokens:N0} tokens\n" +
            $"{usage.ActiveTokens} active device token(s); last activity {last}";

        foreach (var conversation in await _admin.ListConversationsAsync(row.Id, cancellationToken))
        {
            _conversations.Add(conversation);
        }
    }

    /// <summary>
    /// Runs an operation with the busy cursor shown and any failure surfaced.
    /// Every action here touches a remote database, so "it silently did nothing"
    /// is the outcome to design against.
    ///
    /// Re-entrancy is rejected rather than queued. Rebuilding the accounts grid
    /// raises SelectionChanged, which would otherwise start a second operation
    /// while the first is mid-flight and let the two fight over the busy state.
    /// </summary>
    private async Task RunAsync(Func<CancellationToken, Task> operation)
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

        string? failure = null;

        try
        {
            await operation(CancellationToken.None);
        }
#pragma warning disable CA1031 // An operator tool must report failures, not crash on them.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            failure = ex.Message;
            SetStatus("Failed: " + failure);
        }
        finally
        {
            System.Windows.Input.Mouse.OverrideCursor = null;
            _busy = false;
        }

        // Shown only after the busy state is cleared. A modal dialog raised while
        // its owner is busy or disabled is the classic way a WPF app appears to
        // hang: input is blocked and the dialog can sit behind the main window.
        if (failure is not null)
        {
            ShowDialog(failure, MessageBoxImage.Error);
        }
    }

    private void SetStatus(string message) => StatusText.Text = message;

    private void Warn(string message)
    {
        SetStatus(message);
        ShowDialog(message, MessageBoxImage.Warning);
    }

    /// <summary>
    /// Always passes an owner. Without one, WPF picks the active window - and if
    /// that is not this one, the dialog opens behind it and the app looks frozen.
    /// </summary>
    private void ShowDialog(string message, MessageBoxImage icon)
    {
        Activate();
        MessageBox.Show(this, message, "ADA DataManager", MessageBoxButton.OK, icon);
    }

    private bool Confirm(string message, bool danger = false)
    {
        Activate();

        return MessageBox.Show(
            this,
            message,
            "ADA DataManager",
            MessageBoxButton.YesNo,
            danger ? MessageBoxImage.Warning : MessageBoxImage.Question,
            MessageBoxResult.No) == MessageBoxResult.Yes;
    }
}
