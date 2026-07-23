using ERP.MST.Domain.Events;
using ERP.Shared.Core.Domain;

namespace ERP.MST.Domain.Entities
{
    public class EmployeeTeamAssignment : AggregateRoot
    {
        public Guid EmployeeId { get; set; }
        public Guid TeamId { get; set; }
        public Guid? PositionId { get; set; }
        public DateTime EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }

        public static EmployeeTeamAssignment Create(
            Guid employeeId,
            Guid teamId,
            DateTime effectiveFrom,
            Guid? positionId = null)
        {
            var assignment = new EmployeeTeamAssignment
            {
                Id = Guid.NewGuid(),
                EmployeeId = employeeId,
                TeamId = teamId,
                PositionId = positionId,
                EffectiveFrom = effectiveFrom,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            assignment.AddDomainEvent(new EmployeeTeamAssignmentChangedEvent(assignment.EmployeeId, assignment.TeamId, assignment.PositionId));

            return assignment;
        }

        public void End(DateTime effectiveTo)
        {
            EffectiveTo = effectiveTo;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
