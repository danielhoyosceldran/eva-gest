using System.Globalization;
using System.Windows.Data;
using EvaGest.Resources;

namespace EvaGest.Helpers;

public class BackupTypeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Texts.BackupAutomatic : Texts.BackupManual;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
