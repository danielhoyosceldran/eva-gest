using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using EvaGest.Helpers;

namespace EvaGest.Views.Elements;

/// <summary>
/// The hour input used for every time the user types: the appointment's hour and the
/// opening hours of the shop and of each worker. Hours and minutes are two boxes joined
/// by a fixed ":"; two digits in the hours move on to the minutes by themselves.
///
/// <see cref="Time"/> is the same "hh:mm" string the text boxes and combo boxes used to
/// bind to, so the ViewModels and their validation are unchanged. The rules for each
/// keystroke live in <see cref="TimeEntry"/>, where they are tested.
/// </summary>
public partial class TimeBox : UserControl
{
    public static readonly DependencyProperty TimeProperty = DependencyProperty.Register(
        nameof(Time), typeof(string), typeof(TimeBox),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (d, e) => ((TimeBox)d).OnTimeChanged((string?)e.NewValue))
        {
            DefaultUpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        });

    /// <summary>"hh:mm", or "" for no time. Written on every keystroke, already complete
    /// ("1" in the hours is 01:00), so a form saved with Enter mid-typing still reads a
    /// valid time.</summary>
    public string Time
    {
        get => (string)GetValue(TimeProperty);
        set => SetValue(TimeProperty, value);
    }

    /// <summary>Set while the boxes write <see cref="Time"/>, so that write does not come
    /// back and rewrite the boxes under the user's fingers ("1" would turn into "01").</summary>
    private bool _pushing;

    /// <summary>Set while <see cref="Time"/> is copied into the boxes, so filling them is
    /// not taken for typing and written straight back.</summary>
    private bool _filling;

    public TimeBox()
    {
        InitializeComponent();

        Hours.PreviewTextInput += OnHoursText;
        Minutes.PreviewTextInput += OnMinutesText;
        Hours.PreviewKeyDown += OnHoursKey;
        Minutes.PreviewKeyDown += OnMinutesKey;
        DataObject.AddPastingHandler(Hours, OnPaste);
        DataObject.AddPastingHandler(Minutes, OnPaste);
        Hours.TextChanged += (_, _) => Push();
        Minutes.TextChanged += (_, _) => Push();

        // Entering a box selects it, so typing replaces it. A click is taken over for the
        // same reason: otherwise the mouse-up right after would drop the selection.
        foreach (var box in new[] { Hours, Minutes })
        {
            box.GotKeyboardFocus += (_, _) => box.SelectAll();
            box.PreviewMouseLeftButtonDown += (_, e) =>
            {
                if (box.IsKeyboardFocusWithin) return;
                box.Focus();
                e.Handled = true;
            };
        }

        // A click on the outline but outside both boxes starts at the hours
        Outline.MouseLeftButtonDown += (_, _) => Hours.Focus();

        // Leaving the control tidies what was typed: "9" becomes 09:00
        IsKeyboardFocusWithinChanged += (_, e) =>
        {
            if (!(bool)e.NewValue) Tidy();
        };
    }

    // ── From the ViewModel ───────────────────────────────────────────────────

    private void OnTimeChanged(string? value)
    {
        if (_pushing) return;
        // Already what the boxes say: leave them, half-typed minutes included
        if (TimeEntry.Join(Hours.Text, Minutes.Text) == (value ?? string.Empty)) return;

        var (hours, minutes) = TimeEntry.Split(value);
        _filling = true;
        try
        {
            Hours.Text = hours;
            Minutes.Text = minutes;
        }
        finally { _filling = false; }
    }

    // ── To the ViewModel ─────────────────────────────────────────────────────

    private void Push()
    {
        if (_filling) return;
        _pushing = true;
        try { Time = TimeEntry.Join(Hours.Text, Minutes.Text); }
        finally { _pushing = false; }
    }

    private void Tidy()
    {
        var (hours, minutes) = TimeEntry.Split(TimeEntry.Join(Hours.Text, Minutes.Text));
        if (Hours.Text != hours) Hours.Text = hours;
        if (Minutes.Text != minutes) Minutes.Text = minutes;
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

    private void GoToMinutes()
    {
        Minutes.Focus();
        Minutes.SelectAll();
    }

    private void GoToHours()
    {
        Hours.Focus();
        Hours.CaretIndex = Hours.Text.Length;
    }

    private void OnHoursText(object sender, TextCompositionEventArgs e)
    {
        // Every key is decided here; nothing reaches the box on its own
        e.Handled = true;

        if (TimeEntry.IsSeparator(e.Text))
        {
            if (Hours.Text.Length > 0) GoToMinutes();
            return;
        }

        // Hours already full and the caret at their end: the digit belongs to the minutes
        if (Hours.Text.Length == 2 && Hours.SelectionLength == 0 && Hours.CaretIndex == 2)
        {
            GoToMinutes();
            TypeMinutes(e.Text);
            return;
        }

        var result = TimeEntry.TypeHours(Proposed(Hours, e.Text));
        if (!result.Accepted) return;
        Set(Hours, result.Text);
        if (result.JumpToMinutes) GoToMinutes();
    }

    private void OnMinutesText(object sender, TextCompositionEventArgs e)
    {
        e.Handled = true;
        TypeMinutes(e.Text);
    }

    private void TypeMinutes(string input)
    {
        if (TimeEntry.TypeMinutes(Proposed(Minutes, input)) is { } text) Set(Minutes, text);
    }

    private void OnHoursKey(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Up or Key.Down:
                Set(Hours, TimeEntry.StepHours(Hours.Text, e.Key == Key.Up ? 1 : -1));
                Hours.SelectAll();
                e.Handled = true;
                break;
            // Space never reaches PreviewTextInput in a WPF text box, so it is caught here
            case Key.Space:
                if (Hours.Text.Length > 0) GoToMinutes();
                e.Handled = true;
                break;
            case Key.Right when Hours.SelectionLength == 0 && Hours.CaretIndex == Hours.Text.Length:
                GoToMinutes();
                e.Handled = true;
                break;
        }
    }

    private void OnMinutesKey(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Up or Key.Down:
                Set(Minutes, TimeEntry.StepMinutes(Minutes.Text, e.Key == Key.Up ? 1 : -1));
                Minutes.SelectAll();
                e.Handled = true;
                break;
            case Key.Space:
                e.Handled = true;
                break;
            // Backspace or Left at the very start of the minutes go back to the hours
            case Key.Back or Key.Left when Minutes.SelectionLength == 0 && Minutes.CaretIndex == 0:
                GoToHours();
                e.Handled = true;
                break;
        }
    }

    /// <summary>A pasted time ("9:30", "0930") fills both boxes; anything else is ignored.</summary>
    private void OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        e.CancelCommand();
        if (e.DataObject.GetData(DataFormats.UnicodeText) is not string text) return;
        if (TimeEntry.Parse(text) is not { } time) return;

        Hours.Text = time.Hours;
        Set(Minutes, time.Minutes);
        Minutes.Focus();
    }
}
