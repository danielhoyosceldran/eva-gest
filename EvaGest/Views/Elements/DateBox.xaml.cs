using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using EvaGest.Helpers;

namespace EvaGest.Views.Elements;

/// <summary>
/// The date input used for every date the user types (appointment, movement, birthday,
/// every From / To filter, closed days). Day, month and year are three boxes joined by a
/// fixed "/"; a complete part moves on to the next box by itself, and the calendar button
/// still opens a month to pick from.
///
/// <see cref="SelectedDate"/> is the same DateTime? the DatePicker had, so every binding
/// kept its DateOnlyConverter and the ViewModels are unchanged. The rules for each
/// keystroke live in <see cref="DateEntry"/>, where they are tested.
/// </summary>
public partial class DateBox : UserControl
{
    public static readonly DependencyProperty SelectedDateProperty = DependencyProperty.Register(
        nameof(SelectedDate), typeof(DateTime?), typeof(DateBox),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (d, e) => ((DateBox)d).OnSelectedDateChanged((DateTime?)e.NewValue))
        {
            DefaultUpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        });

    /// <summary>The date, or null for none. Written only once the boxes make a real date
    /// (or are all emptied), never for a half-typed one.</summary>
    public DateTime? SelectedDate
    {
        get => (DateTime?)GetValue(SelectedDateProperty);
        set => SetValue(SelectedDateProperty, value);
    }

    public static readonly DependencyProperty IsInvalidProperty = DependencyProperty.Register(
        nameof(IsInvalid), typeof(bool), typeof(DateBox),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    /// <summary>What the boxes hold is not a date: an impossible day (31/02), or parts
    /// left out when focus moved on. Draws the outline red; <see cref="SelectedDate"/>
    /// keeps its last real value until it is put right, so a dialog that saves binds this
    /// (OneWayToSource) to refuse to save while the box is red. Only the control writes it.</summary>
    public bool IsInvalid
    {
        get => (bool)GetValue(IsInvalidProperty);
        set => SetValue(IsInvalidProperty, value);
    }

    /// <summary>Day, month, year: the order the keys move through.</summary>
    private readonly TextBox[] _boxes;

    /// <summary>Set while the boxes write <see cref="SelectedDate"/>, so that write does
    /// not come back and rewrite the boxes under the user's fingers.</summary>
    private bool _pushing;

    /// <summary>Set while a date is copied into the boxes, so filling them is not taken
    /// for typing.</summary>
    private bool _filling;

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    public DateBox()
    {
        InitializeComponent();
        _boxes = [Day, Month, Year];

        for (int i = 0; i < _boxes.Length; i++)
        {
            int index = i;
            var box = _boxes[i];
            box.PreviewTextInput += (_, e) => OnText(index, e);
            box.PreviewKeyDown += (_, e) => OnKey(index, e);
            box.TextChanged += (_, _) => OnEdited();
            DataObject.AddPastingHandler(box, OnPaste);

            // Entering a box selects it, so typing replaces it. A click is taken over for
            // the same reason: otherwise the mouse-up right after would drop the selection.
            box.GotKeyboardFocus += (_, _) => box.SelectAll();
            box.PreviewMouseLeftButtonDown += (_, e) =>
            {
                if (box.IsKeyboardFocusWithin) return;
                box.Focus();
                e.Handled = true;
            };
        }

        // A click on the outline outside the boxes starts at the day. The calendar button
        // handles its own click, so it never gets here.
        Outline.MouseLeftButtonDown += (_, _) => Day.Focus();

        // Leaving the control fills in what was left out ("4/3" is 04/03 of this year)
        IsKeyboardFocusWithinChanged += (_, e) =>
        {
            if (!(bool)e.NewValue && !CalendarPopup.IsOpen) Tidy();
        };

        CalendarButton.Click += (_, _) => OpenCalendar();
        Picker.SelectedDatesChanged += (_, _) =>
        {
            if (!CalendarPopup.IsOpen || Picker.SelectedDate is not DateTime picked) return;
            CalendarPopup.IsOpen = false;
            SetDate(DateOnly.FromDateTime(picked));
        };
        // The calendar keeps the mouse captured after a click on a day, which would make
        // the next click anywhere else only release it. Let go as soon as the button is up.
        Picker.PreviewMouseUp += (_, _) =>
        {
            if (Mouse.Captured is CalendarItem) Mouse.Capture(null);
        };
    }

    // ── From the ViewModel ───────────────────────────────────────────────────

    private void OnSelectedDateChanged(DateTime? value)
    {
        if (_pushing) return;

        DateOnly? date = value is DateTime moment ? DateOnly.FromDateTime(moment) : null;
        // Already what the boxes say: leave them alone
        if (date is not null && DateEntry.Complete(Day.Text, Month.Text, Year.Text) == date) return;
        if (date is null && _boxes.All(b => b.Text.Length == 0)) return;

        Fill(DateEntry.Split(date));
        IsInvalid = false;
    }

    private void Fill((string Day, string Month, string Year) parts)
    {
        _filling = true;
        try
        {
            Day.Text = parts.Day;
            Month.Text = parts.Month;
            Year.Text = parts.Year;
        }
        finally { _filling = false; }
    }

    // ── To the ViewModel ─────────────────────────────────────────────────────

    private void Push(DateOnly? date)
    {
        _pushing = true;
        try { SelectedDate = date?.ToDateTime(TimeOnly.MinValue); }
        finally { _pushing = false; }
    }

    /// <summary>A date from the calendar or a paste: into the boxes and out to the binding.</summary>
    private void SetDate(DateOnly date)
    {
        Fill(DateEntry.Split(date));
        IsInvalid = false;
        Push(date);
    }

    /// <summary>After every keystroke: a complete real date goes out at once, all boxes
    /// emptied means no date, and an impossible one turns the outline red.</summary>
    private void OnEdited()
    {
        if (_filling) return;

        if (_boxes.All(b => b.Text.Length == 0))
        {
            IsInvalid = false;
            Push(null);
            return;
        }

        IsInvalid = DateEntry.IsImpossible(Day.Text, Month.Text, Year.Text);
        if (DateEntry.Complete(Day.Text, Month.Text, Year.Text) is { } date) Push(date);
    }

    private void Tidy()
    {
        var (day, month, year, date) = DateEntry.Tidy(Day.Text, Month.Text, Year.Text, Today);
        if (day == Day.Text && month == Month.Text && year == Year.Text && date is not null)
        {
            IsInvalid = false;
            return;
        }

        Fill((day, month, year));
        if (date is { } real)
        {
            IsInvalid = false;
            Push(real);
        }
        else
        {
            // Something typed that is not a date: say so, and keep the last real one
            IsInvalid = _boxes.Any(b => b.Text.Length > 0);
        }
    }

    // ── Typing ───────────────────────────────────────────────────────────────

    /// <summary>What <paramref name="box"/> would read if <paramref name="input"/> went in
    /// at the caret, replacing the selection.</summary>
    private static string Proposed(TextBox box, string input)
        => box.Text.Remove(box.SelectionStart, box.SelectionLength).Insert(box.SelectionStart, input);

    private static void Set(TextBox box, string text)
    {
        box.Text = text;
        box.CaretIndex = text.Length;
    }

    private void GoTo(int index)
    {
        _boxes[index].Focus();
        _boxes[index].SelectAll();
    }

    private void GoBackTo(int index)
    {
        _boxes[index].Focus();
        _boxes[index].CaretIndex = _boxes[index].Text.Length;
    }

    private void OnText(int index, TextCompositionEventArgs e)
    {
        // Every key is decided here; nothing reaches a box on its own
        e.Handled = true;
        var box = _boxes[index];
        bool last = index == _boxes.Length - 1;

        if (DateEntry.IsSeparator(e.Text))
        {
            if (!last && box.Text.Length > 0) GoTo(index + 1);
            return;
        }

        // Day or month already full, caret at the end: the digit belongs to the next box
        if (!last && box.Text.Length == 2 && box.SelectionLength == 0 && box.CaretIndex == 2)
        {
            GoTo(index + 1);
            Type(index + 1, e.Text);
            return;
        }

        Type(index, e.Text);
    }

    private void Type(int index, string input)
    {
        var box = _boxes[index];
        string proposed = Proposed(box, input);

        if (index == 2)
        {
            if (DateEntry.TypeYear(proposed) is { } year) Set(box, year);
            return;
        }

        var result = index == 0 ? DateEntry.TypeDay(proposed) : DateEntry.TypeMonth(proposed);
        if (!result.Accepted) return;
        Set(box, result.Text);
        if (result.JumpToNext) GoTo(index + 1);
    }

    private void OnKey(int index, KeyEventArgs e)
    {
        var box = _boxes[index];
        bool last = index == _boxes.Length - 1;

        switch (e.Key)
        {
            case Key.Up or Key.Down:
                int delta = e.Key == Key.Up ? 1 : -1;
                Set(box, index switch
                {
                    0 => DateEntry.StepDay(box.Text, delta, Today),
                    1 => DateEntry.StepMonth(box.Text, delta, Today),
                    _ => DateEntry.StepYear(box.Text, delta, Today)
                });
                box.SelectAll();
                e.Handled = true;
                break;
            // Space never reaches PreviewTextInput in a WPF text box, so it is caught here
            case Key.Space:
                if (!last && box.Text.Length > 0) GoTo(index + 1);
                e.Handled = true;
                break;
            case Key.Right when !last && box.SelectionLength == 0 && box.CaretIndex == box.Text.Length:
                GoTo(index + 1);
                e.Handled = true;
                break;
            // Backspace or Left at the very start of a box go back to the one before
            case Key.Back or Key.Left when index > 0 && box.SelectionLength == 0 && box.CaretIndex == 0:
                GoBackTo(index - 1);
                e.Handled = true;
                break;
            // Alt+Down opens the calendar, as it did on the DatePicker
            case Key.System when e.SystemKey == Key.Down:
                OpenCalendar();
                e.Handled = true;
                break;
        }
    }

    /// <summary>A pasted date ("13/10/2026", "2026-10-13") fills all three boxes;
    /// anything else is ignored.</summary>
    private void OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        e.CancelCommand();
        if (e.DataObject.GetData(DataFormats.UnicodeText) is not string text) return;
        if (DateEntry.Parse(text) is { } date) SetDate(date);
    }

    private void OpenCalendar()
    {
        var shown = DateEntry.Complete(Day.Text, Month.Text, Year.Text)
                    ?? (SelectedDate is DateTime current ? DateOnly.FromDateTime(current) : Today);
        // Set before opening, so the change is not taken for a pick
        Picker.SelectedDate = shown.ToDateTime(TimeOnly.MinValue);
        Picker.DisplayDate = shown.ToDateTime(TimeOnly.MinValue);
        CalendarPopup.IsOpen = true;
        Picker.Focus();
    }
}
