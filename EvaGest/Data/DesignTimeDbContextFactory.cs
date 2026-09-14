using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EvaGest.Data;

/// <summary>
/// Used only by the `dotnet ef` tools at design time. The running application builds
/// its DbContext through dependency injection instead (see App.xaml.cs).
/// Without this class, `dotnet ef migrations add` cannot construct the context.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ShopDbContext>
{
    public ShopDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ShopDbContext>()
            .UseSqlite("Data Source=disseny.db")   // never actually written to
            .UseSnakeCaseNamingConvention()
            .Options;

        return new ShopDbContext(options);
    }
}
