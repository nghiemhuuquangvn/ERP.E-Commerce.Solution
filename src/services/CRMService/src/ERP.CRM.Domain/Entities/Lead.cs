using ERP.Shared.Core.Domain;

namespace ERP.CRM.Domain.Entities
{
    public class Lead : AggregateRoot
    {
        public string LeadName { get; set; } = string.Empty;
        public int LeadStatus { get; set; }
        public int LeadSource { get; set; }
        public decimal? EstimatedValue { get; set; }

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

            return lead;
        }
    }
}
