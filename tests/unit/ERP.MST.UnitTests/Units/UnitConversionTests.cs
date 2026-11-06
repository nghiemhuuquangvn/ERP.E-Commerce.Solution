using ERP.MST.Application.Interfaces;
using ERP.MST.Application.Internal;
using ERP.MST.Domain.Entities;
using ERP.Shared.Infrastructure.Exceptions;
using FluentAssertions;
using Moq;
using Xunit;

namespace ERP.MST.UnitTests.Units;

public class UnitConversionTests
{
    // kg is the base weight unit (factor 1); g = 0.001 kg; tonne = 1000 kg; litre is a different base.
    private static readonly UnitOfMeasure Kg = UnitOfMeasure.Create("KG", "Kilogram", "kg", 0, null, 1m, 3);
    private static readonly UnitOfMeasure G = UnitOfMeasure.Create("G", "Gram", "g", 0, Kg.Id, 0.001m, 0);
    private static readonly UnitOfMeasure Tonne = UnitOfMeasure.Create("T", "Tonne", "t", 0, Kg.Id, 1000m, 3);
    private static readonly UnitOfMeasure Litre = UnitOfMeasure.Create("L", "Litre", "l", 1, null, 1m, 2);

    private static IUnitOfMeasureRepository Repo()
    {
        var all = new[] { Kg, G, Tonne, Litre };
        var repo = new Mock<IUnitOfMeasureRepository>();
        repo.Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> ids, CancellationToken _) => (IReadOnlyList<UnitOfMeasure>)all.Where(u => ids.Contains(u.Id)).ToList());
        return repo.Object;
    }

    [Theory]
    [InlineData("kg", "g", 2.5, 2500)]
    [InlineData("g", "kg", 500, 0.5)]
    [InlineData("t", "g", 1, 1000000)]
    [InlineData("kg", "kg", 7, 7)]
    public async Task Converts_Through_The_Common_Base_Unit(string from, string to, double qty, double expected)
    {
        var units = new Dictionary<string, UnitOfMeasure> { ["kg"] = Kg, ["g"] = G, ["t"] = Tonne };

        var result = await new ConvertUnitQueryHandler(Repo()).Handle(new ConvertUnitQuery(units[from].Id, units[to].Id, (decimal)qty), default);

        result!.Quantity.Should().Be((decimal)expected);
        result.ToDecimalPlaces.Should().Be(units[to].DecimalPlaces);
    }

    [Fact]
    public async Task Units_Of_Different_Bases_Are_Refused()
    {
        var act = () => new ConvertUnitQueryHandler(Repo()).Handle(new ConvertUnitQuery(Kg.Id, Litre.Id, 1), default);

        (await act.Should().ThrowAsync<BusinessRuleException>()).Which.RuleName.Should().Be("UOM_INCOMPATIBLE");
    }

    [Fact]
    public async Task Unknown_Unit_Gives_Null()
    {
        (await new ConvertUnitQueryHandler(Repo()).Handle(new ConvertUnitQuery(Kg.Id, Guid.NewGuid(), 1), default)).Should().BeNull();
    }

    [Fact]
    public async Task Lookup_Returns_Decimal_Places()
    {
        var result = await new LookupUnitsOfMeasureQueryHandler(Repo()).Handle(new LookupUnitsOfMeasureQuery([Kg.Id, G.Id]), default);

        result.Single(u => u.Code == "KG").DecimalPlaces.Should().Be(3);
        result.Single(u => u.Code == "G").DecimalPlaces.Should().Be(0);
    }

    [Fact]
    public void Update_Changes_Decimal_Places_Only_When_Given()
    {
        var u = UnitOfMeasure.Create("X", "X", "x", 3, null, 1m, 2);

        u.Update("X2", "x", 1m);
        u.DecimalPlaces.Should().Be(2);

        u.Update("X2", "x", 1m, 4);
        u.DecimalPlaces.Should().Be(4);
    }
}
