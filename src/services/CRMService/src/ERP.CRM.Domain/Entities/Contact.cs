using ERP.Shared.Core.Domain;

namespace ERP.CRM.Domain.Entities
{
    public class Contact : AggregateRoot
    {
        public Guid? UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Company { get; set; }
        public int ContactSource { get; set; }

        public static Contact Create(string name, string? email, string? phone, Guid? userId = null)
        {
            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                Name = name,
                Email = email,
                Phone = phone,
                UserId = userId,
                ContactSource = 0,
                CreatedAt = DateTime.UtcNow
            };

            return contact;
        }
    }
}
