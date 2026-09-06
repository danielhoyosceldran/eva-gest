using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Models;
using EvaGest.Services;

namespace EvaGest.ViewModels.Dialegs;

public partial class ProducteDialogViewModel : DialegViewModelBase
{
    private readonly int? _id;

    [ObservableProperty] private string _nom = string.Empty;
    [ObservableProperty] private string _preuText = "0,00";
    [ObservableProperty] private string _ivaText = "21";
    [ObservableProperty] private string? _categoria;

    public override string Titol => _id is null ? "Nou producte" : "Editar producte";

    /// <summary>New entry: the VAT rate starts at whatever Configuració proposes,
    /// but stays editable per item (RF-07/RF-08).</summary>
    public ProducteDialogViewModel(int ivaBpPerDefecte = 2100)
    {
        IvaText = Percentatges.FormatSenseUnitat(ivaBpPerDefecte);
    }

    public ProducteDialogViewModel(Producte producte)
    {
        _id = producte.Id;
        Nom = producte.Nom;
        PreuText = Diners.FormatExport(producte.PreuCents);
        IvaText = Percentatges.FormatSenseUnitat(producte.IvaBp);
        Categoria = producte.Categoria;
    }

    [RelayCommand]
    private void Guardar()
    {
        if (string.IsNullOrWhiteSpace(Nom))
        {
            ErrorValidacio = "Cal indicar el nom del producte per guardar.";
            return;
        }
        if (!Diners.TryParse(PreuText, out _))
        {
            ErrorValidacio = "El preu no és vàlid.";
            return;
        }
        if (!Percentatges.TryParse(IvaText, out _))
        {
            ErrorValidacio = "L'IVA ha de ser un percentatge entre 0 i 100.";
            return;
        }

        ErrorValidacio = null;
        SolicitarTancar(true);
    }

    [RelayCommand]
    private void Cancellar() => SolicitarTancar(false);

    public Producte AModel()
    {
        Diners.TryParse(PreuText, out int preuCents);
        Percentatges.TryParse(IvaText, out int ivaBp);

        return new Producte
        {
            Id = _id ?? 0,
            Nom = Nom.Trim(),
            PreuCents = preuCents,
            Categoria = string.IsNullOrWhiteSpace(Categoria) ? null : Categoria.Trim(),
            IvaBp = ivaBp,
            Actiu = true
        };
    }
}
