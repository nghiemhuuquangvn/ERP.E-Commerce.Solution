using Xunit;
using FluentAssertions;
using ERP.Identity.Application.DTOs.Auth;
using ERP.Identity.Application.DTOs.Permission;
using ERP.Identity.Application.DTOs.Tenant;
using ERP.Identity.Application.DTOs.Common;

namespace ERP.Identity.UnitTests.Application.DTOs;

public class DTOTests
{
    [Fact]
    public void UserDetailDto_WithValidData_ShouldCreateInstance()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var email = "test@example.com";
        var userName = "testuser";
        var fullName = "Test User";
        var isActive = true;
        var isLocked = false;

        // Act
        var dto = new UserDetailDto(userId, email, userName, fullName, isActive, isLocked, null, new List<string>(), new List<string>(), new List<RoleAssignmentDto>());

        // Assert
        dto.Id.Should().Be(userId);
        dto.Email.Should().Be(email);
        dto.UserName.Should().Be(userName);
        dto.FullName.Should().Be(fullName);
        dto.IsActive.Should().Be(isActive);
        dto.IsLocked.Should().Be(isLocked);
    }

    [Fact]
    public void RoleDto_WithValidData_ShouldCreateInstance()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var roleCode = "admin";
        var roleName = "Administrator";
        var isActive = true;

        // Act
        var dto = new RoleDto(roleId, roleCode, roleName, isActive);

        // Assert
        dto.Id.Should().Be(roleId);
        dto.RoleCode.Should().Be(roleCode);
        dto.RoleName.Should().Be(roleName);
    }

    [Fact]
    public void PermissionDto_WithValidData_ShouldCreateInstance()
    {
        // Arrange
        var permissionId = Guid.NewGuid();
        var code = "view.users";
        var name = "View Users";
        var module = "Users";

        // Act
        var dto = new PermissionDto(permissionId, code, name, module);

        // Assert
        dto.Id.Should().Be(permissionId);
        dto.PermissionCode.Should().Be(code);
        dto.PermissionName.Should().Be(name);
        dto.Module.Should().Be(module);
    }

    [Fact]
    public void TenantDto_WithValidData_ShouldCreateInstance()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var code = "tenant001";
        var name = "Test Tenant";
        var status = (byte)1;

        // Act
        var dto = new TenantDto(tenantId, code, name, status);

        // Assert
        dto.Id.Should().Be(tenantId);
        dto.TenantCode.Should().Be(code);
        dto.TenantName.Should().Be(name);
        dto.Status.Should().Be(status);
    }

    [Fact]
    public void PagedResponse_WithValidData_ShouldCreateInstance()
    {
        // Arrange
        var items = new List<string> { "item1", "item2", "item3" };
        var totalCount = 3;
        var page = 1;
        var pageSize = 10;

        // Act
        var response = new PagedResponse<string>(items, totalCount, page, pageSize);

        // Assert
        response.Items.Should().HaveCount(3);
        response.TotalCount.Should().Be(3);
        response.Page.Should().Be(1);
        response.PageSize.Should().Be(10);
    }

    [Fact]
    public void UpdateProfileRequest_WithValidData_ShouldCreateInstance()
    {
        // Arrange
        var fullName = "New Name";
        var userName = "newusername";

        // Act
        var request = new UpdateProfileRequest(fullName, userName);

        // Assert
        request.FullName.Should().Be(fullName);
        request.UserName.Should().Be(userName);
    }

    [Fact]
    public void ChangePasswordRequest_WithValidData_ShouldCreateInstance()
    {
        // Arrange
        var currentPassword = "oldpassword";
        var newPassword = "newpassword123";

        // Act
        var request = new ChangePasswordRequest(currentPassword, newPassword);

        // Assert
        request.CurrentPassword.Should().Be(currentPassword);
        request.NewPassword.Should().Be(newPassword);
    }

    [Fact]
    public void CreateRoleRequest_WithValidData_ShouldCreateInstance()
    {
        // Arrange
        var roleCode = "editor";
        var roleName = "Editor";

        // Act
        var request = new CreateRoleRequest(roleCode, roleName);

        // Assert
        request.RoleCode.Should().Be(roleCode);
        request.RoleName.Should().Be(roleName);
    }

    [Fact]
    public void ForgotPasswordRequest_WithValidData_ShouldCreateInstance()
    {
        // Arrange
        var email = "test@example.com";
        var tenantCode = "tenant001";

        // Act
        var request = new ForgotPasswordRequest(email, tenantCode);

        // Assert
        request.Email.Should().Be(email);
        request.TenantCode.Should().Be(tenantCode);
    }

    [Fact]
    public void ResetPasswordRequest_WithValidData_ShouldCreateInstance()
    {
        // Arrange
        var token = "reset_token_123";
        var newPassword = "newpassword123";

        // Act
        var request = new ResetPasswordRequest(token, newPassword);

        // Assert
        request.Token.Should().Be(token);
        request.NewPassword.Should().Be(newPassword);
    }

    [Fact]
    public void CreateTenantRequest_WithValidData_ShouldCreateInstance()
    {
        // Arrange
        var code = "newtenant";
        var name = "New Tenant";

        // Act
        var request = new CreateTenantRequest(code, name);

        // Assert
        request.TenantCode.Should().Be(code);
        request.TenantName.Should().Be(name);
    }
}
