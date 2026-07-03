using ERP.SHP.Application.DTOs;
using MediatR;

namespace ERP.SHP.Application.Commands;

public class InitiateReturnCommand : IRequest<ReturnShipmentDto>
{
    public Guid OriginalShipmentId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class AssignReturnTrackingNumberCommand : IRequest<ReturnShipmentDto>
{
    public Guid ReturnShipmentId { get; set; }
    public string TrackingNumber { get; set; } = string.Empty;
}

public class MarkReturnPickedUpCommand : IRequest<ReturnShipmentDto>
{
    public Guid ReturnShipmentId { get; set; }
}

public class MarkReturnReceivedCommand : IRequest<ReturnShipmentDto>
{
    public Guid ReturnShipmentId { get; set; }
}

public class CancelReturnCommand : IRequest<ReturnShipmentDto>
{
    public Guid ReturnShipmentId { get; set; }
}
