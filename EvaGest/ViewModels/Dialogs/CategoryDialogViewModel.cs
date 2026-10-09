using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Models;
using EvaGest.Resources;
using EvaGest.Services;

namespace EvaGest.ViewModels.Dialogs;

public partial class CategoryDialogViewModel : DialogViewModelBase
{
    private readonly ICatalogService _catalog;
    private readonly int? _id;

    [ObservableProperty] private string _name = string.Empty;

    public override string Title => _id is null ? Texts.NewCategoryTitle : Texts.EditCategoryTitle;

    public CategoryDialogViewModel(ICatalogService catalog) => _catalog = catalog;

    public CategoryDialogViewModel(ICatalogService catalog, ExpenseCategory category)
    {
        _catalog = catalog;
        _id = category.Id;
        Name = category.Name;
    }

    [RelayCommand]
    private async Task Save()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorValidation = Texts.CategoryNameRequired;
            return;
        }

        // The name is unique in the database, deactivated categories included, and the
        // page saves only once this dialog has closed. A taken name used to get that far
        // and fail there with the generic error, the typed name already gone (E-06), so
        // it is refused here, where the message can say what to do. Compared ignoring
        // case: "bizum" next to "Bizum" would only be two entries for one thing.
        string name = Name.Trim();
        var taken = (await _catalog.GetCategories()).FirstOrDefault(m =>
            m.Id != _id && string.Equals(m.Name.Trim(), name, StringComparison.CurrentCultureIgnoreCase));
        if (taken is not null)
        {
            ErrorValidation = string.Format(Texts.CategoryNameAlreadyExists, taken.Name);
            return;
        }

        ErrorValidation = null;
        RequestClose(true);
    }

    [RelayCommand]
    private void Cancel() => RequestClose(false);

    public ExpenseCategory AModel() => new() { Id = _id ?? 0, Name = Name.Trim(), Active = true };
}
