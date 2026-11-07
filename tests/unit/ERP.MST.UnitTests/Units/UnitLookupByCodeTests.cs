using ERP.MST.Application.Interfaces;
using ERP.MST.Application.Internal;
using ERP.MST.Domain.Entities;
using FluentAssertions;
using Moq;
using Xunit;

namespace ERP.MST.UnitTests.Units;

public class UnitLookupByCodeTests
{
    [Fact]
    public async Task Units_Are_Found_By_Code_With_Their_Decimals_And_Base_And_Duplicate_Codes_Are_Asked_Once()
    {
        var kg = UnitOfMeasure.Create("KG", "Kilogram", "kg", 0, null, 1m, 3);
        var repo = new Mock<IUnitOfMeasureRepository>();
        repo.Setup(r => r.GetByCodesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync([kg]);

        var result = await new LookupUnitsByCodeQueryHandler(repo.Object).Handle(new LookupUnitsByCodeQuery(["kg", "KG", " ", "kg"]), default);

        result.Should().ContainSingle().Which.Should().Match<UnitLookupDto>(u => u.Id == kg.Id && u.Code == "KG" && u.DecimalPlaces == 3);
        repo.Verify(r => r.GetByCodesAsync(It.Is<IReadOnlyCollection<string>>(c => c.Count == 1), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task No_Codes_Means_No_Query()
    {
        var repo = new Mock<IUnitOfMeasureRepository>();

        (await new LookupUnitsByCodeQueryHandler(repo.Object).Handle(new LookupUnitsByCodeQuery([" "]), default)).Should().BeEmpty();
        repo.Verify(r => r.GetByCodesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
