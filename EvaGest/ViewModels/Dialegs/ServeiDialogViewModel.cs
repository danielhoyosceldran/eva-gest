using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Models;
using EvaGest.Services;

namespace EvaGest.ViewModels.Dialegs;

public partial class ServeiDialogViewModel : DialegViewModelBase
{
    private readonly int? _id;

    [ObservableProperty] private string _nom = string.Empty;
    [ObservableProperty] private string _preuText = "0,00";
    [ObservableProperty] private string _ivaText = "21";
    [ObservableProperty] private string? _duradaMinText;

    public override string Titol => _id is null ? "Nou servei" : "Editar servei";

    /// <summary>New entry: the VAT rate starts at whatever Configuració proposes,
    /// but stays editable per item (RF-07/RF-08).</summary>
    public ServeiDialogViewModel(int ivaBpPerDefecte = 2100)
    {
        IvaText = Percentatges.FormatSenseUnitat(ivaBpPerDefecte);
    }

    public ServeiDialogViewModel(Servei servei)
    {
        _id = servei.Id;
        Nom = servei.Nom;
        PreuText = Diners.FormatExport(servei.PreuCents);
        IvaText = Percentatges.FormatSenseUnitat(servei.IvaBp);
        DuradaMinText = servei.DuradaMin?.ToString();
    }

    [RelayCommand]
    private void Guardar()
    {
        if (string.IsNullOrWhiteSpace(Nom))
        {
            ErrorValidacio = "Cal indicar el nom del servei per guardar.";
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

    /// <summary>Reads the edited fields back into a model, ready to save.</summary>
    public Servei AModel()
    {
        Diners.TryParse(PreuText, out int preuCents);
        Percentatges.TryParse(IvaText, out int ivaBp);
        int? duradaMin = int.TryParse(DuradaMinText, out int d) ? d : null;

        return new Servei
        {
            Id = _id ?? 0,
            Nom = Nom.Trim(),
            PreuCents = preuCents,
            IvaBp = ivaBp,
            DuradaMin = duradaMin,
            Actiu = true
        };
    }
}
