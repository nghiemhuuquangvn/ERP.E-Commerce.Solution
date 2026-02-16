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
        private readonly IDomainEventDispatcher _domainEventDispatcher;

        public AuthDbContext(DbContextOptions<AuthDbContext> options, IDomainEventDispatcher domainEventDispatcher) : base(options)
        {
            _domainEventDispatcher = domainEventDispatcher;
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            // Dispatch Domain Events collection
            var domainEvents = ChangeTracker.Entries<BaseEntity>()
                .SelectMany(e => e.Entity.DomainEvents)
                .ToList();

            // save changes in the database
            var result = await base.SaveChangesAsync(cancellationToken);

            // dispatch events only if save was successful
            foreach (var domainEvent in domainEvents)
                await _domainEventDispatcher.DispatchAsync(domainEvent, cancellationToken);
            // clear domain events
            foreach (var entry in ChangeTracker.Entries<BaseEntity>())
                entry.Entity.ClearDomainEvents();

            return result;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(u => u.Email).IsUnique();
            });

            modelBuilder.Entity<UserRole>()
                .HasKey(ur => new { ur.UserId, ur.RoleId });

            modelBuilder.Entity<RolePermission>()
                .HasKey(rp => new { rp.RoleId, rp.PermissionId });

            modelBuilder.Entity<AuthUserTenant>()
                .HasKey(ut => new { ut.UserId, ut.TenantId });

            modelBuilder.Entity<UserPermission>()
                .HasKey(up => new { up.UserId, up.PermissionId });
            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.HasIndex(rt => rt.Token).IsUnique();
            });
        }
    }
}
