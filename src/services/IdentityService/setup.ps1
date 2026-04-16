# Setup script for Code First migrations
# Run this script to set up the database on any device

param(
    [string]$Action = "setup",
    [string]$MigrationName = "",
    [string]$ProjectPath = "./src/services/IdentityService"
)

$InfrastructureProject = "$ProjectPath/ERP.Identity.Infrastructure"
$ApiProject = "$ProjectPath/ERP.Identity.Api"

Write-Host "================================" -ForegroundColor Cyan
Write-Host "  ERP Identity Service Setup" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""

# Check if dotnet ef is installed
Write-Host "Checking for dotnet-ef installation..." -ForegroundColor Yellow
$efVersion = dotnet ef --version
if ($?) {
    Write-Host "✓ dotnet-ef is installed: $efVersion" -ForegroundColor Green
} else {
    Write-Host "✗ dotnet-ef not found. Installing..." -ForegroundColor Red
    dotnet tool install --global dotnet-ef
    Write-Host "✓ dotnet-ef installed" -ForegroundColor Green
}

Write-Host ""

switch ($Action) {
    "setup" {
        Write-Host "Setting up database..." -ForegroundColor Cyan
        
        # Check if migrations folder exists
        if (-not (Test-Path "$InfrastructureProject/Migrations")) {
            Write-Host "Creating initial migration..." -ForegroundColor Yellow
            dotnet ef migrations add "InitialCreate" `
                --project "$InfrastructureProject/ERP.Identity.Infrastructure.csproj" `
                --startup-project "$ApiProject/ERP.Identity.Api.csproj" `
                --context AuthDbContext
            
            if ($?) {
                Write-Host "✓ Initial migration created" -ForegroundColor Green
            } else {
                Write-Host "✗ Failed to create migration" -ForegroundColor Red
                exit 1
            }
        }
        
        Write-Host "Updating database..." -ForegroundColor Yellow
        dotnet ef database update `
            --project "$InfrastructureProject/ERP.Identity.Infrastructure.csproj" `
            --startup-project "$ApiProject/ERP.Identity.Api.csproj" `
            --context AuthDbContext
        
        if ($?) {
            Write-Host "✓ Database updated successfully" -ForegroundColor Green
        } else {
            Write-Host "✗ Failed to update database" -ForegroundColor Red
            exit 1
        }
    }
    
    "migrate" {
        if ([string]::IsNullOrWhiteSpace($MigrationName)) {
            Write-Host "Error: Migration name is required. Usage: .\setup.ps1 -Action migrate -MigrationName 'YourMigrationName'" -ForegroundColor Red
            exit 1
        }
        
        Write-Host "Creating migration: $MigrationName" -ForegroundColor Yellow
        dotnet ef migrations add "$MigrationName" `
            --project "$InfrastructureProject/ERP.Identity.Infrastructure.csproj" `
            --startup-project "$ApiProject/ERP.Identity.Api.csproj" `
            --context AuthDbContext
        
        if ($?) {
            Write-Host "✓ Migration created: $MigrationName" -ForegroundColor Green
            
            Write-Host "Applying migration..." -ForegroundColor Yellow
            dotnet ef database update `
                --project "$InfrastructureProject/ERP.Identity.Infrastructure.csproj" `
                --startup-project "$ApiProject/ERP.Identity.Api.csproj" `
                --context AuthDbContext
            
            Write-Host "✓ Migration applied" -ForegroundColor Green
        } else {
            Write-Host "✗ Failed to create migration" -ForegroundColor Red
            exit 1
        }
    }
    
    "update" {
        Write-Host "Updating database to latest migration..." -ForegroundColor Yellow
        dotnet ef database update `
            --project "$InfrastructureProject/ERP.Identity.Infrastructure.csproj" `
            --startup-project "$ApiProject/ERP.Identity.Api.csproj" `
            --context AuthDbContext
        
        if ($?) {
            Write-Host "✓ Database updated" -ForegroundColor Green
        } else {
            Write-Host "✗ Failed to update database" -ForegroundColor Red
            exit 1
        }
    }
    
    "reset" {
        Write-Host "WARNING: This will delete the database!" -ForegroundColor Red
        $confirm = Read-Host "Are you sure? (yes/no)"
        
        if ($confirm -eq "yes") {
            Write-Host "Dropping database..." -ForegroundColor Yellow
            dotnet ef database drop --force `
                --project "$InfrastructureProject/ERP.Identity.Infrastructure.csproj" `
                --startup-project "$ApiProject/ERP.Identity.Api.csproj" `
                --context AuthDbContext
            
            if ($?) {
                Write-Host "✓ Database dropped" -ForegroundColor Green
                
                Write-Host "Creating database..." -ForegroundColor Yellow
                dotnet ef database update `
                    --project "$InfrastructureProject/ERP.Identity.Infrastructure.csproj" `
                    --startup-project "$ApiProject/ERP.Identity.Api.csproj" `
                    --context AuthDbContext
                
                Write-Host "✓ Database recreated" -ForegroundColor Green
            } else {
                Write-Host "✗ Failed to drop database" -ForegroundColor Red
                exit 1
            }
        } else {
            Write-Host "Operation cancelled" -ForegroundColor Yellow
        }
    }
    
    "list" {
        Write-Host "Listing migrations..." -ForegroundColor Yellow
        dotnet ef migrations list `
            --project "$InfrastructureProject/ERP.Identity.Infrastructure.csproj" `
            --startup-project "$ApiProject/ERP.Identity.Api.csproj" `
            --context AuthDbContext
    }
    
    "script" {
        Write-Host "Generating SQL script..." -ForegroundColor Yellow
        $scriptPath = "$ProjectPath/database_script.sql"
        dotnet ef migrations script -o "$scriptPath" `
            --project "$InfrastructureProject/ERP.Identity.Infrastructure.csproj" `
            --startup-project "$ApiProject/ERP.Identity.Api.csproj" `
            --context AuthDbContext
        
        if ($?) {
            Write-Host "✓ SQL script generated: $scriptPath" -ForegroundColor Green
        } else {
            Write-Host "✗ Failed to generate script" -ForegroundColor Red
            exit 1
        }
    }
    
    default {
        Write-Host "Usage:" -ForegroundColor Yellow
        Write-Host "  .\setup.ps1 -Action setup              # Initial setup" -ForegroundColor White
        Write-Host "  .\setup.ps1 -Action migrate -MigrationName 'MigrationName'   # Create migration" -ForegroundColor White
        Write-Host "  .\setup.ps1 -Action update             # Update to latest migration" -ForegroundColor White
        Write-Host "  .\setup.ps1 -Action reset              # Reset database" -ForegroundColor White
        Write-Host "  .\setup.ps1 -Action list               # List migrations" -ForegroundColor White
        Write-Host "  .\setup.ps1 -Action script             # Generate SQL script" -ForegroundColor White
        Write-Host ""
        Write-Host "Example:" -ForegroundColor Yellow
        Write-Host "  .\setup.ps1 -Action migrate -MigrationName 'AddNewColumns'" -ForegroundColor White
    }
}

Write-Host ""
Write-Host "Done!" -ForegroundColor Green
