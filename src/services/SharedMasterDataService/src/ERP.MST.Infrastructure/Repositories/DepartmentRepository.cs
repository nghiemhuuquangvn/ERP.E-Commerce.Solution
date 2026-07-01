using ERP.MST.Application.Interfaces;
using ERP.MST.Domain.Entities;
using ERP.MST.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.MST.Infrastructure.Repositories;

public class DepartmentRepository : IDepartmentRepository
{
    private readonly MasterDataDbContext _context;

    public DepartmentRepository(MasterDataDbContext context)
    {
        _context = context;
    }

    public async Task<Department?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Departments
            .FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, cancellationToken);
    }

    public async Task<(IReadOnlyList<Department> Items, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Departments
            .Where(d => !d.IsDeleted)
            .AsQueryable();

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(d => d.DepartmentName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<Department>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Departments
            .Where(d => !d.IsDeleted)
            .OrderBy(d => d.DepartmentName)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.Departments
            .AnyAsync(d => !d.IsDeleted && d.Code == code, cancellationToken);
    }

    public async Task<DepartmentTranslation?> GetTranslationAsync(Guid ownerId, string languageCode, CancellationToken cancellationToken = default)
    {
        return await _context.DepartmentTranslations
            .FirstOrDefaultAsync(t => t.OwnerId == ownerId && t.LanguageCode == languageCode && !t.IsDeleted, cancellationToken);
    }

    public void Add(Department department)
    {
        _context.Departments.Add(department);
    }

    public void Update(Department department)
    {
        _context.Departments.Update(department);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
