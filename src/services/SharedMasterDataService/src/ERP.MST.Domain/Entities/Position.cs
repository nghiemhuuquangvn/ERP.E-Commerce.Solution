using ERP.MST.Domain.Events;
using ERP.Shared.Core.Domain;

namespace ERP.MST.Domain.Entities
{
    public class Position : AggregateRoot
    {
        public string Title { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public new string? Description { get; set; }
        public Guid? DepartmentId { get; set; }
        public bool IsActive { get; set; } = true;

        public static Position Create(
            string title,
            string code,
            string? description = null,
            Guid? departmentId = null)
        {
            var position = new Position
            {
                Id = Guid.NewGuid(),
                Title = title,
                Code = code,
                Description = description,
                DepartmentId = departmentId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            position.AddDomainEvent(new PositionCreatedEvent(position.Id, position.Title, position.Code));

            return position;
        }

        public void Update(string title, string? description)
        {
            Title = title;
            Description = description;
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
