using ERP.ACC.Application.Commands;
using ERP.ACC.Application.EventHandlers;
using ERP.ACC.Application.Interfaces;
using ERP.ACC.Domain.Entities;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ERP.ACC.UnitTests.EventHandlers;

public class PurchaseOrderApprovedEventHandlerTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IBillRepository> _bills = new();

    private PurchaseOrderApprovedIntegrationEventHandler Handler() =>
        new(_mediator.Object, _bills.Object, NullLogger<PurchaseOrderApprovedIntegrationEventHandler>.Instance);

    [Fact]
    public async Task Books_a_bill_for_the_approved_total()
    {
        var evt = new PurchaseOrderApprovedIntegrationEvent
        {
            POId = Guid.NewGuid(), PONumber = "PO-1", SupplierId = Guid.NewGuid(), TotalAmount = 1250m
        };

        await Handler().Handle(evt, default);

        _mediator.Verify(m => m.Send(
            It.Is<CreateBillCommand>(c => c.POId == evt.POId && c.SupplierId == evt.SupplierId && c.Amount == 1250m),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_redelivered_approval_does_not_book_a_second_bill()
    {
        var poId = Guid.NewGuid();
        _bills.Setup(b => b.GetByPurchaseOrderIdAsync(poId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Bill.Create("BILL-1", Guid.NewGuid(), poId, 10m, DateTime.UtcNow));

        await Handler().Handle(new PurchaseOrderApprovedIntegrationEvent { POId = poId, TotalAmount = 10m }, default);

        _mediator.Verify(m => m.Send(It.IsAny<CreateBillCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
