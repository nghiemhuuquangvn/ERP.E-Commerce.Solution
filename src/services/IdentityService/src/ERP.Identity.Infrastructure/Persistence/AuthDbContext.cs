using ERP.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using ERP.Identity.Application.Interfaces;
using ERP.Identity.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ERP.Identity.Infrastructure.Persistence
{
    public class AuthDbContext : DbContext
    {
        public DbSet<User> Users => Set<User>();
        public DbSet<Role> Roles => Set<Role>();
        public DbSet<Permission> Permissions => Set<Permission>();
        public DbSet<UserRole> UserRoles => Set<UserRole>();
        public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
        public DbSet<ExternalLogin> ExternalLogins => Set<ExternalLogin>();
        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
        public DbSet<AuthTenant> Tenants => Set<AuthTenant>();
        public DbSet<EmailConfirmToken> EmailConfirmTokens => Set<EmailConfirmToken>();
        public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
        private readonly IDomainEventDispatcher _domainEventDispatcher;

        public AuthDbContext(DbContextOptions<AuthDbContext> options, IDomainEventDispatcher domainEventDispatcher) : base(options)
        {
            _domainEventDispatcher = domainEventDispatcher;
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            // 1. Cập nhật các trường Audit (CreatedDate, ModifiedDate...)
            UpdateAuditFields();

            // 2. Xử lý TỰ ĐỘNG ATTACH cho các thực thể mồ côi (Fix lỗi Principal Entity)
            AutoAttachPrincipalEntities();

            // 3. Thu thập Domain Events và chuyển thành OutboxMessages
            await ProcessDomainEventsToOutboxAsync(cancellationToken);

            // 4. Lưu tất cả trong một Transaction duy nhất
            try 
            {
                return await base.SaveChangesAsync(cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                // Debug nhanh xem có thực thể nào đang gây lỗi
                var debugInfo = ChangeTracker.Entries()
                    .Select(e => $"Entity: {e.Metadata.Name}, State: {e.State}");
                throw new Exception($"EF Core Save Error. Details: {string.Join(" | ", debugInfo)}", ex);
            }
        }

        private void AutoAttachPrincipalEntities()
        {
            var addedEntries = ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added)
                .ToList();

            foreach (var entry in addedEntries)
            {
                foreach (var foreignKey in entry.Metadata.GetForeignKeys())
                {
                    var fkPropertyName = foreignKey.Properties.Select(p => p.Name).FirstOrDefault();
                    if (fkPropertyName == null) continue;

                    var fkValue = entry.CurrentValues[fkPropertyName];

                    if (fkValue is Guid guidValue && guidValue != Guid.Empty)
                    {
                        var principalType = foreignKey.PrincipalEntityType.ClrType;

                        // Kiểm tra xem ID này đã được track chưa
                        var trackedPrincipal = ChangeTracker.Entries()
                            .FirstOrDefault(e => e.Metadata.ClrType == principalType && 
                                                e.Property("Id")?.CurrentValue?.Equals(guidValue) == true);

                        object principalEntity;

                        if (trackedPrincipal == null)
                        {
                            // Tạo Stub và dùng Set(Type).Attach để EF nhận diện qua DbSet
                            principalEntity = Activator.CreateInstance(principalType)!;
                            principalType.GetProperty("Id")?.SetValue(principalEntity, guidValue);
                            
                            Entry(principalEntity).State = EntityState.Unchanged;
                        }
                        else
                        {
                           principalEntity = trackedPrincipal.Entity;
                        }

                        // Gán Navigation để EF Core không báo "Principal unknown"
                        var navigation = foreignKey.DependentToPrincipal;
                        if (navigation != null)
                        {
                            entry.Reference(navigation.Name).CurrentValue = principalEntity;
                        }
                    }
                }
            }
        }

        private async Task ProcessDomainEventsToOutboxAsync(CancellationToken ct)
        {
            var domainEvents = ChangeTracker.Entries<BaseEntity>()
                .Select(x => x.Entity)
                .SelectMany(x =>
                {
                    var events = x.DomainEvents.ToList();
                    x.ClearDomainEvents();
                    return events;
                })
                .Select(domainEvent => new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    OccurredOnUtc = DateTime.UtcNow,
                    Type = domainEvent.GetType().Name,
                    Content = JsonConvert.SerializeObject(domainEvent, new JsonSerializerSettings
                    {
                        TypeNameHandling = TypeNameHandling.All
                    })
                })
                .ToList();

            if (domainEvents.Any())
            {
                await Set<OutboxMessage>().AddRangeAsync(domainEvents, ct);
            }
        }

        private void UpdateAuditFields()
        {
            var entries = ChangeTracker.Entries<BaseEntity>();
            var utcNow = DateTime.UtcNow;

            foreach (var entry in entries)
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Property("CreatedAt").CurrentValue = utcNow;
                    entry.Property("UpdatedAt").CurrentValue = utcNow;
                    entry.Property("IsDeleted").CurrentValue = false;
                }
                else if (entry.State == EntityState.Modified)
                {
                    entry.Property("UpdatedAt").CurrentValue = utcNow;
                }
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Ignore Description property from BaseEntity for all entity types
            modelBuilder.Entity<User>().Ignore(u => u.Description);
            modelBuilder.Entity<Role>().Ignore(r => r.Description);
            modelBuilder.Entity<Permission>().Ignore(p => p.Description);
            modelBuilder.Entity<AuthTenant>().Ignore(t => t.Description);
            modelBuilder.Entity<RefreshToken>().Ignore(rt => rt.Description);
            modelBuilder.Entity<ExternalLogin>().Ignore(el => el.Description);
            modelBuilder.Entity<EmailConfirmToken>().Ignore(et => et.Description);
            modelBuilder.Entity<PasswordResetToken>().Ignore(prt => prt.Description);

            // 1. User Configuration
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("Users");
                entity.HasKey(u => u.Id);
                entity.HasIndex(u => u.Email).IsUnique();
                entity.Property(u => u.Email).IsRequired().HasMaxLength(256);
                entity.Property(u => u.PasswordHash).IsUnicode(false).IsRequired().HasMaxLength(500);
                entity.Property(u => u.IsActive).IsRequired();
                entity.HasMany(u => u.UserRoles)
                    .WithOne(ur => ur.User)
                    .HasForeignKey(ur => ur.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(u => u.UserPermissions)
                    .WithOne(up => up.User)
                    .HasForeignKey(up => up.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(u => u.UserTenants)
                    .WithOne(ut => ut.User)
                    .HasForeignKey(ut => ut.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Metadata.FindNavigation(nameof(User.UserRoles))
                            ?.SetPropertyAccessMode(PropertyAccessMode.Field);

            });

            modelBuilder.Entity<Role>(entity =>
            {
                entity.ToTable("Roles");
                entity.HasKey(r => r.Id);
                entity.Property(r => r.RoleCode).IsRequired().HasMaxLength(256);
                entity.Property(r => r.RoleName).IsRequired().HasMaxLength(256);

                entity.HasMany(r => r.UserRoles)
                    .WithOne(ur => ur.Role)
                    .HasForeignKey(ur => ur.RoleId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(r => r.RolePermissions)
                    .WithOne(rp => rp.Role)
                    .HasForeignKey(rp => rp.RoleId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<UserRole>(entity =>
            {
                entity.ToTable("UserRoles");
                entity.HasKey(ur => new { ur.UserId, ur.RoleId });

                entity.HasOne(ur => ur.Role)
                    .WithMany(r => r.UserRoles)
                    .HasForeignKey(ur => ur.RoleId);

                entity.HasOne(ur => ur.User)
                    .WithMany(u => u.UserRoles)
                    .HasForeignKey(ur => ur.UserId);
            });

            modelBuilder.Entity<RolePermission>(entity =>
            {
                entity.ToTable("RolePermissions");
                entity.HasKey(rp => new { rp.RoleId, rp.PermissionId });
            });

            modelBuilder.Entity<AuthUserTenant>(entity =>
            {
                entity.ToTable("UserTenants");
                entity.HasKey(ut => new { ut.UserId, ut.TenantId });

                // Chỉ định rõ ràng mối quan hệ để EF không tự suy diễn tên cột
                entity.HasOne(ut => ut.User)
                    .WithMany(u => u.UserTenants)
                    .HasForeignKey(ut => ut.UserId);

                entity.HasOne(ut => ut.Tenant)
                    .WithMany() // Nếu Tenant không có danh sách UserTenants
                    .HasForeignKey(ut => ut.TenantId);
            });

            modelBuilder.Entity<Permission>(entity =>
            {
                entity.ToTable("Permissions");
                entity.HasKey(p => p.Id);
                entity.Property(p => p.PermissionCode).IsRequired().HasMaxLength(256);
                entity.Property(p => p.PermissionName).IsRequired().HasMaxLength(256);

                entity.HasMany(p => p.RolePermissions)
                    .WithOne(rp => rp.Permission)
                    .HasForeignKey(rp => rp.PermissionId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<AuthTenant>(entity =>
            {
                entity.ToTable("Tenants");
                entity.HasKey(t => t.Id);
                entity.Property(t => t.TenantCode).IsRequired().HasMaxLength(256);
                entity.Property(t => t.TenantName).IsRequired().HasMaxLength(256);
                
                entity.HasMany(t => t.UserTenants)
                    .WithOne(ut => ut.Tenant)
                    .HasForeignKey(ut => ut.TenantId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<UserPermission>(entity =>
            {
                entity.ToTable("UserPermissions");
                entity.HasKey(up => new { up.UserId, up.PermissionId });
            });

            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.ToTable("RefreshTokens");
                entity.HasKey(rt => rt.Id);
                entity.HasIndex(rt => rt.Token).IsUnique();
            });

            modelBuilder.Entity<ExternalLogin>(entity =>
            {
                entity.ToTable("ExternalLogins");
                entity.HasKey(el => el.Id);
                entity.HasIndex(el => new { el.Provider, el.ProviderKey }).IsUnique();
            });

            modelBuilder.Entity<EmailConfirmToken>(entity =>
            {
                entity.ToTable("EmailConfirmTokens");
                entity.HasKey(et => et.Id);
                entity.Property(et => et.UserId).IsRequired();
                entity.Property(et => et.Token).IsRequired().HasMaxLength(500);
                entity.Property(et => et.ExpiredAt).IsRequired();
                entity.Property(et => et.ConfirmedAt).IsRequired(false);
            });

            modelBuilder.Entity<PasswordResetToken>(entity =>
            {
                entity.ToTable("PasswordResetTokens");
                entity.HasKey(prt => prt.Id);
                entity.Property(prt => prt.UserId).IsRequired();
                entity.Property(prt => prt.Token).IsRequired().HasMaxLength(500);
                entity.Property(prt => prt.ExpiredAt).IsRequired();
                entity.Property(prt => prt.UsedAt).IsRequired(false);
            });

            modelBuilder.Entity<OutboxMessage>(entity =>
            {
                entity.ToTable("OutboxMessages");
                entity.HasKey(om => om.Id);
                entity.HasIndex(om => om.ProcessedOnUtc).HasFilter("[ProcessedOnUtc] IS NULL");
            });
        }
    }
}
