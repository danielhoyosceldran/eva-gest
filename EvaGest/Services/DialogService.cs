using System.Windows;
using EvaGest.ViewModels.Dialegs;
using EvaGest.Views.Dialegs;

namespace EvaGest.Services;

/// <summary>The only class that knows about Window/MessageBox.</summary>
public class DialogService : IDialogService
{
    public Task<bool> MostrarDialeg<TViewModel>(TViewModel viewModel) where TViewModel : class
    {
        if (viewModel is not DialegViewModelBase vm)
            throw new ArgumentException(
                $"{typeof(TViewModel).Name} must derive from DialegViewModelBase.");

        var finestra = new DialogWindow(vm) { Owner = Application.Current.MainWindow };
        bool confirmat = finestra.ShowDialog() == true;
        return Task.FromResult(confirmat);
    }

    public Task<bool> Confirmar(string titol, string missatge,
                                string textConfirmar, string textCancellar = "Cancel·lar")
    {
        var resultat = MessageBox.Show(missatge, titol,
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        return Task.FromResult(resultat == MessageBoxResult.Yes);
    }

    public Task Informar(string titol, string missatge)
    {
        MessageBox.Show(missatge, titol, MessageBoxButton.OK, MessageBoxImage.Information);
        return Task.CompletedTask;
    }

    public Task<string?> DemanarCarpeta(string titol)
    {
        var dialeg = new Microsoft.Win32.OpenFolderDialog { Title = titol };
        bool ok = dialeg.ShowDialog() == true;
        return Task.FromResult(ok ? dialeg.FolderName : null);
    }
}
