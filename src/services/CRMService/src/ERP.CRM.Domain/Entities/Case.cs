using ERP.CRM.Domain.Events;
using ERP.Shared.Core.Domain;

namespace ERP.CRM.Domain.Entities
{
    public class Case : AggregateRoot
    {
        public string CaseNumber { get; set; } = string.Empty;
        public Guid CustomerId { get; set; }
        public string CaseSubject { get; set; } = string.Empty;
        public string? CaseDescription { get; set; }
        public int CasePriority { get; set; }
        public int CaseStatus { get; set; }
        public string? ResolutionNote { get; set; }
        public Guid? AssignedToUserId { get; set; }

        /// <summary>
        /// Primary factory — generates a CaseNumber automatically and raises CaseOpenedEvent.
        /// </summary>
        public static Case Create(string title, Guid contactId, string description, int priority)
        {
            var caseNumber = $"CASE-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}";

            var caseEntity = new Case
            {
                Id = Guid.NewGuid(),
                CaseNumber = caseNumber,
                CustomerId = contactId,
                CaseSubject = title,
                CaseDescription = description,
                CasePriority = priority,
                CaseStatus = (int)Enums.CaseStatus.Open,
                CreatedAt = DateTime.UtcNow
            };

            caseEntity.AddDomainEvent(new CaseOpenedEvent(caseEntity.Id, contactId, title));

            return caseEntity;
        }

        public void Assign(Guid userId)
        {
            AssignedToUserId = userId;
            if (CaseStatus == (int)Enums.CaseStatus.Open)
                CaseStatus = (int)Enums.CaseStatus.InProgress;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Resolve(string resolution, string? contactEmail = null, string? contactName = null)
        {
            if (CaseStatus == (int)Enums.CaseStatus.Resolved || CaseStatus == (int)Enums.CaseStatus.Closed)
                throw new InvalidOperationException("Case is already resolved or closed.");

            CaseStatus = (int)Enums.CaseStatus.Resolved;
            ResolutionNote = resolution;
            UpdatedAt = DateTime.UtcNow;
            AddDomainEvent(new CaseResolvedEvent(Id, CustomerId, CaseSubject, contactEmail, contactName));
        }

        public void Close()
        {
            if (CaseStatus == (int)Enums.CaseStatus.Closed)
                throw new InvalidOperationException("Case is already closed.");

            CaseStatus = (int)Enums.CaseStatus.Closed;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
