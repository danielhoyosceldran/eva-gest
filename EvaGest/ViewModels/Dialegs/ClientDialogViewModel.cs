using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Models;
using EvaGest.Services;

namespace EvaGest.ViewModels.Dialegs;

public partial class ClientDialogViewModel : DialegViewModelBase
{
    private readonly IClientService _clients;
    private readonly int? _id;

    [ObservableProperty] private string _nom = string.Empty;
    [ObservableProperty] private string _mobil = string.Empty;
    [ObservableProperty] private string? _correu;
    [ObservableProperty] private DateOnly? _dataNaixement;
    [ObservableProperty] private string? _observacions;

    /// <summary>Set when a duplicate is found on save, so the view can show the
    /// non-blocking warning with its two extra actions (pantalles 3.3).</summary>
    [ObservableProperty] private Client? _duplicatTrobat;

    public override string Titol => _id is null ? "Nou client" : "Editar client";

    public ClientDialogViewModel(IClientService clients)
    {
        _clients = clients;
    }

    public ClientDialogViewModel(IClientService clients, Client client) : this(clients)
    {
        _id = client.Id;
        Nom = client.Nom;
        Mobil = client.Mobil;
        Correu = client.Correu;
        DataNaixement = client.DataNaixement;
        Observacions = client.Observacions;
    }

    [RelayCommand]
    private async Task Guardar()
    {
        if (string.IsNullOrWhiteSpace(Nom) || string.IsNullOrWhiteSpace(Mobil))
        {
            ErrorValidacio = "Cal indicar el nom i el mòbil per guardar.";
            return;
        }

        // The warning is informational only: it never blocks saving (RF-03).
        if (DuplicatTrobat is null)
        {
            var possible = await _clients.BuscarPossibleDuplicat(Nom, Mobil);
            if (possible is not null && possible.Id != _id)
            {
                DuplicatTrobat = possible;
                return;
            }
        }

        ErrorValidacio = null;
        SolicitarTancar(true);
    }

    [RelayCommand]
    private void GuardarIgualment()
    {
        ErrorValidacio = null;
        SolicitarTancar(true);
    }

    [RelayCommand]
    private void Cancellar() => SolicitarTancar(false);

    public Client AModel() => new()
    {
        Id = _id ?? 0,
        Nom = Nom.Trim(),
        Mobil = Mobil.Trim(),
        Correu = string.IsNullOrWhiteSpace(Correu) ? null : Correu.Trim(),
        DataNaixement = DataNaixement,
        Observacions = string.IsNullOrWhiteSpace(Observacions) ? null : Observacions.Trim()
    };
}
