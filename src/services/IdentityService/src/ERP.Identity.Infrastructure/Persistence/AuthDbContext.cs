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
        public DbSet<SystemAuthSettings> SystemAuthSettings => Set<SystemAuthSettings>();
        public DbSet<TenantAuthSettings> TenantAuthSettings => Set<TenantAuthSettings>();
        public DbSet<SystemGeneralSettings> SystemGeneralSettings => Set<SystemGeneralSettings>();
        public DbSet<TenantGeneralSettings> TenantGeneralSettings => Set<TenantGeneralSettings>();
        public DbSet<TenantCompanySettings> TenantCompanySettings => Set<TenantCompanySettings>();
        public DbSet<SystemWorkflowSettings> SystemWorkflowSettings => Set<SystemWorkflowSettings>();
        public DbSet<TenantWorkflowSettings> TenantWorkflowSettings => Set<TenantWorkflowSettings>();
        public DbSet<SystemFileStorageSettings> SystemFileStorageSettings => Set<SystemFileStorageSettings>();
        public DbSet<TenantFileStorageSettings> TenantFileStorageSettings => Set<TenantFileStorageSettings>();
        public DbSet<SystemSchedulerSettings> SystemSchedulerSettings => Set<SystemSchedulerSettings>();
        public DbSet<TenantSchedulerSettings> TenantSchedulerSettings => Set<TenantSchedulerSettings>();
        public DbSet<SystemIntegrationSettings> SystemIntegrationSettings => Set<SystemIntegrationSettings>();
        public DbSet<TenantIntegrationSettings> TenantIntegrationSettings => Set<TenantIntegrationSettings>();
        public DbSet<SystemApiSettings> SystemApiSettings => Set<SystemApiSettings>();
        public DbSet<TenantApiSettings> TenantApiSettings => Set<TenantApiSettings>();
        public DbSet<SystemAuditSettings> SystemAuditSettings => Set<SystemAuditSettings>();
        public DbSet<TenantAuditSettings> TenantAuditSettings => Set<TenantAuditSettings>();
        public DbSet<SystemAdvancedSettings> SystemAdvancedSettings => Set<SystemAdvancedSettings>();
        public DbSet<TenantAdvancedSettings> TenantAdvancedSettings => Set<TenantAdvancedSettings>();
        public DbSet<SystemLocalizationSettings> SystemLocalizationSettings => Set<SystemLocalizationSettings>();
        public DbSet<TenantLocalizationSettings> TenantLocalizationSettings => Set<TenantLocalizationSettings>();
        public DbSet<SystemFeatureFlag> SystemFeatureFlags => Set<SystemFeatureFlag>();
        public DbSet<TenantFeatureFlagOverride> TenantFeatureFlagOverrides => Set<TenantFeatureFlagOverride>();
        public DbSet<TwoFactorChallenge> TwoFactorChallenges => Set<TwoFactorChallenge>();
        public DbSet<OAuthState> OAuthStates => Set<OAuthState>();
        public DbSet<SecurityAuditLog> SecurityAuditLogs => Set<SecurityAuditLog>();
        public DbSet<LoginAttempt> LoginAttempts => Set<LoginAttempt>();
        public DbSet<DeviceSession> DeviceSessions => Set<DeviceSession>();
        public DbSet<TokenBlacklistEntry> TokenBlacklist => Set<TokenBlacklistEntry>();
        public DbSet<PasswordHistoryEntry> PasswordHistories => Set<PasswordHistoryEntry>();
        public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
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
            modelBuilder.Entity<SystemAuthSettings>().Ignore(s => s.Description);
            modelBuilder.Entity<TenantAuthSettings>().Ignore(s => s.Description);
            modelBuilder.Entity<TwoFactorChallenge>().Ignore(c => c.Description);
            modelBuilder.Entity<OAuthState>().Ignore(o => o.Description);

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
                entity.HasKey(ur => new { ur.UserId, ur.RoleId, ur.TenantId });

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

            modelBuilder.Entity<SystemAuthSettings>(entity =>
            {
                entity.ToTable("SystemAuthSettings");
                entity.HasKey(s => s.Id);
            });

            modelBuilder.Entity<TenantAuthSettings>(entity =>
            {
                entity.ToTable("TenantAuthSettings");
                entity.HasKey(s => s.Id);
                entity.HasIndex(s => s.TenantId).IsUnique();
            });

            modelBuilder.Entity<SystemGeneralSettings>(entity =>
            {
                entity.ToTable("SystemGeneralSettings");
                entity.HasKey(s => s.Id);
                entity.Property(s => s.DefaultLanguageCode).IsRequired().HasMaxLength(10);
            });

            modelBuilder.Entity<TenantGeneralSettings>(entity =>
            {
                entity.ToTable("TenantGeneralSettings");
                entity.HasKey(s => s.Id);
                entity.Property(s => s.SiteName).HasMaxLength(200);
                entity.Property(s => s.DefaultLanguageCode).IsRequired().HasMaxLength(10);
                entity.Property(s => s.TimeZone).IsRequired().HasMaxLength(100);
                entity.Property(s => s.DateFormat).IsRequired().HasMaxLength(20);
                entity.HasIndex(s => s.TenantId).IsUnique();
            });

            modelBuilder.Entity<TenantCompanySettings>(entity =>
            {
                entity.ToTable("TenantCompanySettings");
                entity.HasKey(s => s.Id);
                entity.Property(s => s.CompanyName).HasMaxLength(200);
                entity.Property(s => s.LegalName).HasMaxLength(200);
                entity.Property(s => s.TaxCode).HasMaxLength(50);
                entity.Property(s => s.Address).HasMaxLength(500);
                entity.Property(s => s.Phone).HasMaxLength(50);
                entity.Property(s => s.Email).HasMaxLength(200);
                entity.Property(s => s.LogoUrl).HasMaxLength(500);
                entity.Property(s => s.Website).HasMaxLength(200);
                entity.HasIndex(s => s.TenantId).IsUnique();
            });

            modelBuilder.Entity<SystemWorkflowSettings>(entity =>
            {
                entity.ToTable("SystemWorkflowSettings");
                entity.HasKey(s => s.Id);
            });

            modelBuilder.Entity<TenantWorkflowSettings>(entity =>
            {
                entity.ToTable("TenantWorkflowSettings");
                entity.HasKey(s => s.Id);
                entity.Property(s => s.AutoApproveThresholdAmount).HasColumnType("decimal(18,2)");
                entity.HasIndex(s => s.TenantId).IsUnique();
            });

            modelBuilder.Entity<SystemFileStorageSettings>(entity =>
            {
                entity.ToTable("SystemFileStorageSettings");
                entity.HasKey(s => s.Id);
            });

            modelBuilder.Entity<TenantFileStorageSettings>(entity =>
            {
                entity.ToTable("TenantFileStorageSettings");
                entity.HasKey(s => s.Id);
                entity.Property(s => s.Provider).IsRequired().HasMaxLength(50);
                entity.Property(s => s.CdnBaseUrl).HasMaxLength(500);
                entity.HasIndex(s => s.TenantId).IsUnique();
            });

            modelBuilder.Entity<SystemSchedulerSettings>(entity =>
            {
                entity.ToTable("SystemSchedulerSettings");
                entity.HasKey(s => s.Id);
            });

            modelBuilder.Entity<TenantSchedulerSettings>(entity =>
            {
                entity.ToTable("TenantSchedulerSettings");
                entity.HasKey(s => s.Id);
                entity.HasIndex(s => s.TenantId).IsUnique();
            });

            modelBuilder.Entity<SystemIntegrationSettings>(entity =>
            {
                entity.ToTable("SystemIntegrationSettings");
                entity.HasKey(s => s.Id);
            });

            modelBuilder.Entity<TenantIntegrationSettings>(entity =>
            {
                entity.ToTable("TenantIntegrationSettings");
                entity.HasKey(s => s.Id);
                entity.Property(s => s.ExternalApiBaseUrl).HasMaxLength(500);
                entity.Property(s => s.ExternalApiKeyEncrypted).HasMaxLength(500);
                entity.HasIndex(s => s.TenantId).IsUnique();
            });

            modelBuilder.Entity<SystemApiSettings>(entity =>
            {
                entity.ToTable("SystemApiSettings");
                entity.HasKey(s => s.Id);
            });

            modelBuilder.Entity<TenantApiSettings>(entity =>
            {
                entity.ToTable("TenantApiSettings");
                entity.HasKey(s => s.Id);
                entity.HasIndex(s => s.TenantId).IsUnique();
            });

            modelBuilder.Entity<SystemAuditSettings>(entity =>
            {
                entity.ToTable("SystemAuditSettings");
                entity.HasKey(s => s.Id);
            });

            modelBuilder.Entity<TenantAuditSettings>(entity =>
            {
                entity.ToTable("TenantAuditSettings");
                entity.HasKey(s => s.Id);
                entity.HasIndex(s => s.TenantId).IsUnique();
            });

            modelBuilder.Entity<SystemAdvancedSettings>(entity =>
            {
                entity.ToTable("SystemAdvancedSettings");
                entity.HasKey(s => s.Id);
            });

            modelBuilder.Entity<TenantAdvancedSettings>(entity =>
            {
                entity.ToTable("TenantAdvancedSettings");
                entity.HasKey(s => s.Id);
                entity.Property(s => s.CustomCssUrl).HasMaxLength(500);
                entity.HasIndex(s => s.TenantId).IsUnique();
            });

            modelBuilder.Entity<SystemLocalizationSettings>(entity =>
            {
                entity.ToTable("SystemLocalizationSettings");
                entity.HasKey(s => s.Id);
                entity.Property(s => s.DefaultLocale).IsRequired().HasMaxLength(20);
            });

            modelBuilder.Entity<TenantLocalizationSettings>(entity =>
            {
                entity.ToTable("TenantLocalizationSettings");
                entity.HasKey(s => s.Id);
                entity.Property(s => s.DefaultLocale).IsRequired().HasMaxLength(20);
                entity.Property(s => s.SupportedLocales).IsRequired().HasMaxLength(500);
                entity.Property(s => s.NumberFormatLocale).IsRequired().HasMaxLength(20);
                entity.HasIndex(s => s.TenantId).IsUnique();
            });

            modelBuilder.Entity<SystemFeatureFlag>(entity =>
            {
                entity.ToTable("SystemFeatureFlags");
                entity.HasKey(f => f.Id);
                entity.Property(f => f.Key).IsRequired().HasMaxLength(200);
                entity.Property(f => f.Description).HasMaxLength(500);
                entity.HasIndex(f => f.Key).IsUnique();
            });

            modelBuilder.Entity<TenantFeatureFlagOverride>(entity =>
            {
                entity.ToTable("TenantFeatureFlagOverrides");
                entity.HasKey(f => f.Id);
                entity.Property(f => f.Key).IsRequired().HasMaxLength(200);
                entity.HasIndex(f => new { f.TenantId, f.Key }).IsUnique();
            });

            modelBuilder.Entity<TwoFactorChallenge>(entity =>
            {
                entity.ToTable("TwoFactorChallenges");
                entity.HasKey(c => c.Id);
                entity.Property(c => c.CodeHash).IsRequired().HasMaxLength(500);
                entity.Property(c => c.Method).IsRequired().HasMaxLength(16);
                entity.Property(c => c.Destination).HasMaxLength(256);
                entity.HasIndex(c => new { c.UserId, c.ExpiredAt });
            });

            modelBuilder.Entity<OAuthState>(entity =>
            {
                entity.ToTable("OAuthStates");
                entity.HasKey(o => o.Id);
                entity.Property(o => o.State).IsRequired().HasMaxLength(256);
                entity.Property(o => o.CodeVerifier).IsRequired().HasMaxLength(256);
                entity.Property(o => o.TenantCode).IsRequired().HasMaxLength(256);
                entity.HasIndex(o => o.State).IsUnique();
            });

            modelBuilder.Entity<SecurityAuditLog>(entity =>
            {
                entity.ToTable("SecurityAuditLogs");
                entity.HasKey(l => l.Id);
                entity.Property(l => l.EventType).IsRequired().HasMaxLength(100);
                entity.HasIndex(l => new { l.UserId, l.OccurredAt });
                entity.HasIndex(l => new { l.TenantId, l.OccurredAt });
                entity.HasIndex(l => new { l.EventType, l.OccurredAt });
            });

            modelBuilder.Entity<LoginAttempt>(entity =>
            {
                entity.ToTable("LoginAttempts");
                entity.HasKey(l => l.Id);
                entity.Property(l => l.IpAddress).IsRequired().HasMaxLength(64);
                entity.HasIndex(l => new { l.IpAddress, l.AttemptedAt });
                entity.HasIndex(l => new { l.Email, l.AttemptedAt });
            });

            modelBuilder.Entity<DeviceSession>(entity =>
            {
                entity.ToTable("DeviceSessions");
                entity.HasKey(d => d.Id);
                entity.Property(d => d.DeviceId).IsRequired().HasMaxLength(256);
                entity.HasIndex(d => new { d.UserId, d.DeviceId }).IsUnique();
                entity.HasIndex(d => new { d.UserId, d.IsActive, d.LastActiveAt });
            });

            modelBuilder.Entity<TokenBlacklistEntry>(entity =>
            {
                entity.ToTable("TokenBlacklist");
                entity.HasKey(t => t.Jti);
                entity.Property(t => t.Jti).HasMaxLength(200);
                entity.HasIndex(t => t.ExpiresAt);
                entity.HasIndex(t => t.UserId);
            });

            modelBuilder.Entity<PasswordHistoryEntry>(entity =>
            {
                entity.ToTable("PasswordHistories");
                entity.HasKey(p => p.Id);
                entity.Property(p => p.PasswordHash).IsUnicode(false).IsRequired().HasMaxLength(500);
                entity.HasIndex(p => new { p.UserId, p.CreatedAt });
            });

            modelBuilder.Entity<ApiKey>(entity =>
            {
                entity.ToTable("ApiKeys");
                entity.HasKey(k => k.Id);
                entity.Property(k => k.Name).IsRequired().HasMaxLength(200);
                entity.Property(k => k.KeyPrefix).IsRequired().HasMaxLength(16);
                entity.Property(k => k.KeyHash).IsUnicode(false).IsRequired().HasMaxLength(500);
                entity.HasIndex(k => k.KeyPrefix).IsUnique();
                entity.HasIndex(k => k.TenantId);
            });
        }
    }
}
