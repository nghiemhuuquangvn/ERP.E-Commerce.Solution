# ERP Identity Service Unit Tests

Comprehensive unit test suite for the Identity Service covering all components including domain entities, services, repositories, controllers, and DTOs.

## Test Structure

```
ERP.Identity.UnitTests/
├── Domain/Entities/              # Domain entity tests
│   ├── UserTests.cs              # User aggregate root tests
│   ├── PermissionTests.cs         # Permission entity tests
│   ├── AuthTenantTests.cs         # AuthTenant entity tests
│   └── PasswordResetTokenTests.cs # PasswordResetToken entity tests
├── Application/
│   ├── Services/                 # Application service tests
│   │   ├── AuthServiceTests.cs
│   │   ├── UserServiceTests.cs
│   │   ├── RoleServiceTests.cs
│   │   ├── PermissionServiceTests.cs
│   │   └── TenantServiceTests.cs
│   └── DTOs/                     # DTO validation tests
│       └── DTOTests.cs
├── Api/
│   └── Controllers/              # API controller tests
│       ├── AuthControllerTests.cs
│       ├── UsersControllerTests.cs
│       ├── RolesControllerTests.cs
│       ├── PermissionsControllerTests.cs
│       └── TenantsControllerTests.cs
└── Authorization/
    └── PermissionGuardTests.cs   # Reflection guard: every [RequirePermission]/
                                  # [AuthorizePermission] literal must exist in
                                  # ERP.Shared.Authorization.PermissionConstants
```

## Running Tests

### Run All Tests
```bash
dotnet test
```

### Run Specific Test Class
```bash
dotnet test --filter "ClassName=UserTests"
```

### Run Tests with Coverage
```bash
dotnet test /p:CollectCoverageMetrics=true
```

### Run Tests in Watch Mode
```bash
dotnet watch test
```

## Test Categories

### 1. Domain Entity Tests

**UserTests** (8 tests)
- UpdateProfile with valid/null values
- ChangePassword
- ConfirmEmail
- Deactivate/Activate
- Unlock

**PermissionTests** (3 tests)
- Create permission
- Unique ID generation
- Timestamp management

**AuthTenantTests** (7 tests)
- Create tenant
- Activate/Deactivate
- IsActive property
- Status management

**PasswordResetTokenTests** (8 tests)
- Token creation
- IsUsed, IsExpired, IsValid properties
- MarkUsed method
- Token validation logic

### 2. Service Tests

**AuthServiceTests** (5 tests)
- ForgotPasswordAsync with valid/invalid email
- ResetPasswordAsync with valid/expired token
- LogoutAsync
- ConfirmEmailAsync

**UserServiceTests** (8 tests)
- GetByIdAsync
- ListByTenantAsync (pagination)
- UpdateProfileAsync
- ChangePasswordAsync
- LockAsync/UnlockAsync
- DeactivateAsync
- AssignRoleAsync

**RoleServiceTests** (8 tests)
- ListByTenantAsync
- GetByIdAsync
- CreateAsync/UpdateAsync/DeleteAsync
- GetPermissionsAsync
- AssignPermissionAsync/RemovePermissionAsync

**PermissionServiceTests** (4 tests)
- ListAllAsync
- CreateAsync/UpdateAsync/DeleteAsync

**TenantServiceTests** (7 tests)
- ListAllAsync
- GetByIdAsync
- CreateAsync/UpdateAsync
- ActivateAsync/DeactivateAsync

### 3. Controller Tests

**AuthControllerTests** (6 tests)
- Register
- Login (success/failure)
- ForgotPassword
- ResetPassword
- ConfirmEmail

**UsersControllerTests** (9 tests)
- List/GetById
- UpdateProfile
- ChangePassword
- Lock/Unlock
- Delete (soft)
- AssignRole/RemoveRole

**RolesControllerTests** (9 tests)
- List/GetById
- Create/Update/Delete
- GetPermissions
- AssignPermission/RemovePermission

**PermissionsControllerTests** (4 tests)
- List
- Create/Update/Delete

**TenantsControllerTests** (7 tests)
- List/GetById
- Create/Update
- Activate/Deactivate

### 4. DTO Tests

**DTOTests** (11 tests)
- UserDetailDto
- RoleDto
- PermissionDto
- TenantDto
- PagedResponse<T>
- UpdateProfileRequest
- ChangePasswordRequest
- CreateRoleRequest
- ForgotPasswordRequest
- ResetPasswordRequest
- CreateTenantRequest

## Test Tools & Frameworks

- **xUnit** - Unit testing framework
- **Moq** - Mocking library for dependencies
- **FluentAssertions** - Assertion library for fluent syntax

## Testing Patterns

### Service Tests Pattern
```csharp
[Fact]
public async Task MethodName_WithScenario_ShouldExpectedResult()
{
    // Arrange - Set up test data and mocks
    var mockRepository = new Mock<IRepository>();
    
    // Act - Execute the method under test
    var result = await service.MethodAsync(data);
    
    // Assert - Verify the result
    result.Should().NotBeNull();
}
```

### Controller Tests Pattern
```csharp
[Fact]
public async Task Action_WithValidRequest_ShouldReturnOk()
{
    // Arrange
    var request = new CreateRequest(...);
    _mockService.Setup(s => s.CreateAsync(request, It.IsAny<CancellationToken>()))
        .ReturnsAsync(expectedResult);
    
    // Act
    var result = await _controller.Create(request, CancellationToken.None);
    
    // Assert
    result.Should().BeOfType<OkObjectResult>();
}
```

## Test Coverage Goals

| Component | Target Coverage | Current |
|-----------|-----------------|---------|
| Domain    | 90%+            | ✓       |
| Services  | 85%+            | ✓       |
| Controllers | 85%+          | ✓       |
| Repositories | 70%+         | Via Mocks |

## Adding New Tests

1. Create test file in appropriate folder
2. Name: `[ComponentName]Tests.cs`
3. Use AAA pattern (Arrange, Act, Assert)
4. Follow naming: `MethodName_Scenario_ExpectedResult`
5. Use FluentAssertions for assertions
6. Mock external dependencies

## CI/CD Integration

Tests are run on:
- Every pull request
- Before merge to main/develop branches
- On scheduled builds

## Troubleshooting

### Mock Issues
- Ensure `Setup` methods match actual method signatures
- Use `It.IsAny<T>()` for flexible argument matching
- Verify `ReturnsAsync` vs `Returns` for async methods

### Assertion Issues
- Use FluentAssertions for better error messages
- Check nullable types with `Should().BeNull()` vs `Should().BeNullOrEmpty()`
- Use `Should().Contain()` for collection assertions

### Test Isolation
- Each test creates fresh mocks (via `[Fact]`)
- No shared state between tests
- Use constructors for test setup, not static methods

## Future Improvements

- [ ] Integration tests with real database
- [ ] Performance benchmarks
- [ ] Load testing scenarios
- [ ] Security/penetration testing
- [ ] API contract testing
- [ ] End-to-end test scenarios
