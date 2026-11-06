using ERP.MFG.Application.Abstractions;
using ERP.MFG.Domain.Entities;
using ERP.MFG.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.MFG.Infrastructure.Repositories;

public sealed class MfgUnitOfWork(MfgDbContext db) : IMfgUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}

public sealed class PlantRepository(MfgDbContext db) : IPlantRepository
{
    public Task<Plant?> GetAsync(Guid id, CancellationToken ct = default)
        => db.Plants.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct);

    public Task<bool> CodeExistsAsync(string normalizedCode, Guid? exceptId, CancellationToken ct = default)
        => db.Plants.AnyAsync(p => p.Code == normalizedCode && !p.IsDeleted && (exceptId == null || p.Id != exceptId), ct);

    public Task<List<Plant>> ListAsync(IReadOnlyCollection<Guid>? onlyIds, bool activeOnly, CancellationToken ct = default)
    {
        var query = db.Plants.AsNoTracking().Where(p => !p.IsDeleted);
        if (onlyIds is not null) query = query.Where(p => onlyIds.Contains(p.Id));
        if (activeOnly) query = query.Where(p => p.IsActive);
        return query.ToListAsync(ct);
    }

    public void Add(Plant plant) => db.Plants.Add(plant);
}

public sealed class ProductionSettingRepository(MfgDbContext db) : IProductionSettingRepository
{
    public Task<ProductionSetting?> GetByPlantAsync(Guid plantId, CancellationToken ct = default)
        => db.ProductionSettings.FirstOrDefaultAsync(s => s.PlantId == plantId && !s.IsDeleted, ct);

    public Task<List<ProductionSettingAudit>> GetAuditAsync(Guid plantId, int take, CancellationToken ct = default)
        => db.ProductionSettingAudits.AsNoTracking().Where(a => a.PlantId == plantId)
            .OrderByDescending(a => a.ChangedAt).Take(take).ToListAsync(ct);

    public void Add(ProductionSetting setting) => db.ProductionSettings.Add(setting);
    public void AddAudit(ProductionSettingAudit audit) => db.ProductionSettingAudits.Add(audit);
}

public sealed class ItemProfileRepository(MfgDbContext db) : IItemProfileRepository
{
    public Task<ItemManufacturingProfile?> GetAsync(Guid itemId, Guid plantId, CancellationToken ct = default)
        => db.ItemProfiles.FirstOrDefaultAsync(p => p.ItemId == itemId && p.PlantId == plantId && !p.IsDeleted, ct);

    public async Task<(List<ItemManufacturingProfile> Items, int Total)> ListAsync(Guid plantId, string? search, int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.ItemProfiles.AsNoTracking().Where(p => p.PlantId == plantId && !p.IsDeleted);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(p => p.ItemCode.ToLower().Contains(term) || p.ItemName.ToLower().Contains(term));
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(p => p.ItemCode).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }

    public void Add(ItemManufacturingProfile profile) => db.ItemProfiles.Add(profile);
}

public sealed class PlantAccessRepository(MfgDbContext db) : IPlantAccessRepository
{
    public Task<List<Guid>> PlantIdsOfUserAsync(Guid userId, CancellationToken ct = default)
        => db.PlantUserAccesses.AsNoTracking().Where(a => a.UserId == userId && !a.IsDeleted).Select(a => a.PlantId).ToListAsync(ct);

    public Task<bool> ExistsAsync(Guid plantId, Guid userId, CancellationToken ct = default)
        => db.PlantUserAccesses.AnyAsync(a => a.PlantId == plantId && a.UserId == userId && !a.IsDeleted, ct);

    public Task<List<PlantUserAccess>> ListByPlantAsync(Guid plantId, CancellationToken ct = default)
        => db.PlantUserAccesses.AsNoTracking().Where(a => a.PlantId == plantId && !a.IsDeleted).ToListAsync(ct);

    public Task<PlantUserAccess?> GetAsync(Guid plantId, Guid userId, CancellationToken ct = default)
        => db.PlantUserAccesses.FirstOrDefaultAsync(a => a.PlantId == plantId && a.UserId == userId && !a.IsDeleted, ct);

    public void Add(PlantUserAccess access) => db.PlantUserAccesses.Add(access);
    public void Remove(PlantUserAccess access) => db.PlantUserAccesses.Remove(access);
}
