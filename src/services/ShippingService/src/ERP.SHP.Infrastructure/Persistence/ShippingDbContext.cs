using ERP.SHP.Domain.Entities;
using ERP.SHP.Domain.Entities.Settings;
using ERP.Shared.Core.Domain;
using ERP.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace ERP.SHP.Infrastructure.Persistence;

public class ShippingDbContext : DbContext
{
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<Carrier> Carriers => Set<Carrier>();
    public DbSet<ReturnShipment> ReturnShipments => Set<ReturnShipment>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<SystemShippingSettings> SystemShippingSettings => Set<SystemShippingSettings>();
    public DbSet<TenantShippingSettings> TenantShippingSettings => Set<TenantShippingSettings>();

    public ShippingDbContext(DbContextOptions<ShippingDbContext> options) : base(options) { }

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
            e.Property(x => x.TotalWeight).HasPrecision(10, 3);
            e.HasIndex(x => x.OrderId);
            e.HasIndex(x => x.Status);
            e.OwnsMany(x => x.Items, item =>
            {
                item.HasKey(i => i.Id);
                item.Property(i => i.Id).ValueGeneratedNever();
                item.Property(i => i.ProductName).IsRequired().HasMaxLength(300);
                item.Property(i => i.Weight).HasPrecision(10, 3);
                item.ToTable("ShipmentItems");
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
            e.HasIndex(x => x.Code).IsUnique();
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
            e.HasIndex(x => x.TenantId).IsUnique();
        });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
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
}
