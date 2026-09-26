using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Resources;

namespace EvaGest.ViewModels.Dialogs;

/// <summary>One row of the chronological history block (pantalles 3.4), with the
/// amount already formatted so the view stays free of conversion logic.</summary>
public record HistoryRow(DateOnly Date, string Type, string Concept, string Status, string Amount);

public partial class ClientRecordViewModel : DialogViewModelBase
{
    private readonly IClientService _clients;
    private readonly IReportsService _reports;
    private readonly IDialogService _dialogs;

    /// <summary>
    /// Whether the record shows what the client has spent. Clients is a public page, but
    /// money figures are the owner's: with owner mode closed the spend indicators are
    /// hidden and the history's amount column reads "—". Fixed when the record opens.
    /// </summary>
    public bool ShowAmounts { get; }

    [ObservableProperty] private Client _client;
    [ObservableProperty] private ClientIndicators? _indicators;

    public ObservableCollection<HistoryRow> History { get; } = [];

    public string TotalSpentText => Indicators is null ? "—" : Money.Format(Indicators.TotalSpentCents);

    /// <summary>Formatted through AppLanguage.Culture like every other amount. Using the
    /// bare "C2" took CurrentCulture, so this one figure followed Windows' regional
    /// settings while the rest of the screen followed the app's language.</summary>
    public string AverageText => Indicators?.AveragePerVisitEuros is decimal m
        ? m.ToString("C2", AppLanguage.Culture)
        : "—";
    public string FrequencyText => Indicators?.FrequencyDays is double f ? f.ToString("0.0") : "—";
    public string SleepWakeText => Client.Asleep ? Texts.Wake : Texts.Sleep;

    partial void OnIndicatorsChanged(ClientIndicators? value)
    {
        OnPropertyChanged(nameof(TotalSpentText));
        OnPropertyChanged(nameof(AverageText));
        OnPropertyChanged(nameof(FrequencyText));
    }

    partial void OnClientChanged(Client value) => OnPropertyChanged(nameof(SleepWakeText));

    /// <summary>True once the client has actually been deleted, so the host page
    /// knows to remove it from its own list instead of just refreshing.</summary>
    public bool Deleted { get; private set; }

    public override string Title => Client.Name;

    public ClientRecordViewModel(IClientService clients, IReportsService reports, IDialogService dialogs, Client client,
        bool showAmounts)
    {
        ShowAmounts = showAmounts;
        _clients = clients;
        _reports = reports;
        _dialogs = dialogs;
        _client = client;
    }

    public async Task Load()
    {
        Indicators = await _reports.GetClientIndicators(Client.Id);

        History.Clear();
        foreach (var row in await _clients.GetClientHistory(Client.Id))
        {
            bool isAppointment = row.Type == HistoryType.Appointment;
            History.Add(new HistoryRow(
                row.Date,
                isAppointment ? Texts.HistoryAppointment : Texts.HistorySale,
                row.Concept ?? Texts.HistorySale,
                Labels(row.Type, row.Status),
                ShowAmounts && row.AmountCents is int cents ? Money.Format(cents) : "—"));
        }
    }

    private static string Labels(string type, string rawStatus) => type == HistoryType.Appointment
        ? EvaGest.Services.Labels.Text(Enum.Parse<AppointmentStatus>(rawStatus))
        : EvaGest.Services.Labels.Text(Enum.Parse<SaleStatus>(rawStatus));

    [RelayCommand]
    private void CloseRecord() => RequestClose(true);

    [RelayCommand]
    private async Task SleepWake()
    {
        if (Client.Asleep) await _clients.Wake(Client.Id);
        else await _clients.Sleep(Client.Id);

        Client = (await _clients.GetById(Client.Id))!;
    }

    [RelayCommand]
    private async Task Delete()
    {
        var (appointments, sales) = await _clients.CountHistory(Client.Id);

        bool confirmed = await _dialogs.Confirm(
            string.Format(Texts.DeleteClientTitle, Client.Name),
            string.Format(Texts.DeleteClientMessage, appointments, sales),
            Texts.DeleteForever);

        if (!confirmed) return;

        await _clients.Delete(Client.Id);
        Deleted = true;
        RequestClose(true);
    }
}
