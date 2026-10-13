using EvaGest.ViewModels.Dialogs;

namespace EvaGest.Tests.Infra;

/// <summary>The few gestures the scenario scripts repeat in the dialogs, named after what
/// the user does rather than how the ViewModel exposes it.</summary>
public static class DialogSteps
{
    /// <summary>Types into the sale's search box and presses Enter on the first match.</summary>
    public static void SearchAndAdd(this SaleDialogViewModel vm, string typed)
    {
        vm.CatalogAdder.Text = typed;
        vm.CatalogAdder.PickHighlightedCommand.Execute(null);
    }

    /// <summary>Adds a custom concept and types its description and price.</summary>
    public static void AddConcept(this SaleDialogViewModel vm, string description, string price)
    {
        vm.AddCustomConceptCommand.Execute(null);
        vm.Lines[^1].Description = description;
        vm.Lines[^1].PriceText = price;
    }

    /// <summary>Picks the payment method by the name on its button.</summary>
    public static void PayWith(this SaleDialogViewModel vm, string method)
        => vm.PaymentMethod = vm.ActiveMethods.Single(m => m.Name == method);

    /// <summary>Types part of a registered client's name in a client box and presses Enter.</summary>
    public static void PickClient(this ViewModels.Elements.ClientPickerViewModel picker, string typed)
    {
        picker.Text = typed;
        picker.PickHighlightedCommand.Execute(null);
    }
}
