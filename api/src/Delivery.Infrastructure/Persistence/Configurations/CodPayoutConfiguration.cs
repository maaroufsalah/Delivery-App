using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class CodPayoutConfiguration : IEntityTypeConfiguration<CodPayout>
{
    public void Configure(EntityTypeBuilder<CodPayout> builder)
    {
        builder.ToTable("cod_payouts");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.Code).HasColumnName("code").IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(e => e.ClientId).HasColumnName("clientId").IsRequired();
        builder.Property(e => e.AgencyId).HasColumnName("agencyId").IsRequired();
        builder.Property(e => e.Amount).HasColumnName("amount").HasPrecision(12, 2).IsRequired();
        builder.Property(e => e.Method).HasColumnName("method").HasConversion<string>().IsRequired();
        builder.Property(e => e.BankAccountId).HasColumnName("bankAccountId");
        builder.Property(e => e.Reference).HasColumnName("reference");
        builder.Property(e => e.Note).HasColumnName("note");
        builder.Property(e => e.ProofName).HasColumnName("proofName");
        builder.Property(e => e.ProofMimeType).HasColumnName("proofMimeType");
        builder.Property(e => e.ProofPath).HasColumnName("proofPath");
        builder.Property(e => e.PaidAt).HasColumnName("paidAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.PaidById).HasColumnName("paidById");
        builder.Property(e => e.CreatedById).HasColumnName("createdById");
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.Property(e => e.UpdatedAt).HasColumnName("updatedAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.UpdatedAt, true);
        builder.HasIndex(e => e.Code).IsUnique();
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => new { e.ClientId, e.CreatedAt });
        builder.HasIndex(e => new { e.AgencyId, e.CreatedAt });
        builder.HasOne(e => e.Client).WithMany(e => e.CodPayouts).HasForeignKey(e => e.ClientId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(e => e.Agency).WithMany(e => e.CodPayouts).HasForeignKey(e => e.AgencyId).OnDelete(DeleteBehavior.Restrict).IsRequired();
        builder.HasOne(e => e.BankAccount).WithMany(e => e.CodPayouts).HasForeignKey(e => e.BankAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.PaidBy).WithMany(e => e.PaidCodPayouts).HasForeignKey(e => e.PaidById).OnDelete(DeleteBehavior.SetNull);
    }
}
