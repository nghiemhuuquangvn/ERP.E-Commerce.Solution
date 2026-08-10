using System.Text.Json;
using ERP.CHB.Application.Interfaces;

namespace ERP.CHB.Infrastructure.Gateway;

public class CrmEscalationClient : ICrmEscalationClient
{
    private readonly IErpGatewayClient _gateway;

    public CrmEscalationClient(IErpGatewayClient gateway)
    {
        _gateway = gateway;
    }

    public async Task<Guid> OpenCaseAsync(Guid callerUserId, string? callerJwt, string subject, string transcriptSummary, CancellationToken ct)
    {
        // CRMService's OpenCaseCommand needs a ContactId, not a UserId — GetContactByUserIdQuery
        // (added to CRMService alongside this feature) resolves it. A customer without an
        // existing Contact (e.g. one who never placed an order — Contacts are today
        // auto-provisioned by SalesOrderCreatedEventHandler) throws here, same as a 403.
        var contactJson = await _gateway.GetAsync($"/api/v1/crm/contacts/by-user/{callerUserId}", callerJwt, ct);
        using var contactDoc = JsonDocument.Parse(contactJson);
        if (!contactDoc.RootElement.TryGetProperty("id", out var contactIdEl))
            throw new InvalidOperationException($"No CRM contact found for user {callerUserId} (lookup returned: {contactJson}).");

        var contactId = contactIdEl.GetGuid();

        var body = new
        {
            title = subject,
            contactId,
            description = transcriptSummary,
            priority = 1 // CRM's Priority scale — 1 is its lowest/default tier; chatbot escalations aren't pre-triaged as urgent.
        };

        var caseJson = await _gateway.PostAsync("/api/v1/crm/cases", body, callerJwt, ct);
        using var caseDoc = JsonDocument.Parse(caseJson);
        if (!caseDoc.RootElement.TryGetProperty("id", out var caseIdEl))
            throw new InvalidOperationException($"CRM case creation did not return an id (response: {caseJson}).");

        return caseIdEl.GetGuid();
    }
}
