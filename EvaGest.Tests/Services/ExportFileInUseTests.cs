using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Dialogs;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// F-03: re-exporting a period while its CSV was open in Excel (which opens files with no
/// sharing) failed with the generic error, and since the files were written one by one
/// the folder could end up with the new sales file next to the old VAT file. The export
/// must check first: if any file it would replace is in use, it throws
/// <see cref="ExportFileInUseException"/> and no file in the folder changes.
///
/// Excel is simulated by holding the file open with <see cref="FileShare.None"/>.
/// </summary>
public sealed class ExportFileInUseTests : IDisposable
{
    private static readonly DateOnly Day = new(2026, 9, 7);
    private static readonly string Period = $"{Day:yyyyMMdd}-{Day:yyyyMMdd}";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "EvaGestExportInUse_" + Guid.NewGuid());

    public ExportFileInUseTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    /// <summary>A sale and a till movement with VAT on <see cref="Day"/>, so all three
    /// files (sales, VAT, till VAT) are written.</summary>
    private static async Task Seed(TestDatabase testDb)
    {
        await using var db = testDb.Context();
        var method = Make.Method();
        db.PaymentMethods.Add(method);
        await db.SaveChangesAsync();
        db.Sales.Add(Make.Sale(Day, method.Id, SaleStatus.Active, Make.Line(1_500)));
        db.CashMovements.Add(new CashMovement
        {
            Date = Day, Type = MovementType.Out, AmountCents = 1_210, BaseCents = 1_000, VatCents = 210,
            VatBp = 2100, PaymentMethodId = method.Id, Concept = "Material"
        });
        await db.SaveChangesAsync();
    }

    /// <summary>One more sale, so a second export would write different contents.</summary>
    private static async Task AnotherSale(TestDatabase testDb)
    {
        await using var db = testDb.Context();
        int methodId = db.PaymentMethods.First().Id;
        db.Sales.Add(Make.Sale(Day, methodId, SaleStatus.Active, Make.Line(4_000, 1000)));
        await db.SaveChangesAsync();
    }

    /// <summary>Every file in the folder, by name, with its bytes.</summary>
    private Dictionary<string, byte[]> Snapshot()
        => Directory.GetFiles(_folder).ToDictionary(f => Path.GetFileName(f), f => File.ReadAllBytes(f));

    private static void ShouldBeUnchanged(Dictionary<string, byte[]> after, Dictionary<string, byte[]> before)
    {
        after.Keys.Should().BeEquivalentTo(before.Keys, "no file may be added (no temporary file either) or removed");
        foreach (var (name, bytes) in before)
            after[name].Should().Equal(bytes, $"{name} must keep its previous contents");
    }

    private static FileStream HeldOpenLikeExcel(string path)
        => new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

    [Theory]
    [InlineData("vendes_")]
    [InlineData("iva_")]
    [InlineData("iva_caixa_")]
    public async Task A_target_file_open_in_another_program_stops_the_export_before_any_file_changes(string prefix)
    {
        await using var testDb = new TestDatabase();
        await Seed(testDb);
        var export = new ExportService(new TestFactory(testDb.Options));
        await export.ExportSales(Day, Day, _folder);
        string target = Path.Combine(_folder, $"{prefix}{Period}.csv");
        File.Exists(target).Should().BeTrue("precondition: the first export wrote this file");
        await AnotherSale(testDb);
        var before = Snapshot();

        Func<Task> again;
        ExportFileInUseException? thrown;
        using (HeldOpenLikeExcel(target))
        {
            again = () => export.ExportSales(Day, Day, _folder);
            thrown = (await again.Should().ThrowAsync<ExportFileInUseException>()).Which;
        }

        thrown.FileName.Should().Be(Path.GetFileName(target), "the user has to know which file to close");
        ShouldBeUnchanged(Snapshot(), before);
    }

    [Fact]
    public async Task Once_the_file_is_closed_the_export_replaces_all_the_files()
    {
        await using var testDb = new TestDatabase();
        await Seed(testDb);
        var export = new ExportService(new TestFactory(testDb.Options));
        await export.ExportSales(Day, Day, _folder);
        await AnotherSale(testDb);
        var before = Snapshot();

        await export.ExportSales(Day, Day, _folder);

        var after = Snapshot();
        after.Keys.Should().BeEquivalentTo(before.Keys);
        after[$"vendes_{Period}.csv"].Should().NotEqual(before[$"vendes_{Period}.csv"]);
        after[$"iva_{Period}.csv"].Should().NotEqual(before[$"iva_{Period}.csv"]);
    }

    // ── The export dialog ────────────────────────────────────────────────────

    [Fact]
    public async Task The_export_dialog_names_the_file_in_use_and_stays_open()
    {
        await using var testDb = new TestDatabase();
        await Seed(testDb);
        var export = new ExportService(new TestFactory(testDb.Options));
        await export.ExportSales(Day, Day, _folder);
        await AnotherSale(testDb);
        var before = Snapshot();
        string target = Path.Combine(_folder, $"iva_{Period}.csv");

        var dialogs = new TestDialogService { FolderChosen = _folder };
        var vm = new ExportDialogViewModel(export, dialogs) { From = Day, To = Day };
        bool? closed = null;
        vm.Close += ok => closed = ok;

        using (HeldOpenLikeExcel(target))
        {
            var press = () => vm.ExportCommand.ExecuteAsync(null);
            await press.Should().NotThrowAsync("the user gets a message, not the generic error");
        }

        vm.ErrorValidation.Should().NotBeNullOrWhiteSpace();
        vm.ErrorValidation.Should().Contain(Path.GetFileName(target), "the message says which file to close");
        closed.Should().BeNull("the dialog stays open so the export can be retried");
        ShouldBeUnchanged(Snapshot(), before);
    }

    [Fact]
    public async Task The_export_dialog_still_writes_the_files_when_nothing_is_in_use()
    {
        await using var testDb = new TestDatabase();
        await Seed(testDb);
        var dialogs = new TestDialogService { FolderChosen = _folder };
        var vm = new ExportDialogViewModel(new ExportService(new TestFactory(testDb.Options)), dialogs)
        {
            From = Day, To = Day
        };
        bool? closed = null;
        vm.Close += ok => closed = ok;

        await vm.ExportCommand.ExecuteAsync(null);

        vm.ErrorValidation.Should().BeNull();
        closed.Should().BeTrue();
        File.Exists(Path.Combine(_folder, $"vendes_{Period}.csv")).Should().BeTrue();
        File.Exists(Path.Combine(_folder, $"iva_{Period}.csv")).Should().BeTrue();
    }
}
