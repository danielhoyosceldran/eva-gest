using System.Globalization;
using System.Windows.Data;
using EvaGest.Resources;
using EvaGest.Services;

namespace EvaGest.Helpers;

/// <summary>Shows who took a backup: the daily automatic copy, the user, or the app
/// itself before an update changed the database. Bound to the whole
/// <see cref="BackupInfo"/>; a bare IsAutomatic flag is still understood.</summary>
public class BackupTypeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        => value switch
        {
            BackupInfo backup => Label(backup),
            true => Texts.BackupAutomatic,
            _ => Texts.BackupManual
        };

    /// <summary>The same wording for code, so the settings page's "last backup" line
    /// and the restore list cannot name one copy two ways.</summary>
    public static string Label(BackupInfo backup)
        => backup.IsBeforeUpdate ? Texts.BackupBeforeUpdate
         : backup.IsAutomatic ? Texts.BackupAutomatic
         : Texts.BackupManual;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
