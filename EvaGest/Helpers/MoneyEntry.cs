using System.Windows;
using System.Windows.Controls;
using EvaGest.Services;

namespace EvaGest.Helpers;

/// <summary>
/// Behaviour behind the <c>MoneyBox</c> style: when the user leaves a money box, what
/// they typed is rewritten in the app's own shape, so "0" reads "0,00" and "15,5" reads
/// "15,50". Only the shape changes — the amount is the one <see cref="Money.TryParse"/>
/// already read from the text.
/// </summary>
public static class MoneyEntry
{
    /// <summary>
    /// The text a money box should show once the user leaves it. Anything that does not
    /// parse (including an empty box) is left exactly as typed: Save is what explains
    /// the problem, and rewriting a mistake would hide what the user wrote.
    /// </summary>
    public static string Tidy(string? text)
        => Money.TryParse(text, out int cents) ? Money.FormatInput(cents) : text ?? string.Empty;

    public static readonly DependencyProperty TidyOnBlurProperty =
        DependencyProperty.RegisterAttached("TidyOnBlur", typeof(bool), typeof(MoneyEntry),
            new PropertyMetadata(false, OnTidyOnBlurChanged));

    public static bool GetTidyOnBlur(DependencyObject d) => (bool)d.GetValue(TidyOnBlurProperty);
    public static void SetTidyOnBlur(DependencyObject d, bool value) => d.SetValue(TidyOnBlurProperty, value);

    private static void OnTidyOnBlurChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox box) return;
        box.LostKeyboardFocus -= OnLostKeyboardFocus;
        if ((bool)e.NewValue) box.LostKeyboardFocus += OnLostKeyboardFocus;
    }

    private static void OnLostKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
    {
        var box = (TextBox)sender;
        string tidy = Tidy(box.Text);

        // Writing Text goes back through the box's binding to the ViewModel, so the
        // ViewModel holds the same "0,00" the user now sees.
        if (tidy != box.Text) box.Text = tidy;
    }
}
