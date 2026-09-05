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

    public ProducteDialogViewModel() { }

    public ProducteDialogViewModel(Producte producte)
    {
        _id = producte.Id;
        Nom = producte.Nom;
        PreuText = Diners.FormatExport(producte.PreuCents);
        IvaText = Percentatges.Format(producte.IvaBp).Replace(" %", "");
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

        ErrorValidacio = null;
        SolicitarTancar(true);
    }

    [RelayCommand]
    private void Cancellar() => SolicitarTancar(false);

    public Producte AModel()
    {
        Diners.TryParse(PreuText, out int preuCents);
        int ivaBp = (int)Math.Round(decimal.Parse(IvaText.Replace(",", ".").Replace("%", ""),
            System.Globalization.CultureInfo.InvariantCulture) * 100);

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
