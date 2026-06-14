using ERP.Shared.Core.Domain;

namespace ERP.CRM.Domain.Entities
{
    public class Case : AggregateRoot
    {
        public string CaseNumber { get; set; } = string.Empty;
        public Guid CustomerId { get; set; }
        public string CaseSubject { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int CasePriority { get; set; }
        public int CaseStatus { get; set; }
        public string? ResolutionNote { get; set; }

        public static Case Create(string caseNumber, Guid customerId, string subject, int priority)
        {
            var caseEntity = new Case
            {
                Id = Guid.NewGuid(),
                CaseNumber = caseNumber,
                CustomerId = customerId,
                CaseSubject = subject,
                CasePriority = priority,
                CaseStatus = 0,
                CreatedAt = DateTime.UtcNow
            };

            return caseEntity;
        }
    }
}
