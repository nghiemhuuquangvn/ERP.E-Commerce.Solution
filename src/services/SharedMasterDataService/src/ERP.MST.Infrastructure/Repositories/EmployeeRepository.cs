using ERP.MST.Application.Interfaces;
using ERP.MST.Domain.Entities;
using ERP.MST.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.MST.Infrastructure.Repositories;

public class EmployeeRepository : IEmployeeRepository
{
    private readonly MasterDataDbContext _context;

    public EmployeeRepository(MasterDataDbContext context)
    {
        _context = context;
    }

    public async Task<Employee?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Employees
            .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted, cancellationToken);
    }

    public async Task<(IReadOnlyList<Employee> Items, int TotalCount)> ListAsync(
        Guid? departmentId,
        int? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Employees
            .Where(e => !e.IsDeleted)
            .AsQueryable();

        if (departmentId.HasValue)
            query = query.Where(e => e.DepartmentId == departmentId.Value);

        if (status.HasValue)
            query = query.Where(e => e.EmploymentStatus == status.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(e => e.LastName)
            .ThenBy(e => e.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<Employee>> GetActiveEmployeesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Employees
            .Where(e => !e.IsDeleted && e.EmploymentStatus == 0)
            .OrderBy(e => e.LastName)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _context.Employees
            .AnyAsync(e => !e.IsDeleted && e.Email == email, cancellationToken);
    }

    public async Task<HashSet<string>> GetExistingEmailsAsync(IReadOnlyCollection<string> lowerCaseEmails, CancellationToken cancellationToken = default)
    {
        if (lowerCaseEmails.Count == 0) return new HashSet<string>();
        var found = await _context.Employees
            .Where(e => !e.IsDeleted && e.Email != null && lowerCaseEmails.Contains(e.Email.ToLower()))
            .Select(e => e.Email!.ToLower())
            .ToListAsync(cancellationToken);
        return new HashSet<string>(found);
    }

    public async Task<HashSet<string>> GetExistingNumbersAsync(IReadOnlyCollection<string> upperCaseNumbers, CancellationToken cancellationToken = default)
    {
        if (upperCaseNumbers.Count == 0) return new HashSet<string>();
        var found = await _context.Employees
            .Where(e => !e.IsDeleted && upperCaseNumbers.Contains(e.EmployeeNumber.ToUpper()))
            .Select(e => e.EmployeeNumber.ToUpper())
            .ToListAsync(cancellationToken);
        return new HashSet<string>(found);
    }

    public async Task<int> GetMaxNumberSuffixAsync(string prefix, int digits, CancellationToken cancellationToken = default)
    {
        var length = prefix.Length + digits;
        var numbers = await _context.Employees
            .IgnoreQueryFilters()   // deleted rows too, and only this tenant's (below): a number is never reused
            .Where(e => e.EmployeeNumber.StartsWith(prefix) && e.EmployeeNumber.Length == length && e.TenantId == _context.CurrentTenantId)
            .Select(e => e.EmployeeNumber)
            .ToListAsync(cancellationToken);
        var max = 0;
        foreach (var number in numbers)
            if (int.TryParse(number.AsSpan(prefix.Length), out var suffix) && suffix > max) max = suffix;
        return max;
    }

    public void ClearTracking() => _context.ChangeTracker.Clear();

    public void Add(Employee employee)
    {
        _context.Employees.Add(employee);
    }

    public void Update(Employee employee)
    {
        _context.Employees.Update(employee);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
