using System.Globalization;
using System.Windows;
using System.Windows.Data;
using EvaGest.ViewModels.Dialegs;

namespace EvaGest.Helpers;

/// <summary>"Anul·lar venda" only makes sense once the sale already exists (pantalles 3.2).</summary>
public class ModeEditarAVisibilitatConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        => value is VendaDialogViewModel.Mode.Editar ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
