using System.Windows;
using EvaGest.Resources;
using EvaGest.ViewModels.Dialogs;
using EvaGest.Views.Dialogs;

namespace EvaGest.Services;

/// <summary>The only class that knows about Window/MessageBox.</summary>
public class DialogService : IDialogService
{
    public Task<bool> ShowDialog<TViewModel>(TViewModel viewModel) where TViewModel : class
    {
        if (viewModel is not DialogViewModelBase vm)
            throw new ArgumentException(
                $"{typeof(TViewModel).Name} must derive from DialogViewModelBase.");

        var window = new DialogWindow(vm) { Owner = Application.Current.MainWindow };
        bool confirmed = window.ShowDialog() == true;
        return Task.FromResult(confirmed);
    }

    public Task<bool> Confirm(string title, string message,
                              string textConfirm, string? textCancel = null)
    {
        var result = MessageBox.Show(message, title,
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        return Task.FromResult(result == MessageBoxResult.Yes);
    }

    public Task Inform(string title, string message)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        return Task.CompletedTask;
    }

    public Task<string?> AskFolder(string title)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = title };
        bool ok = dialog.ShowDialog() == true;
        return Task.FromResult(ok ? dialog.FolderName : null);
    }
}
