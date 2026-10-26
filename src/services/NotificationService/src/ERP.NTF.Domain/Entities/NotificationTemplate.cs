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

    public string Render(Dictionary<string, string> variables) => Fill(Body, variables);

    /// <summary>The subject carries placeholders too ("Order #{OrderNumber} confirmed").</summary>
    public string RenderSubject(Dictionary<string, string> variables) => Fill(Subject, variables);

    /// <summary>Replaces <c>{name}</c> with the variable of that name, ignoring case ({OrderNumber} = orderNumber); unknown placeholders stay.</summary>
    public static string Fill(string text, Dictionary<string, string> variables)
    {
        if (string.IsNullOrEmpty(text) || variables.Count == 0) return text;
        var byName = new Dictionary<string, string>(variables, StringComparer.OrdinalIgnoreCase);
        return System.Text.RegularExpressions.Regex.Replace(text, @"\{([A-Za-z0-9_]+)\}",
            m => byName.TryGetValue(m.Groups[1].Value, out var value) ? value : m.Value);
    }
}
