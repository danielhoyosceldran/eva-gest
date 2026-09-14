using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Resources;

namespace EvaGest.ViewModels.Dialogs;

/// <summary>One collapsible FAQ entry (pantalles 3.10). <paramref name="Section"/> is an
/// internal code, never shown: it only decides which questions come first.</summary>
public record FaqQuestion(string Section, string Question, string Answer);

/// <summary>The pages a FAQ entry can belong to, as codes the language cannot change.</summary>
public static class FaqSection
{
    public const string Home = "Home";
    public const string Agenda = "Agenda";
    public const string Clients = "Clients";
    public const string Workers = "Workers";
    public const string Catalog = "Catalog";
    public const string Sales = "Sales";
    public const string Till = "Till";
    public const string Settings = "Settings";
}

/// <summary>
/// Ajuda (FAQ), pantalles 3.10. When opened from a specific page, that page's
/// questions are prioritised — shown first, rather than filtered out, so the
/// answer to something learned in another section is never hidden.
/// </summary>
public partial class HelpViewModel : DialogViewModelBase
{
    public override string Title => Texts.NavHelp;

    public ObservableCollection<FaqQuestion> Questions { get; }

    public HelpViewModel(string? prioritySection = null)
    {
        var all = FaqContent.All();
        Questions = new ObservableCollection<FaqQuestion>(
            prioritySection is null
                ? all
                : all.OrderByDescending(p => p.Section == prioritySection));
    }

    [RelayCommand]
    private void CloseHelp() => RequestClose(true);
}

/// <summary>
/// The FAQ entries from RF-19, grouped by the section they belong to. Built on demand
/// rather than held in a static field, so the wording comes from whichever language the
/// session started in.
/// </summary>
public static class FaqContent
{
    public static List<FaqQuestion> All() =>
    [
        new(FaqSection.Sales, Texts.FaqSaleRegisterQuestion, Texts.FaqSaleRegisterAnswer),
        new(FaqSection.Sales, Texts.FaqSaleEditQuestion, Texts.FaqSaleEditAnswer),
        new(FaqSection.Sales, Texts.FaqSaleCustomLineQuestion, Texts.FaqSaleCustomLineAnswer),
        new(FaqSection.Agenda, Texts.FaqAppointmentCreateQuestion, Texts.FaqAppointmentCreateAnswer),
        new(FaqSection.Agenda, Texts.FaqAppointmentCompleteQuestion, Texts.FaqAppointmentCompleteAnswer),
        new(FaqSection.Agenda, Texts.FaqNoShowQuestion, Texts.FaqNoShowAnswer),
        new(FaqSection.Clients, Texts.FaqClientNewQuestion, Texts.FaqClientNewAnswer),
        new(FaqSection.Clients, Texts.FaqClientHistoryQuestion, Texts.FaqClientHistoryAnswer),
        new(FaqSection.Catalog, Texts.FaqServiceEditQuestion, Texts.FaqServiceEditAnswer),
        new(FaqSection.Catalog, Texts.FaqProductEditQuestion, Texts.FaqProductEditAnswer),
        new(FaqSection.Till, Texts.FaqMovementQuestion, Texts.FaqMovementAnswer),
        new(FaqSection.Settings, Texts.FaqBackupQuestion, Texts.FaqBackupAnswer),
        new(FaqSection.Settings, Texts.FaqRestoreQuestion, Texts.FaqRestoreAnswer),
        new(FaqSection.Workers, Texts.FaqWorkerNewQuestion, Texts.FaqWorkerNewAnswer),
        new(FaqSection.Workers, Texts.FaqWorkerHolidayQuestion, Texts.FaqWorkerHolidayAnswer),
        new(FaqSection.Settings, Texts.FaqVatIncludedQuestion, Texts.FaqVatIncludedAnswer),
        new(FaqSection.Settings, Texts.FaqClosedDayQuestion, Texts.FaqClosedDayAnswer),
        new(FaqSection.Sales, Texts.FaqExportQuestion, Texts.FaqExportAnswer),
    ];
}
