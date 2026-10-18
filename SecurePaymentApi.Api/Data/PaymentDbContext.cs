
using Microsoft.EntityFrameworkCore;
using SecurePaymentApi.Api.Models;

namespace SecurePaymentApi.Api.Data;

public class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options)
        : base(options)
    {
    }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(entity =>
        {
            entity.Property(a => a.AccountHolderId)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(a => a.Balance)
                .HasPrecision(18, 2);

            entity.Property(a => a.Version)
                .IsConcurrencyToken();
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.Property(p => p.Amount)
                .HasPrecision(18, 2);

            entity.Property(p => p.Status)
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.HasOne<Account>()
                .WithMany()
                .HasForeignKey(p => p.FromAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Account>()
                .WithMany()
                .HasForeignKey(p => p.ToAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(p => p.CreatedAtUtc);

            entity.Property(p => p.IdempotencyKey)
                .IsRequired()
                .HasMaxLength(100);

            entity.HasIndex(p => p.IdempotencyKey)
                .IsUnique();
        });
    }
}
