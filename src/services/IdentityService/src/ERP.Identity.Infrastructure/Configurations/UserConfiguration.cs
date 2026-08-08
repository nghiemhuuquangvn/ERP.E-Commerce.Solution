using ERP.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        // =========================
        // TABLE
        // =========================
        builder.ToTable("Users");

        // =========================
        // KEY
        // =========================
        builder.HasKey(u => u.Id);

        // =========================
        // PROPERTIES
        // =========================
        builder.Property(u => u.Email)
               .IsRequired()
               .HasMaxLength(256);

        builder.Property(u => u.PasswordHash)
               .IsRequired()
               .HasMaxLength(500);

        builder.Property(u => u.IsActive)
               .IsRequired();

        builder.Property(u => u.CreatedAt)
               .IsRequired();

        // =========================
        // USER ROLES (FIELD MAPPING)
        // =========================
        builder
            .HasMany<UserRole>("UserRoles")
            .WithOne()
            .HasForeignKey(ur => ur.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .Navigation("UserRoles")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // =========================
        // INDEXES
        // =========================
        builder.HasIndex(u => new { u.Email, u.TenantId }).IsUnique();
    }
}
