using MediatR;

namespace ERP.WH.Application.Commands.Inventory;

/// <summary>
/// Stock effect of a POSService transaction (pos-topic): a completed sale takes the lines
/// out of stock, a voided completed sale (<see cref="IsReturn"/>) puts them back.
/// </summary>
public class RecordPointOfSaleStockCommand : IRequest<RecordPointOfSaleStockResponse>
{
    public Guid TransactionId { get; set; }
    public bool IsReturn { get; set; }
    public List<PointOfSaleLine> Lines { get; set; } = new();
}

public class PointOfSaleLine
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

public class RecordPointOfSaleStockResponse
{
    public int LinesApplied { get; set; }
    public int LinesAlreadyApplied { get; set; }
    public int LinesWithoutInventory { get; set; }
}
