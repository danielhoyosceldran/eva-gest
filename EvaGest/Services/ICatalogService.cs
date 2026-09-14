using EvaGest.Models;

namespace EvaGest.Services;

public interface ICatalogService
{
    // --- Services ---
    Task<List<Service>> GetServices(bool onlyActive = false);
    Task<Service?> GetService(int id);
    Task<int> CreateService(Service service);
    Task UpdateService(Service service);
    Task ChangeServiceStatus(int id, bool active);

    /// <summary>
    /// Removes the service, or deactivates it when an appointment or a sale line already
    /// points at it: deleting it there would empty a row of the history. Either way it
    /// stops being offered when booking or charging.
    /// </summary>
    Task<DeleteResult> DeleteService(int id);

    // --- Products ---
    Task<List<Product>> GetProducts(bool onlyActive = false);
    Task<Product?> GetProduct(int id);
    Task<int> CreateProduct(Product product);
    Task UpdateProduct(Product product);
    Task ChangeProductStatus(int id, bool active);

    /// <summary>Same rule as <see cref="DeleteService"/>: sold products are deactivated,
    /// never removed.</summary>
    Task<DeleteResult> DeleteProduct(int id);

    // --- Payment methods ---
    Task<List<PaymentMethod>> GetMethods(bool onlyActive = false);
    Task<int> CreateMethod(string name);
    Task UpdateMethod(PaymentMethod method);

    /// <summary>
    /// Deactivating the last active payment method would make it impossible to take
    /// money, so the service refuses it and the UI explains why (pantalles 2.5).
    /// </summary>
    Task<bool> CanDeactivateMethod(int id);
    Task ChangeMethodStatus(int id, bool active);

    /// <summary>
    /// Removes the payment method, or deactivates it when a sale or a cash movement uses
    /// it. Returns <see cref="DeleteResult.Blocked"/> for the last active one:
    /// with none left there would be no way to take money (pantalles 2.5).
    /// </summary>
    Task<DeleteResult> DeleteMethod(int id);
}
