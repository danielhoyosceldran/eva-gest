using System.Globalization;
using System.Windows;
using System.Windows.Data;
using EvaGest.ViewModels.Dialogs;

namespace EvaGest.Helpers;

/// <summary>"Anul·lar sale" only makes sense once the sale already exists (pantalles 3.2).</summary>
public class EditModeToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        => value is SaleDialogViewModel.Mode.Edit ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
