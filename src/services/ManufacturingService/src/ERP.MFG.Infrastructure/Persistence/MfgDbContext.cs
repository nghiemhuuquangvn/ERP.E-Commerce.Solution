using ERP.MFG.Domain.Entities;
using ERP.Shared.Core.Domain;
using ERP.Shared.Infrastructure.Data;
using ERP.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace ERP.MFG.Infrastructure.Persistence;

/// <summary>
/// ManufacturingDB (PostgreSQL). Tenant isolation as in every service: query filter on every tenant-owned
/// entity (last in <see cref="OnModelCreating"/>) and <c>EnforceTenantOnSave</c> on both save paths.
/// The schema is created by the SQL scripts in <c>src/Database/Manufacturing</c>, never by EF.
/// </summary>
public class MfgDbContext : DbContext, ITenantScopedDbContext
{
    private readonly ITenantProvider _tenantProvider;

    public MfgDbContext(DbContextOptions<MfgDbContext> options, ITenantProvider tenantProvider) : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    public Guid? CurrentTenantId => _tenantProvider.CurrentTenantId;

    public DbSet<DocumentSequence> DocumentSequences => Set<DocumentSequence>();
    public DbSet<Plant> Plants => Set<Plant>();
    public DbSet<ProductionSetting> ProductionSettings => Set<ProductionSetting>();
    public DbSet<ProductionSettingAudit> ProductionSettingAudits => Set<ProductionSettingAudit>();
    public DbSet<ItemManufacturingProfile> ItemProfiles => Set<ItemManufacturingProfile>();
    public DbSet<PlantUserAccess> PlantUserAccesses => Set<PlantUserAccess>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<WorkCalendar> Calendars => Set<WorkCalendar>();
    public DbSet<WorkCalendarPattern> CalendarPatterns => Set<WorkCalendarPattern>();
    public DbSet<WorkCalendarException> CalendarExceptions => Set<WorkCalendarException>();
    public DbSet<WorkCalendarExceptionShift> CalendarExceptionShifts => Set<WorkCalendarExceptionShift>();
    public DbSet<WorkCenter> WorkCenters => Set<WorkCenter>();
    public DbSet<Machine> Machines => Set<Machine>();
    public DbSet<StandardOperation> StandardOperations => Set<StandardOperation>();
    public DbSet<ReasonCode> ReasonCodes => Set<ReasonCode>();
    public DbSet<Crew> Crews => Set<Crew>();
    public DbSet<CrewMember> CrewMembers => Set<CrewMember>();
    public DbSet<Bom> Boms => Set<Bom>();
    public DbSet<BomLine> BomLines => Set<BomLine>();
    public DbSet<Routing> Routings => Set<Routing>();
    public DbSet<RoutingOperation> RoutingOperations => Set<RoutingOperation>();
    public DbSet<ProductionOrder> ProductionOrders => Set<ProductionOrder>();
    public DbSet<ProductionOrderMaterial> ProductionOrderMaterials => Set<ProductionOrderMaterial>();
    public DbSet<ProductionOrderOperation> ProductionOrderOperations => Set<ProductionOrderOperation>();
    public DbSet<ProductionOrderOutput> ProductionOrderOutputs => Set<ProductionOrderOutput>();
    public DbSet<ProductionOrderStatusHistory> ProductionOrderStatusHistories => Set<ProductionOrderStatusHistory>();
    public DbSet<MaterialTransaction> MaterialTransactions => Set<MaterialTransaction>();
    public DbSet<MaterialTransactionLine> MaterialTransactionLines => Set<MaterialTransactionLine>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DocumentSequence>(e =>
        {
            e.ToTable("DocumentSequences");
            e.Property(x => x.DocType).HasMaxLength(10).IsRequired();
            e.HasIndex(x => new { x.TenantId, x.DocType, x.PlantId, x.Period }).IsUnique();
        });

        modelBuilder.Entity<Plant>(e =>
        {
            e.ToTable("Plants");
            e.Property(x => x.Code).HasMaxLength(20).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.TimeZoneId).HasMaxLength(50).IsRequired();
            e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        });

        modelBuilder.Entity<ProductionSetting>(e =>
        {
            e.ToTable("ProductionSettings");
            e.Property(x => x.OverProductionTolerancePct).HasPrecision(5, 2);
            e.Property(x => x.UnderCompletionTolerancePct).HasPrecision(5, 2);
            e.Property(x => x.OverIssueTolerancePct).HasPrecision(5, 2);
            e.Property(x => x.DefaultIssueMethod).HasConversion<short>();
            e.Property(x => x.CostingMethod).HasConversion<short>();
            e.Property(x => x.FgLotNumberFormat).HasMaxLength(100).IsRequired();
            e.HasIndex(x => new { x.TenantId, x.PlantId }).IsUnique();
        });

        modelBuilder.Entity<ProductionSettingAudit>(e =>
        {
            e.ToTable("ProductionSettingAudits");
            e.Property(x => x.Field).HasMaxLength(100).IsRequired();
            e.Property(x => x.OldValue).HasMaxLength(200).IsRequired();
            e.Property(x => x.NewValue).HasMaxLength(200).IsRequired();
            e.Property(x => x.ChangedBy).HasMaxLength(200).IsRequired();
            e.HasIndex(x => new { x.TenantId, x.PlantId, x.ChangedAt });
        });

        modelBuilder.Entity<ItemManufacturingProfile>(e =>
        {
            e.ToTable("ItemManufacturingProfiles");
            e.Property(x => x.ItemCode).HasMaxLength(100).IsRequired();
            e.Property(x => x.ItemName).HasMaxLength(255).IsRequired();
            e.Property(x => x.MakeOrBuy).HasConversion<short>();
            e.Property(x => x.LotSizingPolicy).HasConversion<short>();
            e.Property(x => x.DefaultIssueMethod).HasConversion<short?>();
            foreach (var q in new[] { nameof(ItemManufacturingProfile.FixedLotQty), nameof(ItemManufacturingProfile.MinOrderQty), nameof(ItemManufacturingProfile.OrderMultiple),
                         nameof(ItemManufacturingProfile.SafetyStockQty), nameof(ItemManufacturingProfile.StandardLotSize), nameof(ItemManufacturingProfile.StandardCost) })
                e.Property(q).HasPrecision(18, 6);
            e.HasIndex(x => new { x.TenantId, x.ItemId, x.PlantId }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.PlantId, x.ItemCode });
        });

        modelBuilder.Entity<PlantUserAccess>(e =>
        {
            e.ToTable("PlantUserAccesses");
            e.HasIndex(x => new { x.TenantId, x.PlantId, x.UserId }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.UserId });
        });

        modelBuilder.Entity<Shift>(e =>
        {
            e.ToTable("Shifts");
            e.Property(x => x.Code).HasMaxLength(20).IsRequired();
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Ignore(x => x.RawMinutes);
            e.Ignore(x => x.DurationMinutes);
            e.HasIndex(x => new { x.TenantId, x.PlantId, x.Code }).IsUnique();
        });

        modelBuilder.Entity<WorkCalendar>(e =>
        {
            e.ToTable("WorkCalendars");
            e.Property(x => x.Code).HasMaxLength(20).IsRequired();
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.HasIndex(x => new { x.TenantId, x.PlantId, x.Code }).IsUnique();
            e.HasMany(x => x.Patterns).WithOne().HasForeignKey(p => p.CalendarId);
            e.HasMany(x => x.Exceptions).WithOne().HasForeignKey(p => p.CalendarId);
            e.Navigation(x => x.Patterns).UsePropertyAccessMode(PropertyAccessMode.Field);
            e.Navigation(x => x.Exceptions).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<WorkCalendarPattern>(e =>
        {
            e.ToTable("WorkCalendarPatterns");
            e.Property(x => x.DayOfWeek).HasConversion<short>();
            e.HasIndex(x => new { x.TenantId, x.CalendarId, x.DayOfWeek, x.ShiftId }).IsUnique();
        });

        modelBuilder.Entity<WorkCalendarException>(e =>
        {
            e.ToTable("WorkCalendarExceptions");
            e.Property(x => x.Type).HasConversion<short>();
            e.HasIndex(x => new { x.TenantId, x.CalendarId, x.Date }).IsUnique();
            e.HasMany(x => x.Shifts).WithOne().HasForeignKey(s => s.ExceptionId);
            e.Navigation(x => x.Shifts).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<WorkCalendarExceptionShift>(e => e.ToTable("WorkCalendarExceptionShifts"));

        modelBuilder.Entity<WorkCenter>(e =>
        {
            e.ToTable("WorkCenters");
            e.Property(x => x.Code).HasMaxLength(20).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Type).HasConversion<short>();
            e.Property(x => x.EfficiencyPct).HasPrecision(5, 2);
            e.Property(x => x.UtilizationPct).HasPrecision(5, 2);
            e.Property(x => x.LaborRatePerHour).HasPrecision(18, 4);
            e.Property(x => x.MachineRatePerHour).HasPrecision(18, 4);
            e.HasIndex(x => new { x.TenantId, x.PlantId, x.Code }).IsUnique();
        });

        modelBuilder.Entity<Machine>(e =>
        {
            e.ToTable("Machines");
            e.Property(x => x.Code).HasMaxLength(20).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Status).HasConversion<short>();
            e.Property(x => x.IdealCycleTimeSeconds).HasPrecision(10, 2);
            e.Property(x => x.SerialNo).HasMaxLength(100);
            e.HasIndex(x => new { x.TenantId, x.PlantId, x.Code }).IsUnique();
        });

        modelBuilder.Entity<StandardOperation>(e =>
        {
            e.ToTable("StandardOperations");
            e.Property(x => x.Code).HasMaxLength(20).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.SetupTimeMinutes).HasPrecision(10, 2);
            e.Property(x => x.RunTimeMinutes).HasPrecision(10, 4);
            e.Property(x => x.RunTimeBasis).HasConversion<short>();
            e.Property(x => x.BatchSize).HasPrecision(18, 6);
            e.HasIndex(x => new { x.TenantId, x.PlantId, x.Code }).IsUnique();
        });

        modelBuilder.Entity<ReasonCode>(e =>
        {
            e.ToTable("ReasonCodes");
            e.Property(x => x.Type).HasConversion<short>();
            e.Property(x => x.Code).HasMaxLength(20).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Category).HasConversion<short?>();
            e.HasIndex(x => new { x.TenantId, x.Type, x.Code }).IsUnique();
        });

        modelBuilder.Entity<Crew>(e =>
        {
            e.ToTable("Crews");
            e.Property(x => x.Code).HasMaxLength(20).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.HasIndex(x => new { x.TenantId, x.PlantId, x.Code }).IsUnique();
            e.HasMany(x => x.Members).WithOne().HasForeignKey(m => m.CrewId);
            e.Navigation(x => x.Members).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<CrewMember>(e =>
        {
            e.ToTable("CrewMembers");
            e.Property(x => x.LaborRatePerHour).HasPrecision(18, 4);
            e.HasIndex(x => new { x.TenantId, x.EmployeeId });
        });

        modelBuilder.Entity<Bom>(e =>
        {
            e.ToTable("Boms");
            e.Property(x => x.ItemCode).HasMaxLength(100).IsRequired();
            e.Property(x => x.ItemName).HasMaxLength(255).IsRequired();
            e.Property(x => x.BaseQuantity).HasPrecision(18, 6);
            e.Property(x => x.YieldPct).HasPrecision(5, 2);
            e.Property(x => x.Status).HasConversion<short>();
            e.Property(x => x.ApprovedBy).HasMaxLength(200);
            e.Ignore(x => x.Lines);
            e.HasIndex(x => new { x.TenantId, x.ItemId, x.PlantId, x.Version }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.PlantId, x.Status });
            e.HasMany(x => x.AllLines).WithOne().HasForeignKey(l => l.BomId);
            e.Navigation(x => x.AllLines).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<BomLine>(e =>
        {
            e.ToTable("BomLines");
            e.Property(x => x.LineType).HasConversion<short>();
            e.Property(x => x.ComponentCode).HasMaxLength(100).IsRequired();
            e.Property(x => x.ComponentName).HasMaxLength(255).IsRequired();
            e.Property(x => x.Quantity).HasPrecision(18, 6);
            e.Property(x => x.ScrapPct).HasPrecision(5, 2);
            e.Property(x => x.CostAllocationPct).HasPrecision(5, 2);
            e.Property(x => x.IssueMethod).HasConversion<short>();
            e.Property(x => x.AlternativeGroup).HasMaxLength(20);
            e.Property(x => x.Notes).HasMaxLength(500);
            e.HasIndex(x => new { x.TenantId, x.BomId, x.LineNo }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.ComponentItemId });
        });

        modelBuilder.Entity<Routing>(e =>
        {
            e.ToTable("Routings");
            e.Property(x => x.ItemCode).HasMaxLength(100).IsRequired();
            e.Property(x => x.ItemName).HasMaxLength(255).IsRequired();
            e.Property(x => x.Status).HasConversion<short>();
            e.Property(x => x.ApprovedBy).HasMaxLength(200);
            e.Ignore(x => x.Operations);
            e.HasIndex(x => new { x.TenantId, x.ItemId, x.PlantId, x.Version }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.PlantId, x.Status });
            e.HasMany(x => x.AllOperations).WithOne().HasForeignKey(o => o.RoutingId);
            e.Navigation(x => x.AllOperations).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<RoutingOperation>(e =>
        {
            e.ToTable("RoutingOperations");
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.WorkCenterCode).HasMaxLength(20).IsRequired();
            e.Property(x => x.SetupTimeMinutes).HasPrecision(10, 2);
            e.Property(x => x.RunTimeMinutes).HasPrecision(10, 4);
            e.Property(x => x.RunTimeBasis).HasConversion<short>();
            e.Property(x => x.BatchSize).HasPrecision(18, 6);
            e.Property(x => x.Instructions).HasMaxLength(2000);
            e.HasIndex(x => new { x.TenantId, x.RoutingId, x.OperationSeq }).IsUnique().HasFilter("\"IsDeleted\" = false");
            e.HasIndex(x => new { x.TenantId, x.WorkCenterId });
        });

        modelBuilder.Entity<ProductionOrder>(e =>
        {
            e.ToTable("ProductionOrders");
            e.Property(x => x.OrderNo).HasMaxLength(30).IsRequired();
            e.Property(x => x.ItemCode).HasMaxLength(100).IsRequired();
            e.Property(x => x.ItemName).HasMaxLength(255).IsRequired();
            e.Property(x => x.Quantity).HasPrecision(18, 6);
            e.Property(x => x.CompletedQty).HasPrecision(18, 6);
            e.Property(x => x.ScrappedQty).HasPrecision(18, 6);
            e.Property(x => x.OrderType).HasConversion<short>();
            e.Property(x => x.Status).HasConversion<short>();
            e.Property(x => x.StatusBeforeHold).HasConversion<short?>();
            e.Property(x => x.SourceType).HasConversion<short>();
            e.Property(x => x.Priority).HasConversion<short>();
            e.Property(x => x.Notes).HasMaxLength(1000);
            e.Property(x => x.ReservationSyncError).HasMaxLength(500);
            e.Ignore(x => x.IsUntouched);
            e.Ignore(x => x.Materials);
            e.Ignore(x => x.Operations);
            e.Ignore(x => x.Outputs);
            e.Ignore(x => x.History);
            e.HasIndex(x => new { x.TenantId, x.OrderNo }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.PlantId, x.Status, x.DueDate });
            e.HasIndex(x => new { x.TenantId, x.ItemId, x.Status });
            e.HasIndex(x => new { x.TenantId, x.ReservationNextAttemptAt }).HasFilter("\"ReservationSyncPending\" = true");
            e.HasMany(x => x.AllMaterials).WithOne().HasForeignKey(m => m.OrderId);
            e.Navigation(x => x.AllMaterials).HasField("_materials").UsePropertyAccessMode(PropertyAccessMode.Field);
            e.HasMany(x => x.AllOperations).WithOne().HasForeignKey(o => o.OrderId);
            e.Navigation(x => x.AllOperations).HasField("_operations").UsePropertyAccessMode(PropertyAccessMode.Field);
            e.HasMany(x => x.AllOutputs).WithOne().HasForeignKey(o => o.OrderId);
            e.Navigation(x => x.AllOutputs).HasField("_outputs").UsePropertyAccessMode(PropertyAccessMode.Field);
            e.HasMany(x => x.AllHistory).WithOne().HasForeignKey(h => h.OrderId);
            e.Navigation(x => x.AllHistory).HasField("_history").UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<ProductionOrderMaterial>(e =>
        {
            e.ToTable("ProductionOrderMaterials");
            e.Property(x => x.ComponentCode).HasMaxLength(100).IsRequired();
            e.Property(x => x.ComponentName).HasMaxLength(255).IsRequired();
            foreach (var q in new[] { nameof(ProductionOrderMaterial.RequiredQty), nameof(ProductionOrderMaterial.IssuedQty), nameof(ProductionOrderMaterial.ReturnedQty),
                         nameof(ProductionOrderMaterial.ConsumedQty), nameof(ProductionOrderMaterial.ReservedQty) })
                e.Property(q).HasPrecision(18, 6);
            e.Property(x => x.ScrapPct).HasPrecision(5, 2);
            e.Property(x => x.IssueMethod).HasConversion<short>();
            e.Property(x => x.AlternativeGroup).HasMaxLength(20);
            e.Ignore(x => x.NetIssuedQty);
            e.Ignore(x => x.OpenNeed);
            e.HasIndex(x => new { x.TenantId, x.OrderId, x.LineNo }).IsUnique().HasFilter("\"IsDeleted\" = false");
            e.HasIndex(x => new { x.TenantId, x.ComponentItemId });
        });

        modelBuilder.Entity<ProductionOrderOperation>(e =>
        {
            e.ToTable("ProductionOrderOperations");
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.WorkCenterCode).HasMaxLength(20).IsRequired();
            e.Property(x => x.SetupTimeMinutes).HasPrecision(10, 2);
            e.Property(x => x.RunTimeMinutes).HasPrecision(10, 4);
            e.Property(x => x.StandardMinutes).HasPrecision(12, 2);
            e.Property(x => x.BatchSize).HasPrecision(18, 6);
            e.Property(x => x.RunTimeBasis).HasConversion<short>();
            e.Property(x => x.Status).HasConversion<short>();
            e.Ignore(x => x.HasWork);
            foreach (var q in new[] { nameof(ProductionOrderOperation.GoodQty), nameof(ProductionOrderOperation.ScrapQty), nameof(ProductionOrderOperation.ReworkQty), nameof(ProductionOrderOperation.IpqcPassedQty) })
                e.Property(q).HasPrecision(18, 6);
            e.HasIndex(x => new { x.TenantId, x.OrderId, x.OperationSeq }).IsUnique().HasFilter("\"IsDeleted\" = false");
            e.HasIndex(x => new { x.TenantId, x.WorkCenterId, x.Status });
        });

        modelBuilder.Entity<ProductionOrderOutput>(e =>
        {
            e.ToTable("ProductionOrderOutputs");
            e.Property(x => x.ItemCode).HasMaxLength(100).IsRequired();
            e.Property(x => x.ItemName).HasMaxLength(255).IsRequired();
            e.Property(x => x.LineType).HasConversion<short>();
            e.Property(x => x.PlannedQty).HasPrecision(18, 6);
            e.Property(x => x.ReceivedQty).HasPrecision(18, 6);
            e.Property(x => x.CostAllocationPct).HasPrecision(5, 2);
            e.HasIndex(x => new { x.TenantId, x.OrderId, x.LineNo }).IsUnique().HasFilter("\"IsDeleted\" = false");
        });

        modelBuilder.Entity<ProductionOrderStatusHistory>(e =>
        {
            e.ToTable("ProductionOrderStatusHistories");
            e.Property(x => x.FromStatus).HasConversion<short>();
            e.Property(x => x.ToStatus).HasConversion<short>();
            e.Property(x => x.Note).HasMaxLength(500);
            e.Property(x => x.ChangedBy).HasMaxLength(200).IsRequired();
            e.HasIndex(x => new { x.TenantId, x.OrderId, x.ChangedAt });
        });

        modelBuilder.Entity<MaterialTransaction>(e =>
        {
            e.ToTable("MaterialTransactions");
            e.Property(x => x.DocNo).HasMaxLength(30).IsRequired();
            e.Property(x => x.OrderNo).HasMaxLength(30).IsRequired();
            e.Property(x => x.Type).HasConversion<short>();
            e.Property(x => x.Status).HasConversion<short>();
            e.Property(x => x.Mode).HasConversion<short>();
            e.Property(x => x.Note).HasMaxLength(500);
            e.Property(x => x.IdempotencyKey).HasMaxLength(100);
            e.Property(x => x.RequestHash).HasMaxLength(64);
            e.Property(x => x.LastError).HasMaxLength(500);
            e.Property(x => x.LastErrorCode).HasMaxLength(60);
            e.Ignore(x => x.Lines);
            e.Ignore(x => x.PendingLines);
            e.Ignore(x => x.PostedLines);
            e.Ignore(x => x.AllLinesPosted);
            e.Ignore(x => x.RollbackComplete);
            e.Ignore(x => x.IsIssue);
            e.HasIndex(x => new { x.TenantId, x.DocNo }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique().HasFilter("\"IdempotencyKey\" IS NOT NULL");
            e.HasIndex(x => new { x.TenantId, x.OrderId });
            e.HasIndex(x => new { x.TenantId, x.NextAttemptAt }).HasFilter("\"Status\" = 1");
            e.HasIndex(x => new { x.TenantId, x.PlantId, x.Status, x.CreatedAt });
            e.HasMany(x => x.AllLines).WithOne().HasForeignKey(l => l.TransactionId);
            e.Navigation(x => x.AllLines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<MaterialTransactionLine>(e =>
        {
            e.ToTable("MaterialTransactionLines");
            e.Property(x => x.ItemCode).HasMaxLength(100).IsRequired();
            e.Property(x => x.MovementKey).HasMaxLength(100).IsRequired();
            e.Property(x => x.Status).HasConversion<short>();
            e.Property(x => x.Qty).HasPrecision(18, 6);
            e.Property(x => x.UnitCost).HasPrecision(18, 6);
            e.Property(x => x.Amount).HasPrecision(18, 4);
            e.Property(x => x.Error).HasMaxLength(500);
            e.Ignore(x => x.CompensationKey);
            e.HasIndex(x => new { x.TenantId, x.TransactionId, x.LineNo }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.MovementKey }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.OrderMaterialId });
        });

        // The outbox is not tenant-owned: the tenant travels inside every event (EventTenant).
        modelBuilder.Entity<OutboxMessage>(e =>
        {
            e.ToTable("OutboxMessages");
            e.HasKey(x => x.Id);
            e.Property(x => x.Type).HasMaxLength(500).IsRequired();
            e.HasIndex(x => x.OccurredOnUtc).HasFilter("\"ProcessedOnUtc\" IS NULL");
        });

        ConfigureBaseEntities(modelBuilder);

        // Last, so it composes with any per-entity filter configured above.
        modelBuilder.ApplyTenantQueryFilters(this);
    }

    /// <summary>
    /// Common mapping of everything that derives from <see cref="BaseEntity"/>: ids come from the code, the SQL Server
    /// <c>RowVersion</c> and the domain event list are not columns, and optimistic locking uses PostgreSQL's
    /// <c>xmin</c> system column (a changed row = a new xmin, BR-GEN-03).
    /// </summary>
    private static void ConfigureBaseEntities(ModelBuilder modelBuilder)
    {
        foreach (var type in modelBuilder.Model.GetEntityTypes().Where(t => typeof(BaseEntity).IsAssignableFrom(t.ClrType)).ToList())
        {
            var entity = modelBuilder.Entity(type.ClrType);
            entity.Property(nameof(BaseEntity.Id)).ValueGeneratedNever();
            entity.Ignore(nameof(BaseEntity.RowVersion));
            entity.Ignore(nameof(BaseEntity.DomainEvents));
            entity.Property(nameof(BaseEntity.Description)).HasMaxLength(2000);
            entity.Property(nameof(BaseEntity.CreatedBy)).HasMaxLength(200);
            entity.Property(nameof(BaseEntity.UpdatedBy)).HasMaxLength(200);
            entity.Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ChangeTracker.EnforceTenantOnSave(CurrentTenantId);
        WriteOutbox();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ChangeTracker.EnforceTenantOnSave(CurrentTenantId);
        WriteOutbox();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Outbox rule: the events an aggregate raised leave in the same transaction as its change. The rows were stamped with their tenant just above, so each
    /// event gets the tenant first, is then serialised with its type (TypeNameHandling.All, so the processor can restore it) and the aggregate's list is cleared.
    /// </summary>
    private void WriteOutbox()
    {
        foreach (var entity in ChangeTracker.Entries<BaseEntity>().Select(e => e.Entity).Where(e => e.DomainEvents.Count > 0).ToList())
        {
            EventTenant.StampFrom(entity);
            foreach (var domainEvent in entity.DomainEvents)
                OutboxMessages.Add(new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    Type = domainEvent.GetType().AssemblyQualifiedName ?? domainEvent.GetType().Name,
                    Content = JsonConvert.SerializeObject(domainEvent, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All }),
                    OccurredOnUtc = domainEvent.OccurredOn,
                });
            entity.ClearDomainEvents();
        }
    }
}
