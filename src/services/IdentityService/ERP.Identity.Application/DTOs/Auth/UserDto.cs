using ERP.Identity.Domain.Entities;

public class UserDto
{
    public Guid Id { get; set; }
    public string? Email { get;  set; }
    public List<string>? Roles { get; set; }
    public List<string>? Permissions { get; set; }
}