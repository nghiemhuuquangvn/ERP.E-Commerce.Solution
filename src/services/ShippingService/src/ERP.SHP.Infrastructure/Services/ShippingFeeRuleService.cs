using ERP.Shared.Infrastructure.Exceptions;
using ERP.SHP.Application.DTOs;
using ERP.SHP.Application.Interfaces;
using ERP.SHP.Domain.Entities;
using ERP.SHP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.SHP.Infrastructure.Services;

/// <summary>The shop's delivery price list (ROADMAP P1-11c). Invalid input (negative fee, contradictory weights, a name used twice) is a business-rule error, HTTP 400.</summary>
public class ShippingFeeRuleService : IShippingFeeRuleService
{
    private readonly ShippingDbContext _db;

    public ShippingFeeRuleService(ShippingDbContext db) => _db = db;

    public async Task<IReadOnlyList<ShippingFeeRuleDto>> ListAsync(CancellationToken ct) =>
        (await _db.ShippingFeeRules.AsNoTracking().OrderBy(r => r.Priority).ThenBy(r => r.Name).ToListAsync(ct)).Select(ToDto).ToList();

    public async Task<ShippingFeeRuleDto> CreateAsync(SaveShippingFeeRuleRequest request, CancellationToken ct)
    {
        var tenantId = _db.CurrentTenantId ?? throw new BusinessRuleException("Shipping", "No shop is associated with this request.");
        ShippingFeeRule rule;
        try
        {
            rule = ShippingFeeRule.Create(tenantId, request.Name, request.Priority, request.Fee);
            Apply(rule, request);
        }
        catch (ArgumentException ex)
        {
            throw new BusinessRuleException("Shipping", ex.Message);
        }
        await EnsureNameFreeAsync(rule.Name, null, ct);
        _db.ShippingFeeRules.Add(rule);
        await _db.SaveChangesAsync(ct);
        return ToDto(rule);
    }

    public async Task<ShippingFeeRuleDto> UpdateAsync(Guid id, SaveShippingFeeRuleRequest request, CancellationToken ct)
    {
        var rule = await _db.ShippingFeeRules.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new KeyNotFoundException($"Fee row {id} not found.");
        try { Apply(rule, request); }
        catch (ArgumentException ex) { throw new BusinessRuleException("Shipping", ex.Message); }
        await EnsureNameFreeAsync(rule.Name, rule.Id, ct);
        await _db.SaveChangesAsync(ct);
        return ToDto(rule);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var rule = await _db.ShippingFeeRules.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new KeyNotFoundException($"Fee row {id} not found.");
        _db.ShippingFeeRules.Remove(rule);
        await _db.SaveChangesAsync(ct);
    }

    private async Task EnsureNameFreeAsync(string name, Guid? ownId, CancellationToken ct)
    {
        if (await _db.ShippingFeeRules.AnyAsync(r => r.Name == name && r.Id != ownId, ct))
            throw new BusinessRuleException("Shipping", $"A fee row named '{name}' already exists.");
    }

    private static void Apply(ShippingFeeRule rule, SaveShippingFeeRuleRequest r) =>
        rule.Update(r.Name, r.Priority, r.IsActive, r.MatchProvinces, r.MinWeightGrams, r.MaxWeightGrams, r.Fee);

    private static ShippingFeeRuleDto ToDto(ShippingFeeRule r) =>
        new(r.Id, r.Name, r.Priority, r.IsActive, r.MatchProvinces, r.MinWeightGrams, r.MaxWeightGrams, r.Fee);
}
