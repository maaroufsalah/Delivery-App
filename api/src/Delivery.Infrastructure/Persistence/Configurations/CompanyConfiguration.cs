using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations;

internal sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("companies");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(e => e.Slug).HasColumnName("slug").IsRequired();
        builder.HasIndex(e => e.Slug).IsUnique();
        builder.Property(e => e.CustomDomain).HasColumnName("customDomain");
        builder.HasIndex(e => e.CustomDomain).IsUnique();
        builder.Property(e => e.Name).HasColumnName("name").IsRequired();
        builder.Property(e => e.ShortName).HasColumnName("shortName").HasMaxLength(20);
        builder.Property(e => e.LegalName).HasColumnName("legalName");
        builder.Property(e => e.TaxId).HasColumnName("taxId");
        builder.Property(e => e.Rc).HasColumnName("rc");
        builder.Property(e => e.ProLegalName).HasColumnName("proLegalName");
        builder.Property(e => e.ProTaxId).HasColumnName("proTaxId");
        builder.Property(e => e.Address).HasColumnName("address");
        builder.Property(e => e.City).HasColumnName("city");
        builder.Property(e => e.Phone).HasColumnName("phone");
        builder.Property(e => e.Email).HasColumnName("email");
        builder.Property(e => e.Website).HasColumnName("website");
        builder.Property(e => e.Country).HasColumnName("country").HasMaxLength(2).IsFixedLength().IsRequired();
        builder.Property(e => e.Currency).HasColumnName("currency").HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(e => e.Timezone).HasColumnName("timezone").IsRequired();
        builder.Property(e => e.DefaultLocale).HasColumnName("defaultLocale").HasMaxLength(8).IsRequired();
        builder.Property(e => e.Locales).HasColumnName("locales");
        builder.Property(e => e.LogoUrl).HasColumnName("logoUrl");
        builder.Property(e => e.LogoBackground).HasColumnName("logoBackground").HasMaxLength(9);
        builder.Property(e => e.IconUrl).HasColumnName("iconUrl");
        builder.Property(e => e.PrimaryColor).HasColumnName("primaryColor").HasMaxLength(9).IsRequired();
        builder.Property(e => e.SecondaryColor).HasColumnName("secondaryColor").HasMaxLength(9).IsRequired();
        builder.Property(e => e.Theme).HasColumnName("theme").HasColumnType("jsonb");
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(e => e.TrialEndsAt).HasColumnName("trialEndsAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.Plan).HasColumnName("plan");
        builder.Property(e => e.LastActivityAt).HasColumnName("lastActivityAt").HasColumnType("timestamptz(3)");
        builder.Property(e => e.CreatedAt).HasColumnName("createdAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.DefaultNow, true);
        builder.Property(e => e.UpdatedAt).HasColumnName("updatedAt").HasColumnType("timestamptz(3)").IsRequired().HasAnnotation(PersistenceAnnotations.UpdatedAt, true);
    }
}
