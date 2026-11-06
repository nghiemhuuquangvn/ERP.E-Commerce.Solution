using ERP.MST.Application.Interfaces;
using ERP.MST.Application.Internal;
using ERP.MST.Domain.Entities;
using FluentAssertions;
using Moq;
using Xunit;

namespace ERP.MST.UnitTests.Units;

public class EmployeeLookupTests
{
    private static Employee Make(string number, int status)
    {
        var e = Employee.Create(number, "An", "Nguyen", Guid.NewGuid(), new DateTime(2026, 1, 1));
        e.EmploymentStatus = status;
        return e;
    }

    private static LookupEmployeesQueryHandler Arrange(params Employee[] all)
    {
        var repo = new Mock<IEmployeeRepository>();
        repo.Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> ids, CancellationToken _) => (IReadOnlyList<Employee>)all.Where(e => ids.Contains(e.Id)).ToList());
        return new LookupEmployeesQueryHandler(repo.Object);
    }

    [Fact]
    public async Task An_Employed_Person_Is_Active_Including_One_On_Leave_And_A_Terminated_One_Is_Not()
    {
        var active = Make("NV001", 0);
        var onLeave = Make("NV002", 1);
        var gone = Make("NV003", 2);
        var handler = Arrange(active, onLeave, gone);

        var result = await handler.Handle(new LookupEmployeesQuery([active.Id, onLeave.Id, gone.Id, Guid.NewGuid()]), default);

        result.Should().HaveCount(3);
        result.Single(r => r.EmployeeNumber == "NV001").IsActive.Should().BeTrue();
        result.Single(r => r.EmployeeNumber == "NV002").IsActive.Should().BeTrue();
        result.Single(r => r.EmployeeNumber == "NV003").IsActive.Should().BeFalse();
        result.First().FullName.Should().Be("An Nguyen");
    }

    [Fact]
    public async Task No_Ids_Gives_An_Empty_List()
    {
        (await Arrange().Handle(new LookupEmployeesQuery([]), default)).Should().BeEmpty();
    }
}
