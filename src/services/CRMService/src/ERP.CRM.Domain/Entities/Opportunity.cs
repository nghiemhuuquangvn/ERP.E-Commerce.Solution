using ERP.CRM.Domain.Events;
using ERP.Shared.Core.Domain;

namespace ERP.CRM.Domain.Entities
{
    public class Opportunity : AggregateRoot
    {
        public string OpportunityName { get; set; } = string.Empty;
        public Guid ContactId { get; set; }
        public int OpportunityStage { get; set; }
        public int ProbabilityPercent { get; set; }
        public decimal? EstimatedValue { get; set; }
        public DateTime? ExpectedCloseDate { get; set; }
        public Guid? AssignedToUserId { get; set; }
        public string? LostReason { get; set; }

        private static readonly int[] StageProbabilities = { 10, 20, 40, 60, 100, 0 };

        public static Opportunity Create(string opportunityName, decimal? estimatedValue, DateTime? expectedCloseDate)
        {
            var opportunity = new Opportunity
            {
                Id = Guid.NewGuid(),
                OpportunityName = opportunityName,
                OpportunityStage = 0,
                ProbabilityPercent = 10,
                EstimatedValue = estimatedValue,
                ExpectedCloseDate = expectedCloseDate,
                CreatedAt = DateTime.UtcNow
            };

            return opportunity;
        }

        public static Opportunity Create(string title, Guid contactId, decimal value, DateTime expectedCloseDate, Guid? assignedToUserId = null)
        {
            var opportunity = new Opportunity
            {
                Id = Guid.NewGuid(),
                OpportunityName = title,
                ContactId = contactId,
                OpportunityStage = (int)Enums.OpportunityStage.Prospecting,
                ProbabilityPercent = 10,
                EstimatedValue = value,
                ExpectedCloseDate = expectedCloseDate,
                AssignedToUserId = assignedToUserId,
                CreatedAt = DateTime.UtcNow
            };

            opportunity.AddDomainEvent(new OpportunityCreatedEvent(opportunity.Id, contactId, title, value));

            return opportunity;
        }

        public void AdvanceStage()
        {
            if (OpportunityStage >= (int)Enums.OpportunityStage.Negotiation)
                throw new InvalidOperationException("Cannot advance stage beyond Negotiation. Use MarkWon or MarkLost.");

            OpportunityStage++;
            ProbabilityPercent = StageProbabilities[OpportunityStage];
            UpdatedAt = DateTime.UtcNow;
        }

        public void MarkWon()
        {
            if (OpportunityStage == (int)Enums.OpportunityStage.Won || OpportunityStage == (int)Enums.OpportunityStage.Lost)
                throw new InvalidOperationException("Opportunity is already closed.");

            OpportunityStage = (int)Enums.OpportunityStage.Won;
            ProbabilityPercent = 100;
            UpdatedAt = DateTime.UtcNow;
            AddDomainEvent(new OpportunityWonEvent(Id, EstimatedValue ?? 0, ContactId, OpportunityName));
        }

        public void MarkLost(string reason)
        {
            if (OpportunityStage == (int)Enums.OpportunityStage.Won || OpportunityStage == (int)Enums.OpportunityStage.Lost)
                throw new InvalidOperationException("Opportunity is already closed.");

            OpportunityStage = (int)Enums.OpportunityStage.Lost;
            ProbabilityPercent = 0;
            LostReason = reason;
            UpdatedAt = DateTime.UtcNow;
            AddDomainEvent(new OpportunityLostEvent(Id));
        }
    }
}
