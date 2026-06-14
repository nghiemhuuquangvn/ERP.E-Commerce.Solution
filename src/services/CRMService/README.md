# CRM Service

Customer Relationship Management including contacts, leads, opportunities, and customer support cases for the ERP E-Commerce Solution.

## Overview

The CRM Service manages all customer-facing business development and support:
- **Contacts** — Individual customer/prospect records (linked to Identity UserId)
- **Leads** — Unqualified prospects with qualification workflow
- **Opportunities** — Qualified sales prospects with stage-based pipeline
- **Cases** — Customer support tickets with resolution workflow
- **Activities** — Call/email/meeting logs linked to contacts, leads, opportunities

## Core Domain Model

### Aggregates

**Contact** (Aggregate Root)
- ContactId, UserId (from Identity Service when customer registers)
- Name, Email, Phone
- Company, Title
- ContactSource (Manual, UserRegistered, Lead, Import)
- List<Activity> children

**Lead** (Aggregate Root)
- LeadId, LeadName
- LeadStatus (New, Contacted, Qualified, Disqualified)
- LeadSource
- EstimatedValue
- Converts to Opportunity on qualification

**Opportunity** (Aggregate Root)
- OpportunityId, OpportunityName
- OpportunityStage (Prospecting, Qualification, Proposal, Negotiation, ClosedWon, ClosedLost)
- ProbabilityPercent (auto-set by stage: 10% → 100%)
- EstimatedValue, ExpectedCloseDate
- List<Activity> children

**Case** (Aggregate Root)
- CaseId, CaseNumber
- CustomerId (UserId)
- CaseSubject, Description
- CasePriority (Low, Medium, High, Critical)
- CaseStatus (Open, InProgress, Resolved, Closed)
- ResolutionNote required before closing

**Activity** (Entity, child of Contact/Lead/Opportunity/Case)
- ActivityId, ActivityType (Call, Email, Meeting, Task)
- Subject, Notes, DueDate, CompletedDate

## Key Business Rules

1. A Lead must be Qualified before converting to Opportunity
2. Contact is auto-created when UserCreatedIntegrationEvent arrives with UserType=Customer
3. Cases must have resolution note before closing
4. Opportunity probability auto-set: Prospecting=10%, Qualification=25%, Proposal=50%, Negotiation=75%, ClosedWon=100%
5. Lead can be disqualified (archived, not pursued)

## Database

**Database Name:** `CrmDB`

**Key Tables:**
- Contacts
- Leads, Opportunities
- Cases, CaseResolutions
- Activities
- OutboxMessages

## Integration Events

### Published Events

**LeadCreatedIntegrationEvent**
- Subscribers: ReportingService

**LeadQualifiedIntegrationEvent**
- Subscribers: SalesOrderService, ReportingService

**OpportunityWonIntegrationEvent**
- Triggers sales order creation workflow
- Subscribers: SalesOrderService, ReportingService

**CaseOpenedIntegrationEvent**
- Subscribers: NotificationService, ReportingService

**CaseResolvedIntegrationEvent**
- Subscribers: NotificationService, ReportingService

### Consumed Events

**UserRegisteredIntegrationEvent** (from Identity)
→ Auto-create Contact if UserType=Customer

**SalesOrderCreatedIntegrationEvent** (from SO)
→ Attach order to Contact history

**SalesOrderDeliveredIntegrationEvent** (from SO)
→ Record fulfillment in Contact timeline

## API Endpoints

```
GET    /api/v1/contacts                    - List contacts
POST   /api/v1/contacts                    - Create contact
GET    /api/v1/contacts/{id}               - Contact detail
PUT    /api/v1/contacts/{id}               - Update contact
GET    /api/v1/contacts/{id}/activities    - Activity history

GET    /api/v1/leads                       - List leads
POST   /api/v1/leads                       - Create lead
GET    /api/v1/leads/{id}                  - Lead detail
POST   /api/v1/leads/{id}/qualify          - Qualify lead → Opportunity
POST   /api/v1/leads/{id}/disqualify       - Disqualify lead

GET    /api/v1/opportunities               - List opportunities
POST   /api/v1/opportunities               - Create opportunity
GET    /api/v1/opportunities/{id}          - Opportunity detail
PUT    /api/v1/opportunities/{id}/stage    - Update stage
POST   /api/v1/opportunities/{id}/won      - Mark won
POST   /api/v1/opportunities/{id}/lost     - Mark lost

GET    /api/v1/cases                       - List cases
POST   /api/v1/cases                       - Open case
GET    /api/v1/cases/{id}                  - Case detail
PUT    /api/v1/cases/{id}                  - Update case
POST   /api/v1/cases/{id}/resolve          - Resolve case
POST   /api/v1/cases/{id}/close            - Close case

POST   /api/v1/activities                  - Log activity
```

## Dependencies

**Services that depend on CRM Service:**
- ReportingService (consumes events)

**Services CRM depends on:**
- IdentityService (events, user validation)
- SalesOrderService (events)

## Technology Stack

- **.NET 10**
- **EF Core** — ORM
- **SQL Server** — CrmDB
- **Kafka** — Event streaming
- **MediatR** — CQRS

---

**Last Updated:** 2026-06-13  
**Version:** 1.0.0
