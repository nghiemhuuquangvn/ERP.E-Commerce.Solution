using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ERP.Identity.Domain.Entities;

namespace ERP.Identity.Infrastructure.Configurations;

public class ExternalLoginConfiguration : IEntityTypeConfiguration<ExternalLogin>
{
    public void Configure(EntityTypeBuilder<ExternalLogin> builder)
    {
        // =========================
        // TABLE
        // =========================
        builder.ToTable("ExternalLogins");

        // =========================
        // KEY
        // =========================
        builder.HasKey(el => el.Id);

        // =========================
        // PROPERTIES
        // =========================
        builder.Property(el => el.Provider)
               .IsRequired()
               .HasMaxLength(100);

        builder.Property(el => el.ProviderKey)
               .IsRequired()
               .HasMaxLength(200);

        builder.Property(el => el.Email)
               .IsRequired()
               .HasMaxLength(256);

        builder.Property(el => el.IsActive)
               .IsRequired();

        builder.Property(el => el.CreatedAt)
               .IsRequired();

        // =========================
        // RELATIONSHIPS
        // =========================
        builder.HasOne<User>()
               .WithMany(u => u.ExternalLogins)
               .HasForeignKey(el => el.UserId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}