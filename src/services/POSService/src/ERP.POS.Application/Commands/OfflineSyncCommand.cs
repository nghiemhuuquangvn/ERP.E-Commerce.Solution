using ERP.POS.Application.DTOs;
using MediatR;

namespace ERP.POS.Application.Commands;

public class OfflineTransactionItemRequest
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }
}

/// <summary>
/// A transaction completed while the terminal had no network connectivity. The
/// client (POS terminal app) generates ClientTransactionId locally and replays it
/// here once connectivity is restored — this backend endpoint is the "sync queue"
/// half of offline mode; the actual offline storage/queueing happens on the client,
/// which is outside this service's boundary.
/// </summary>
public class OfflineTransactionRequest
{
    public Guid ClientTransactionId { get; set; }
    public Guid TerminalId { get; set; }
    public Guid CashierId { get; set; }
    public Guid? CashShiftId { get; set; }
    public List<OfflineTransactionItemRequest> Items { get; set; } = new();
    public decimal DiscountAmount { get; set; }
    public List<TenderRequest> Tenders { get; set; } = new();
}

public class OfflineSyncResultDto
{
    public Guid ClientTransactionId { get; set; }
    public bool AlreadySynced { get; set; }
    public TransactionDto? Transaction { get; set; }
    public string? Error { get; set; }
}

public class SyncOfflineTransactionsCommand : IRequest<List<OfflineSyncResultDto>>
{
    public List<OfflineTransactionRequest> Transactions { get; set; } = new();
}
