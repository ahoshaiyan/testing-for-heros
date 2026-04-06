using FoodApp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FoodApp.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Restaurant> Restaurants => Set<Restaurant>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<MenuItemImage> MenuItemImages => Set<MenuItemImage>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresExtension("postgis");

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Email).IsRequired().HasMaxLength(300);
            entity.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Name).IsRequired().HasMaxLength(200);
            entity.Property(r => r.Description).IsRequired().HasMaxLength(2000);
            entity.Property(r => r.IconUrl).HasMaxLength(500);
            entity.Property(r => r.BannerImageUrl).HasMaxLength(500);
            entity.HasIndex(r => r.Name);
        });

        modelBuilder.Entity<MenuItem>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Name).IsRequired().HasMaxLength(200);
            entity.Property(m => m.Description).IsRequired().HasMaxLength(2000);
            entity.Property(m => m.Price).HasPrecision(18, 2);
            entity.HasOne(m => m.Restaurant)
                  .WithMany(r => r.MenuItems)
                  .HasForeignKey(m => m.RestaurantId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(m => new { m.RestaurantId, m.SortOrder });
        });

        modelBuilder.Entity<MenuItemImage>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Url).IsRequired().HasMaxLength(500);
            entity.HasOne(i => i.MenuItem)
                  .WithMany(m => m.Images)
                  .HasForeignKey(i => i.MenuItemId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(i => new { i.MenuItemId, i.SortOrder });
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(o => o.Id);
            entity.Property(o => o.Status).HasConversion<string>();
            entity.Property(o => o.MessageToDriver).HasMaxLength(2000);
            entity.HasOne(o => o.User)
                  .WithMany()
                  .HasForeignKey(o => o.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(o => o.Restaurant)
                  .WithMany()
                  .HasForeignKey(o => o.RestaurantId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(o => o.UserId);
            entity.HasIndex(o => o.CreatedAt);
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(oi => oi.Id);
            entity.Property(oi => oi.UnitPrice).HasPrecision(18, 2);
            entity.Property(oi => oi.ItemName).IsRequired().HasMaxLength(200);
            entity.HasOne(oi => oi.Order)
                  .WithMany(o => o.Items)
                  .HasForeignKey(oi => oi.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(oi => oi.MenuItem)
                  .WithMany()
                  .HasForeignKey(oi => oi.MenuItemId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
