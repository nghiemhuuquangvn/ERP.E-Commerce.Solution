using ERP.POS.Application.Commands;
using ERP.POS.Application.Interfaces;
using ERP.POS.Domain.Entities;
using MediatR;

namespace ERP.POS.Application.Handlers;

/// <summary>
/// Replays transactions completed on a terminal while offline. Idempotent by
/// ClientTransactionId — a retried sync (e.g. the terminal app isn't sure the first
/// attempt reached the server) must not create a duplicate transaction or double-count
/// shift sales.
/// </summary>
public class SyncOfflineTransactionsCommandHandler : IRequestHandler<SyncOfflineTransactionsCommand, List<OfflineSyncResultDto>>
{
    private readonly IPosUnitOfWork _uow;

    public SyncOfflineTransactionsCommandHandler(IPosUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<List<OfflineSyncResultDto>> Handle(SyncOfflineTransactionsCommand request, CancellationToken cancellationToken)
    {
        var results = new List<OfflineSyncResultDto>();

        foreach (var req in request.Transactions)
        {
            try
            {
                var existing = await _uow.Transactions.GetByIdAsync(req.ClientTransactionId, cancellationToken);
                if (existing != null)
                {
                    results.Add(new OfflineSyncResultDto
                    {
                        ClientTransactionId = req.ClientTransactionId,
                        AlreadySynced = true,
                        Transaction = OpenTransactionCommandHandler.MapToDto(existing)
                    });
                    continue;
                }

                var transaction = Transaction.Open(req.TerminalId, req.CashierId, req.CashShiftId, req.ClientTransactionId);

                // Offline, the terminal already took the customer's money at the price it showed, so that price stands (the tender must match it); a difference
                // from the system price is reported, not refused (R113).
                var warnings = new List<string>();
                foreach (var item in req.Items)
                {
                    transaction.AddItem(item.ProductId, item.ProductName, item.Quantity, item.UnitPrice, item.TaxRate);

                    var cached = await _uow.ProductCache.GetByIdAsync(item.ProductId, cancellationToken);
                    if (cached is null || cached.Price <= 0)
                        warnings.Add($"{item.ProductName}: sold at {item.UnitPrice:0.00}, no system price known.");
                    else if (Math.Round(cached.Price, 2) != Math.Round(item.UnitPrice, 2))
                        warnings.Add($"{item.ProductName}: sold at {item.UnitPrice:0.00}, system price {cached.Price:0.00}.");
                }

                if (req.DiscountAmount > 0)
                    transaction.ApplyDiscount(req.DiscountAmount);

                var tenders = req.Tenders.Select(t => (t.PaymentMethod, t.Amount)).ToList();
                transaction.CompleteWithTenders(tenders);

                await _uow.Transactions.AddAsync(transaction, cancellationToken);

                if (transaction.CashShiftId.HasValue)
                {
                    var shift = await _uow.CashShifts.GetByIdAsync(transaction.CashShiftId.Value, cancellationToken);
                    if (shift != null)
                    {
                        shift.RecordTransaction(transaction.TotalAmount);
                        await _uow.CashShifts.UpdateAsync(shift, cancellationToken);
                    }
                }

                await _uow.SaveChangesAsync(cancellationToken);

                results.Add(new OfflineSyncResultDto
                {
                    ClientTransactionId = req.ClientTransactionId,
                    AlreadySynced = false,
                    Transaction = OpenTransactionCommandHandler.MapToDto(transaction),
                    PriceWarnings = warnings
                });
            }
            catch (Exception ex)
            {
                // Best-effort batch: one malformed offline transaction must not block
                // the rest of the batch from syncing.
                results.Add(new OfflineSyncResultDto
                {
                    ClientTransactionId = req.ClientTransactionId,
                    AlreadySynced = false,
                    Error = ex.Message
                });
            }
        }

        return results;
    }
}
