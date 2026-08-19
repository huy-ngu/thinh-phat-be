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
            entity.Property(p => p.ImageUrl).HasMaxLength(500).IsRequired();
            entity.Property(p => p.ImagePublicId).HasMaxLength(200);
            entity.Property(p => p.CreatedAtUtc).HasDefaultValueSql("timezone('utc', now())");
            entity.HasIndex(p => p.Name);
        });

        // Order (Đơn chốt) Configuration
        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("orders");
            entity.HasKey(o => o.Id);
            entity.Property(o => o.Address).HasMaxLength(300).IsRequired();
            entity.Property(o => o.TotalAmount).HasPrecision(18, 2).IsRequired();
            entity.Property(o => o.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(o => o.Note).HasMaxLength(500);
            entity.Property(o => o.CreatedAtUtc).HasDefaultValueSql("timezone('utc', now())");

            entity.HasIndex(o => o.Status);
            entity.HasIndex(o => new { o.CustomerId, o.CreatedAtUtc });
            entity.HasIndex(o => new { o.ProductId, o.CreatedAtUtc });

            // Relationships
            entity.HasOne(o => o.Product)
                  .WithMany()
                  .HasForeignKey(o => o.ProductId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(o => o.Customer)
                  .WithMany()
                  .HasForeignKey(o => o.CustomerId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
