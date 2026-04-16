@echo off
REM Batch script for Code First migrations setup
REM Usage: setup.bat [setup|migrate|update|reset|list|script]

setlocal enabledelayedexpansion

if "%1"=="" goto default
set ACTION=%1
set MIGRATION_NAME=%2
set PROJECT_PATH=src\services\IdentityService
set INFRA_PROJECT=%PROJECT_PATH%\ERP.Identity.Infrastructure
set API_PROJECT=%PROJECT_PATH%\ERP.Identity.Api

echo.
echo ================================
echo   ERP Identity Service Setup
echo ================================
echo.

REM Check if dotnet ef is installed
echo Checking for dotnet-ef installation...
dotnet ef --version >nul 2>&1
if %errorlevel% equ 0 (
    echo. [OK] dotnet-ef is installed
) else (
    echo. [ERROR] dotnet-ef not found. Installing...
    dotnet tool install --global dotnet-ef
    echo. [OK] dotnet-ef installed
)

echo.

if "%ACTION%"=="setup" goto setup
if "%ACTION%"=="migrate" goto migrate
if "%ACTION%"=="update" goto update
if "%ACTION%"=="reset" goto reset
if "%ACTION%"=="list" goto list
if "%ACTION%"=="script" goto script

:default
echo Usage:
echo   setup.bat setup              - Initial setup
echo   setup.bat migrate [name]     - Create migration
echo   setup.bat update             - Update to latest migration
echo   setup.bat reset              - Reset database
echo   setup.bat list               - List migrations
echo   setup.bat script             - Generate SQL script
echo.
echo Example:
echo   setup.bat migrate AddNewColumns
goto end

:setup
echo Setting up database...
if not exist "%INFRA_PROJECT%\Migrations" (
    echo Creating initial migration...
    dotnet ef migrations add "InitialCreate" ^
        --project "%INFRA_PROJECT%\ERP.Identity.Infrastructure.csproj" ^
        --startup-project "%API_PROJECT%\ERP.Identity.Api.csproj" ^
        --context AuthDbContext
    
    if !errorlevel! equ 0 (
        echo. [OK] Initial migration created
    ) else (
        echo. [ERROR] Failed to create migration
        goto end
    )
)

echo Updating database...
dotnet ef database update ^
    --project "%INFRA_PROJECT%\ERP.Identity.Infrastructure.csproj" ^
    --startup-project "%API_PROJECT%\ERP.Identity.Api.csproj" ^
    --context AuthDbContext

if !errorlevel! equ 0 (
    echo. [OK] Database updated successfully
) else (
    echo. [ERROR] Failed to update database
)
goto end

:migrate
if "%MIGRATION_NAME%"=="" (
    echo Error: Migration name is required
    echo Usage: setup.bat migrate YourMigrationName
    goto end
)

echo Creating migration: %MIGRATION_NAME%
dotnet ef migrations add "%MIGRATION_NAME%" ^
    --project "%INFRA_PROJECT%\ERP.Identity.Infrastructure.csproj" ^
    --startup-project "%API_PROJECT%\ERP.Identity.Api.csproj" ^
    --context AuthDbContext

if !errorlevel! equ 0 (
    echo. [OK] Migration created: %MIGRATION_NAME%
    
    echo Applying migration...
    dotnet ef database update ^
        --project "%INFRA_PROJECT%\ERP.Identity.Infrastructure.csproj" ^
        --startup-project "%API_PROJECT%\ERP.Identity.Api.csproj" ^
        --context AuthDbContext
    
    echo. [OK] Migration applied
) else (
    echo. [ERROR] Failed to create migration
)
goto end

:update
echo Updating database to latest migration...
dotnet ef database update ^
    --project "%INFRA_PROJECT%\ERP.Identity.Infrastructure.csproj" ^
    --startup-project "%API_PROJECT%\ERP.Identity.Api.csproj" ^
    --context AuthDbContext

if !errorlevel! equ 0 (
    echo. [OK] Database updated
) else (
    echo. [ERROR] Failed to update database
)
goto end

:reset
echo WARNING: This will delete the database!
set /p CONFIRM="Are you sure? (yes/no): "

if /i "%CONFIRM%"=="yes" (
    echo Dropping database...
    dotnet ef database drop --force ^
        --project "%INFRA_PROJECT%\ERP.Identity.Infrastructure.csproj" ^
        --startup-project "%API_PROJECT%\ERP.Identity.Api.csproj" ^
        --context AuthDbContext
    
    if !errorlevel! equ 0 (
        echo. [OK] Database dropped
        
        echo Creating database...
        dotnet ef database update ^
            --project "%INFRA_PROJECT%\ERP.Identity.Infrastructure.csproj" ^
            --startup-project "%API_PROJECT%\ERP.Identity.Api.csproj" ^
            --context AuthDbContext
        
        echo. [OK] Database recreated
    ) else (
        echo. [ERROR] Failed to drop database
    )
) else (
    echo Operation cancelled
)
goto end

:list
echo Listing migrations...
dotnet ef migrations list ^
    --project "%INFRA_PROJECT%\ERP.Identity.Infrastructure.csproj" ^
    --startup-project "%API_PROJECT%\ERP.Identity.Api.csproj" ^
    --context AuthDbContext
goto end

:script
echo Generating SQL script...
set SCRIPT_PATH=database_script.sql
dotnet ef migrations script -o "%SCRIPT_PATH%" ^
    --project "%INFRA_PROJECT%\ERP.Identity.Infrastructure.csproj" ^
    --startup-project "%API_PROJECT%\ERP.Identity.Api.csproj" ^
    --context AuthDbContext

if !errorlevel! equ 0 (
    echo. [OK] SQL script generated: %SCRIPT_PATH%
) else (
    echo. [ERROR] Failed to generate script
)

:end
echo.
echo Done!
endlocal
