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

/// <summary>Admin: book the order's missing reverse parcels again (a carrier refused earlier). Books only what is not booked yet.</summary>
public class RebookReturnCommand : IRequest<ReturnShipmentDto>
{
    public Guid ReturnShipmentId { get; set; }
}

/// <summary>Admin: cancel a return whose pickup was booked at a carrier and is not collected yet - at the carrier FIRST, then here.</summary>
public class CancelReturnAtCarrierCommand : IRequest<ReturnShipmentDto>
{
    public Guid ReturnShipmentId { get; set; }
}
