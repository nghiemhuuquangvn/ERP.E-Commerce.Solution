using ERP.Shared.Core.Domain;

namespace ERP.CRM.Domain.Entities
{
    public class Opportunity : AggregateRoot
    {
        public string OpportunityName { get; set; } = string.Empty;
        public int OpportunityStage { get; set; }
        public int ProbabilityPercent { get; set; }
        public decimal? EstimatedValue { get; set; }
        public DateTime? ExpectedCloseDate { get; set; }

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
    }
}
