namespace ERP.Identity.Api.Authorization;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePermissionAttribute : Attribute
{
    public IReadOnlyList<string> Permissions { get; }
    public RequirePermissionAttribute(params string[] permissions)
    {
        Permissions = permissions;
    }
}