using Microsoft.EntityFrameworkCore;
using Shared.Models;

namespace Shared.Data;

public class GoPuffDbContext : DbContext
{
    public GoPuffDbContext(DbContextOptions<GoPuffDbContext> options) : base(options) { }

    public DbSet<FulfillmentCentre> FulfillmentCentres => Set<FulfillmentCentre>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<Inventory> Inventories => Set<Inventory>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FulfillmentCentre>(e =>
        {
            e.ToTable("fulfillment_centres");
            e.HasKey(f => f.Id);
            e.Property(f => f.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            e.Property(f => f.Name).HasColumnName("name");
            e.Property(f => f.Lat).HasColumnName("lat");
            e.Property(f => f.Lon).HasColumnName("lon");
            e.HasIndex(f => f.Lat).HasDatabaseName("idx_fc_lat");
            e.HasIndex(f => f.Lon).HasDatabaseName("idx_fc_lon");
        });

        modelBuilder.Entity<Item>(e =>
        {
            e.ToTable("items");
            e.HasKey(i => i.Id);
            e.Property(i => i.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            e.Property(i => i.Name).HasColumnName("name");
            e.HasIndex(i => i.Name).HasDatabaseName("idx_item_name");
        });

        modelBuilder.Entity<Inventory>(e =>
        {
            e.ToTable("inventories");
            e.HasKey(i => new { i.ItemId, i.FcId });
            e.Property(i => i.ItemId).HasColumnName("item_id");
            e.Property(i => i.FcId).HasColumnName("fc_id");
            e.Property(i => i.Quantity).HasColumnName("quantity");
            e.HasOne(i => i.Item).WithMany().HasForeignKey(i => i.ItemId);
            e.HasOne(i => i.FulfillmentCentre).WithMany().HasForeignKey(i => i.FcId);
        });

        modelBuilder.Entity<Order>(e =>
        {
            e.ToTable("orders");
            e.HasKey(o => o.Id);
            e.Property(o => o.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            e.Property(o => o.UserId).HasColumnName("user_id");
            e.Property(o => o.DeliveryLat).HasColumnName("delivery_lat");
            e.Property(o => o.DeliveryLon).HasColumnName("delivery_lon");
            e.Property(o => o.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<OrderItem>(e =>
        {
            e.ToTable("order_items");
            e.HasKey(oi => new { oi.OrderId, oi.ItemId, oi.FcId });
            e.Property(oi => oi.OrderId).HasColumnName("order_id");
            e.Property(oi => oi.ItemId).HasColumnName("item_id");
            e.Property(oi => oi.FcId).HasColumnName("fc_id");
            e.Property(oi => oi.Quantity).HasColumnName("quantity");
            e.HasOne(oi => oi.Item).WithMany().HasForeignKey(oi => oi.ItemId);
            e.HasOne(oi => oi.FulfillmentCentre).WithMany().HasForeignKey(oi => oi.FcId);
            e.HasOne<Order>().WithMany(o => o.Items).HasForeignKey(oi => oi.OrderId);
        });
    }
}
