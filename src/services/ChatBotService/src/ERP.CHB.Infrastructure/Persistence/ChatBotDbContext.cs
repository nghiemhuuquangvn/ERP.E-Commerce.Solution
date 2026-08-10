using ERP.CHB.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ERP.CHB.Infrastructure.Persistence;

public class ChatBotDbContext : DbContext
{
    public DbSet<Conversation> Conversations => Set<Conversation>();

    public ChatBotDbContext(DbContextOptions<ChatBotDbContext> options) : base(options) { }

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
    }
}
