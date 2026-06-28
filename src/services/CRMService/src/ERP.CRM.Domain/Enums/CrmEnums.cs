namespace ERP.CRM.Domain.Enums;

public enum LeadStatus
{
    New = 0,
    Contacted = 1,
    Qualified = 2,
    Lost = 3
}

public enum LeadSource
{
    Website = 0,
    Referral = 1,
    SocialMedia = 2,
    Email = 3,
    Phone = 4,
    Other = 5
}

public enum OpportunityStage
{
    Prospecting = 0,
    Qualification = 1,
    Proposal = 2,
    Negotiation = 3,
    Won = 4,
    Lost = 5
}

public enum CasePriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

public enum CaseStatus
{
    Open = 0,
    InProgress = 1,
    Resolved = 2,
    Closed = 3
}

public enum ContactSource
{
    Manual = 0,
    UserRegistration = 1,
    Import = 2
}
