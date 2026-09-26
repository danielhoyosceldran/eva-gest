using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using AwesomeAssertions;
using EvaGest.Tests.Infra;
using Xunit;

namespace EvaGest.Tests.Views;

/// <summary>
/// What a closed ComboBox shows under the app's own ComboBox style. The style replaces
/// WPF's template, and a closed box that prints the item's type name renders without any
/// binding error — only reading the laid-out text catches it.
/// </summary>
[Collection(WpfCollection.Name)]
public class ComboBoxDisplayTests(ApplicationWpf app)
{
    /// <summary>Stands in for a catalogue entity with no ToString override (PaymentMethod,
    /// Product, ExpenseCategory). A plain class, not a record: a record's ToString would
    /// print the name inside "Option { Name = … }".</summary>
    public sealed class Option(string name)
    {
        public string Name { get; } = name;
    }

    [Fact]
    public void A_closed_combo_with_DisplayMemberPath_shows_the_selected_name()
    {
        // The payment method box showed "EvaGest.Tests…Option"-style type names: WPF turns
        // DisplayMemberPath into an ItemTemplateSelector, and the style's closed box only
        // passed the ItemTemplate through. Client, Worker and Service escaped only because
        // their ToString happens to return the name.
        app.Runs(() =>
        {
            var combo = new ComboBox
            {
                ItemsSource = new[] { new Option("Efectiu"), new Option("Targeta") },
                DisplayMemberPath = nameof(Option.Name),
                SelectedIndex = 1,
            };

            LayOut(combo);

            Texts(combo).Should().Contain("Targeta")
                .And.NotContain(t => t.Contains(nameof(Option)));
        });
    }

    [Fact]
    public void A_closed_combo_with_an_ItemTemplate_still_shows_the_selected_name()
    {
        // The client box in the appointment dialog, which already worked, must keep working.
        app.Runs(() =>
        {
            var text = new FrameworkElementFactory(typeof(TextBlock));
            text.SetBinding(TextBlock.TextProperty, new Binding(nameof(Option.Name)));

            var combo = new ComboBox
            {
                ItemsSource = new[] { new Option("Joan Puig") },
                ItemTemplate = new DataTemplate { VisualTree = text },
                SelectedIndex = 0,
            };

            LayOut(combo);

            Texts(combo).Should().Contain("Joan Puig");
        });
    }

    /// <summary>Hosts the combo the way a view does and runs a real layout pass, so the
    /// app's implicit ComboBox style is applied and its closed box is built.</summary>
    private static void LayOut(ComboBox combo)
    {
        var host = new Border { Child = combo };
        combo.ApplyTemplate();
        host.Measure(new Size(300, 60));
        host.Arrange(new Rect(0, 0, 300, 60));
        host.UpdateLayout();
    }

    private static List<string> Texts(DependencyObject root) =>
        Descendants<TextBlock>(root).Select(t => t.Text).ToList();

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var deeper in Descendants<T>(child)) yield return deeper;
        }
    }
}
