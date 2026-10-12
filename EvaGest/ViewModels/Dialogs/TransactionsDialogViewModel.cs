using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvaGest.Helpers;
using EvaGest.Models;
using EvaGest.Resources;
using EvaGest.Services;
using EvaGest.ViewModels.Elements;
using Serilog;

namespace EvaGest.ViewModels.Dialogs;

/// <summary>
/// The large "every transaction" window opened from the Reports page: sales and cash
/// movements in one read-only table, filtered by period, type, client, worker, payment
/// method, category and free text, with the totals of what is on screen underneath and
/// an export of exactly those rows. Nothing here edits: corrections stay on the Sales
/// and Till pages.
/// </summary>
public partial class TransactionsDialogViewModel : DialogViewModelBase
{
    private readonly ITransactionsService _transactions;
    private readonly IExportService _export;
    private readonly IDialogService _dialogs;
    private readonly DateOnly _today;

    public override string Title => Texts.TransactionsTitle;

    /// <summary>A table to browse, not a form: it opens as large as the screen allows.</summary>
    public override bool FillsScreen => true;

    /// <param name="today">The date the period shortcuts count from; the tests fix it.</param>
    public TransactionsDialogViewModel(ITransactionsService transactions, IExportService export,
                                       IDialogService dialogs, DateOnly today)
    {
        _transactions = transactions;
        _export = export;
        _dialogs = dialogs;
        _today = today;

        // Opens on this month, the period most questions are about. Set on the fields so
        // no reload starts before Load() is called.
        _from = new DateOnly(today.Year, today.Month, 1);
        _to = today;

        // Search boxes over the lists that grow long; an empty box means "all"
        ClientPicker = new(FilterClients, c => c.Name, c => Client = c, dialogs,
            Texts.ClientBrowserTitle, Texts.AnyOption, c => c.Mobile);
        WorkerPicker = new(FilterWorkers, w => w.Name, w => Worker = w, dialogs,
            Texts.WorkerBrowserTitle, Texts.AnyOption);
        CategoryPicker = new(FilterCategories, c => c.Label, c => Category = c, dialogs,
            Texts.CategoryBrowserTitle, Texts.AnyOption);

        KindOptions =
        [
            new KindOption(null, Texts.AllKinds, SelectKind),
            new KindOption(TransactionKind.Sale, Texts.SalesPlural, SelectKind),
            new KindOption(TransactionKind.CashIn, Texts.CashInPlural, SelectKind),
            new KindOption(TransactionKind.CashOut, Texts.CashOutPlural, SelectKind),
        ];
        // Selected only once the list exists: selecting reports back to SelectKind,
        // which walks the list.
        KindOptions[0].IsSelected = true;
    }

    // ── Rows ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// One table row, every figure already formatted (a raw *Cents value bound to a
    /// TextBlock reads cents as euros). <see cref="SortKey"/> and <see cref="SignedTotalCents"/>
    /// are what the date and total columns sort by: their text would sort "10/02" before
    /// "9/02" and "100,00 €" before "20,00 €".
    /// </summary>
    public record TransactionRow(
        TransactionEntry Entry,
        DateTime SortKey,
        string DateText,
        string TimeText,
        string KindText,
        string ClientText,
        string WorkerText,
        string ConceptText,
        string DetailText,
        string CategoryText,
        string MethodText,
        string BaseText,
        string VatText,
        string TotalText,
        long SignedTotalCents,
        string StatusText,
        bool Voided,
        bool IsMoneyOut);

    public ObservableCollection<TransactionRow> Rows { get; } = [];

    /// <summary>The entries behind <see cref="Rows"/>, kept for the export: the file
    /// holds exactly what the table shows.</summary>
    private List<TransactionEntry> _shown = [];

    public bool HasNoResults => !Loading && Rows.Count == 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoResults))]
    private bool _loading;

    // ── Filters ──────────────────────────────────────────────────────────────

    [ObservableProperty] private DateOnly _from;
    [ObservableProperty] private DateOnly _to;
    [ObservableProperty] private TransactionKind? _kind;
    [ObservableProperty] private Client? _client;
    [ObservableProperty] private Worker? _worker;
    [ObservableProperty] private PaymentMethod? _paymentMethod;
    [ObservableProperty] private CategoryOption? _category;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _includeVoided;

    /// <summary>One segment of the type selector (All / Sales / Cash-ins / Cash-outs).</summary>
    public partial class KindOption(TransactionKind? kind, string label, Action<KindOption> onSelected) : ObservableObject
    {
        public TransactionKind? Kind { get; } = kind;
        public string Label { get; } = label;

        [ObservableProperty] private bool _isSelected;

        // Only the segment being switched on reports itself; the radio group switches the
        // previous one off on its own.
        partial void OnIsSelectedChanged(bool value)
        {
            if (value) onSelected(this);
        }
    }

    /// <summary>One entry of the category box: the text it shows and the filter it stands for.</summary>
    public record CategoryOption(string Label, TransactionCategory Value);

    public IReadOnlyList<KindOption> KindOptions { get; }
    public ObservableCollection<Client> FilterClients { get; } = [];
    public ObservableCollection<Worker> FilterWorkers { get; } = [];
    public ObservableCollection<PaymentMethod> FilterMethods { get; } = [];
    public ObservableCollection<CategoryOption> FilterCategories { get; } = [];

    public SearchPickerViewModel<Client> ClientPicker { get; }
    public SearchPickerViewModel<Worker> WorkerPicker { get; }
    public SearchPickerViewModel<CategoryOption> CategoryPicker { get; }

    // ── Footer ───────────────────────────────────────────────────────────────

    [ObservableProperty] private string _countText = string.Empty;
    [ObservableProperty] private string _incomeText = "—";
    [ObservableProperty] private string _outgoingsText = "—";
    [ObservableProperty] private string _netText = "—";
    [ObservableProperty] private string _salesBaseText = "—";
    [ObservableProperty] private string _salesVatText = "—";

    // ── Loading ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Set while several filters change at once (a period shortcut moves From and To,
    /// clearing resets them all), so the table reloads once at the end instead of once
    /// per box.
    /// </summary>
    private bool _changingSeveralFilters;

    /// <summary>Which search is the current one. A search overtaken by a newer one (the
    /// user typing on) discards its result instead of overwriting the newer table.</summary>
    private int _generation;

    /// <summary>Called once by the host before the window opens: fills the filter boxes,
    /// then the table. A failure is logged and said in the error line, and the window
    /// still opens, so changing a filter can try again.</summary>
    public async Task Load()
    {
        try
        {
            var options = await _transactions.GetFilterOptions();
            FillOptions(options);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Transactions dialog could not load its filter options");
            ErrorValidation = Texts.TransactionsNotLoaded;
            return;
        }

        await Search();
    }

    private void FillOptions(TransactionFilterOptions options)
    {
        foreach (var c in options.Clients) FilterClients.Add(c);
        foreach (var w in options.Workers) FilterWorkers.Add(w);
        foreach (var m in options.Methods) FilterMethods.Add(m);

        // Families in the order the shop thinks of them: what was sold, then what each
        // product shelf sold, then what the money went out on.
        FilterCategories.Add(new(Texts.Services, TransactionCategory.Of(LineType.Service)));
        FilterCategories.Add(new(Texts.Products, TransactionCategory.Of(LineType.Product)));
        FilterCategories.Add(new(Texts.OtherConcepts, TransactionCategory.Of(LineType.Other)));
        foreach (var p in options.ProductCategories)
            FilterCategories.Add(new(string.Format(Texts.ProductCategoryOption, p), TransactionCategory.OfProducts(p)));
        foreach (var e in options.ExpenseCategories)
            FilterCategories.Add(new(string.Format(Texts.ExpenseCategoryOption, e.Name), TransactionCategory.OfExpense(e.Id)));
    }

    /// <summary>Runs the search for the current filters and rewrites the table and its
    /// footer in one synchronous block once the result is back.</summary>
    public async Task Search()
    {
        int generation = ++_generation;
        Loading = true;
        try
        {
            var found = await _transactions.Search(CurrentFilter());
            if (generation != _generation) return;

            _shown = found;
            Rows.Clear();
            foreach (var entry in found) Rows.Add(ToRow(entry));

            var totals = TransactionTotals.From(found);
            CountText = string.Format(Texts.TransactionsCount, totals.Count);
            IncomeText = Money.Format(totals.IncomeCents);
            OutgoingsText = Money.Format(totals.CashOutCents);
            NetText = Money.Format(totals.NetCents);
            SalesBaseText = Money.Format(totals.SalesBaseCents);
            SalesVatText = Money.Format(totals.SalesVatCents);

            ErrorValidation = null;
        }
        catch (Exception ex)
        {
            if (generation != _generation) return;
            Log.Error(ex, "Transactions dialog search failed");
            ErrorValidation = Texts.TransactionsNotLoaded;
        }
        finally
        {
            if (generation == _generation)
            {
                Loading = false;
                OnPropertyChanged(nameof(HasNoResults));
            }
        }
    }

    public TransactionFilter CurrentFilter() => new(
        From, To, Kind, Client?.Id, Worker?.Id, PaymentMethod?.Id, Category?.Value,
        string.IsNullOrWhiteSpace(SearchText) ? null : SearchText, IncludeVoided);

    /// <summary>Search() already logs and reports its own failure, so nothing it throws
    /// is left unobserved here.</summary>
    private void FilterChanged()
    {
        if (!_changingSeveralFilters) _ = Search();
    }

    partial void OnFromChanged(DateOnly value)
    {
        // An inverted range is never a question anybody means: the other end follows.
        if (value > To) To = value;
        else FilterChanged();
    }

    partial void OnToChanged(DateOnly value)
    {
        if (value < From) From = value;
        else FilterChanged();
    }

    partial void OnKindChanged(TransactionKind? value) => FilterChanged();
    partial void OnClientChanged(Client? value) { ClientPicker.Show(value); FilterChanged(); }
    partial void OnWorkerChanged(Worker? value) { WorkerPicker.Show(value); FilterChanged(); }
    partial void OnPaymentMethodChanged(PaymentMethod? value) => FilterChanged();
    partial void OnCategoryChanged(CategoryOption? value) { CategoryPicker.Show(value); FilterChanged(); }
    partial void OnSearchTextChanged(string value) => FilterChanged();
    partial void OnIncludeVoidedChanged(bool value) => FilterChanged();

    private void SelectKind(KindOption option)
    {
        foreach (var other in KindOptions)
            if (other != option) other.IsSelected = false;
        Kind = option.Kind;
    }

    // ── Commands ─────────────────────────────────────────────────────────────

    [RelayCommand]
    private Task SelectToday() => SetPeriod(_today, _today);

    [RelayCommand]
    private Task SelectThisWeek() => SetPeriod(WeekHelper.MondayOfWeek(_today), _today);

    [RelayCommand]
    private Task SelectThisMonth() => SetPeriod(new DateOnly(_today.Year, _today.Month, 1), _today);

    [RelayCommand]
    private Task SelectThisYear() => SetPeriod(new DateOnly(_today.Year, 1, 1), _today);

    private async Task SetPeriod(DateOnly from, DateOnly to)
    {
        _changingSeveralFilters = true;
        try { (From, To) = (from, to); }
        finally { _changingSeveralFilters = false; }

        await Search();
    }

    /// <summary>Clears every filter but the period, which has its own shortcuts and is
    /// the one thing the user rarely wants thrown away with the rest.</summary>
    [RelayCommand]
    private async Task ClearFilters()
    {
        _changingSeveralFilters = true;
        try
        {
            KindOptions[0].IsSelected = true;
            Kind = null;
            Client = null;
            Worker = null;
            PaymentMethod = null;
            Category = null;
            // Also empties text typed in a box but never picked
            ClientPicker.Clear();
            WorkerPicker.Clear();
            CategoryPicker.Clear();
            SearchText = string.Empty;
            IncludeVoided = false;
        }
        finally { _changingSeveralFilters = false; }

        await Search();
    }

    [RelayCommand]
    private async Task Export()
    {
        if (_shown.Count == 0)
        {
            ErrorValidation = Texts.ExportTableEmpty;
            return;
        }

        var folder = await _dialogs.AskFolder(Texts.ChooseDestinationFolder);
        if (folder is null) return;

        string path;
        try
        {
            path = await _export.ExportTransactions(_shown, From, To, folder);
        }
        catch (ExportFileInUseException ex)
        {
            Log.Error(ex, "Transactions export to {Folder} refused: {File} is open in another program", folder, ex.FileName);
            ErrorValidation = string.Format(Texts.ExportFileInUse, ex.FileName);
            return;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Error(ex, "Transactions export to {Folder} failed", folder);
            ErrorValidation = Texts.ExportFolderNotWritable;
            return;
        }

        ErrorValidation = null;
        await _dialogs.Inform(Texts.ExportDoneTitle, string.Format(Texts.ExportTableDone, path));
    }

    [RelayCommand]
    private void CloseWindow() => RequestClose(false);

    // ── Formatting ───────────────────────────────────────────────────────────

    private static TransactionRow ToRow(TransactionEntry e)
    {
        bool moneyOut = e.Kind == TransactionKind.CashOut;

        // Money out reads negative in every amount column, so a glance down the total
        // column already says which way the money went.
        string Amount(int? cents) => cents is int c ? Money.Format(moneyOut ? -c : c) : "—";

        return new TransactionRow(
            Entry: e,
            SortKey: e.Date.ToDateTime(e.Time ?? TimeOnly.MinValue),
            DateText: e.Date.ToString("dd/MM/yyyy"),
            TimeText: e.Time?.ToString("HH:mm") ?? "—",
            KindText: Labels.Text(e.Kind),
            ClientText: string.IsNullOrEmpty(e.ClientName) ? "—" : e.ClientName,
            WorkerText: e.WorkerName ?? (e.Kind == TransactionKind.Sale ? Texts.Unassigned : "—"),
            ConceptText: string.IsNullOrEmpty(e.Concept) ? "—" : e.Concept,
            DetailText: Detail(e),
            CategoryText: Labels.Category(e),
            MethodText: e.MethodName ?? Texts.DeletedPaymentMethod,
            BaseText: Amount(e.BaseCents),
            VatText: Amount(e.VatCents),
            TotalText: Money.Format(e.SignedTotalCents),
            SignedTotalCents: e.SignedTotalCents,
            StatusText: e.Voided ? Texts.Voided : Texts.Active,
            Voided: e.Voided,
            IsMoneyOut: moneyOut);
    }

    /// <summary>
    /// The concept cell's tooltip: one line per sale line with its amount, then the
    /// notes. A sale's concept cell only has room for "Tall + Cera"; this is where the
    /// quantities and the split of the total can be read without leaving the table.
    /// </summary>
    private static string Detail(TransactionEntry e)
    {
        var lines = e.Lines
            .Select(l => l.Quantity > 1
                ? $"{l.Quantity} × {l.Description} — {Money.Format(l.AmountCents)}"
                : $"{l.Description} — {Money.Format(l.AmountCents)}")
            .ToList();

        if (lines.Count == 0 && !string.IsNullOrEmpty(e.Concept)) lines.Add(e.Concept);
        if (!string.IsNullOrWhiteSpace(e.Notes)) lines.Add(e.Notes);

        return lines.Count > 0 ? string.Join(Environment.NewLine, lines) : "—";
    }
}
