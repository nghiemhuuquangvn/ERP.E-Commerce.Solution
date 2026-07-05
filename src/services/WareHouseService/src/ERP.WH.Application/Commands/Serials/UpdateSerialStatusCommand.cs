using MediatR;
using ERP.WH.Application.Interfaces;
using ERP.WH.Application.Commands.Inventory;
using ERP.WH.Application.Queries.Serials;

namespace ERP.WH.Application.Commands.Serials;

public enum SerialAction
{
    Reserve,
    Sell,
    MarkDamaged,
    ReturnToStock
}

public class UpdateSerialStatusCommand : IRequest<SerialDto>
{
    public string SerialNumber { get; set; } = string.Empty;
    public SerialAction Action { get; set; }
}

public class UpdateSerialStatusCommandHandler : IRequestHandler<UpdateSerialStatusCommand, SerialDto>
{
    private readonly IWarehouseUnitOfWork _uow;

    public UpdateSerialStatusCommandHandler(IWarehouseUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<SerialDto> Handle(UpdateSerialStatusCommand request, CancellationToken cancellationToken)
    {
        var serial = await _uow.InventorySerials.GetBySerialNumberAsync(request.SerialNumber, cancellationToken)
            ?? throw new InvalidOperationException($"Serial number '{request.SerialNumber}' not found");

        switch (request.Action)
        {
            case SerialAction.Reserve:
                serial.Reserve();
                break;
            case SerialAction.Sell:
                serial.Sell();
                break;
            case SerialAction.MarkDamaged:
                serial.MarkDamaged();
                break;
            case SerialAction.ReturnToStock:
                serial.ReturnToStock();
                break;
        }

        await _uow.InventorySerials.UpdateAsync(serial, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return new SerialDto
        {
            SerialId = serial.SerialId,
            ProductId = serial.ProductId,
            SerialNumber = serial.SerialNumber,
            LotId = serial.LotId,
            Status = serial.Status.ToString(),
            ReceivedDate = serial.ReceivedDate,
            SoldDate = serial.SoldDate
        };
    }
}
