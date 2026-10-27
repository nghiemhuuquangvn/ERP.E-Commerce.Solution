using ERP.Shared.Infrastructure.Data;
using ERP.SHP.Domain.Entities;
using ERP.SHP.Domain.Entities.Settings;
using ERP.Shared.Core.Domain;
using ERP.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace ERP.SHP.Infrastructure.Persistence;

public class ShippingDbContext : DbContext, ITenantScopedDbContext
{
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<Carrier> Carriers => Set<Carrier>();
    public DbSet<ReturnShipment> ReturnShipments => Set<ReturnShipment>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<SystemShippingSettings> SystemShippingSettings => Set<SystemShippingSettings>();
    public DbSet<TenantShippingSettings> TenantShippingSettings => Set<TenantShippingSettings>();
    public DbSet<TenantCarrierCredential> TenantCarrierCredentials => Set<TenantCarrierCredential>();
    public DbSet<CarrierSelectionRule> CarrierSelectionRules => Set<CarrierSelectionRule>();
    public DbSet<CarrierWebhookLog> CarrierWebhookLogs => Set<CarrierWebhookLog>();

    private readonly ITenantProvider _tenantProvider;

    /// <summary>
    /// Tenant of the current request (JWT / Gateway host tenant) or consumer message
    /// (TenantScope). Read by the tenant query filter on every query and used to
    /// stamp/validate every save.
    /// </summary>
    public Guid? CurrentTenantId => _tenantProvider.CurrentTenantId;

    public ShippingDbContext(DbContextOptions<ShippingDbContext> options, ITenantProvider tenantProvider) : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Shipment>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.CarrierName).IsRequired().HasMaxLength(200);
            e.Property(x => x.TrackingNumber).HasMaxLength(100);
            e.Property(x => x.RecipientName).IsRequired().HasMaxLength(200);
            e.Property(x => x.RecipientPhone).HasMaxLength(50);
            e.Property(x => x.AddressStreet).IsRequired().HasMaxLength(300);
            e.Property(x => x.AddressWard).HasMaxLength(100);
            e.Property(x => x.AddressDistrict).IsRequired().HasMaxLength(100);
            e.Property(x => x.AddressProvince).IsRequired().HasMaxLength(100);
            e.Property(x => x.AddressPostalCode).HasMaxLength(20);
            e.Property(x => x.AddressCountry).IsRequired().HasMaxLength(100);
            e.Property(x => x.CancelReason).HasMaxLength(500);
            e.Property(x => x.ReceivedByName).HasMaxLength(200);
            e.Property(x => x.SignatureUrl).HasMaxLength(1000);
            e.Property(x => x.PhotoUrl).HasMaxLength(1000);
            e.Property(x => x.CodAmount).HasPrecision(18, 2);
            e.Property(x => x.DeclaredValue).HasPrecision(18, 2);
            e.Property(x => x.CarrierFee).HasPrecision(18, 2);
            e.Property(x => x.CarrierOrderCode).HasMaxLength(100);
            e.Property(x => x.SelectionNote).HasMaxLength(1000);
            e.Property(x => x.BookingError).HasMaxLength(1000);
            e.Property(x => x.ReviewNote).HasMaxLength(500);
            e.HasIndex(x => x.TrackingNumber);
            // Columns created by 001_CreateShippingDB.sql carry the unit / older name.
            e.Property(x => x.TotalWeight).HasPrecision(10, 3).HasColumnName("TotalWeightKg");
            e.Property(x => x.DeliveredAt).HasColumnName("ActualDelivery");
            e.HasIndex(x => x.OrderId);
            e.HasIndex(x => x.Status);
            e.OwnsMany(x => x.Items, item =>
            {
                item.HasKey(i => i.Id);
                item.Property(i => i.Id).ValueGeneratedNever();
                item.Property(i => i.ProductName).IsRequired().HasMaxLength(300);
                item.Property(i => i.Weight).HasPrecision(10, 3);
                item.ToTable("ShipmentItems");
                item.Property(i => i.Weight).HasPrecision(10, 3).HasColumnName("WeightKg");
            });
            e.OwnsMany(x => x.TrackingEvents, ev =>
            {
                ev.HasKey(t => t.Id);
                ev.Property(t => t.Id).ValueGeneratedNever();
                ev.Property(t => t.Status).IsRequired().HasMaxLength(50);
                ev.Property(t => t.Location).IsRequired().HasMaxLength(200);
                ev.Property(t => t.Description).IsRequired().HasMaxLength(500);
                ev.ToTable("ShipmentTrackingEvents");
            });
        });

        modelBuilder.Entity<Carrier>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.Code).IsRequired().HasMaxLength(20);
            e.Property(x => x.BaseRatePerKg).HasPrecision(10, 4);
            e.Property(x => x.InterProvinceSurcharge).HasPrecision(10, 2);
            e.Property(x => x.OriginProvince).HasMaxLength(100);
            e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique(); // carrier codes are per tenant
        });

        modelBuilder.Entity<ReturnShipment>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Reason).IsRequired().HasMaxLength(500);
            e.Property(x => x.TrackingNumber).HasMaxLength(100);
            e.HasIndex(x => x.OriginalShipmentId);
            e.HasIndex(x => x.OrderId);
        });

        modelBuilder.Entity<OutboxMessage>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => x.ProcessedOnUtc);
        });

        modelBuilder.Entity<SystemShippingSettings>(e =>
        {
            e.ToTable("SystemShippingSettings");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<TenantShippingSettings>(e =>
        {
            e.ToTable("TenantShippingSettings");
            e.HasKey(x => x.Id);
            e.Property(x => x.DefaultShippingFee).HasPrecision(18, 2);
            e.Property(x => x.FreeShippingThreshold).HasPrecision(18, 2);
            e.Property(x => x.SenderName).HasMaxLength(200);
            e.Property(x => x.SenderPhone).HasMaxLength(50);
            e.Property(x => x.SenderAddress).HasMaxLength(300);
            e.Property(x => x.SenderWard).HasMaxLength(100);
            e.Property(x => x.SenderDistrict).HasMaxLength(100);
            e.Property(x => x.SenderProvince).HasMaxLength(100);
            e.HasIndex(x => x.TenantId).IsUnique();
        });

        modelBuilder.Entity<TenantCarrierCredential>(e =>
        {
            e.ToTable("TenantCarrierCredentials");
            e.HasKey(x => x.Id);
            e.Property(x => x.CarrierCode).HasMaxLength(20).IsRequired();
            e.Property(x => x.PublicId).HasMaxLength(200);
            e.Property(x => x.Username).HasMaxLength(200);
            e.Property(x => x.Secret1Encrypted).HasMaxLength(1000);
            e.Property(x => x.Secret2Encrypted).HasMaxLength(1000);
            e.HasIndex(x => new { x.TenantId, x.CarrierCode }).IsUnique();
        });

        modelBuilder.Entity<CarrierSelectionRule>(e =>
        {
            e.ToTable("CarrierSelectionRules");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.MatchProvinces).HasMaxLength(1000);
            e.Property(x => x.MinOrderValue).HasPrecision(18, 2);
            e.Property(x => x.MaxOrderValue).HasPrecision(18, 2);
            e.Property(x => x.CarrierCode).HasMaxLength(20).IsRequired();
            e.HasIndex(x => new { x.TenantId, x.Priority });
        });

        modelBuilder.Entity<CarrierWebhookLog>(e =>
        {
            e.ToTable("CarrierWebhookLogs");
            e.HasKey(x => x.Id);
            e.Property(x => x.CarrierCode).HasMaxLength(20).IsRequired();
            e.Property(x => x.TrackingCode).HasMaxLength(100).IsRequired();
            e.Property(x => x.CarrierStatus).HasMaxLength(50).IsRequired();
            e.HasIndex(x => new { x.TenantId, x.CarrierCode, x.TrackingCode, x.CarrierStatus, x.ActionTime }).IsUnique();
        });

        // Last, so it composes with any per-entity filter configured above.
        modelBuilder.ApplyTenantQueryFilters(this);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Stamp/validate the tenant first, so the events serialized below carry it.
        ChangeTracker.EnforceTenantOnSave(CurrentTenantId);
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
            EventTenant.StampFrom(entry.Entity);

        var domainEntities = ChangeTracker.Entries<BaseEntity>()
            .Where(e => e.Entity.DomainEvents.Any()).ToList();

        var outboxMessages = domainEntities
            .SelectMany(e => e.Entity.DomainEvents)
            .Select(ev => new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = ev.GetType().AssemblyQualifiedName ?? ev.GetType().Name,
                Content = JsonConvert.SerializeObject(ev, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All }),
                OccurredOnUtc = ev.OccurredOn
            }).ToList();

        domainEntities.ForEach(e => e.Entity.ClearDomainEvents());

        var result = await base.SaveChangesAsync(cancellationToken);

        if (outboxMessages.Any())
        {
            OutboxMessages.AddRange(outboxMessages);
            await base.SaveChangesAsync(cancellationToken);
        }

        return result;
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ChangeTracker.EnforceTenantOnSave(CurrentTenantId);
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ChangeTracker.EnforceTenantOnSave(CurrentTenantId);
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}
