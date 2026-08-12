using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ProductManagementSystem.Api.Models;

namespace ProductManagementSystem.Api.Data;

public class ApplicationDbContext : IdentityDbContext<IdentityUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<ApprovedProductsCache> ApprovedProductsCache => Set<ApprovedProductsCache>();
    public DbSet<ProductApprovalHistory> ProductApprovalHistory => Set<ProductApprovalHistory>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Product>(entity =>
        {
            entity.Property(p => p.Name).IsRequired().HasMaxLength(200);
            entity.Property(p => p.Description).HasMaxLength(2000);
            entity.Property(p => p.Price).HasPrecision(18, 2);
            entity.Property(p => p.CreatedBy).IsRequired().HasMaxLength(100);
            entity.Property(p => p.UpdatedBy).IsRequired().HasMaxLength(100);
            entity.HasIndex(p => p.Status);
            entity.HasIndex(p => p.CreatedBy);
        });

        builder.Entity<ApprovedProductsCache>(entity =>
        {
            entity.Property(p => p.Name).IsRequired().HasMaxLength(200);
            entity.Property(p => p.Price).HasPrecision(18, 2);
            entity.Property(p => p.ApprovedBy).IsRequired().HasMaxLength(100);
            entity.HasIndex(p => p.ProductId);
        });

        builder.Entity<ProductApprovalHistory>(entity =>
        {
            entity.Property(h => h.Action).IsRequired().HasMaxLength(50);
            entity.Property(h => h.ActorId).IsRequired().HasMaxLength(100);
            entity.Property(h => h.ActorName).IsRequired().HasMaxLength(200);
            entity.Property(h => h.Note).HasMaxLength(500);
            entity.HasOne(h => h.Product)
                .WithMany(p => p.History)
                .HasForeignKey(h => h.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
