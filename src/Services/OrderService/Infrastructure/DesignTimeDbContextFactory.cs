using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OrderService.Infrastructure;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<OrderDbContext>
{
    public OrderDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<OrderDbContext>();

        var connectionString = "Host=localhost;Port=5432;Database=microservices_db;Username=postgres;Password=postgres";
        optionsBuilder.UseNpgsql(connectionString);

        return new OrderDbContext(optionsBuilder.Options);
    }
}