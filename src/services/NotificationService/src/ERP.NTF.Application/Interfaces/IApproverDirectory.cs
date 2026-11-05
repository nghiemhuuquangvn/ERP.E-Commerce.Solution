namespace ERP.NTF.Application.Interfaces;

/// <summary>
/// Who holds a permission in the current tenant (the approvers of a purchase order, a sales order, a leave request). The answer comes from
/// IdentityService (what a token of that user carries); it is cached for a minute, so a role change reaches the approvers within that time.
/// Throws when Identity cannot answer, so the message is retried instead of reaching nobody.
/// </summary>
public interface IApproverDirectory
{
    Task<IReadOnlyList<Guid>> GetUserIdsAsync(string permission, CancellationToken ct = default);
}
