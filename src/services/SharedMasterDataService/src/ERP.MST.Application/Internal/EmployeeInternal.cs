using ERP.MST.Application.Interfaces;
using ERP.MST.Domain.Enums;
using MediatR;

namespace ERP.MST.Application.Internal;

// Service-to-service read of employees (ManufacturingService crews). Identity facts only: number, name, whether still employed.

public record EmployeeLookupDto(Guid Id, string EmployeeNumber, string FullName, bool IsActive);

public record LookupEmployeesQuery(IReadOnlyCollection<Guid> Ids) : IRequest<List<EmployeeLookupDto>>;

/// <summary>"Active" here means not terminated (an employee on leave is still employed).</summary>
public class LookupEmployeesQueryHandler(IEmployeeRepository employees) : IRequestHandler<LookupEmployeesQuery, List<EmployeeLookupDto>>
{
    public async Task<List<EmployeeLookupDto>> Handle(LookupEmployeesQuery request, CancellationToken ct)
    {
        var ids = request.Ids.Distinct().ToList();
        if (ids.Count == 0) return new();
        var found = await employees.GetByIdsAsync(ids, ct);
        return found.Select(e => new EmployeeLookupDto(e.Id, e.EmployeeNumber, e.FullName, e.EmploymentStatus != (int)EmploymentStatus.Terminated)).ToList();
    }
}
