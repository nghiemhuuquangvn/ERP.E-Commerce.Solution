using ERP.Shared.Core.Domain;
using ERP.NTF.Domain.Enums;

namespace ERP.NTF.Domain.Entities;

public class NotificationTemplate : AggregateRoot, ISharedReferenceData
{
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public NotificationChannel Channel { get; private set; }
    public string Subject { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public string? Variables { get; private set; }
    public bool IsActive { get; private set; }

    private NotificationTemplate() { }

    public static NotificationTemplate Create(
        string code,
        string name,
        NotificationChannel channel,
        string subject,
        string body,
        string? variables = null)
    {
        return new NotificationTemplate
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            Channel = channel,
            Subject = subject,
            Body = body,
            Variables = variables,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string name, string subject, string body, string? variables)
    {
        Name = name;
        Subject = subject;
        Body = body;
        Variables = variables;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    public string Render(Dictionary<string, string> variables)
    {
        var result = Body;
        foreach (var (key, value) in variables)
            result = result.Replace($"{{{key}}}", value);
        return result;
    }
}
