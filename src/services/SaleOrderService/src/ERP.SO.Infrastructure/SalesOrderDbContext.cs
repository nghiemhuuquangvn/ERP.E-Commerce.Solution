using ERP.SO.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ERP.SO.Infrastructure
{
    public class SalesOrderDbContext : DbContext
    {
        public SalesOrderDbContext(DbContextOptions<SalesOrderDbContext> options)
            : base(options) { }

        public DbSet<SalesOrder> SalesOrders { get; set; } = null!;

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
                    li.WithOwner().HasForeignKey("OrderId");
                    li.HasKey("Id");
                    li.Property(p => p.ProductId).IsRequired();
                    li.Property(p => p.ProductName).IsRequired().HasMaxLength(500);
                    li.Property(p => p.Quantity).IsRequired();
                    li.Property(p => p.UnitPrice).IsRequired().HasPrecision(18, 2);
                    li.Property(p => p.ShippedQuantity).IsRequired();
                });

                eb.OwnsMany(e => e.InternalNotes, n =>
                {
                    n.WithOwner().HasForeignKey("OrderId");
                    n.HasKey("Id");
                    n.Property(p => p.Note).IsRequired().HasMaxLength(2000);
                    n.Property(p => p.AddedBy).HasMaxLength(200);
                    n.Property(p => p.CreatedAt).IsRequired();
                });

                eb.OwnsMany(e => e.StatusHistory, h =>
                {
                    h.WithOwner().HasForeignKey("OrderId");
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
        }
    }
}
