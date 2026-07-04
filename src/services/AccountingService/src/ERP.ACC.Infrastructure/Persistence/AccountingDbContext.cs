using ERP.ACC.Domain.Entities;
using ERP.Shared.Core.Domain;
using ERP.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Reflection;

namespace ERP.ACC.Infrastructure.Persistence
{
    public class AccountingDbContext : DbContext
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
        public DbSet<OutboxMessage> OutboxMessages { get; set; }

        public AccountingDbContext(DbContextOptions<AccountingDbContext> options) : base(options)
        {
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
                e.HasMany(j => j.Lines)
                    .WithOne()
                    .HasForeignKey(l => l.JournalEntryId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<JournalLine>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).ValueGeneratedNever();
                e.Property(x => x.Debit).HasColumnType("decimal(18,2)");
                e.Property(x => x.Credit).HasColumnType("decimal(18,2)");
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
                e.HasIndex(x => x.Code).IsUnique();
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
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
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
                    Type = domainEvent.GetType().FullName ?? domainEvent.GetType().Name,
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
    }
}
