using AwesomeAssertions;
using EvaGest.Models;
using EvaGest.Resources;
using EvaGest.Services;
using EvaGest.Tests.Infra;
using EvaGest.ViewModels.Dialogs;
using Xunit;

namespace EvaGest.Tests.Services;

/// <summary>
/// E-06. Payment methods and expense categories have a unique name in the database,
/// deactivated ones included, but their dialogs only checked that a name was typed. The
/// dialog closed, the page's insert hit the unique index and the user got the generic
/// error with what they typed already gone. The dialog now refuses a taken name.
/// </summary>
public class CatalogDuplicateNameTests
{
    private static async Task<bool> Save(DialogViewModelBase vm, Func<Task> save)
    {
        bool closed = false;
        vm.Close += _ => closed = true;
        await save();
        return closed;
    }

    [Fact]
    public async Task A_new_method_with_the_name_of_a_deactivated_one_is_refused_in_the_dialog()
    {
        await using var testDb = new TestDatabase();
        var catalog = new CatalogService(new TestFactory(testDb.Options));
        await catalog.CreateMethod("Efectiu");  // something must stay active
        int bizum = await catalog.CreateMethod("Bizum");
        await catalog.ChangeMethodStatus(bizum, active: false);

        var vm = new PaymentMethodDialogViewModel(catalog) { Name = "  bizum " };
        bool closed = await Save(vm, () => vm.SaveCommand.ExecuteAsync(null));

        closed.Should().BeFalse("the page would then fail on the unique index");
        vm.ErrorValidation.Should().Be(string.Format(Texts.MethodNameAlreadyExists, "Bizum"));
    }

    [Fact]
    public async Task Renaming_a_method_to_another_ones_name_is_refused()
    {
        await using var testDb = new TestDatabase();
        var catalog = new CatalogService(new TestFactory(testDb.Options));
        await catalog.CreateMethod("Efectiu");
        int card = await catalog.CreateMethod("Targeta");
        var method = (await catalog.GetMethods()).Single(m => m.Id == card);

        var vm = new PaymentMethodDialogViewModel(catalog, method) { Name = "Efectiu" };
        bool closed = await Save(vm, () => vm.SaveCommand.ExecuteAsync(null));

        closed.Should().BeFalse();
        vm.ErrorValidation.Should().NotBeNull();
    }

    [Fact]
    public async Task A_method_keeping_its_own_name_saves()
    {
        await using var testDb = new TestDatabase();
        var catalog = new CatalogService(new TestFactory(testDb.Options));
        int id = await catalog.CreateMethod("Efectiu");
        var method = (await catalog.GetMethods()).Single(m => m.Id == id);

        var vm = new PaymentMethodDialogViewModel(catalog, method);
        bool closed = await Save(vm, () => vm.SaveCommand.ExecuteAsync(null));

        closed.Should().BeTrue();
        vm.ErrorValidation.Should().BeNull();
    }

    [Fact]
    public async Task A_new_category_with_a_taken_name_is_refused_in_the_dialog()
    {
        await using var testDb = new TestDatabase();
        var catalog = new CatalogService(new TestFactory(testDb.Options));
        int rent = await catalog.CreateCategory("Lloguer");
        await catalog.ChangeCategoryStatus(rent, active: false);

        var vm = new CategoryDialogViewModel(catalog) { Name = "LLOGUER" };
        bool closed = await Save(vm, () => vm.SaveCommand.ExecuteAsync(null));

        closed.Should().BeFalse();
        vm.ErrorValidation.Should().Be(string.Format(Texts.CategoryNameAlreadyExists, "Lloguer"));
    }

    [Fact]
    public async Task A_new_category_with_a_free_name_saves()
    {
        await using var testDb = new TestDatabase();
        var catalog = new CatalogService(new TestFactory(testDb.Options));
        await catalog.CreateCategory("Lloguer");

        var vm = new CategoryDialogViewModel(catalog) { Name = "Llum" };
        bool closed = await Save(vm, () => vm.SaveCommand.ExecuteAsync(null));

        closed.Should().BeTrue();
        await catalog.CreateCategory(vm.AModel().Name);
        (await catalog.GetCategories()).Select(c => c.Name).Should().BeEquivalentTo(["Lloguer", "Llum"]);
    }
}
