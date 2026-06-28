using ERP.HRM.Domain.Entities;

namespace ERP.HRM.Application.Interfaces;

public interface IEmployeeRepository
{
    Task<Employee?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Employee> Items, int TotalCount)> ListAsync(
        Guid? departmentId,
        int? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Employee>> GetActiveEmployeesAsync(CancellationToken cancellationToken = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);
    void Add(Employee employee);
    void Update(Employee employee);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IDepartmentRepository
{
    Task<Department?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Department> Items, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Department>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default);
    void Add(Department department);
    void Update(Department department);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface ILeaveRequestRepository
{
    Task<LeaveRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<LeaveRequest> Items, int TotalCount)> ListAsync(
        Guid? employeeId,
        int? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    void Add(LeaveRequest leaveRequest);
    void Update(LeaveRequest leaveRequest);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IPaySlipRepository
{
    Task<PaySlip?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<PaySlip> Items, int TotalCount)> ListAsync(
        Guid? employeeId,
        int? year,
        int? month,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<bool> ExistsForPeriodAsync(Guid employeeId, int year, int month, CancellationToken cancellationToken = default);
    void Add(PaySlip paySlip);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
