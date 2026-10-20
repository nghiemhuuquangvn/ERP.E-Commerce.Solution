using ERP.Shared.Core.Domain;
using ERP.Shared.Messaging;
using ERP.Shared.Infrastructure.Data;
using ERP.CHB.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ERP.CHB.Infrastructure.Persistence;

public class ChatBotDbContext : DbContext, ITenantScopedDbContext
{
    public DbSet<Conversation> Conversations => Set<Conversation>();

    private readonly ITenantProvider _tenantProvider;

    /// <summary>
    /// Tenant of the current request (JWT / Gateway host tenant) or consumer message
    /// (TenantScope). Read by the tenant query filter on every query and used to
    /// stamp/validate every save.
    /// </summary>
    public Guid? CurrentTenantId => _tenantProvider.CurrentTenantId;

    public ChatBotDbContext(DbContextOptions<ChatBotDbContext> options, ITenantProvider tenantProvider) : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Conversation>(e =>
        {
            e.ToTable("Conversations");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.GuestSessionId).HasMaxLength(200);
            e.Property(x => x.CreatedBy).HasMaxLength(200);
            e.HasIndex(x => new { x.TenantId, x.CreatedBy });
            e.HasIndex(x => new { x.TenantId, x.GuestSessionId });

            // No MVP Outbox — ChatBotService doesn't publish integration events (see plan
            // doc, section 1) — so DomainEvents/RowVersion from BaseEntity aren't mapped.
            e.Ignore(x => x.DomainEvents);
            e.Ignore(x => x.RowVersion);
            // No soft-delete UX for conversations either (no delete endpoint exists) and
            // 001_CreateChatBotDB.sql never added the column — without this Ignore, EF
            // maps BaseEntity.IsDeleted by convention and every query 500s with
            // "Invalid column name 'IsDeleted'" the moment real data makes it run.
            e.Ignore(x => x.IsDeleted);

            e.OwnsMany(x => x.Messages, m =>
            {
                m.ToTable("ConversationMessages");
                m.WithOwner().HasForeignKey(x => x.ConversationId);
                m.HasKey(x => x.Id);
                m.Property(x => x.Id).ValueGeneratedNever();
                m.Property(x => x.Content).IsRequired();
                m.Property(x => x.ToolName).HasMaxLength(100);
                m.Property(x => x.ToolCallId).HasMaxLength(200);
                m.HasIndex(x => x.ConversationId);
            });
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
