using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Dialogs;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// An error line says what to fix, so it must not outlive the fix (the sale dialog kept
/// "pick a client" after Sense client was chosen), and a date box that reads something
/// impossible (31/02) must stop the save instead of letting the dialog use the last
/// real date (manual test B23, bug B-44).
/// </summary>
public class StaleNoticeTests
{
    private static ClientDialogViewModel NewClient(TestDatabase testDb)
        => new(new ClientService(new TestFactory(testDb.Options)), new TestDialogService());

    [Fact]
    public async Task The_error_goes_away_when_the_user_changes_the_form()
    {
        await using var testDb = new TestDatabase();
        var vm = NewClient(testDb);
        vm.Name = "Joan";

        await vm.SaveCommand.ExecuteAsync(null);
        vm.ErrorValidation.Should().NotBeNull("the mobile is missing");

        vm.Mobile = "612345678";

        vm.ErrorValidation.Should().BeNull("what it asked for has been typed");
    }

    [Fact]
    public async Task The_error_stays_while_nothing_has_changed()
    {
        await using var testDb = new TestDatabase();
        var vm = NewClient(testDb);

        await vm.SaveCommand.ExecuteAsync(null);

        vm.ErrorValidation.Should().NotBeNull("setting it must not clear it by itself");
    }

    [Fact]
    public async Task A_client_with_an_impossible_birthday_is_not_saved()
    {
        await using var testDb = new TestDatabase();
        var vm = NewClient(testDb);
        vm.Name = "Joan García";
        vm.Mobile = "612345678";
        vm.IsDateInvalid = true;

        await vm.SaveCommand.ExecuteAsync(null);

        vm.ErrorValidation.Should().Be(EvaGest.Resources.Texts.DateInvalid);
    }
}
