using System.Reflection;
using Xunit;
using FluentAssertions;
using ERP.Shared.Authorization;

namespace ERP.Identity.UnitTests.Authorization;

/// <summary>
/// Guards against the exact bug class documented in PermissionConstants.cs's own comments:
/// Accounting, CRM, and HRM controllers each shipped an [AuthorizePermission("some:string")]
/// whose literal did not exist in PermissionConstants, silently 403'ing every role (including
/// SuperAdmin) until someone noticed. This scans every controller in the IdentityService API
/// assembly and fails the build the moment a permission literal drifts from the shared
/// PermissionConstants vocabulary — turning a silent runtime 403 into a compile-time-adjacent
/// test failure. Extend the assemblies list below as other services adopt this same test.
/// </summary>
public class PermissionGuardTests
{
    private static readonly HashSet<string> AllKnownPermissions =
        new(PermissionConstants.GetAllPermissions(), StringComparer.Ordinal);

    private static IEnumerable<Assembly> ScannedAssemblies =>
    [
        typeof(ERP.Identity.Api.Controllers.PermissionsController).Assembly
    ];

    private static IEnumerable<(MethodInfo Method, string Permission)> AllRequiredPermissions()
    {
        // AuthorizePermissionAttribute/AuthorizeAnyPermissionAttribute don't expose their
        // permission list via a public member (only a private `_requiredPermissions`
        // field) — reflection into the field is the only way to read it from outside.
        foreach (var assembly in ScannedAssemblies)
        {
            foreach (var type in assembly.GetTypes())
            {
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    foreach (var attr in method.GetCustomAttributes<AuthorizePermissionAttribute>())
                        foreach (var permission in GetRequiredPermissions(attr))
                            yield return (method, permission);

                    foreach (var attr in method.GetCustomAttributes<AuthorizeAnyPermissionAttribute>())
                        foreach (var permission in GetRequiredPermissions(attr))
                            yield return (method, permission);
                }
            }
        }
    }

    private static string[] GetRequiredPermissions(Attribute attr)
    {
        var field = attr.GetType().GetField("_requiredPermissions", BindingFlags.NonPublic | BindingFlags.Instance);
        return field?.GetValue(attr) as string[] ?? [];
    }

    [Fact]
    public void EveryRequiredPermission_MustExistInPermissionConstants()
    {
        var missing = AllRequiredPermissions()
            .Where(x => !AllKnownPermissions.Contains(x.Permission))
            .Select(x => $"{x.Method.DeclaringType!.Name}.{x.Method.Name} -> \"{x.Permission}\"")
            .Distinct()
            .ToList();

        missing.Should().BeEmpty(
            "every permission string referenced by [AuthorizePermission]/[AuthorizeAnyPermission] must exist in " +
            "PermissionConstants.GetAllPermissions() — otherwise the endpoint silently 403s for every role, " +
            "including SuperAdmin (the exact incident that already hit Accounting, CRM, and HRM)");
    }

    [Fact]
    public void AtLeastOnePermissionAttribute_IsActuallyScanned()
    {
        // Sanity check so this guard can't silently pass by scanning zero attributes
        // (e.g. if a future refactor moves controllers to a different assembly).
        AllRequiredPermissions().Should().NotBeEmpty();
    }
}
