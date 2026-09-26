namespace EvaGest.Services;

/// <summary>Outcome of checking a PIN or a recovery code.</summary>
public enum AccessResult
{
    /// <summary>The secret matched.</summary>
    Accepted,

    /// <summary>The secret did not match; the attempt counts toward the lockout.</summary>
    Wrong,

    /// <summary>Too many wrong attempts in a row: nothing is checked until the lockout
    /// ends (<see cref="IOwnerAccessService.LockoutRemaining"/>).</summary>
    LockedOut
}

/// <summary>
/// Owner mode: whether the private pages (Sales, Till, Reports, Workers, Catalogue,
/// Settings) are open right now, and the PIN that opens them.
///
/// This keeps workers from seeing the shop's figures by accident; it is not protection
/// of the data itself — the SQLite file is on the same machine, unencrypted. There are
/// no users or roles: one PIN, one owner.
///
/// Registered as a singleton so the whole app shares one unlocked/locked state.
/// </summary>
public interface IOwnerAccessService
{
    /// <summary>True while owner mode is open.</summary>
    bool IsUnlocked { get; }

    /// <summary>Raised whenever <see cref="IsUnlocked"/> changes.</summary>
    event Action? Changed;

    /// <summary>How long until another attempt is allowed; zero when not locked out.</summary>
    TimeSpan LockoutRemaining { get; }

    /// <summary>False until the owner has created a PIN (new install, or the first start
    /// after this feature arrived).</summary>
    Task<bool> HasPin();

    /// <summary>Checks the PIN and opens owner mode if it matches.</summary>
    Task<AccessResult> Unlock(string pin);

    /// <summary>
    /// Checks the paper recovery code. On success owner mode stays closed, but
    /// <see cref="CreatePin"/> is allowed once, so the owner can choose a new PIN.
    /// </summary>
    Task<AccessResult> CheckRecoveryCode(string code);

    /// <summary>
    /// Stores a new PIN together with a brand-new recovery code, opens owner mode and
    /// returns the code so it can be shown once. Only allowed when no PIN exists yet or
    /// right after <see cref="CheckRecoveryCode"/> succeeded.
    /// </summary>
    Task<string> CreatePin(string pin);

    /// <summary>Replaces the PIN after checking the current one. The recovery code on
    /// paper stays valid.</summary>
    Task<AccessResult> ChangePin(string currentPin, string newPin);

    /// <summary>Closes owner mode.</summary>
    void Lock();

    /// <summary>Called on every keystroke or click, so the idle timer restarts.</summary>
    void RegisterActivity();

    /// <summary>Closes owner mode if nothing has happened for <c>IdleTimeout</c>.
    /// Polled from a timer in the shell.</summary>
    void LockIfIdle();
}
