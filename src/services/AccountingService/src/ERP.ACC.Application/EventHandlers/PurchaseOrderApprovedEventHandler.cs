using ERP.ACC.Application.Commands;
using ERP.ACC.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.ACC.Application.EventHandlers
{
    /// <summary>
    /// PurchaseOrderService's PurchaseOrderApprovedDomainEvent (final approval). The bill used
    /// to be created from PurchaseOrderCreated, which is raised before any line exists — so
    /// every bill had Amount 0. Lines are frozen after Draft, so the approved total is final.
    /// </summary>
    public class PurchaseOrderApprovedIntegrationEvent : INotification
    {
        public Guid POId { get; set; }
        public string PONumber { get; set; } = string.Empty;
        public Guid SupplierId { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public class PurchaseOrderApprovedIntegrationEventHandler : INotificationHandler<PurchaseOrderApprovedIntegrationEvent>
    {
        private readonly IMediator _mediator;
        private readonly IBillRepository _bills;
        private readonly ILogger<PurchaseOrderApprovedIntegrationEventHandler> _logger;

        public PurchaseOrderApprovedIntegrationEventHandler(
            IMediator mediator,
            IBillRepository bills,
            ILogger<PurchaseOrderApprovedIntegrationEventHandler> logger)
        {
            _mediator = mediator;
            _bills = bills;
            _logger = logger;
        }

        public async Task Handle(PurchaseOrderApprovedIntegrationEvent @event, CancellationToken cancellationToken)
        {
            // Kafka delivers at least once — a redelivered approval must not book a second bill.
            if (await _bills.GetByPurchaseOrderIdAsync(@event.POId, cancellationToken) != null)
            {
                _logger.LogInformation("Bill for purchase order {PONumber} already exists — approval event ignored", @event.PONumber);
                return;
            }

            var billNumber = $"BILL-{@event.POId:N}".Substring(0, 20).ToUpper();

            await _mediator.Send(new CreateBillCommand
            {
                BillNumber = billNumber,
                SupplierId = @event.SupplierId,
                POId = @event.POId,
                Amount = @event.TotalAmount,
                DueDate = DateTime.UtcNow.AddDays(30)
            }, cancellationToken);
        }
    }
}
