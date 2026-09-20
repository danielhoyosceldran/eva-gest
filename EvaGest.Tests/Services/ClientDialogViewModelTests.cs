using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Dialogs;
using Xunit;

namespace EvaGest.Tests.Services;

public class ClientDialogViewModelTests
{
    private static ClientDialogViewModel Build(TestDatabase testDb)
        => new(new ClientService(new TestFactory(testDb.Options)));

    private static ClientDialogViewModel Build(TestDatabase testDb, Client existing)
        => new(new ClientService(new TestFactory(testDb.Options)), existing);

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

    [Fact]
    public async Task A_name_another_client_already_has_is_refused_with_a_message_saying_what_to_do()
    {
        // The regression: this used to be a notice with a "save it anyway" button, and
        // pressing it reached ClientService.Create with a colliding key, so the unique
        // index threw DbUpdateException at the user as an unexpected-error dialog and
        // the client was lost. It is refused here instead, where the wording can help.
        await using var testDb = new TestDatabase();
        var clients = new ClientService(new TestFactory(testDb.Options));
        await clients.Create(new Client { Name = "Joan García", Mobile = "612345678" });

        var vm = Build(testDb);
        vm.Name = "joan  garcia";          // same client, typed carelessly
        vm.Mobile = "699999999";           // a different number does not make them new

        await vm.SaveCommand.ExecuteAsync(null);

        vm.ErrorValidation.Should().NotBeNull();
        vm.ErrorValidation.Should().Contain("Joan García").And.Contain("612345678");
    }

    [Fact]
    public async Task A_name_nobody_else_has_saves_even_on_a_phone_someone_else_uses()
    {
        await using var testDb = new TestDatabase();
        var clients = new ClientService(new TestFactory(testDb.Options));
        await clients.Create(new Client { Name = "Joan García", Mobile = "612345678" });

        var vm = Build(testDb);
        vm.Name = "Joan Pérez";            // a relative on the household number
        vm.Mobile = "612345678";

        await vm.SaveCommand.ExecuteAsync(null);

        vm.ErrorValidation.Should().BeNull();
    }

    [Fact]
    public async Task Editing_a_client_without_renaming_them_does_not_collide_with_itself()
    {
        await using var testDb = new TestDatabase();
        var clients = new ClientService(new TestFactory(testDb.Options));
        int id = await clients.Create(new Client { Name = "Joan García", Mobile = "612345678" });

        var vm = Build(testDb, (await clients.GetById(id))!);
        vm.Mobile = "699999999";

        await vm.SaveCommand.ExecuteAsync(null);

        vm.ErrorValidation.Should().BeNull();
    }
}
