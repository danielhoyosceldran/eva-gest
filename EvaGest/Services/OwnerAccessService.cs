using EvaGest.Models;
using Serilog;

namespace EvaGest.Services;

/// <summary>
/// Holds owner mode's state in memory and the PIN's hash in the settings table.
/// The clock and the hash's iteration count are injectable so the tests can move
/// time forward and stay fast; the app uses the defaults.
/// </summary>
public class OwnerAccessService(
    ISettingsService settings, Func<DateTime>? clock = null, int iterations = OwnerPin.DefaultIterations)
    : IOwnerAccessService
{
    /// <summary>Owner mode closes by itself after this long without a key or a click.</summary>
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(5);

    /// <summary>Wrong attempts in a row allowed before the lockout starts.</summary>
    public const int MaxAttempts = 5;

    public static readonly TimeSpan LockoutDuration = TimeSpan.FromSeconds(30);

    private readonly Func<DateTime> _now = clock ?? (() => DateTime.Now);

    private bool _isUnlocked;
    private int _failedAttempts;
    private DateTime _lockedOutUntil = DateTime.MinValue;
    private DateTime _lastActivity;

    /// <summary>Set by a correct recovery code; spent by the next <see cref="CreatePin"/>.</summary>
    private bool _recoveryAccepted;

    public event Action? Changed;

    public bool IsUnlocked
    {
        get => _isUnlocked;
        private set
        {
            if (_isUnlocked == value) return;
            _isUnlocked = value;
            Changed?.Invoke();
        }
    }

    public TimeSpan LockoutRemaining
    {
        get
        {
            var remaining = _lockedOutUntil - _now();
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }

    public async Task<bool> HasPin()
        => !string.IsNullOrEmpty(await settings.Get(ConfigKeys.OwnerPin));

    public async Task<AccessResult> Unlock(string pin)
    {
        var result = await Check(pin, ConfigKeys.OwnerPin, "PIN");
        if (result == AccessResult.Accepted)
        {
            _lastActivity = _now();
            IsUnlocked = true;
            Log.Information("Owner mode unlocked");
        }
        return result;
    }

    public async Task<AccessResult> CheckRecoveryCode(string code)
    {
        var result = await Check(OwnerPin.NormaliseRecoveryCode(code), ConfigKeys.OwnerRecoveryCode, "recovery code");
        if (result == AccessResult.Accepted)
        {
            _recoveryAccepted = true;
            Log.Information("Owner recovery code accepted");
        }
        return result;
    }

    public async Task<string> CreatePin(string pin)
    {
        // Guarded here and not only in the dialog: without it, anyone could replace an
        // existing PIN by opening the "create" path some other way.
        if (await HasPin() && !_recoveryAccepted)
            throw new InvalidOperationException("A PIN already exists; change it or recover it instead.");
        if (!OwnerPin.IsValid(pin))
            throw new ArgumentException("The PIN must be 4 to 6 digits.", nameof(pin));

        string code = OwnerPin.NewRecoveryCode();
        await settings.Save(ConfigKeys.OwnerPin, OwnerPin.Hash(pin, iterations));
        await settings.Save(ConfigKeys.OwnerRecoveryCode,
            OwnerPin.Hash(OwnerPin.NormaliseRecoveryCode(code), iterations));

        bool recovered = _recoveryAccepted;
        _recoveryAccepted = false;
        _lastActivity = _now();
        IsUnlocked = true;

        Log.Information(recovered ? "Owner PIN reset with the recovery code" : "Owner PIN created");
        return code;
    }

    public async Task<AccessResult> ChangePin(string currentPin, string newPin)
    {
        if (!OwnerPin.IsValid(newPin))
            throw new ArgumentException("The PIN must be 4 to 6 digits.", nameof(newPin));

        var result = await Check(currentPin, ConfigKeys.OwnerPin, "PIN");
        if (result != AccessResult.Accepted) return result;

        await settings.Save(ConfigKeys.OwnerPin, OwnerPin.Hash(newPin, iterations));
        Log.Information("Owner PIN changed");
        return result;
    }

    public void Lock()
    {
        if (!IsUnlocked) return;
        IsUnlocked = false;
        Log.Information("Owner mode locked");
    }

    public void RegisterActivity() => _lastActivity = _now();

    public void LockIfIdle()
    {
        if (IsUnlocked && _now() - _lastActivity >= IdleTimeout)
        {
            IsUnlocked = false;
            Log.Information("Owner mode locked after {Minutes} idle minutes", IdleTimeout.TotalMinutes);
        }
    }

    /// <summary>
    /// The one place a secret is compared, so the PIN and the recovery code share the
    /// same attempt counter: alternating between them cannot buy extra guesses.
    /// </summary>
    private async Task<AccessResult> Check(string secret, string key, string what)
    {
        if (LockoutRemaining > TimeSpan.Zero) return AccessResult.LockedOut;

        if (OwnerPin.Verify(secret, await settings.Get(key)))
        {
            _failedAttempts = 0;
            return AccessResult.Accepted;
        }

        _failedAttempts++;
        Log.Warning("Wrong owner {What} ({Attempts} in a row)", what, _failedAttempts);

        if (_failedAttempts >= MaxAttempts)
        {
            _failedAttempts = 0;
            _lockedOutUntil = _now() + LockoutDuration;
            Log.Warning("Owner access locked out for {Seconds} s", LockoutDuration.TotalSeconds);
            return AccessResult.LockedOut;
        }
        return AccessResult.Wrong;
    }
}
