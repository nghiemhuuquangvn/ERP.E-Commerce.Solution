using ERP.Shared.Infrastructure.Exceptions;
using ERP.SHP.Application.DTOs;
using ERP.SHP.Application.Interfaces;
using ERP.SHP.Domain.Entities;
using ERP.SHP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.SHP.Infrastructure.Services;

/// <summary>The shop's carrier selection rules (ROADMAP P1-11). Invalid input (unknown carrier, contradictory limits) is a business-rule error, HTTP 400.</summary>
public class CarrierRuleService : ICarrierRuleService
{
    private readonly ShippingDbContext _db;
    private readonly ICarrierProviderResolver _carriers;

    public CarrierRuleService(ShippingDbContext db, ICarrierProviderResolver carriers)
    {
        _db = db;
        _carriers = carriers;
    }

    public async Task<IReadOnlyList<CarrierRuleDto>> ListAsync(CancellationToken ct) =>
        (await _db.CarrierSelectionRules.AsNoTracking().OrderBy(r => r.Priority).ThenBy(r => r.Name).ToListAsync(ct)).Select(ToDto).ToList();

    public async Task<CarrierRuleDto> CreateAsync(SaveCarrierRuleRequest request, CancellationToken ct)
    {
        var tenantId = _db.CurrentTenantId ?? throw new BusinessRuleException("Shipping", "No shop is associated with this request.");
        CarrierSelectionRule rule;
        try
        {
            rule = CarrierSelectionRule.Create(tenantId, request.Name, request.Priority, request.CarrierCode);
            Apply(rule, request);
        }
        catch (ArgumentException ex)
        {
            throw new BusinessRuleException("Shipping", ex.Message);
        }
        _db.CarrierSelectionRules.Add(rule);
        await _db.SaveChangesAsync(ct);
        return ToDto(rule);
    }

    public async Task<CarrierRuleDto> UpdateAsync(Guid id, SaveCarrierRuleRequest request, CancellationToken ct)
    {
        var rule = await _db.CarrierSelectionRules.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new KeyNotFoundException($"Rule {id} not found.");
        try { Apply(rule, request); }
        catch (ArgumentException ex) { throw new BusinessRuleException("Shipping", ex.Message); }
        await _db.SaveChangesAsync(ct);
        return ToDto(rule);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var rule = await _db.CarrierSelectionRules.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new KeyNotFoundException($"Rule {id} not found.");
        _db.CarrierSelectionRules.Remove(rule);
        await _db.SaveChangesAsync(ct);
    }

    private void Apply(CarrierSelectionRule rule, SaveCarrierRuleRequest r)
    {
        if (_carriers.Provider(r.CarrierCode) is { Implemented: false })
            throw new ArgumentException($"{r.CarrierCode} is not integrated yet: a rule cannot send orders to it.");
        rule.Update(r.Name, r.Priority, r.IsActive, r.MatchProvinces, r.MinWeightGrams, r.MaxWeightGrams, r.MinOrderValue, r.MaxOrderValue, r.CodOnly, r.CarrierCode);
    }

    private static CarrierRuleDto ToDto(CarrierSelectionRule r) =>
        new(r.Id, r.Name, r.Priority, r.IsActive, r.MatchProvinces, r.MinWeightGrams, r.MaxWeightGrams, r.MinOrderValue, r.MaxOrderValue, r.CodOnly, r.CarrierCode);
}
