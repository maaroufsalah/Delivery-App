using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class BankAccountConfiguration : IEntityTypeConfiguration<BankAccount>
{
    public void Configure(EntityTypeBuilder<BankAccount> builder)
    {
        builder.ToTable("bank_accounts");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.BankId).HasColumnName("bankId").IsRequired();
        builder.Property(e => e.Label).HasColumnName("label").IsRequired();
        builder.Property(e => e.Rib).HasColumnName("rib").HasMaxLength(24);
        builder.Property(e => e.Iban).HasColumnName("iban");
        builder.Property(e => e.Holder).HasColumnName("holder");
        builder.Property(e => e.IsActive).HasColumnName("isActive").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.Property(e => e.UpdatedAt).HasColumnName("updatedAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.UpdatedAt, true);
        builder.Property(e => e.DeletedAt).HasColumnName("deletedAt").HasColumnType("timestamptz(3)");
        builder.HasIndex(e => e.Label).IsUnique();
        builder.HasIndex(e => e.BankId);
        builder.HasOne(e => e.Bank).WithMany(e => e.Accounts).HasForeignKey(e => e.BankId).OnDelete(DeleteBehavior.Restrict).IsRequired();
    }
}
