using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RepairShop.Infrastructure.Persistence;

/// <summary>
/// Used only by the EF Core tooling (dotnet ef migrations ...). Never used at runtime.
/// Override the connection string with REPAIRSHOP_DESIGN_CONNECTION if needed.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<RepairShopDbContext>
{
    public RepairShopDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("REPAIRSHOP_DESIGN_CONNECTION")
                 ?? "Host=localhost;Port=5432;Database=repairshop_design;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<RepairShopDbContext>()
            .UseNpgsql(cs)
            .Options;

        return new RepairShopDbContext(options);
    }
}
