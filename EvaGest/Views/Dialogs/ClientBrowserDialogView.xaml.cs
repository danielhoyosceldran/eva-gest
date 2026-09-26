using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EvaGest.ViewModels.Dialogs;

namespace EvaGest.Views.Dialogs;

public partial class ClientBrowserDialogView : UserControl
{
    public ClientBrowserDialogView()
    {
        InitializeComponent();

        // Down from the search bar goes into the list, so the whole dialog works from
        // the keyboard: type, arrow down to the person, Enter.
        Search.PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Down || List.Items.Count == 0) return;
            if (List.SelectedIndex < 0) List.SelectedIndex = 0;
            (List.ItemContainerGenerator.ContainerFromIndex(List.SelectedIndex) as ListBoxItem)?.Focus();
            e.Handled = true;
        };
    }

    /// <summary>A double-click on a row picks that client, like the button does.</summary>
    private void OnListDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Only on a row: a double-click on the scrollbar must not pick whoever is selected
        if (ItemsControl.ContainerFromElement(List, e.OriginalSource as DependencyObject) is not ListBoxItem) return;
        if (DataContext is ClientBrowserDialogViewModel vm && vm.ChooseCommand.CanExecute(null))
            vm.ChooseCommand.Execute(null);
    }
}
