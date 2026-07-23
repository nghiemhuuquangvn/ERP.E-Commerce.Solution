using MediatR;

namespace ERP.WH.Application.Commands.Inventory;

public class ReleaseReservationCommand : IRequest<ReleaseReservationResponse>
{
    public Guid ReservationId { get; set; }
    public required string Reason { get; set; }
}

public class ReleaseReservationResponse
{
    public Guid ReservationId { get; set; }
    public required string Status { get; set; }
    public DateTime ReleasedAt { get; set; }
}
