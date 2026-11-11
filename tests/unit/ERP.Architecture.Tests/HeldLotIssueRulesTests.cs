using ERP.Shared.Authorization;
using ERP.WH.API.Controllers;
using ERP.WH.Application.Commands.Inventory;
using FluentAssertions;
using MediatR;
using Moq;
using Xunit;

namespace ERP.Architecture.Tests;

/// <summary>
/// W7: issuing a lot that is on quality hold (a rework order consumes the lot it reworks) is a privilege of the internal service API.
/// The user endpoint binds the same command, so it must drop the flag before the command is sent.
/// </summary>
public class HeldLotIssueRulesTests
{
    [Fact]
    public async Task A_user_with_inventory_manage_cannot_set_AllowHeldLot_through_the_movement_endpoint()
    {
        RecordStockMovementCommand? sent = null;
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<RecordStockMovementCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<RecordStockMovementResponse>, CancellationToken>((c, _) => sent = (RecordStockMovementCommand)c)
            .ReturnsAsync(new RecordStockMovementResponse { MovementId = Guid.NewGuid(), MovementType = "ProductionIssue" });
        var user = new Mock<IUserContextService>();
        user.Setup(u => u.HasPermission("inventory:manage")).Returns(true);
        var controller = new StockMovementController(mediator.Object, user.Object);

        await controller.RecordMovement(new RecordStockMovementCommand
        {
            MovementType = "ProductionIssue", ProductId = Guid.NewGuid(), Quantity = 1, Reference = "X-1#1", LotId = Guid.NewGuid(), AllowHeldLot = true, Reason = "try"
        });

        sent.Should().NotBeNull("the command was sent");
        sent!.AllowHeldLot.Should().BeFalse("only the internal stock API may issue a held lot");
    }

    [Fact]
    public void Only_the_internal_command_carries_the_flag_to_the_service()
    {
        // the internal route is the one place that maps it; the public controller sets it to false (test above)
        typeof(InternalProductionMovementCommand).GetProperty(nameof(InternalProductionMovementCommand.AllowHeldLot)).Should().NotBeNull();
        var internalController = typeof(InternalStockController).GetMethod(nameof(InternalStockController.Movement));
        internalController!.GetCustomAttributes(typeof(ERP.Shared.Authorization.ServiceAuth.AuthorizeInternalServiceAttribute), true).Should().NotBeEmpty();
    }
}
