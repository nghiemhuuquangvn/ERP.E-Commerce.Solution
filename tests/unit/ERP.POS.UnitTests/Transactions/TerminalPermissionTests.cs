using ERP.POS.API.Controllers;
using ERP.POS.Application.Commands;
using ERP.POS.Application.DTOs;
using ERP.POS.Application.Queries;
using ERP.Shared.Authorization;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace ERP.POS.UnitTests.Transactions;

/// <summary>R110: the staff app lists the POS terminals so a cashier can pick one; creating and changing terminals stays an administrator's job.</summary>
public class TerminalPermissionTests
{
    private readonly Mock<IMediator> _mediator = new();

    private TerminalsController Controller(params string[] permissions)
    {
        _mediator.Setup(m => m.Send(It.IsAny<ListTerminalsQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<TerminalDto>());
        _mediator.Setup(m => m.Send(It.IsAny<GetTerminalQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(new TerminalDto { Id = Guid.NewGuid(), Name = "T1" });
        _mediator.Setup(m => m.Send(It.IsAny<CreateTerminalCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(new TerminalDto { Id = Guid.NewGuid(), Name = "T1" });
        return new TerminalsController(_mediator.Object, new UserContext { Permissions = permissions });
    }

    [Fact]
    public async Task ACashierWithPosTransactionsCanListAndReadTerminals()
    {
        var c = Controller("pos:transactions");
        (await c.List(null, default)).Should().BeOfType<OkObjectResult>();
        (await c.GetById(Guid.NewGuid(), default)).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task AnAdministratorWithTerminalsManageStillCanList()
    {
        (await Controller("pos:terminals:manage").List(null, default)).Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task SomeoneWithNeitherPermissionIsForbiddenToListOrRead()
    {
        var c = Controller("pos:reports:view");
        (await c.List(null, default)).Should().BeOfType<ForbidResult>();
        (await c.GetById(Guid.NewGuid(), default)).Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task ACashierCannotCreateRenameOrSwitchTerminals()
    {
        var c = Controller("pos:transactions");
        (await c.Create(new CreateTerminalCommand(), default)).Should().BeOfType<ForbidResult>();
        (await c.Activate(Guid.NewGuid(), default)).Should().BeOfType<ForbidResult>();
        (await c.Deactivate(Guid.NewGuid(), default)).Should().BeOfType<ForbidResult>();
        (await c.Rename(Guid.NewGuid(), new RenameTerminalCommand(), default)).Should().BeOfType<ForbidResult>();
    }
}
