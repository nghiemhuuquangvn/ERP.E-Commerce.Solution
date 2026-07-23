using ERP.WH.Domain.Aggregates.Inventory;
using ERP.WH.Application.Commands.Inventory;
using Microsoft.EntityFrameworkCore;

namespace ERP.WH.Infrastructure.Persistence;

public class ReservationRepository : IReservationRepository
{
    private readonly WarehouseDbContext _context;

    public ReservationRepository(WarehouseDbContext context)
    {
        _context = context;
    }

    public async Task<Reservation?> GetByIdAsync(Guid reservationId, CancellationToken cancellationToken)
    {
        return await _context.Reservations.FirstOrDefaultAsync(r => r.ReservationId == reservationId, cancellationToken);
    }

    public async Task AddAsync(Reservation reservation, CancellationToken cancellationToken)
    {
        await _context.Reservations.AddAsync(reservation, cancellationToken);
    }

    public async Task UpdateAsync(Reservation reservation, CancellationToken cancellationToken)
    {
        _context.Reservations.Update(reservation);
    }

    public async Task<List<Reservation>> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken)
    {
        return await _context.Reservations
            .Where(r => r.OrderId == orderId)
            .ToListAsync(cancellationToken);
    }
}
