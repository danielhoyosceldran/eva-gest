using EvaGest.Data;
using EvaGest.Models;
using Microsoft.EntityFrameworkCore;

namespace EvaGest.Services;

/// <summary>
/// The simplest CRUD in the project, and deliberately the first one built (Phase 3):
/// it fixes the View -> ViewModel -> Service pattern that every later page repeats.
/// Deleting is conditional: an entry nothing points at is removed for real, while one
/// already used by an appointment or a sale is only deactivated, so those rows keep
/// their meaning. Either way it disappears from the pickers, which is what the user
/// wanted out of "delete".
/// </summary>
public class CatalogService(IDbContextFactory<ShopDbContext> factory) : ICatalogService
{
    public async Task<List<Service>> GetServices(bool onlyActive = false)
    {
        await using var db = await factory.CreateDbContextAsync();
        var query = db.Services.AsNoTracking().AsQueryable();
        if (onlyActive) query = query.Where(s => s.Active);
        return await query.OrderBy(s => s.Name).ToListAsync();
    }

    public async Task<Service?> GetService(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Services.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<int> CreateService(Service service)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.Services.Add(service);
        await db.SaveChangesAsync();
        return service.Id;
    }

    public async Task UpdateService(Service service)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.Services.Update(service);
        await db.SaveChangesAsync();
    }

    public async Task ChangeServiceStatus(int id, bool active)
    {
        await using var db = await factory.CreateDbContextAsync();
        var service = await db.Services.FirstAsync(s => s.Id == id);
        service.Active = active;
        await db.SaveChangesAsync();
    }

    public async Task<DeleteResult> DeleteService(int id)
    {
        await using var db = await factory.CreateDbContextAsync();

        var service = await db.Services.FirstOrDefaultAsync(s => s.Id == id);
        if (service is null) return DeleteResult.Deleted;

        // An appointment nulls its service on delete and a sale line would lose the link
        // its per-service report totals are built from: both count as history.
        bool used = await db.Appointments.AnyAsync(c => c.ServiceId == id)
                    || await db.SaleLines.AnyAsync(l => l.ServiceId == id);

        if (used)
        {
            service.Active = false;
            await db.SaveChangesAsync();
            return DeleteResult.Deactivated;
        }

        db.Services.Remove(service);
        await db.SaveChangesAsync();
        return DeleteResult.Deleted;
    }

    public async Task<List<Product>> GetProducts(bool onlyActive = false)
    {
        await using var db = await factory.CreateDbContextAsync();
        var query = db.Products.AsNoTracking().AsQueryable();
        if (onlyActive) query = query.Where(p => p.Active);
        return await query.OrderBy(p => p.Name).ToListAsync();
    }

    public async Task<Product?> GetProduct(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<int> CreateProduct(Product product)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product.Id;
    }

    public async Task UpdateProduct(Product product)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.Products.Update(product);
        await db.SaveChangesAsync();
    }

    public async Task ChangeProductStatus(int id, bool active)
    {
        await using var db = await factory.CreateDbContextAsync();
        var product = await db.Products.FirstAsync(p => p.Id == id);
        product.Active = active;
        await db.SaveChangesAsync();
    }

    public async Task<DeleteResult> DeleteProduct(int id)
    {
        await using var db = await factory.CreateDbContextAsync();

        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id);
        if (product is null) return DeleteResult.Deleted;

        bool used = await db.SaleLines.AnyAsync(l => l.ProductId == id);

        if (used)
        {
            product.Active = false;
            await db.SaveChangesAsync();
            return DeleteResult.Deactivated;
        }

        db.Products.Remove(product);
        await db.SaveChangesAsync();
        return DeleteResult.Deleted;
    }

    public async Task<List<PaymentMethod>> GetMethods(bool onlyActive = false)
    {
        await using var db = await factory.CreateDbContextAsync();
        var query = db.PaymentMethods.AsNoTracking().AsQueryable();
        if (onlyActive) query = query.Where(m => m.Active);
        return await query.OrderBy(m => m.Name).ToListAsync();
    }

    public async Task<int> CreateMethod(string name)
    {
        await using var db = await factory.CreateDbContextAsync();
        var method = new PaymentMethod { Name = name, Active = true };
        db.PaymentMethods.Add(method);
        await db.SaveChangesAsync();
        return method.Id;
    }

    public async Task UpdateMethod(PaymentMethod method)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.PaymentMethods.Update(method);
        await db.SaveChangesAsync();
    }

    public async Task<bool> CanDeactivateMethod(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        int remainingActive = await db.PaymentMethods.CountAsync(m => m.Active && m.Id != id);
        return remainingActive > 0;
    }

    public async Task ChangeMethodStatus(int id, bool active)
    {
        await using var db = await factory.CreateDbContextAsync();
        var method = await db.PaymentMethods.FirstAsync(m => m.Id == id);
        method.Active = active;
        await db.SaveChangesAsync();
    }

    public async Task<DeleteResult> DeleteMethod(int id)
    {
        await using var db = await factory.CreateDbContextAsync();

        var method = await db.PaymentMethods.FirstOrDefaultAsync(m => m.Id == id);
        if (method is null) return DeleteResult.Deleted;

        // Removing the last active one leaves no way to charge, exactly as deactivating
        // it would (pantalles 2.5), so the same guard covers both.
        bool isLastActive = method.Active
                            && !await db.PaymentMethods.AnyAsync(m => m.Active && m.Id != id);
        if (isLastActive) return DeleteResult.Blocked;

        // The foreign keys from sales and cash movements cascade: deleting a used method
        // would take the sales themselves with it, which is the opposite of the intent.
        bool used = await db.Sales.AnyAsync(v => v.PaymentMethodId == id)
                    || await db.CashMovements.AnyAsync(m => m.PaymentMethodId == id);

        if (used)
        {
            method.Active = false;
            await db.SaveChangesAsync();
            return DeleteResult.Deactivated;
        }

        db.PaymentMethods.Remove(method);
        await db.SaveChangesAsync();
        return DeleteResult.Deleted;
    }
}
