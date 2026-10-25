using ERP.Shared.Infrastructure.Data;
using ERP.ACC.Domain.Entities;
using ERP.ACC.Domain.Entities.Settings;
using ERP.Shared.Core.Domain;
using ERP.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Reflection;

namespace ERP.ACC.Infrastructure.Persistence
{
    public class AccountingDbContext : DbContext, ITenantScopedDbContext
    {
        public DbSet<Account> Accounts { get; set; }
        public DbSet<JournalEntry> JournalEntries { get; set; }
        public DbSet<JournalLine> JournalLines { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<Bill> Bills { get; set; }
        public DbSet<FiscalPeriod> FiscalPeriods { get; set; }
        public DbSet<TaxRate> TaxRates { get; set; }
        public DbSet<BankAccount> BankAccounts { get; set; }
        public DbSet<BankStatement> BankStatements { get; set; }
        public DbSet<PostingAccountMapping> PostingAccountMappings { get; set; }
        public DbSet<ClosingPolicy> ClosingPolicies { get; set; }
        public DbSet<ReportProfile> ReportProfiles { get; set; }
        public DbSet<OffBalanceEntry> OffBalanceEntries { get; set; }
        public DbSet<PaymentApplication> PaymentApplications { get; set; }
        public DbSet<Loan> Loans { get; set; }
        public DbSet<NoteText> NoteTexts { get; set; }
        public DbSet<LoanInstallment> LoanInstallments { get; set; }
        public DbSet<OutboxMessage> OutboxMessages { get; set; }
        public DbSet<SystemTaxSettings> SystemTaxSettings { get; set; }
        public DbSet<TenantTaxSettings> TenantTaxSettings { get; set; }

        private readonly ITenantProvider _tenantProvider;

        /// <summary>
        /// Tenant of the current request (JWT / Gateway host tenant) or consumer message
        /// (TenantScope). Read by the tenant query filter on every query and used to
        /// stamp/validate every save.
        /// </summary>
        public Guid? CurrentTenantId => _tenantProvider.CurrentTenantId;

        public AccountingDbContext(DbContextOptions<AccountingDbContext> options, ITenantProvider tenantProvider) : base(options)
        {
            _tenantProvider = tenantProvider;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

            modelBuilder.Entity<OutboxMessage>()
                .HasIndex(m => m.ProcessedOnUtc)
                .HasFilter("[ProcessedOnUtc] IS NULL");

            modelBuilder.Entity<JournalEntry>(e =>
            {
                e.Property(x => x.EntryDate).HasColumnType("date");
                e.Property(x => x.TotalDebit).HasColumnType("decimal(18,2)");
                e.Property(x => x.TotalCredit).HasColumnType("decimal(18,2)");
                e.Property(x => x.ReferenceType).HasMaxLength(100);
                e.Property(x => x.SourceKey).HasMaxLength(150);
                e.Property(x => x.PostedBy).HasMaxLength(256);
                e.Property(x => x.VoidedBy).HasMaxLength(256);
                e.Property(x => x.VoidReason).HasMaxLength(500);
                // The idempotency key: the same business document can never be posted twice for a tenant.
                e.HasIndex(x => new { x.TenantId, x.SourceKey }).IsUnique().HasFilter("[SourceKey] IS NOT NULL");
                e.HasMany(j => j.Lines)
                    .WithOne()
                    .HasForeignKey(l => l.JournalEntryId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Invoice>(e =>
            {
                // DB column is TotalAmount; entity property is Amount
                e.Property(x => x.Amount).HasColumnName("TotalAmount").HasColumnType("decimal(18,2)");
                e.Property(x => x.AmountPaid).HasColumnType("decimal(18,2)");
            });

            modelBuilder.Entity<PostingAccountMapping>(e =>
            {
                e.ToTable("PostingAccountMappings");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedNever();
                e.Property(x => x.PostingKey).IsRequired().HasMaxLength(50);
                e.HasIndex(x => new { x.TenantId, x.PostingKey }).IsUnique().HasFilter("[IsDeleted] = 0");
            });

            modelBuilder.Entity<ClosingPolicy>(e =>
            {
                e.ToTable("ClosingPolicies");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedNever();
                e.HasIndex(x => x.TenantId).IsUnique().HasFilter("[IsDeleted] = 0");
            });

            modelBuilder.Entity<NoteText>(e =>
            {
                e.ToTable("NoteTexts");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedNever();
                e.Property(x => x.SectionKey).IsRequired().HasMaxLength(50);
                e.Property(x => x.Text).IsRequired().HasMaxLength(4000);
                e.HasIndex(x => new { x.TenantId, x.SectionKey }).IsUnique().HasFilter("[IsDeleted] = 0");
            });

            modelBuilder.Entity<Loan>(e =>
            {
                e.ToTable("Loans");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedNever();
                e.Property(x => x.LoanNumber).IsRequired().HasMaxLength(50);
                e.Property(x => x.Lender).IsRequired().HasMaxLength(200);
                e.Property(x => x.Principal).HasColumnType("decimal(18,2)");
                e.Property(x => x.InterestRatePercent).HasColumnType("decimal(9,4)");
                e.Property(x => x.StartDate).HasColumnType("date");
                e.Property(x => x.CancelReason).HasMaxLength(500);
                e.Property(x => x.PostedToLedger).HasDefaultValue(false);
                e.HasIndex(x => new { x.TenantId, x.LoanNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
                e.HasMany(l => l.Installments).WithOne().HasForeignKey(i => i.LoanId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<LoanInstallment>(e =>
            {
                e.ToTable("LoanInstallments");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedNever();
                e.Property(x => x.DueDate).HasColumnType("date");
                e.Property(x => x.PaidOn).HasColumnType("date");
                e.Property(x => x.PrincipalAmount).HasColumnType("decimal(18,2)");
                e.Property(x => x.InterestAmount).HasColumnType("decimal(18,2)");
                e.HasIndex(x => x.LoanId);
            });

            modelBuilder.Entity<PaymentApplication>(e =>
            {
                e.ToTable("PaymentApplications");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedNever();
                e.Property(x => x.PaidOn).HasColumnType("date");
                e.Property(x => x.Amount).HasColumnType("decimal(18,2)");
                e.Property(x => x.SourceKey).HasMaxLength(150);
                e.HasIndex(x => new { x.TenantId, x.DocumentType, x.DocumentId, x.PaidOn });
                e.HasIndex(x => new { x.TenantId, x.SourceKey }).IsUnique().HasFilter("[SourceKey] IS NOT NULL AND [IsDeleted] = 0");
            });

            modelBuilder.Entity<OffBalanceEntry>(e =>
            {
                e.ToTable("OffBalanceEntries");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedNever();
                e.Property(x => x.Amount).HasColumnType("decimal(18,2)");
                e.Property(x => x.Reference).HasMaxLength(100);
                e.Property(x => x.VoidReason).HasMaxLength(500);
                e.HasIndex(x => new { x.TenantId, x.AccountId, x.EntryDate });
            });

            modelBuilder.Entity<ReportProfile>(e =>
            {
                e.ToTable("ReportProfiles");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedNever();
                e.HasIndex(x => x.TenantId).IsUnique().HasFilter("[IsDeleted] = 0");
            });

            modelBuilder.Entity<Bill>(e =>
            {
                // Same class of mismatch as Invoice above: DB column is TotalAmount; entity
                // property is Amount.
                e.Property(x => x.Amount).HasColumnName("TotalAmount").HasColumnType("decimal(18,2)");
            });

            modelBuilder.Entity<FiscalPeriod>(e =>
            {
                // DB column is Name; entity property is PeriodName
                e.Property(x => x.PeriodName).HasColumnName("Name");
            });

            modelBuilder.Entity<JournalLine>(e =>
            {
                // DB table is JournalEntryLines; DbSet is JournalLines
                e.ToTable("JournalEntryLines");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedNever();
                // DB columns are DebitAmount/CreditAmount; entity properties are Debit/Credit
                e.Property(x => x.Debit).HasColumnName("DebitAmount").HasColumnType("decimal(18,2)");
                e.Property(x => x.Credit).HasColumnName("CreditAmount").HasColumnType("decimal(18,2)");
                e.Property(x => x.Description).HasMaxLength(500);
                e.HasIndex(x => x.JournalEntryId);
                e.HasIndex(x => x.AccountId);
            });

            modelBuilder.Entity<TaxRate>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedNever();
                e.Property(x => x.Name).IsRequired().HasMaxLength(100);
                e.Property(x => x.Code).IsRequired().HasMaxLength(20);
                e.Property(x => x.Rate).HasColumnType("decimal(8,4)");
                e.Property(x => x.Description).HasMaxLength(500);
                e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique(); // tax codes are per tenant
            });

            modelBuilder.Entity<BankAccount>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedNever();
                e.Property(x => x.AccountName).IsRequired().HasMaxLength(200);
                e.Property(x => x.BankName).IsRequired().HasMaxLength(200);
                e.Property(x => x.AccountNumber).IsRequired().HasMaxLength(50);
                e.Property(x => x.CurrencyCode).IsRequired().HasMaxLength(10);
                e.Property(x => x.CurrentBalance).HasColumnType("decimal(18,2)");
                e.HasIndex(x => x.AccountNumber);
            });

            modelBuilder.Entity<BankStatement>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedNever();
                e.Property(x => x.OpeningBalance).HasColumnType("decimal(18,2)");
                e.Property(x => x.ClosingBalance).HasColumnType("decimal(18,2)");
                e.Property(x => x.Notes).HasMaxLength(1000);
                e.HasIndex(x => x.BankAccountId);
                e.HasIndex(x => new { x.BankAccountId, x.StatementDate });
            });

            modelBuilder.Entity<SystemTaxSettings>(e =>
            {
                e.ToTable("SystemTaxSettings");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedNever();
            });

            modelBuilder.Entity<TenantTaxSettings>(e =>
            {
                e.ToTable("TenantTaxSettings");
                e.HasKey(x => x.Id);
                e.Property(x => x.CompanyTaxCode).HasMaxLength(50);
                e.Property(x => x.DefaultTaxRatePercent).HasColumnType("decimal(8,4)");
                e.HasIndex(x => x.TenantId).IsUnique();
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

            var aggregateRoots = ChangeTracker.Entries<BaseEntity>()
                .Where(e => e.Entity.DomainEvents.Count != 0)
                .Select(e => e.Entity)
                .ToList();

            var domainEvents = aggregateRoots
                .SelectMany(aggregate => aggregate.DomainEvents)
                .ToList();

            foreach (var domainEvent in domainEvents)
            {
                var outboxMessage = new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    // Must be assembly-qualified — see the identical fix/explanation in
                    // HRMService's HrmDbContext.cs (same bug: Type.GetType() in the
                    // OutboxProcessor's assembly can't resolve a bare FullName from a
                    // different assembly, so a bare FullName here means every domain
                    // event silently never gets published).
                    Type = domainEvent.GetType().AssemblyQualifiedName ?? domainEvent.GetType().Name,
                    Content = JsonConvert.SerializeObject(domainEvent, new JsonSerializerSettings
                    {
                        TypeNameHandling = TypeNameHandling.All
                    }),
                    OccurredOnUtc = domainEvent.OccurredOn,
                    ProcessedOnUtc = null
                };

                await OutboxMessages.AddAsync(outboxMessage, cancellationToken);
            }

            var result = await base.SaveChangesAsync(cancellationToken);

            foreach (var aggregateRoot in aggregateRoots)
                aggregateRoot.ClearDomainEvents();

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
}
