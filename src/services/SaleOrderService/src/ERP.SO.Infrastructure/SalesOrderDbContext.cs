using ERP.SO.Domain.Entities;
using ERP.SO.Domain.Entities.Settings;
using ERP.SO.Domain.Sagas;
using ERP.Shared.Infrastructure.Data;
using ERP.Shared.Messaging;
using Microsoft.EntityFrameworkCore;

namespace ERP.SO.Infrastructure
{
    public class SalesOrderDbContext : DbContext, ITenantScopedDbContext
    {
        private readonly ITenantProvider _tenantProvider;

        public SalesOrderDbContext(DbContextOptions<SalesOrderDbContext> options, ITenantProvider tenantProvider)
            : base(options)
        {
            _tenantProvider = tenantProvider;
        }

        /// <summary>
        /// Tenant of the current request (JWT / Gateway host) or consumer message (TenantScope).
        /// Read by the tenant query filter on every query and used to stamp/validate every save.
        /// </summary>
        public Guid? CurrentTenantId => _tenantProvider.CurrentTenantId;

        public DbSet<SalesOrder> SalesOrders { get; set; } = null!;
        public DbSet<OrderNumberSequence> OrderNumberSequences { get; set; } = null!;
        public DbSet<CustomerSalesProfile> CustomerSalesProfiles { get; set; } = null!;
        public DbSet<CommissionPlan> CommissionPlans { get; set; } = null!;
        public DbSet<CommissionRecord> CommissionRecords { get; set; } = null!;
        public DbSet<SalesQuotation> SalesQuotations { get; set; } = null!;
        public DbSet<OrderFulfillmentSagaState> OrderFulfillmentSagas { get; set; } = null!;
        public DbSet<SystemCartCheckoutSettings> SystemCartCheckoutSettings { get; set; } = null!;
        public DbSet<TenantCartCheckoutSettings> TenantCartCheckoutSettings { get; set; } = null!;
        public DbSet<SystemOrderSettings> SystemOrderSettings { get; set; } = null!;
        public DbSet<TenantOrderSettings> TenantOrderSettings { get; set; } = null!;

        /// <summary>
        /// Outbox for the isolated Settings-changed Kafka publish path
        /// (<see cref="ERP.SO.Infrastructure.BackgroundJobs.SettingsOutboxProcessor"/>).
        /// Distinct from, and does not affect, the RabbitMQ order-fulfillment event flow.
        /// </summary>
        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<SalesOrder>(eb =>
            {
                eb.HasKey(e => e.Id);
                eb.Property(e => e.Id).ValueGeneratedNever();
                eb.Property(e => e.CustomerId).IsRequired();
                eb.Property(e => e.CustomerName).IsRequired().HasMaxLength(200);
                eb.Property(e => e.CustomerEmail).IsRequired().HasMaxLength(255);
                eb.Property(e => e.OrderDate).IsRequired();
                eb.Property(e => e.Status).IsRequired();
                eb.Property(e => e.TotalAmount).IsRequired().HasPrecision(18, 2);
                eb.Property(e => e.Notes).HasMaxLength(1000);
                eb.Property(e => e.TrackingNumber).HasMaxLength(200);
                eb.Property(e => e.Carrier).HasMaxLength(200);
                eb.Property(e => e.ReturnReason).HasMaxLength(1000);
                eb.Property(e => e.RefundAmount).HasPrecision(18, 2);
                eb.Property(e => e.OrderNo).IsRequired().HasMaxLength(50);
                eb.Property(e => e.OrderSource).IsRequired();
                eb.Property(e => e.PaymentMethod).HasMaxLength(20);
                eb.Property(e => e.DiscountAmount).IsRequired().HasPrecision(18, 2);

                eb.HasIndex(e => new { e.TenantId, e.OrderNo }).IsUnique().HasFilter("[OrderNo] <> ''"); // numbering is per tenant

                eb.OwnsOne(e => e.ShippingAddress, address =>
                {
                    address.Property(a => a.Street).IsRequired().HasMaxLength(255);
                    address.Property(a => a.City).IsRequired().HasMaxLength(100);
                    address.Property(a => a.State).IsRequired().HasMaxLength(100);
                    address.Property(a => a.PostalCode).IsRequired().HasMaxLength(20);
                    address.Property(a => a.Country).IsRequired().HasMaxLength(50);
                });

                eb.OwnsMany(e => e.LineItems, li =>
                {
                    li.ToTable("SalesOrderItems");
                    li.WithOwner().HasForeignKey("OrderId");
                    // Shadow "Id" key with no explicit type defaults to int by convention, but
                    // the actual column (001_CreateSalesDB.sql) is uniqueidentifier — every read
                    // that included this collection 500'd with "Unable to cast object of type
                    // 'System.Guid' to type 'System.Int32'" the moment any order actually had
                    // line items/notes/status history to include (empty tables never hit it).
                    li.Property<Guid>("Id");
                    li.HasKey("Id");
                    li.Property(p => p.ProductId).IsRequired();
                    li.Property(p => p.ProductName).IsRequired().HasMaxLength(500);
                    li.Property(p => p.Quantity).IsRequired();
                    li.Property(p => p.UnitPrice).IsRequired().HasPrecision(18, 2);
                    li.Property(p => p.ShippedQuantity).IsRequired();
                    li.Ignore(p => p.RemainingQuantity);
                    li.Ignore(p => p.LineTotal);
                });

                eb.OwnsMany(e => e.InternalNotes, n =>
                {
                    n.ToTable("OrderNotes");
                    n.WithOwner().HasForeignKey("OrderId");
                    // Same class of bug as LineItems above — OrderNotes.Id is uniqueidentifier.
                    n.Property<Guid>("Id");
                    n.HasKey("Id");
                    n.Property(p => p.Note).IsRequired().HasMaxLength(2000);
                    n.Property(p => p.AddedBy).HasMaxLength(200);
                    n.Property(p => p.CreatedAt).IsRequired();
                });

                eb.OwnsMany(e => e.StatusHistory, h =>
                {
                    h.ToTable("OrderStatusHistory");
                    h.WithOwner().HasForeignKey("OrderId");
                    // Same class of bug as LineItems above — OrderStatusHistory.Id is uniqueidentifier.
                    h.Property<Guid>("Id");
                    h.HasKey("Id");
                    h.Property(p => p.FromStatus).IsRequired();
                    h.Property(p => p.ToStatus).IsRequired();
                    h.Property(p => p.Reason).HasMaxLength(1000);
                    h.Property(p => p.ChangedBy).HasMaxLength(200);
                    h.Property(p => p.ChangedAt).IsRequired();
                });

                eb.HasIndex(e => e.CustomerId);
                eb.HasIndex(e => e.Status);
                eb.HasIndex(e => e.OrderDate);
            });

            modelBuilder.Entity<CustomerSalesProfile>(eb =>
            {
                eb.ToTable("CustomerSalesProfiles");
                eb.HasKey(e => e.Id);
                eb.Property(e => e.CreditLimit).IsRequired().HasPrecision(18, 2);
                eb.Property(e => e.CurrentBalance).IsRequired().HasPrecision(18, 2);
                eb.HasIndex(e => new { e.TenantId, e.CustomerId }).IsUnique();
            });

            modelBuilder.Entity<CommissionPlan>(eb =>
            {
                eb.ToTable("CommissionPlans");
                eb.HasKey(e => e.Id);
                eb.Property(e => e.Name).IsRequired().HasMaxLength(200);
                eb.Property(e => e.RateType).IsRequired();
                eb.Property(e => e.RateValue).IsRequired().HasPrecision(9, 6);
                eb.Property(e => e.IsActive).IsRequired();
            });

            modelBuilder.Entity<CommissionRecord>(eb =>
            {
                eb.ToTable("CommissionRecords");
                eb.HasKey(e => e.Id);
                eb.Property(e => e.PlanId).IsRequired();
                eb.Property(e => e.SalesRepId).IsRequired();
                eb.Property(e => e.OrderId).IsRequired();
                eb.Property(e => e.Amount).IsRequired().HasPrecision(18, 2);
                eb.Property(e => e.Status).IsRequired();
                eb.Property(e => e.CalculatedAt).IsRequired();

                eb.HasIndex(e => e.SalesRepId);
                eb.HasIndex(e => e.OrderId).IsUnique();
            });

            modelBuilder.Entity<SalesQuotation>(eb =>
            {
                eb.HasKey(e => e.Id);
                eb.Property(e => e.Id).ValueGeneratedNever();
                eb.Property(e => e.CustomerId).IsRequired();
                eb.Property(e => e.CustomerName).IsRequired().HasMaxLength(200);
                eb.Property(e => e.CustomerEmail).IsRequired().HasMaxLength(255);
                eb.Property(e => e.QuotationNo).IsRequired().HasMaxLength(50);
                eb.Property(e => e.Status).IsRequired();
                eb.Property(e => e.ValidUntil).IsRequired();
                eb.Property(e => e.TotalAmount).IsRequired().HasPrecision(18, 2);

                eb.OwnsMany(e => e.LineItems, li =>
                {
                    li.ToTable("SalesQuotationItems");
                    li.WithOwner().HasForeignKey("QuotationId");
                    // The column is uniqueidentifier; an undeclared shadow key would default to int
                    // (same fix as SalesOrderItems above).
                    li.Property<Guid>("Id");
                    li.HasKey("Id");
                    li.Property(p => p.ProductId).IsRequired();
                    li.Property(p => p.ProductName).IsRequired().HasMaxLength(500);
                    li.Property(p => p.Quantity).IsRequired();
                    li.Property(p => p.UnitPrice).IsRequired().HasPrecision(18, 2);
                    li.Ignore(p => p.LineTotal);
                });

                eb.HasIndex(e => e.CustomerId);
                eb.HasIndex(e => new { e.TenantId, e.QuotationNo }).IsUnique(); // numbering is per tenant
            });

            modelBuilder.Entity<OrderNumberSequence>(eb =>
            {
                eb.ToTable("OrderNumberSequences");
                eb.HasKey(e => e.Id);
                eb.Property(e => e.Prefix).IsRequired().HasMaxLength(20);
                eb.HasIndex(e => new { e.TenantId, e.Prefix, e.Year, e.Month }).IsUnique();
            });

            modelBuilder.Entity<OrderFulfillmentSagaState>(eb =>
            {
                eb.ToTable("OrderFulfillmentSagas");
                eb.HasKey(e => e.Id);
                eb.Property(e => e.Id).ValueGeneratedNever();
                eb.Property(e => e.TenantId).IsRequired();
                eb.Property(e => e.OrderId).IsRequired();
                eb.Property(e => e.CurrentStep).IsRequired();
                eb.Property(e => e.Status).IsRequired();
                eb.Property(e => e.Approved).IsRequired();
                eb.Property(e => e.InventoryReserved).IsRequired();
                eb.Property(e => e.ErrorMessage).HasMaxLength(1000);
                eb.Property(e => e.RetryCount).IsRequired();
                eb.Property(e => e.LastUpdatedAt).IsRequired();
                eb.Property(e => e.CreatedAt).IsRequired();

                eb.HasIndex(e => e.OrderId).IsUnique();
                eb.HasIndex(e => new { e.Status, e.LastUpdatedAt });
            });

            modelBuilder.Entity<SystemCartCheckoutSettings>(eb =>
            {
                eb.ToTable("SystemCartCheckoutSettings");
                eb.HasKey(e => e.Id);
                eb.Property(e => e.Id).ValueGeneratedNever();
            });

            modelBuilder.Entity<TenantCartCheckoutSettings>(eb =>
            {
                eb.ToTable("TenantCartCheckoutSettings");
                eb.HasKey(e => e.Id);
                eb.HasIndex(e => e.TenantId).IsUnique();
            });

            modelBuilder.Entity<SystemOrderSettings>(eb =>
            {
                eb.ToTable("SystemOrderSettings");
                eb.HasKey(e => e.Id);
                eb.Property(e => e.Id).ValueGeneratedNever();
            });

            modelBuilder.Entity<TenantOrderSettings>(eb =>
            {
                eb.ToTable("TenantOrderSettings");
                eb.HasKey(e => e.Id);
                eb.Property(e => e.OrderNumberPrefix).HasMaxLength(20);
                eb.HasIndex(e => e.TenantId).IsUnique();
            });

            // Outbox table configuration (Settings-changed Kafka publish path only)
            modelBuilder.Entity<OutboxMessage>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Type).IsRequired();
                entity.Property(e => e.Content).IsRequired();
                entity.Property(e => e.OccurredOnUtc).IsRequired();

                entity.HasIndex(e => e.ProcessedOnUtc)
                    .HasDatabaseName("IX_OutboxMessages_ProcessedOnUtc_Pending")
                    .HasFilter("[ProcessedOnUtc] IS NULL");
            });

            // Last, so it composes with any per-entity filter configured above.
            modelBuilder.ApplyTenantQueryFilters(this);
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
}
