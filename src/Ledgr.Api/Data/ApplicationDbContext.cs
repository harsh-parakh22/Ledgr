using Ledgr.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Ledgr.Api.Data;

public class ApplicationDbContext : DbContext
{
    private static readonly ValueConverter<DateTime, DateTime> UtcConverter = new(
        value => value.ToUniversalTime(),
        value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static readonly ValueConverter<DateTime?, DateTime?> NullableUtcConverter = new(
        value => value.HasValue ? value.Value.ToUniversalTime() : value,
        value => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : value);

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();

    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(product =>
        {
            product.Property(p => p.Sku).HasMaxLength(50).IsRequired();
            product.Property(p => p.Name).HasMaxLength(150).IsRequired();
            product.Property(p => p.Description).HasMaxLength(500);
            product.Property(p => p.Category).HasMaxLength(80).IsRequired();
            product.Property(p => p.UnitPrice).HasPrecision(18, 2);
            product.Property(p => p.CreatedAtUtc).HasConversion(UtcConverter);
            product.Property(p => p.UpdatedAtUtc).HasConversion(NullableUtcConverter);
            product.Property(p => p.Version).IsConcurrencyToken();

            product.HasIndex(p => p.Sku).IsUnique();
            product.HasIndex(p => new { p.Category, p.IsActive });
            product.ToTable(t => t.HasCheckConstraint("CK_Products_QuantityOnHand", "QuantityOnHand >= 0"));
        });

        modelBuilder.Entity<InventoryMovement>(movement =>
        {
            movement.Property(m => m.Reason).HasMaxLength(250).IsRequired();
            movement.Property(m => m.ReferenceId).HasMaxLength(64);
            movement.Property(m => m.CreatedAtUtc).HasConversion(UtcConverter);

            movement
                .HasOne(m => m.Product)
                .WithMany(p => p.Movements)
                .HasForeignKey(m => m.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            movement.HasIndex(m => new { m.ProductId, m.CreatedAtUtc });
        });

        modelBuilder.Entity<Order>(order =>
        {
            order.Property(o => o.OrderNumber).HasMaxLength(50).IsRequired();
            order.Property(o => o.IdempotencyKey).HasMaxLength(64).IsRequired();
            order.Property(o => o.CustomerName).HasMaxLength(120).IsRequired();
            order.Property(o => o.CustomerEmail).HasMaxLength(150);
            order.Property(o => o.TotalAmount).HasPrecision(18, 2);
            order.Property(o => o.CreatedAtUtc).HasConversion(UtcConverter);
            order.Property(o => o.CancelledAtUtc).HasConversion(NullableUtcConverter);
            order.Property(o => o.Version).IsConcurrencyToken();

            order.HasIndex(o => o.OrderNumber).IsUnique();
            order.HasIndex(o => o.IdempotencyKey).IsUnique();
            order.HasIndex(o => o.CreatedAtUtc);
        });

        modelBuilder.Entity<OrderItem>(item =>
        {
            item.Property(i => i.UnitPrice).HasPrecision(18, 2);

            item
                .HasOne(i => i.Order)
                .WithMany(o => o.Items)
                .HasForeignKey(i => i.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            item
                .HasOne(i => i.Product)
                .WithMany(p => p.OrderItems)
                .HasForeignKey(i => i.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PrepareChanges();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        PrepareChanges();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void PrepareChanges()
    {
        foreach (var entry in ChangeTracker.Entries<InventoryMovement>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException("Inventory movements are append-only.");
            }
        }

        // The concurrency token is bumped here rather than in each service, so a new
        // write path cannot forget to participate in optimistic concurrency checks.
        foreach (var entry in ChangeTracker.Entries<Product>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.Version++;
            }
        }

        foreach (var entry in ChangeTracker.Entries<Order>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.Version++;
            }
        }
    }
}
