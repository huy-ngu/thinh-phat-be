using Microsoft.EntityFrameworkCore;
using ThinhPhat.Api.Data.Entities;
using ThinhPhat.Api.Shared.Constants;

namespace ThinhPhat.Api.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderStatusHistory> OrderStatusHistories => Set<OrderStatusHistory>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Role Configuration & Seed
        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("roles");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Name).HasMaxLength(50).IsRequired();
            entity.HasIndex(r => r.Name).IsUnique();
            entity.Property(r => r.Description).HasMaxLength(250);

            // Seed default roles
            entity.HasData(
                new Role
                {
                    Id = RoleConstants.AdminRoleId,
                    Name = RoleConstants.Admin,
                    Description = "System Administrator with full access"
                },
                new Role
                {
                    Id = RoleConstants.UserRoleId,
                    Name = RoleConstants.User,
                    Description = "Standard regular user"
                }
            );
        });

        // User Configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Email).HasMaxLength(256).IsRequired();
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.PasswordHash).HasMaxLength(256).IsRequired();
            entity.Property(u => u.FullName).HasMaxLength(120).IsRequired();
            entity.Property(u => u.CreatedAtUtc).HasDefaultValueSql("timezone('utc', now())");

            // Relationship with Role
            entity.HasOne(u => u.Role)
                  .WithMany(r => r.Users)
                  .HasForeignKey(u => u.RoleId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // RefreshToken Configuration
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");
            entity.HasKey(rt => rt.Id);
            entity.Property(rt => rt.Token).HasMaxLength(256).IsRequired();
            entity.HasIndex(rt => rt.Token).IsUnique();
            entity.HasIndex(rt => new { rt.UserId, rt.CreatedAtUtc });
            entity.Property(rt => rt.ReplacedByToken).HasMaxLength(256);
            entity.Property(rt => rt.CreatedAtUtc).HasDefaultValueSql("timezone('utc', now())");

            // Relationship with User
            entity.HasOne(rt => rt.User)
                  .WithMany(u => u.RefreshTokens)
                  .HasForeignKey(rt => rt.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Customer Configuration
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("customers");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name).HasMaxLength(150).IsRequired();
            entity.Property(c => c.PhoneNumber).HasMaxLength(20).IsRequired();
            
            // Đảm bảo chỉ có 1 số điện thoại duy nhất trong toàn hệ thống
            entity.HasIndex(c => c.PhoneNumber).IsUnique();
            
            entity.Property(c => c.CreatedAtUtc).HasDefaultValueSql("timezone('utc', now())");
        });

        // Product Configuration
        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("products");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Name).HasMaxLength(200).IsRequired();
            entity.Property(p => p.Price).HasPrecision(18, 2).IsRequired();
            entity.Property(p => p.CostPrice).HasPrecision(18, 2).HasDefaultValue(0m);
            entity.Property(p => p.ImageUrl).HasMaxLength(500).IsRequired();
            entity.Property(p => p.ImagePublicId).HasMaxLength(200);
            entity.Property(p => p.IsDeleted).HasDefaultValue(false).IsRequired();
            entity.Property(p => p.DeletedAtUtc);
            entity.Property(p => p.CreatedAtUtc).HasDefaultValueSql("timezone('utc', now())");

            entity.HasIndex(p => p.Name);
            entity.HasIndex(p => p.IsDeleted);

            // Global Query Filter cho Soft Delete
            entity.HasQueryFilter(p => !p.IsDeleted);
        });

        // Order (Đơn hàng tích hợp Telesale & GHTK) Configuration
        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("orders");
            entity.HasKey(o => o.Id);
            entity.Property(o => o.Province).HasMaxLength(100).IsRequired().HasDefaultValue(string.Empty);
            entity.Property(o => o.District).HasMaxLength(100).IsRequired().HasDefaultValue(string.Empty);
            entity.Property(o => o.Ward).HasMaxLength(100);
            entity.Property(o => o.Address).HasMaxLength(300).IsRequired();
            entity.Property(o => o.TotalAmount).HasPrecision(18, 2).IsRequired();
            entity.Property(o => o.ShippingFee).HasPrecision(18, 2);
            entity.Property(o => o.Weight).HasPrecision(10, 2);
            entity.Property(o => o.GhtkId).HasMaxLength(100);
            entity.Property(o => o.EstimatedPickTime).HasMaxLength(100);
            entity.Property(o => o.EstimatedDeliverTime).HasMaxLength(100);
            entity.Property(o => o.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
            entity.Property(o => o.Note).HasMaxLength(500);
            entity.Property(o => o.CreatedAtUtc).HasDefaultValueSql("timezone('utc', now())");

            // Indexes
            entity.HasIndex(o => o.Status);
            entity.HasIndex(o => o.GhtkId);
            entity.HasIndex(o => o.CustomerId);
            entity.HasIndex(o => o.WarehouseId);
            entity.HasIndex(o => o.CreatedAtUtc);
            entity.HasIndex(o => new { o.Status, o.CreatedAtUtc });

            // Relationships
            entity.HasOne(o => o.Customer)
                  .WithMany()
                  .HasForeignKey(o => o.CustomerId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(o => o.Warehouse)
                  .WithMany()
                  .HasForeignKey(o => o.WarehouseId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(o => o.Items)
                  .WithOne(i => i.Order)
                  .HasForeignKey(i => i.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(o => o.StatusHistories)
                  .WithOne(h => h.Order)
                  .HasForeignKey(h => h.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // OrderItem (Chi tiết sản phẩm đơn hàng) Configuration
        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.ToTable("order_items");
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Quantity).IsRequired();
            entity.Property(i => i.UnitPrice).HasPrecision(18, 2).IsRequired();
            entity.Property(i => i.TotalPrice).HasPrecision(18, 2).IsRequired();

            entity.HasIndex(i => i.OrderId);
            entity.HasIndex(i => i.ProductId);

            // Relationship with Product
            entity.HasOne(i => i.Product)
                  .WithMany()
                  .HasForeignKey(i => i.ProductId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // OrderStatusHistory (Lịch sử trạng thái đơn hàng) Configuration
        modelBuilder.Entity<OrderStatusHistory>(entity =>
        {
            entity.ToTable("order_status_histories");
            entity.HasKey(h => h.Id);
            entity.Property(h => h.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
            entity.Property(h => h.Note).HasMaxLength(500);
            entity.Property(h => h.CreatedAtUtc).HasDefaultValueSql("timezone('utc', now())");

            entity.HasIndex(h => new { h.OrderId, h.CreatedAtUtc });
            entity.HasIndex(h => h.Status);
        });

        // Warehouse (Kho hàng) Configuration
        modelBuilder.Entity<Warehouse>(entity =>
        {
            entity.ToTable("warehouses");
            entity.HasKey(w => w.Id);
            entity.Property(w => w.Name).HasMaxLength(200).IsRequired();
            entity.Property(w => w.Province).HasMaxLength(100).IsRequired().HasDefaultValue(string.Empty);
            entity.Property(w => w.District).HasMaxLength(100).IsRequired().HasDefaultValue(string.Empty);
            entity.Property(w => w.Ward).HasMaxLength(100);
            entity.Property(w => w.Address).HasMaxLength(300).IsRequired();
            entity.Property(w => w.PhoneNumber).HasMaxLength(20).IsRequired();
            entity.Property(w => w.CreatedAtUtc).HasDefaultValueSql("timezone('utc', now())");

            entity.HasIndex(w => w.Name);
            entity.HasIndex(w => w.PhoneNumber);
        });
    }
}
