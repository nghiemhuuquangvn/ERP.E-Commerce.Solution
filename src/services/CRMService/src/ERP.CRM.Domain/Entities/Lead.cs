using ERP.CRM.Domain.Events;
using ERP.Shared.Core.Domain;

namespace ERP.CRM.Domain.Entities
{
    public class Lead : AggregateRoot
    {
        public string LeadName { get; set; } = string.Empty;
        public int LeadStatus { get; set; }
        public int LeadSource { get; set; }
        public decimal? EstimatedValue { get; set; }
        public Guid? ContactId { get; set; }

        public static Lead Create(string leadName, int leadSource)
        {
            var lead = new Lead
            {
                Id = Guid.NewGuid(),
                LeadName = leadName,
                LeadStatus = 0,
                LeadSource = leadSource,
                CreatedAt = DateTime.UtcNow
            };

            lead.AddDomainEvent(new LeadCreatedEvent(lead.Id, leadName));

            return lead;
        }

        public void Contact()
        {
            if (LeadStatus == (int)Enums.LeadStatus.New)
            {
                LeadStatus = (int)Enums.LeadStatus.Contacted;
                UpdatedAt = DateTime.UtcNow;
            }
        }

        public void Qualify(Guid opportunityId)
        {
            if (LeadStatus != (int)Enums.LeadStatus.New && LeadStatus != (int)Enums.LeadStatus.Contacted)
                throw new InvalidOperationException("Lead must be in New or Contacted status to qualify.");

            LeadStatus = (int)Enums.LeadStatus.Qualified;
            UpdatedAt = DateTime.UtcNow;
            AddDomainEvent(new LeadQualifiedEvent(Id, opportunityId));
        }

        public void MarkLost(string reason)
        {
            LeadStatus = (int)Enums.LeadStatus.Lost;
            UpdatedAt = DateTime.UtcNow;
            AddDomainEvent(new LeadLostEvent(Id, reason));
        }
    }
}
