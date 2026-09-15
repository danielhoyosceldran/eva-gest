using AwesomeAssertions;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Dialogs;
using Xunit;

namespace EvaGest.Tests.Services;

public class ClientDialogViewModelTests
{
    private static ClientDialogViewModel Build(TestDatabase testDb)
        => new(new ClientService(new TestFactory(testDb.Options)));

    [Fact]
    public async Task A_malformed_mobile_cannot_be_saved()
    {
        await using var testDb = new TestDatabase();
        var vm = Build(testDb);
        vm.Name = "Joan García";
        vm.Mobile = "123456";

        await vm.SaveCommand.ExecuteAsync(null);

        vm.ErrorValidation.Should().NotBeNull();
    }

    [Fact]
    public async Task A_mobile_with_spaces_saves_once_it_has_nine_digits()
    {
        await using var testDb = new TestDatabase();
        var vm = Build(testDb);
        vm.Name = "Joan García";
        vm.Mobile = "61 23 45 678";

        await vm.SaveCommand.ExecuteAsync(null);

        vm.ErrorValidation.Should().BeNull();
    }

    [Fact]
    public async Task A_malformed_email_cannot_be_saved()
    {
        await using var testDb = new TestDatabase();
        var vm = Build(testDb);
        vm.Name = "Joan García";
        vm.Mobile = "612345678";
        vm.Email = "joanexample.com";

        await vm.SaveCommand.ExecuteAsync(null);

        vm.ErrorValidation.Should().NotBeNull();
    }

    [Fact]
    public async Task An_empty_email_is_allowed_since_it_is_optional()
    {
        await using var testDb = new TestDatabase();
        var vm = Build(testDb);
        vm.Name = "Joan García";
        vm.Mobile = "612345678";

        await vm.SaveCommand.ExecuteAsync(null);

        vm.ErrorValidation.Should().BeNull();
    }

    [Fact]
    public async Task A_valid_email_saves()
    {
        await using var testDb = new TestDatabase();
        var vm = Build(testDb);
        vm.Name = "Joan García";
        vm.Mobile = "612345678";
        vm.Email = "joan@example.com";

        await vm.SaveCommand.ExecuteAsync(null);

        vm.ErrorValidation.Should().BeNull();
    }
}
