using ERP.Shared.Core.Domain;

namespace ERP.CRM.Domain.Entities;

public class ContactTag : BaseEntity
{
    public Guid ContactId { get; private set; }
    public string Tag { get; private set; } = string.Empty;

    private ContactTag() { }

    public static ContactTag Create(Guid contactId, string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            throw new InvalidOperationException("Tag cannot be empty.");

        return new ContactTag
        {
            Id = Guid.NewGuid(),
            ContactId = contactId,
            Tag = tag.Trim().ToLowerInvariant(),
            CreatedAt = DateTime.UtcNow
        };
    }
}
