using ERP.NTF.Domain.Entities;
using ERP.NTF.Domain.Entities.Settings;
using ERP.Shared.Core.Domain;
using ERP.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace ERP.NTF.Infrastructure.Persistence;

public class NotificationDbContext : DbContext
{
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationTemplate> Templates => Set<NotificationTemplate>();
    public DbSet<NotificationPreference> Preferences => Set<NotificationPreference>();
    public DbSet<WebhookEndpoint> WebhookEndpoints => Set<WebhookEndpoint>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<SystemNotificationSettings> SystemNotificationSettings => Set<SystemNotificationSettings>();
    public DbSet<TenantNotificationSettings> TenantNotificationSettings => Set<TenantNotificationSettings>();
    public DbSet<SystemEmailSettings> SystemEmailSettings => Set<SystemEmailSettings>();
    public DbSet<TenantEmailSettings> TenantEmailSettings => Set<TenantEmailSettings>();
    public DbSet<SystemSmsSettings> SystemSmsSettings => Set<SystemSmsSettings>();
    public DbSet<TenantSmsSettings> TenantSmsSettings => Set<TenantSmsSettings>();

    public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Notification>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.RecipientId).IsRequired().HasMaxLength(200);
            e.Property(x => x.RecipientEmail).IsRequired().HasMaxLength(320);
            e.Property(x => x.Subject).IsRequired().HasMaxLength(500);
            e.Property(x => x.Body).IsRequired();
            // Live column is "LastError" (001_CreateNotificationDB.sql) but the C#
            // property is "ErrorMessage" — same class of bug as NotificationPreference
            // .OptOutAt below; unmapped, this 500'd with "Invalid column name
            // 'ErrorMessage'" the moment any notification was actually inserted.
            e.Property(x => x.ErrorMessage).HasColumnName("LastError").HasMaxLength(2000);
            e.Property(x => x.IsRead);
            e.Property(x => x.ReadAt);
            e.HasIndex(x => x.RecipientId);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => new { x.Status, x.ScheduledFor });
            e.OwnsMany(x => x.Logs, log =>
            {
                log.HasKey(l => l.Id);
                log.Property(l => l.Id).ValueGeneratedNever();
                log.Property(l => l.EventType).IsRequired().HasMaxLength(100);
                log.Property(l => l.Details).IsRequired().HasMaxLength(1000);
                log.ToTable("NotificationLogs");
            });
        });

        modelBuilder.Entity<NotificationTemplate>(e =>
        {
            // DbSet<NotificationTemplate> is exposed as "Templates" (see property above), which
            // EF would otherwise use as the table name by convention — but the actual table
            // (001_CreateNotificationDB.sql) is "NotificationTemplates", so every read/write
            // 500'd with "Invalid object name 'Templates'".
            e.ToTable("NotificationTemplates");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Code).IsRequired().HasMaxLength(100);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.Subject).IsRequired().HasMaxLength(500);
            e.Property(x => x.Body).IsRequired();
            e.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<NotificationPreference>(e =>
        {
            // Same class of bug as NotificationTemplate above: DbSet property is "Preferences",
            // actual table is "NotificationPreferences".
            e.ToTable("NotificationPreferences");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            // The live column is "OptedOutAt" (001_CreateNotificationDB.sql) but the C#
            // property is "OptOutAt" — unmapped, this 500'd with "Invalid column name
            // 'OptOutAt'" the moment any preference row was actually queried.
            e.Property(x => x.OptOutAt).HasColumnName("OptedOutAt");
            e.HasIndex(x => x.UserId).IsUnique();
        });

        modelBuilder.Entity<WebhookEndpoint>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.Url).IsRequired().HasMaxLength(2000);
            e.Property(x => x.Secret).HasMaxLength(500);
            e.Property(x => x.EventTypesRaw).HasColumnName("EventTypes").HasMaxLength(1000);
            e.Ignore(x => x.EventTypes);
            e.HasIndex(x => x.IsActive);
        });

        modelBuilder.Entity<OutboxMessage>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => x.ProcessedOnUtc);
        });

        modelBuilder.Entity<SystemNotificationSettings>(e =>
        {
            e.ToTable("SystemNotificationSettings");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<TenantNotificationSettings>(e =>
        {
            e.ToTable("TenantNotificationSettings");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.TenantId).IsUnique();
        });

        modelBuilder.Entity<SystemEmailSettings>(e =>
        {
            e.ToTable("SystemEmailSettings");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<TenantEmailSettings>(e =>
        {
            e.ToTable("TenantEmailSettings");
            e.HasKey(x => x.Id);
            e.Property(x => x.SmtpHost).HasMaxLength(255);
            e.Property(x => x.SmtpUsername).HasMaxLength(255);
            e.Property(x => x.SmtpPasswordEncrypted).HasMaxLength(500);
            e.Property(x => x.FromAddress).HasMaxLength(320);
            e.Property(x => x.FromName).HasMaxLength(200);
            e.HasIndex(x => x.TenantId).IsUnique();
        });

        modelBuilder.Entity<SystemSmsSettings>(e =>
        {
            e.ToTable("SystemSmsSettings");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<TenantSmsSettings>(e =>
        {
            e.ToTable("TenantSmsSettings");
            e.HasKey(x => x.Id);
            e.Property(x => x.ProviderName).HasMaxLength(100);
            e.Property(x => x.ApiKeyEncrypted).HasMaxLength(500);
            e.Property(x => x.FromNumber).HasMaxLength(32);
            e.HasIndex(x => x.TenantId).IsUnique();
        });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var domainEntities = ChangeTracker.Entries<BaseEntity>()
            .Where(e => e.Entity.DomainEvents.Any())
            .ToList();

        var outboxMessages = domainEntities
            .SelectMany(e => e.Entity.DomainEvents)
            .Select(domainEvent => new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = domainEvent.GetType().AssemblyQualifiedName ?? domainEvent.GetType().Name,
                Content = JsonConvert.SerializeObject(domainEvent, new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.All
                }),
                OccurredOnUtc = domainEvent.OccurredOn
            })
            .ToList();

        domainEntities.ForEach(e => e.Entity.ClearDomainEvents());

        var result = await base.SaveChangesAsync(cancellationToken);

        if (outboxMessages.Any())
        {
            OutboxMessages.AddRange(outboxMessages);
            await base.SaveChangesAsync(cancellationToken);
        }

        return result;
    }
}
