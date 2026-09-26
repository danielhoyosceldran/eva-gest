using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Helpers;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Resources;

namespace EvaGest.ViewModels.Elements;

/// <summary>Full-size on the Agenda page, compact when embedded in the appointment dialog.</summary>
public enum ModeGrid { Agenda, Selector }

/// <summary>
/// The time grid, shared by the Agenda page and the appointment dialog. The only
/// behavioural difference between the two hosts is the pair of callbacks passed in,
/// so the layout, the lane packing and the loading pipeline exist exactly once.
///
/// It shows either three days starting on any date (today and the next two, by default)
/// or a whole Monday-to-Sunday week. The choice is one shared setting
/// (<see cref="ConfigKeys.AgendaDays"/>), read on every load, so flipping it in the
/// dialog's picker also changes the Agenda page and the other way round.
///
/// Nothing here may touch Dispatcher, Brush or Application.Current: the tests build
/// this off the UI thread. The now-line timer lives in the view's code-behind.
/// </summary>
public partial class WeekGridViewModel : ObservableObject
{
    private readonly IAppointmentService _appointments;
    private readonly IAvailabilityService _availability;
    private readonly ISettingsService _settings;
    private readonly Action<DateOnly, TimeOnly> _onSlotClick;
    private readonly Action<Appointment>? _onAppointmentClick;
    private readonly Action<DateOnly>? _onDaySelected;
    private readonly Func<DateOnly, Task>? _navigate;

    private GridAppointmentViewModel? _ghost;
    private DateOnly? _dateGhost;

    public WeekGridViewModel(
        IAppointmentService appointments, IAvailabilityService availability, ISettingsService settings,
        ModeGrid mode,
        Action<DateOnly, TimeOnly> onSlotClick, Action<Appointment>? onAppointmentClick = null,
        Action<DateOnly>? onDaySelected = null, Func<DateOnly, Task>? navigate = null)
    {
        _appointments = appointments;
        _availability = availability;
        _settings = settings;
        _onSlotClick = onSlotClick;
        _onAppointmentClick = onAppointmentClick;
        _onDaySelected = onDaySelected;
        _navigate = navigate;

        Mode = mode;
        SlotHeightPx = mode is ModeGrid.Agenda ? 44 : 26;
        RulerWidthPx = mode is ModeGrid.Agenda ? 64 : 44;
        MinAppointmentHeightPx = mode is ModeGrid.Agenda ? 22 : 14;

        // Columns are created for the default view; LoadRange adds or drops them if the
        // stored setting says otherwise.
        ResizeDays(ThreeDays);
    }

    /// <summary>The two views the toggle switches between.</summary>
    public const int ThreeDays = 3;
    public const int WholeWeek = 7;

    /// <summary>Anything stored other than 7 means the three-day view, so a missing or
    /// hand-edited setting can never leave the grid with an odd number of columns.</summary>
    public static int ValidDayCount(int stored) => stored == WholeWeek ? WholeWeek : ThreeDays;

    public ModeGrid Mode { get; }
    public double SlotHeightPx { get; }
    public double RulerWidthPx { get; }
    public double MinAppointmentHeightPx { get; }

    /// <summary>First day shown: any date in the three-day view, always a Monday in the week view.</summary>
    [ObservableProperty] private DateOnly _rangeStart;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StepBackTooltip), nameof(StepForwardTooltip), nameof(ToggleViewText))]
    private bool _isThreeDayView = true;
    [ObservableProperty] private string _rangeText = string.Empty;
    [ObservableProperty] private bool _loading;
    [ObservableProperty] private int _slotMinutes = GridHelper.DefaultSlotMinutes;
    [ObservableProperty] private int _gridStartMinute = GridHelper.DefaultRange.start;
    [ObservableProperty] private int _gridEndMinute = GridHelper.DefaultRange.fi;
    [ObservableProperty] private double _pixelsPerMinute;
    [ObservableProperty] private double _totalHeightPx;
    [ObservableProperty] private double _hourHeightPx;
    [ObservableProperty] private double _initialOffsetPx;
    [ObservableProperty] private double _nowTop;
    [ObservableProperty] private bool _nowVisible;
    [ObservableProperty] private int? _selectedAppointmentId;

    public ObservableCollection<GridDayViewModel> Days { get; } = [];
    public ObservableCollection<RulerHourViewModel> Hours { get; } = [];

    /// <summary>First opening minute of the days shown, used for the initial scroll position.</summary>
    private int _firstOpenMinute = GridHelper.DefaultRange.start;

    /// <summary>First opening minute per weekday, for "book on this day" defaults.</summary>
    private Dictionary<Weekday, int> _openings = [];

    [RelayCommand]
    private void AppointmentClick(GridAppointmentViewModel? block)
    {
        if (block?.Model is { } appointment) _onAppointmentClick?.Invoke(appointment);
    }

    // Navigation lives on the grid itself so both hosts get it: the Agenda toolbar and
    // the picker embedded in the appointment dialog, which otherwise had no way to leave
    // the days on screen. The single arrows move one day in the three-day view (one week
    // in the week view); the double arrows, shown only in the three-day view, jump three.
    [RelayCommand] private Task StepBack() => Go(RangeStart.AddDays(IsThreeDayView ? -1 : -WholeWeek));
    [RelayCommand] private Task StepForward() => Go(RangeStart.AddDays(IsThreeDayView ? 1 : WholeWeek));
    [RelayCommand] private Task JumpBack() => Go(RangeStart.AddDays(-ThreeDays));
    [RelayCommand] private Task JumpForward() => Go(RangeStart.AddDays(ThreeDays));
    [RelayCommand] private Task GoToToday() => Go(DateOnly.FromDateTime(DateTime.Today));

    public string StepBackTooltip => IsThreeDayView ? Texts.PreviousDay : Texts.WeekBack;
    public string StepForwardTooltip => IsThreeDayView ? Texts.NextDay : Texts.WeekForward;

    /// <summary>The toggle names the view it switches to, not the one on screen.</summary>
    public string ToggleViewText => IsThreeDayView ? Texts.ShowWeekView : Texts.ShowThreeDayView;

    /// <summary>
    /// Flips between three days and the whole week and remembers the choice. Going to
    /// three days keeps today on screen when the week shown contains it (the usual case),
    /// otherwise it starts on the first day that was shown; going to the week snaps back
    /// to that day's Monday inside <see cref="LoadRange"/>.
    /// </summary>
    [RelayCommand]
    private async Task ToggleView()
    {
        bool toThreeDays = !IsThreeDayView;
        await _settings.Save(ConfigKeys.AgendaDays, (toThreeDays ? ThreeDays : WholeWeek).ToString());

        var today = DateOnly.FromDateTime(DateTime.Today);
        var start = toThreeDays && Days.Any(d => d.Date == today) ? today : RangeStart;
        await Go(start);
    }

    /// <summary>A host that must do more than redraw the grid (the Agenda keeps its
    /// worker filter and its day detail in step) takes navigation over; the dialog's
    /// picker lets the grid load itself.</summary>
    private Task Go(DateOnly start) => _navigate is { } navigate ? navigate(start) : LoadRange(start);

    /// <summary>The whole day header books on that day, at its first opening slot.</summary>
    [RelayCommand]
    /// <summary>
    /// The day header opens that day's detail when the host offers one (pantalles 2.2).
    /// Without a handler — the appointment dialog's picker — it keeps its older meaning of
    /// "book at this day's first open slot", which is the only useful action there.
    /// </summary>
    private void HeaderClick(GridDayViewModel? day)
    {
        if (day is null) return;

        if (_onDaySelected is { } select) select(day.Date);
        else ActivateSlot(day.Date, GridHelper.ATime(FirstSlotOf(day.Date)));
    }

    /// <summary>Entry point for a click on empty grid space, from GridDayViewModel.</summary>
    public void ActivateSlot(DateOnly date, TimeOnly time) => _onSlotClick(date, time);

    /// <summary>First bookable slot of a day: its opening time, or the top of the grid
    /// when the day has no schedule. Used by the header and by the toolbar button, so a
    /// new appointment never defaults to an arbitrary fixed hour.</summary>
    public int FirstSlotOf(DateOnly date)
    {
        int minute = _openings.GetValueOrDefault(WeekHelper.ToWeekday(date), _firstOpenMinute);
        return Math.Max(minute, GridStartMinute);
    }

    // ---------- Keyboard navigation ----------

    private int _focusedDay = -1;
    private int _focusedMinute;

    /// <summary>Arrow keys walk the grid; deltaDays moves sideways, deltaSlots up/down.</summary>
    public void MoveFocus(int deltaDays, int deltaSlots)
    {
        if (_focusedDay < 0)
        {
            _focusedDay = Math.Max(0, Days.ToList().FindIndex(d => d.IsToday));
            _focusedMinute = FirstSlotOf(Days[_focusedDay].Date);
        }
        else
        {
            _focusedDay = Math.Clamp(_focusedDay + deltaDays, 0, Days.Count - 1);
            _focusedMinute = Math.Clamp(_focusedMinute + deltaSlots * SlotMinutes,
                                       GridStartMinute, Math.Max(GridStartMinute, GridEndMinute - SlotMinutes));
        }
        PaintFocus();
    }

    public void GoToFirstSlot()
    {
        if (_focusedDay < 0) _focusedDay = Math.Max(0, Days.ToList().FindIndex(d => d.IsToday));
        _focusedMinute = FirstSlotOf(Days[_focusedDay].Date);
        PaintFocus();
    }

    public void ActivateFocus()
    {
        if (_focusedDay < 0) return;
        ActivateSlot(Days[_focusedDay].Date, GridHelper.ATime(_focusedMinute));
    }

    /// <summary>The focused slot reuses the hover highlight, so there is one visual
    /// language for "this is where the appointment would go".</summary>
    private void PaintFocus()
    {
        for (int i = 0; i < Days.Count; i++)
        {
            if (i == _focusedDay) Days[i].HoverAtMinute(_focusedMinute);
            else Days[i].StopHover();
        }
    }

    /// <summary>
    /// Loads the days starting at <paramref name="start"/>: three of them from that exact
    /// date, or the whole week containing it (from its Monday), depending on the stored
    /// view. filter narrows the range to a subset of appointments (Agenda's toolbar
    /// worker filter); the appointment dialog's picker never passes one, so it keeps
    /// showing everyone's bookings when checking for a free slot.
    /// </summary>
    public async Task LoadRange(DateOnly start, Func<Appointment, bool>? filter = null)
    {
        Loading = true;
        try
        {
            int dayCount = ValidDayCount(await _settings.GetInt(ConfigKeys.AgendaDays, ThreeDays));
            IsThreeDayView = dayCount == ThreeDays;
            if (!IsThreeDayView) start = WeekHelper.MondayOfWeek(start);
            ResizeDays(dayCount);

            RangeStart = start;
            var end = start.AddDays(dayCount - 1);
            RangeText = FormatRange(start, end);

            // Three round trips for the whole range, never one per day.
            var all = await _appointments.GetByRange(start, end);
            if (filter is not null) all = all.Where(filter).ToList();
            var closed = await _availability.ClosedDaysIn(start, end);
            var intervalsPerDay = await _availability.WeeklyIntervals();
            SlotMinutes = GridHelper.IsValidSlotMinutes(
                await _settings.GetInt(ConfigKeys.AgendaSlotMinutes, GridHelper.DefaultSlotMinutes));

            // Opening hours of the days on screen only, so the three-day view is not
            // stretched to fit a longer day that is not even shown.
            var rangeIntervals = new List<(TimeOnly start, TimeOnly fi)>();
            for (int i = 0; i < dayCount; i++)
            {
                var day = WeekHelper.ToWeekday(start.AddDays(i));
                if (intervalsPerDay.TryGetValue(day, out var intervals)) rangeIntervals.AddRange(intervals);
            }

            var visible = all.Select(c => (c.Time, c.DurationMin));
            if (_ghost is { } f && _dateGhost is not null)
                visible = visible.Append((GridHelper.ATime(GhostStartMinute), GhostDuration));

            (GridStartMinute, GridEndMinute) = GridHelper.VisibleRange(rangeIntervals, visible.ToList());
            _firstOpenMinute = rangeIntervals.Count == 0
                ? GridStartMinute
                : rangeIntervals.Min(f => GridHelper.DayMinutes(f.start));
            _openings = intervalsPerDay.ToDictionary(
                p => p.Key, p => p.Value.Min(f => GridHelper.DayMinutes(f.start)));

            RecomputeMetrics();
            FillRuler();

            for (int i = 0; i < dayCount; i++)
            {
                var date = start.AddDays(i);
                var day = Days[i];
                day.Date = date;
                day.IsToday = date == DateOnly.FromDateTime(DateTime.Today);
                day.Closed = closed.ContainsKey(date);
                day.ClosedReason = closed.GetValueOrDefault(date);

                var dayIntervals = day.Closed
                    ? []
                    : intervalsPerDay.GetValueOrDefault(WeekHelper.ToWeekday(date), []);

                FillBands(day, dayIntervals, intervalsPerDay.Count > 0);
                FillAppointments(day, all.Where(c => c.Date == date).ToList());
            }

            ApplyGhost();
            RefreshNow();
        }
        finally { Loading = false; }
    }

    /// <summary>
    /// Grows or shrinks the column list to <paramref name="count"/>, keeping the columns
    /// that survive: moving within the same view reuses every column, so WPF only
    /// rebinds them instead of rebuilding the whole grid.
    /// </summary>
    private void ResizeDays(int count)
    {
        if (Days.Count == count) return;

        while (Days.Count > count) Days.RemoveAt(Days.Count - 1);
        while (Days.Count < count) Days.Add(new GridDayViewModel(this));

        // The keyboard focus may point past the last column now.
        _focusedDay = -1;
        foreach (var day in Days) day.StopHover();
    }

    /// <summary>
    /// Ghost block for the dialog: shows where the appointment being edited would land,
    /// and how it collides with its neighbours, before the warnings even fire.
    /// </summary>
    public void ShowGhost(DateOnly date, TimeOnly time, int durationMin)
    {
        GhostStartMinute = GridHelper.DayMinutes(time);
        GhostDuration = Math.Max(1, durationMin);
        _dateGhost = date;

        _ghost ??= new GridAppointmentViewModel
        {
            IsGhost = true,
            Title = Texts.ThisAppointment,
            TimeText = string.Empty
        };

        ApplyGhost();
    }

    private int GhostStartMinute { get; set; }
    private int GhostDuration { get; set; } = 30;

    private void ApplyGhost()
    {
        if (_ghost is not { } ghost || _dateGhost is not { } date) return;

        foreach (var day in Days) day.Appointments.Remove(ghost);

        var column = Days.FirstOrDefault(d => d.Date == date);
        if (column is null) return;

        ghost.Top = GridHelper.Top(GhostStartMinute, GridStartMinute, PixelsPerMinute);
        ghost.Height = GridHelper.Height(GhostDuration, PixelsPerMinute, MinAppointmentHeightPx);
        ghost.LaneIndex = 0;
        ghost.LaneCount = 1;
        column.Appointments.Add(ghost);
    }

    /// <summary>Moves the red "now" marker. Called once a minute by the view.</summary>
    public void RefreshNow()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        int now = GridHelper.DayMinutes(TimeOnly.FromDateTime(DateTime.Now));

        NowVisible = Days.Any(d => d.Date == today) && now >= GridStartMinute && now <= GridEndMinute;
        NowTop = GridHelper.Top(now, GridStartMinute, PixelsPerMinute);
    }

    private static string FormatRange(DateOnly start, DateOnly end)
    {
        var culture = AppLanguage.Culture;
        return start.Month == end.Month
            ? string.Format(Texts.WeekRangeSameMonth, start.Day, end.Day,
                            end.ToString("MMMM yyyy", culture))
            : string.Format(Texts.WeekRangeAcrossMonths,
                            start.ToString("d MMM", culture),
                            end.ToString("d MMM yyyy", culture));
    }

    private void RecomputeMetrics()
    {
        PixelsPerMinute = GridHelper.PixelsPerMinute(SlotHeightPx, SlotMinutes);
        HourHeightPx = PixelsPerMinute * 60;
        TotalHeightPx = (GridEndMinute - GridStartMinute) * PixelsPerMinute;
        // Open on the padding hour above the first opening, not on the opening itself,
        // so the empty hour before the day starts is visible from the first frame.
        InitialOffsetPx = GridHelper.Top(
            Math.Max(GridStartMinute, _firstOpenMinute - GridHelper.OpeningPaddingMinutes),
            GridStartMinute, PixelsPerMinute);
    }

    private void FillRuler()
    {
        Hours.Clear();
        foreach (int minute in GridHelper.RulerHours(GridStartMinute, GridEndMinute, HourHeightPx))
            Hours.Add(new RulerHourViewModel
            {
                Top = GridHelper.Top(minute, GridStartMinute, PixelsPerMinute),
                Label = GridHelper.ATime(minute).ToString("HH\\:mm")
            });
    }

    private void FillBands(GridDayViewModel day,
        IReadOnlyList<(TimeOnly start, TimeOnly fi)> intervals, bool hasConfiguredSchedules)
    {
        day.Bands.Clear();

        // With no opening hours configured at all, shading every column head to toe as
        // "outside opening hours" is technically true and completely useless. Leave the
        // week clear until the barbershop has a schedule.
        if (!hasConfiguredSchedules && !day.Closed) return;

        var type = day.Closed ? BandType.ClosedDay : BandType.OutsideSchedule;

        foreach (var (start, fi) in GridHelper.OutsideScheduleBands(intervals, GridStartMinute, GridEndMinute))
            day.Bands.Add(new GridBandViewModel
            {
                Top = GridHelper.Top(start, GridStartMinute, PixelsPerMinute),
                Height = (fi - start) * PixelsPerMinute,
                Type = type
            });
    }

    private void FillAppointments(GridDayViewModel day, IReadOnlyList<Appointment> appointments)
    {
        day.Appointments.Clear();

        // Cancelled and no-show appointments free up their slot, so they must not take
        // a lane either — same rule AvailabilityService.Check already applies.
        var active = appointments
            .Where(c => c.Status is not (AppointmentStatus.Cancelled or AppointmentStatus.NoShow))
            .OrderBy(c => c.Time).ToList();

        var blocks = active
            .Select(c => new TimeBlock(GridHelper.DayMinutes(c.Time),
                                          GridHelper.DayMinutes(c.Time) + c.DurationMin))
            .ToList();
        var lanes = GridHelper.DistributeLanes(blocks);

        for (int i = 0; i < active.Count; i++)
        {
            var block = CreateBlock(active[i]);
            block.LaneIndex = lanes[i].Index;
            block.LaneCount = Math.Max(1, lanes[i].Total);
            day.Appointments.Add(block);
        }

        // Cancelled ones ride along in lane 0, dimmed, so the history stays visible.
        foreach (var appointment in appointments.Where(c => c.Status is AppointmentStatus.Cancelled or AppointmentStatus.NoShow))
            day.Appointments.Add(CreateBlock(appointment));
    }

    private GridAppointmentViewModel CreateBlock(Appointment appointment)
    {
        var block = GridAppointmentViewModel.From(appointment,
            GridHelper.Top(appointment.Time, GridStartMinute, PixelsPerMinute),
            GridHelper.Height(appointment.DurationMin, PixelsPerMinute, MinAppointmentHeightPx));
        block.IsSelected = SelectedAppointmentId == appointment.Id;
        return block;
    }

    partial void OnSelectedAppointmentIdChanged(int? value)
    {
        foreach (var day in Days)
            foreach (var block in day.Appointments)
                block.IsSelected = value is not null && block.Id == value;
    }
}
