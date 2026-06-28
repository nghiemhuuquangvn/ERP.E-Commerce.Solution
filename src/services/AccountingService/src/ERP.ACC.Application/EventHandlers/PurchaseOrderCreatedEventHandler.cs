using ERP.ACC.Application.Commands;
using MediatR;

namespace ERP.ACC.Application.EventHandlers
{
    public class PurchaseOrderCreatedIntegrationEvent
    {
        public Guid POId { get; set; }
        public Guid SupplierId { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public class PurchaseOrderCreatedIntegrationEventHandler : INotificationHandler<PurchaseOrderCreatedIntegrationEvent>
    {
        private readonly IMediator _mediator;

        public PurchaseOrderCreatedIntegrationEventHandler(IMediator mediator) => _mediator = mediator;

        public async Task Handle(PurchaseOrderCreatedIntegrationEvent @event, CancellationToken cancellationToken)
        {
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
