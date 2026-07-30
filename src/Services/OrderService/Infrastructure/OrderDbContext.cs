using Microsoft.EntityFrameworkCore;
using OrderService.Domain;

namespace OrderService.Infrastructure;

public class OrderDbContext : DbContext
{
    public OrderDbContext(DbContextOptions<OrderDbContext> options)
        : base(options)
    {
    }

    public DbSet<Order> Orders { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CustomerName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Ignore(e => e.Total); // Propiedad calculada, no se persiste

            // Items como owned entities en su propia tabla (OrderItems)
            entity.OwnsMany(e => e.Items, item =>
            {
                item.WithOwner().HasForeignKey("OrderId");
                item.Property<int>("Id");
                item.HasKey("Id");
                item.ToTable("OrderItems");

                item.Property(i => i.ProductId).IsRequired();
                item.Property(i => i.ProductName).IsRequired().HasMaxLength(200);
                item.Property(i => i.UnitPrice).HasColumnType("decimal(18,2)");
                item.Property(i => i.Quantity).IsRequired();
            });

            entity.HasIndex(e => e.CreatedAt);
        });
    }
}