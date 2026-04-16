# Code First Project Documentation

## Project Structure

```
ERP.E-Commerce.Solution/
├── src/services/IdentityService/
│   ├── ERP.Identity.Api/               # REST API
│   │   ├── Program.cs                  # DI/Middleware setup
│   │   ├── Controllers/                # API endpoints
│   │   ├── appsettings.Development.json
│   │   └── appsettings.json
│   ├── ERP.Identity.Application/       # Business logic
│   │   ├── DTOs/                       # Data transfer objects
│   │   ├── Interfaces/                 # Contracts
│   │   └── Services/                   # Use cases
│   ├── ERP.Identity.Domain/            # Core entities
│   │   ├── Entities/                   # Domain models
│   │   ├── Events/                     # Domain events
│   │   ├── ValueObjects/               # Value types
│   │   └── Common/                     # Base classes
│   ├── ERP.Identity.Infrastructure/    # Data access
│   │   ├── Persistence/                # DbContext & migrations
│   │   ├── Repositories/               # Data repositories
│   │   ├── Services/                   # External services
│   │   └── Migrations/                 # EF Core migrations
│   ├── CODE_FIRST_SETUP.md             # Detailed setup guide
│   ├── QUICK_START.md                  # Quick reference
│   ├── setup.ps1                       # PowerShell setup script
│   ├── setup.bat                       # Windows batch script
│   └── setup.sh                        # Bash script
```

## Code First Approach

### What is Code First?

Code First means:
1. **Define entities in C#** → Domain models
2. **Create migrations** → Generate database schema
3. **Apply migrations** → Create/update database

### Benefits

- ✅ Database schema version controlled in Git
- ✅ Easy to run on different machines/environments
- ✅ Schema changes tracked with migration history
- ✅ Development database synced with production
- ✅ Easier rollback/forward of schema changes

## Entity Design

### Base Entity (All entities inherit from this)

```csharp
public abstract class BaseEntity
{
    public Guid Id { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public string? Description { get; set; } // Ignored in DB
    public IReadOnlyCollection<IDomainEvent> DomainEvents { get; }
}
```

### Example Entity

```csharp
public class User : BaseEntity
{
    public string Email { get; set; }
    public string PasswordHash { get; set; }
    public bool IsActive { get; set; }
    
    // Relationships
    public ICollection<UserRole> UserRoles { get; set; }
    public ICollection<RefreshToken> RefreshTokens { get; set; }
}
```

## Workflow

### 1. Modify an Entity

```csharp
public class Product : BaseEntity
{
    public string Name { get; set; }
    public string Description { get; set; }
    public decimal Price { get; set; }  // New property
}
```

### 2. Create Migration

```bash
./setup.ps1 -Action migrate -MigrationName "AddProductPriceColumn"
```

This generates:
```
Migrations/
├── 20260322120000_AddProductPriceColumn.cs
├── 20260322120000_AddProductPriceColumn.Designer.cs
└── AuthDbContextModelSnapshot.cs
```

### 3. Apply Migration

```bash
./setup.ps1 -Action update
```

Runs all pending migrations against the database.

## Key Configuration

### Connection String

**File:** `appsettings.Development.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=AuthDB;Integrated Security=true;TrustServerCertificate=true;"
  }
}
```

### DbContext Configuration

**File:** `AuthDbContext.cs`

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    
    // Entity configurations
    modelBuilder.Entity<User>(entity =>
    {
        entity.ToTable("Users");
        entity.HasKey(u => u.Id);
        entity.Property(u => u.Email).IsRequired().HasMaxLength(256);
        
        entity.HasMany(u => u.UserRoles)
            .WithOne(ur => ur.User)
            .HasForeignKey(ur => ur.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    });
}
```

## Migration Management

### View Migration History

```bash
./setup.ps1 -Action list
```

### Rollback Last Migration

```bash
dotnet ef database update 0  # Rollback all
dotnet ef database update MigrationName  # Rollback to specific
```

### Generate SQL Script

```bash
./setup.ps1 -Action script
```

Creates `database_script.sql` for manual review/execution.

## Best Practices

### ✅ DO

- ✓ Create migrations for schema changes
- ✓ Use meaningful migration names
- ✓ Keep migrations small and focused
- ✓ Review migration before applying
- ✓ Test on development database first
- ✓ Commit migrations to version control

### ❌ DON'T

- ✗ Delete migration files manually
- ✗ Modify migration timestamps
- ✗ Use raw SQL in production code
- ✗ Skip migrations in development
- ✗ Apply untested migrations to production

## Common Tasks

### Add New Entity

1. Create class inheriting from `BaseEntity`
2. Add `DbSet<YourEntity>` to `AuthDbContext`
3. Configure in `OnModelCreating`
4. Run `setup.ps1 -Action migrate -MigrationName "AddYourEntity"`
5. Run `setup.ps1 -Action update`

### Rename Property

1. Rename property in entity
2. Run `setup.ps1 -Action migrate -MigrationName "RenamePropertyName"`
3. Edit migration to use `RenameColumn` instead of `DropColumn` + `AddColumn`
4. Run `setup.ps1 -Action update`

### Delete Property

1. Remove property from entity
2. Run `setup.ps1 -Action migrate -MigrationName "RemovePropertyName"`
3. Run `setup.ps1 -Action update`

### Change Column Type

1. Update property type in entity
2. Run `setup.ps1 -Action migrate -MigrationName "ChangePropertyType"`
3. Review migration carefully before applying

## Troubleshooting

### "Cannot create migrations folder"

**Solution:** Ensure Infrastructure project is in correct location and references Domain project

### "Migration already exists"

**Solution:** Migration already pending application. Run:
```bash
./setup.ps1 -Action update
```

### "Database still uses old schema"

**Solution:** Check pending migrations:
```bash
./setup.ps1 -Action list
dotnet ef database update
```

### "Key not found in database"

**Solution:** Ensure migrations are applied:
```bash
./setup.ps1 -Action update
```

## Advanced Topics

### Custom SQL in Migrations

```csharp
public partial class CustomMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("UPDATE Users SET IsActive = 1");
    }
}
```

### Conditional Migrations

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    if (!migrationBuilder.ActiveProvider.Contains("SqlServer"))
    {
        return; // Skip for non-SQL Server databases
    }
    
    migrationBuilder.AddColumn<string>(
        name: "CustomField",
        table: "Users");
}
```

### Snapshot-Only Mode

For complex scenarios, create migrations without EF generation:

```bash
dotnet ef migrations script --no-transactions --idempotent
```

## Performance Considerations

### Indexing

```csharp
modelBuilder.Entity<User>()
    .HasIndex(u => u.Email)
    .IsUnique();
```

### Lazy Loading

```csharp
public virtual ICollection<UserRole> UserRoles { get; set; }
```

### View Creation

```csharp
modelBuilder.Entity<UserSummary>()
    .ToView("vw_UserSummary")
    .HasNoKey();
```

---

For questions or issues, refer to [CODE_FIRST_SETUP.md](./CODE_FIRST_SETUP.md) or [QUICK_START.md](./QUICK_START.md)
