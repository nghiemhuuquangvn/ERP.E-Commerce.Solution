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
    private readonly ITransactionRepository _transactions;
    private readonly ICashShiftRepository _shifts;

    public SyncOfflineTransactionsCommandHandler(ITransactionRepository transactions, ICashShiftRepository shifts)
    {
        _transactions = transactions;
        _shifts = shifts;
    }

    public async Task<List<OfflineSyncResultDto>> Handle(SyncOfflineTransactionsCommand request, CancellationToken cancellationToken)
    {
        var results = new List<OfflineSyncResultDto>();

        foreach (var req in request.Transactions)
        {
            try
            {
                var existing = await _transactions.GetByIdAsync(req.ClientTransactionId, cancellationToken);
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

                foreach (var item in req.Items)
                    transaction.AddItem(item.ProductId, item.ProductName, item.Quantity, item.UnitPrice, item.TaxRate);

                if (req.DiscountAmount > 0)
                    transaction.ApplyDiscount(req.DiscountAmount);

                var tenders = req.Tenders.Select(t => (t.PaymentMethod, t.Amount)).ToList();
                transaction.CompleteWithTenders(tenders);

                await _transactions.AddAsync(transaction, cancellationToken);

                if (transaction.CashShiftId.HasValue)
                {
                    var shift = await _shifts.GetByIdAsync(transaction.CashShiftId.Value, cancellationToken);
                    if (shift != null)
                    {
                        shift.RecordTransaction(transaction.TotalAmount);
                        await _shifts.UpdateAsync(shift, cancellationToken);
                    }
                }

                results.Add(new OfflineSyncResultDto
                {
                    ClientTransactionId = req.ClientTransactionId,
                    AlreadySynced = false,
                    Transaction = OpenTransactionCommandHandler.MapToDto(transaction)
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
