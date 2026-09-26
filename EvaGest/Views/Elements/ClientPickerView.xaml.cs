using System.ComponentModel;
using System.Windows.Controls;
using EvaGest.ViewModels.Elements;

namespace EvaGest.Views.Elements;

public partial class ClientPickerView : UserControl
{
    private ClientPickerViewModel? _vm;

    public ClientPickerView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            if (_vm is not null) _vm.PropertyChanged -= OnViewModelChanged;
            _vm = DataContext as ClientPickerViewModel;
            if (_vm is not null) _vm.PropertyChanged += OnViewModelChanged;
        };
    }

    /// <summary>
    /// A pick writes the client's name into the box from code, which leaves the caret
    /// at the start; put it at the end, where the user would carry on typing. The name
    /// is already in the box by then: the view model writes the text before the client.
    /// </summary>
    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ClientPickerViewModel.SelectedClient) && _vm?.SelectedClient is not null)
            Box.CaretIndex = Box.Text.Length;
    }
}
