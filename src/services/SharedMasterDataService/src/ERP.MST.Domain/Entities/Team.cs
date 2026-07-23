using ERP.MST.Domain.Events;
using ERP.Shared.Core.Domain;

namespace ERP.MST.Domain.Entities
{
    public class Team : AggregateRoot
    {
        public Guid DepartmentId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public Guid? LeaderEmployeeId { get; set; }
        public bool IsActive { get; set; } = true;

        public static Team Create(
            Guid departmentId,
            string name,
            string code,
            Guid? leaderEmployeeId = null)
        {
            var team = new Team
            {
                Id = Guid.NewGuid(),
                DepartmentId = departmentId,
                Name = name,
                Code = code,
                LeaderEmployeeId = leaderEmployeeId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            team.AddDomainEvent(new TeamCreatedEvent(team.Id, team.DepartmentId, team.Name, team.Code));

            return team;
        }

        public void Update(string name, string code, Guid? leaderEmployeeId)
        {
            Name = name;
            Code = code;
            LeaderEmployeeId = leaderEmployeeId;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Deactivate()
        {
            IsActive = false;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Activate()
        {
            IsActive = true;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
