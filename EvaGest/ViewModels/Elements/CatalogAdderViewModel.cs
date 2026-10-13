using EvaGest.Helpers;
using EvaGest.Models;
using EvaGest.Resources;
using EvaGest.Services;

namespace EvaGest.ViewModels.Elements;

/// <summary>
/// The one box the sale dialog adds lines from. It searches services and products
/// together, so the user never has to decide first which of the two lists a thing
/// lives in, and picking a match adds it at once: there is no separate "+ Servei"
/// button to forget (the old box only selected, and a sale with a service sitting in
/// the box but no line was refused as empty).
///
/// After each pick the box empties and keeps the focus, ready for the next item. When
/// what is typed is not in the catalogue, the last option of the dropdown offers to add
/// it as a custom concept with that text as its description.
///
/// It is a <see cref="SearchPickerViewModel"/>, so it reuses that box's view, keyboard
/// handling and "Veure la llista" dialog; only what a pick does is different.
/// </summary>
public sealed class CatalogAdderViewModel : SearchPickerViewModel
{
    private readonly IEnumerable<Service> _services;
    private readonly IEnumerable<Product> _products;
    private readonly Action<object> _onAdd;

    /// <param name="services">The owner's active services, read live on every search.</param>
    /// <param name="products">The owner's active products, likewise.</param>
    /// <param name="onAdd">Called with the <see cref="Service"/>, <see cref="Product"/> or
    /// <see cref="FreeConcept"/> picked.</param>
    public CatalogAdderViewModel(
        IEnumerable<Service> services, IEnumerable<Product> products, Action<object> onAdd,
        IDialogService? dialogs)
        : base(dialogs, Texts.CatalogBrowserTitle, Texts.CatalogSearchPlaceholder)
    {
        _services = services;
        _products = products;
        _onAdd = onAdd;
    }

    /// <summary>A custom concept asked for from the dropdown, carrying what was typed.</summary>
    public sealed record FreeConcept(string Description);

    /// <summary>Services first, then products, each labelled with its kind and price so
    /// two things with similar names are told apart before they are added.</summary>
    protected override IEnumerable<PickerOption> Options()
        => _services.Select(s => new PickerOption(s, s.Name, Detail(Texts.Service, s.PriceCents)))
            .Concat(_products.Select(p => new PickerOption(p, p.Name, Detail(Texts.Product, p.PriceCents))));

    private static string Detail(string kind, int priceCents) => $"{kind} · {Money.Format(priceCents)}";

    /// <summary>Whatever is typed can always become a custom concept, offered last so a
    /// real match is still what Enter picks first.</summary>
    protected override IEnumerable<PickerOption> Extras(string query)
    {
        string text = query.Trim();
        if (text.Length == 0) yield break;
        yield return new PickerOption(new FreeConcept(text), string.Format(Texts.AddAsCustomConcept, text));
    }

    /// <summary>A pick is added straight away and the box emptied for the next one.
    /// Emptying it reports null back here, which is ignored.</summary>
    protected override void Report(object? value)
    {
        if (value is null) return;
        _onAdd(value);
        Clear();
    }

    /// <summary>
    /// Leaving the box only closes the dropdown. The base class would pick the one
    /// option the text still names, which here would add a line just because focus
    /// moved away; and emptying the box would throw away what was typed.
    /// </summary>
    public override void Commit() => IsDropdownOpen = false;
}
