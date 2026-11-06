using ERP.WH.Application.Interfaces;
using ERP.WH.Application.Queries.Warehouses;
using ERP.WH.Domain.Aggregates.Location;
using FluentAssertions;
using Moq;
using Xunit;

namespace ERP.WH.UnitTests.Lookup;

public class WarehouseLookupTests
{
    private static LookupWarehousesQueryHandler Arrange(params Warehouse[] all)
    {
        var repo = new Mock<IWarehouseRepository>();
        repo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(all.ToList());
        var uow = new Mock<IWarehouseUnitOfWork>();
        uow.SetupGet(u => u.Warehouses).Returns(repo.Object);
        return new LookupWarehousesQueryHandler(uow.Object);
    }

    [Fact]
    public async Task Returns_Only_The_Asked_Warehouses_With_Their_Active_Flag()
    {
        var rm = new Warehouse("WH-RM", "Raw materials");
        var fg = new Warehouse("WH-FG", "Finished goods");
        var other = new Warehouse("WH-X", "Other");
        typeof(Warehouse).GetProperty(nameof(Warehouse.IsActive))!.SetValue(fg, false);
        var handler = Arrange(rm, fg, other);

        var result = await handler.Handle(new LookupWarehousesQuery { Ids = [rm.Id, fg.Id, Guid.NewGuid()] }, default);

        result.Should().HaveCount(2);
        result.Single(w => w.Id == rm.Id).IsActive.Should().BeTrue();
        result.Single(w => w.Id == fg.Id).IsActive.Should().BeFalse();
        result.Select(w => w.Code).Should().BeEquivalentTo("WH-RM", "WH-FG");
    }

    [Fact]
    public async Task No_Ids_Gives_An_Empty_List()
    {
        (await Arrange(new Warehouse("A", "A")).Handle(new LookupWarehousesQuery(), default)).Should().BeEmpty();
    }
}
