using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Models;
using EvaGest.Resources;

namespace EvaGest.ViewModels.Dialogs;

public partial class CategoryDialogViewModel : DialogViewModelBase
{
    private readonly int? _id;

    [ObservableProperty] private string _name = string.Empty;

    public override string Title => _id is null ? Texts.NewCategoryTitle : Texts.EditCategoryTitle;

    public CategoryDialogViewModel() { }

    public CategoryDialogViewModel(ExpenseCategory category)
    {
        _id = category.Id;
        Name = category.Name;
    }

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorValidation = Texts.CategoryNameRequired;
            return;
        }

        ErrorValidation = null;
        RequestClose(true);
    }

    [RelayCommand]
    private void Cancel() => RequestClose(false);

    public ExpenseCategory AModel() => new() { Id = _id ?? 0, Name = Name.Trim(), Active = true };
}
