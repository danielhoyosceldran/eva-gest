using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Helpers;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.ViewModels.Dialogs;
using EvaGest.ViewModels.Elements;
using EvaGest.Resources;

namespace EvaGest.ViewModels.Pages;

/// <summary>
/// Configuració (pantalles 2.9). Everything the user can change lives in the database
/// (RF-23), so this page is the only place those keys are written from.
///
/// Flags and free-text fields save as they are edited; the opening hours and the VAT
/// mode do not, because both need validating or confirming first.
/// </summary>
public partial class SettingsViewModel(
    IBackupService backup, IExportService export, ISettingsService settings,
    IAvailabilityService availability, IDialogService dialogs)
    : PageViewModelBase
{
    public override string Title => Texts.NavSettings;

    /// <summary>Guards the initial assignments during Load from writing straight back.</summary>
    private bool _loaded;

    // ── Shop details ─────────────────────────────────────────────────────────
    [ObservableProperty] private string _shopName = string.Empty;
    [ObservableProperty] private string _shopAddress = string.Empty;
    [ObservableProperty] private string _shopPhone = string.Empty;
    [ObservableProperty] private string? _shopConfirmation;
    [ObservableProperty] private string? _errorShop;

    // ── VAT ──────────────────────────────────────────────────────────────────
    [ObservableProperty] private string _defaultVatText = "21";
    [ObservableProperty] private string? _vatError;
    [ObservableProperty] private string? _vatConfirmation;
    [ObservableProperty] private bool _applyVatToTill;

    public IReadOnlyList<VatMode> ModesVat { get; } = [VatMode.Included, VatMode.NotIncluded];

    [ObservableProperty] private VatMode _modeVat = VatMode.Included;

    /// <summary>The value actually stored, so a declined change can be put back.</summary>
    private VatMode _modeVatSaved = VatMode.Included;

    public string VatModeExplanation => ModeVat == VatMode.Included
        ? Texts.VatIncludedExplanation
        : Texts.VatExcludedExplanation;

    // ── Agenda ───────────────────────────────────────────────────────────────
    /// <summary>Row granularity of the agenda grid. Only 15/30/60 are offered, because
    /// anything else would not divide the hour cleanly.</summary>
    public IReadOnlyList<int> SlotMinutesOptions { get; } = [15, 30, 60];

    [ObservableProperty] private int _slotMinutes = GridHelper.DefaultSlotMinutes;
    [ObservableProperty] private string _defaultAppointmentDurationText = "30";
    [ObservableProperty] private string? _errorAgenda;

    // ── Opening hours ────────────────────────────────────────────────────────
    /// <summary>The seven rows of the opening-hours form, always in weekday order.</summary>
    public ObservableCollection<DayScheduleViewModel> ScheduleDays { get; } =
        [.. Enum.GetValues<Weekday>().Select(d => new DayScheduleViewModel { Day = d })];

    [ObservableProperty] private string? _scheduleError;
    [ObservableProperty] private string? _scheduleConfirmation;

    // ── Days closed ──────────────────────────────────────────────────────────
    public ObservableCollection<ClosedDay> ClosedDays { get; } = [];

    [ObservableProperty] private DateOnly _newClosedDate = DateOnly.FromDateTime(DateTime.Today);
    [ObservableProperty] private string _newClosedReason = string.Empty;

    public bool HasNoClosedDays => ClosedDays.Count == 0;

    // ── Backups ──────────────────────────────────────────────────────────────
    [ObservableProperty] private string _lastBackupText = string.Empty;
    [ObservableProperty] private string _backupTimeText = "20:00";
    [ObservableProperty] private string _backupsToKeepText = "15";
    [ObservableProperty] private string? _errorBackup;

    // ── Notices and sounds ───────────────────────────────────────────────────
    [ObservableProperty] private bool _showGuestNotice = true;
    [ObservableProperty] private bool _confirmationSound = true;

    // ── Language ─────────────────────────────────────────────────────────────
    public IReadOnlyList<Language> Languages { get; } = [Language.Catalan, Language.Spanish];

    [ObservableProperty] private Language _language = AppLanguage.Current;
    [ObservableProperty] private string? _languageConfirmation;

    public async Task Load()
    {
        Loading = true;
        _loaded = false;
        try
        {
            ShopName = await settings.Get(ConfigKeys.ShopName) ?? string.Empty;
            ShopAddress = await settings.Get(ConfigKeys.ShopAddress) ?? string.Empty;
            ShopPhone = await settings.Get(ConfigKeys.ShopPhone) ?? string.Empty;

            DefaultVatText = Percentages.FormatWithoutUnit(
                await settings.GetInt(ConfigKeys.DefaultVatBp, 2100));
            _modeVatSaved = Enum.TryParse<VatMode>(
                await settings.Get(ConfigKeys.CurrentVatMode), out var mode) ? mode : VatMode.Included;
            ModeVat = _modeVatSaved;
            ApplyVatToTill = await settings.GetBool(ConfigKeys.ApplyVatToTill, false);

            SlotMinutes = GridHelper.IsValidSlotMinutes(
                await settings.GetInt(ConfigKeys.AgendaSlotMinutes, GridHelper.DefaultSlotMinutes));
            DefaultAppointmentDurationText =
                (await settings.GetInt(ConfigKeys.DefaultAppointmentDurationMin, 30)).ToString();

            BackupTimeText = await settings.Get(ConfigKeys.BackupTime) ?? "20:00";
            BackupsToKeepText =
                (await settings.GetInt(ConfigKeys.BackupsToKeep, 15)).ToString();

            ShowGuestNotice = await settings.GetBool(ConfigKeys.ShowGuestNotice, true);
            ConfirmationSound = await settings.GetBool(ConfigKeys.ConfirmationSound, true);

            Language = AppLanguage.Parse(await settings.Get(ConfigKeys.Language));

            var backups = await backup.ListAll();
            var last = backups.OrderByDescending(c => c.Date).FirstOrDefault();
            LastBackupText = last is null
                ? Texts.NoBackupYet
                : string.Format(Texts.LastBackupLine,
                                last.Date.ToString("dd/MM/yyyy HH:mm"),
                                last.IsAutomatic ? Texts.BackupAutomatic : Texts.BackupManual);

            var schedule = await availability.WeeklyIntervals();
            foreach (var day in ScheduleDays) day.Fill(schedule.GetValueOrDefault(day.Day, []));

            await LoadClosedDays();

            ScheduleError = null;
            ScheduleConfirmation = null;
            VatError = null;
            VatConfirmation = null;
            ErrorAgenda = null;
            ErrorBackup = null;
            ShopConfirmation = null;
            ErrorShop = null;
        }
        finally
        {
            _loaded = true;
            Loading = false;
        }
    }

    // ── Shop details ─────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task SaveShopDetails()
    {
        if (!string.IsNullOrWhiteSpace(ShopPhone) && !ContactValidator.IsValidPhone(ShopPhone))
        {
            ErrorShop = Texts.PhoneInvalid;
            return;
        }

        ErrorShop = null;
        await settings.Save(ConfigKeys.ShopName, ShopName.Trim());
        await settings.Save(ConfigKeys.ShopAddress, ShopAddress.Trim());
        await settings.Save(ConfigKeys.ShopPhone, ShopPhone.Trim());
        ShopConfirmation = Texts.DataSaved;
    }

    // ── VAT ──────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task SaveDefaultVat()
    {
        if (!Percentages.TryParse(DefaultVatText, out int bp))
        {
            VatError = Texts.VatPercentOutOfRange;
            return;
        }

        VatError = null;
        await settings.Save(ConfigKeys.DefaultVatBp, bp.ToString());
        VatConfirmation = string.Format(Texts.DefaultVatSaved, Percentages.Format(bp));
    }

    /// <summary>
    /// Changing the mode reinterprets every catalogue price, so it is the one setting
    /// that asks before saving (CU-10). Declining puts the picker back rather than
    /// leaving it showing a value that was never stored.
    /// </summary>
    partial void OnModeVatChanged(VatMode value)
    {
        OnPropertyChanged(nameof(VatModeExplanation));
        if (!_loaded || value == _modeVatSaved) return;

        _ = ConfirmVatModeChange(value);
    }

    private async Task ConfirmVatModeChange(VatMode newValue)
    {
        string heading = newValue == VatMode.NotIncluded
            ? Texts.VatModeSwitchToExcluded
            : Texts.VatModeSwitchToIncluded;

        bool confirmed = await dialogs.Confirm(
            Texts.ConfirmVatModeTitle,
            string.Format(Texts.ConfirmVatModeMessage, heading),
            Texts.UnderstoodChange);

        if (!confirmed)
        {
            _loaded = false;
            ModeVat = _modeVatSaved;
            _loaded = true;
            return;
        }

        _modeVatSaved = newValue;
        await settings.Save(ConfigKeys.CurrentVatMode, newValue.ToString());
        VatConfirmation = Texts.VatModeSaved;
    }

    partial void OnApplyVatToTillChanged(bool value)
    {
        if (!_loaded) return;
        _ = settings.SaveBool(ConfigKeys.ApplyVatToTill, value);
    }

    // ── Agenda ───────────────────────────────────────────────────────────────

    partial void OnSlotMinutesChanged(int value)
    {
        if (!_loaded) return;
        _ = settings.Save(ConfigKeys.AgendaSlotMinutes,
            GridHelper.IsValidSlotMinutes(value).ToString());
    }

    [RelayCommand]
    private async Task SaveDefaultDuration()
    {
        if (!NumberValidator.TryParseAtLeast(DefaultAppointmentDurationText, 1, out int minutes))
        {
            ErrorAgenda = Texts.DefaultDurationInvalid;
            return;
        }

        ErrorAgenda = null;
        await settings.Save(ConfigKeys.DefaultAppointmentDurationMin, minutes.ToString());
    }

    // ── Opening hours ────────────────────────────────────────────────────────

    /// <summary>Backups Monday onto Tuesday-Friday, which is how most weeks actually look.</summary>
    [RelayCommand]
    private void ApplyMondayToRest()
    {
        var monday = ScheduleDays[0];
        foreach (var day in ScheduleDays.Skip(1).Take(4)) monday.CopyTo(day);
        ScheduleConfirmation = null;
    }

    [RelayCommand]
    private async Task SaveSchedule()
    {
        ScheduleConfirmation = null;

        // Validate every row first, so all the bad ones light up at once rather than
        // one per attempt.
        var perDay = new Dictionary<Weekday, List<(TimeOnly start, TimeOnly fi)>>();
        bool hasErrors = false;

        foreach (var day in ScheduleDays)
        {
            var result = day.Check();
            if (!result.IsValid) { hasErrors = true; continue; }
            if (result.Intervals.Count > 0) perDay[day.Day] = result.Intervals;
        }

        if (hasErrors)
        {
            ScheduleError = Texts.CheckRedDays;
            return;
        }

        ScheduleError = null;
        await availability.SaveWeeklySchedule(perDay);
        ScheduleConfirmation = perDay.Count == 0
            ? Texts.ScheduleSavedAllClosed
            : Texts.ScheduleSaved;
    }

    // ── Days closed ──────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task AddClosedDay()
    {
        await availability.AddClosedDay(NewClosedDate, NewClosedReason);
        NewClosedReason = string.Empty;
        await LoadClosedDays();
    }

    [RelayCommand]
    private async Task RemoveClosedDay(ClosedDay day)
    {
        await availability.DeleteClosedDay(day.Id);
        await LoadClosedDays();
    }

    private async Task LoadClosedDays()
    {
        ClosedDays.Clear();
        foreach (var d in await availability.ClosedDays()) ClosedDays.Add(d);
        OnPropertyChanged(nameof(HasNoClosedDays));
    }

    // ── Backups ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task SaveBackupOptions()
    {
        if (!TimeValidator.IsValidTime(BackupTimeText, out var time))
        {
            ErrorBackup = Texts.BackupHourInvalid;
            return;
        }
        if (!NumberValidator.TryParseAtLeast(BackupsToKeepText, 1, out int howmany))
        {
            ErrorBackup = Texts.KeepAtLeastOneBackup;
            return;
        }

        ErrorBackup = null;
        await settings.Save(ConfigKeys.BackupTime, time.ToString("HH\\:mm", CultureInfo.InvariantCulture));
        await settings.Save(ConfigKeys.BackupsToKeep, howmany.ToString());

        // Lowering the number has to take effect now, not at the next backup, or the
        // extra rows sit there until something else happens to trigger a cleanup.
        await backup.DeleteOldBackups();
        await Load();
    }

    [RelayCommand]
    private async Task BackupNow()
    {
        var backupFile = await backup.MakeManualBackup();
        await dialogs.Inform(Texts.BackupDoneTitle,
            string.Format(Texts.BackupDoneMessage,
                          backupFile.Date.ToString("dd/MM/yyyy HH:mm")));
        await Load();
    }

    [RelayCommand]
    private async Task RestoreBackup()
    {
        var vm = new RestoreDialogViewModel(backup, dialogs);
        await vm.Load();
        if (!await dialogs.ShowDialog(vm)) return;

        await dialogs.Inform(Texts.BackupRestoredTitle, Texts.BackupRestoredReopen);
        await Load();
    }

    [RelayCommand]
    private async Task ExportData()
    {
        var vm = new ExportDialogViewModel(export, dialogs);
        await dialogs.ShowDialog(vm);
    }

    // ── Notices and sounds ───────────────────────────────────────────────────

    partial void OnShowGuestNoticeChanged(bool value)
    {
        if (!_loaded) return;
        _ = settings.SaveBool(ConfigKeys.ShowGuestNotice, value);
    }

    partial void OnConfirmationSoundChanged(bool value)
    {
        if (!_loaded) return;
        _ = settings.SaveBool(ConfigKeys.ConfirmationSound, value);
    }

    // ── Language ─────────────────────────────────────────────────────────────

    /// <summary>Saved straight away, but only read at startup: everything already on
    /// screen keeps the wording it was built with, so the page says so instead of
    /// half-translating itself.</summary>
    partial void OnLanguageChanged(Language value)
    {
        if (!_loaded) return;
        _ = settings.Save(ConfigKeys.Language, value.ToString());
        LanguageConfirmation = value == AppLanguage.Current ? null : Texts.LanguageSaved;
    }
}
