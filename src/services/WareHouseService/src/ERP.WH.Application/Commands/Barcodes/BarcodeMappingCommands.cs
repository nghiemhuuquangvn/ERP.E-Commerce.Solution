using MediatR;
using ERP.WH.Application.Interfaces;
using ERP.WH.Domain.Aggregates.Inventory;

namespace ERP.WH.Application.Commands.Barcodes;

public class BarcodeMappingDto
{
    public Guid Id { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public Guid ProductId { get; set; }
    public Guid? LotId { get; set; }
    public string? SerialNumber { get; set; }
}

public class CreateBarcodeMappingCommand : IRequest<BarcodeMappingDto>
{
    public string Barcode { get; set; } = string.Empty;
    public Guid ProductId { get; set; }
    public Guid? LotId { get; set; }
    public string? SerialNumber { get; set; }
}

public class CreateBarcodeMappingCommandHandler : IRequestHandler<CreateBarcodeMappingCommand, BarcodeMappingDto>
{
    private readonly IWarehouseUnitOfWork _uow;

    public CreateBarcodeMappingCommandHandler(IWarehouseUnitOfWork uow) => _uow = uow;

    public async Task<BarcodeMappingDto> Handle(CreateBarcodeMappingCommand request, CancellationToken cancellationToken)
    {
        var existing = await _uow.BarcodeMappings.GetByBarcodeAsync(request.Barcode, cancellationToken);
        if (existing != null)
            throw new InvalidOperationException($"Barcode '{request.Barcode}' is already mapped");

        var mapping = new BarcodeMapping(request.Barcode, request.ProductId, request.LotId, request.SerialNumber);
        await _uow.BarcodeMappings.AddAsync(mapping, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return Map(mapping);
    }

    internal static BarcodeMappingDto Map(BarcodeMapping m) => new()
    {
        Id = m.Id,
        Barcode = m.Barcode,
        ProductId = m.ProductId,
        LotId = m.LotId,
        SerialNumber = m.SerialNumber
    };
}

public class DeleteBarcodeMappingCommand : IRequest
{
    public Guid Id { get; set; }
}

public class DeleteBarcodeMappingCommandHandler : IRequestHandler<DeleteBarcodeMappingCommand>
{
    private readonly IWarehouseUnitOfWork _uow;

    public DeleteBarcodeMappingCommandHandler(IWarehouseUnitOfWork uow) => _uow = uow;

    public async Task Handle(DeleteBarcodeMappingCommand request, CancellationToken cancellationToken)
    {
        await _uow.BarcodeMappings.DeleteAsync(request.Id, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
    }
}
