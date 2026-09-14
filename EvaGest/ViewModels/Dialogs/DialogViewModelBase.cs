using CommunityToolkit.Mvvm.ComponentModel;

namespace EvaGest.ViewModels.Dialogs;

/// <summary>Base for every dialog ViewModel. Raises <see cref="Close"/> instead of
/// closing a window directly, so dialog ViewModels stay unaware of WPF (capa-mvvm 3.2).</summary>
public abstract partial class DialogViewModelBase : ObservableObject
{
    [ObservableProperty]
    private string? _errorValidation;

    public abstract string Title { get; }

    public event Action<bool>? Close;

    protected void RequestClose(bool confirmed) => Close?.Invoke(confirmed);
}
