#!/bin/bash

# Bash script for Code First migrations setup
# Usage: ./setup.sh [setup|migrate|update|reset|list|script]

ACTION="${1:-default}"
MIGRATION_NAME="$2"
PROJECT_PATH="src/services/IdentityService"
INFRA_PROJECT="$PROJECT_PATH/ERP.Identity.Infrastructure"
API_PROJECT="$PROJECT_PATH/ERP.Identity.Api"

# Color codes
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

echo ""
echo -e "${CYAN}================================${NC}"
echo -e "${CYAN}  ERP Identity Service Setup${NC}"
echo -e "${CYAN}================================${NC}"
echo ""

# Check if dotnet ef is installed
echo -e "${YELLOW}Checking for dotnet-ef installation...${NC}"
if dotnet ef --version &> /dev/null; then
    EF_VERSION=$(dotnet ef --version)
    echo -e "${GREEN}✓ dotnet-ef is installed: $EF_VERSION${NC}"
else
    echo -e "${RED}✗ dotnet-ef not found. Installing...${NC}"
    dotnet tool install --global dotnet-ef
    echo -e "${GREEN}✓ dotnet-ef installed${NC}"
fi

echo ""

case $ACTION in
    setup)
        echo -e "${CYAN}Setting up database...${NC}"
        
        # Check if migrations folder exists
        if [ ! -d "$INFRA_PROJECT/Migrations" ]; then
            echo -e "${YELLOW}Creating initial migration...${NC}"
            dotnet ef migrations add "InitialCreate" \
                --project "$INFRA_PROJECT/ERP.Identity.Infrastructure.csproj" \
                --startup-project "$API_PROJECT/ERP.Identity.Api.csproj" \
                --context AuthDbContext
            
            if [ $? -eq 0 ]; then
                echo -e "${GREEN}✓ Initial migration created${NC}"
            else
                echo -e "${RED}✗ Failed to create migration${NC}"
                exit 1
            fi
        fi
        
        echo -e "${YELLOW}Updating database...${NC}"
        dotnet ef database update \
            --project "$INFRA_PROJECT/ERP.Identity.Infrastructure.csproj" \
            --startup-project "$API_PROJECT/ERP.Identity.Api.csproj" \
            --context AuthDbContext
        
        if [ $? -eq 0 ]; then
            echo -e "${GREEN}✓ Database updated successfully${NC}"
        else
            echo -e "${RED}✗ Failed to update database${NC}"
            exit 1
        fi
        ;;
        
    migrate)
        if [ -z "$MIGRATION_NAME" ]; then
            echo -e "${RED}Error: Migration name is required${NC}"
            echo "Usage: $0 migrate YourMigrationName"
            exit 1
        fi
        
        echo -e "${YELLOW}Creating migration: $MIGRATION_NAME${NC}"
        dotnet ef migrations add "$MIGRATION_NAME" \
            --project "$INFRA_PROJECT/ERP.Identity.Infrastructure.csproj" \
            --startup-project "$API_PROJECT/ERP.Identity.Api.csproj" \
            --context AuthDbContext
        
        if [ $? -eq 0 ]; then
            echo -e "${GREEN}✓ Migration created: $MIGRATION_NAME${NC}"
            
            echo -e "${YELLOW}Applying migration...${NC}"
            dotnet ef database update \
                --project "$INFRA_PROJECT/ERP.Identity.Infrastructure.csproj" \
                --startup-project "$API_PROJECT/ERP.Identity.Api.csproj" \
                --context AuthDbContext
            
            echo -e "${GREEN}✓ Migration applied${NC}"
        else
            echo -e "${RED}✗ Failed to create migration${NC}"
            exit 1
        fi
        ;;
        
    update)
        echo -e "${YELLOW}Updating database to latest migration...${NC}"
        dotnet ef database update \
            --project "$INFRA_PROJECT/ERP.Identity.Infrastructure.csproj" \
            --startup-project "$API_PROJECT/ERP.Identity.Api.csproj" \
            --context AuthDbContext
        
        if [ $? -eq 0 ]; then
            echo -e "${GREEN}✓ Database updated${NC}"
        else
            echo -e "${RED}✗ Failed to update database${NC}"
            exit 1
        fi
        ;;
        
    reset)
        echo -e "${RED}WARNING: This will delete the database!${NC}"
        read -p "Are you sure? (yes/no): " CONFIRM
        
        if [ "$CONFIRM" = "yes" ]; then
            echo -e "${YELLOW}Dropping database...${NC}"
            dotnet ef database drop --force \
                --project "$INFRA_PROJECT/ERP.Identity.Infrastructure.csproj" \
                --startup-project "$API_PROJECT/ERP.Identity.Api.csproj" \
                --context AuthDbContext
            
            if [ $? -eq 0 ]; then
                echo -e "${GREEN}✓ Database dropped${NC}"
                
                echo -e "${YELLOW}Creating database...${NC}"
                dotnet ef database update \
                    --project "$INFRA_PROJECT/ERP.Identity.Infrastructure.csproj" \
                    --startup-project "$API_PROJECT/ERP.Identity.Api.csproj" \
                    --context AuthDbContext
                
                echo -e "${GREEN}✓ Database recreated${NC}"
            else
                echo -e "${RED}✗ Failed to drop database${NC}"
                exit 1
            fi
        else
            echo -e "${YELLOW}Operation cancelled${NC}"
        fi
        ;;
        
    list)
        echo -e "${YELLOW}Listing migrations...${NC}"
        dotnet ef migrations list \
            --project "$INFRA_PROJECT/ERP.Identity.Infrastructure.csproj" \
            --startup-project "$API_PROJECT/ERP.Identity.Api.csproj" \
            --context AuthDbContext
        ;;
        
    script)
        echo -e "${YELLOW}Generating SQL script...${NC}"
        SCRIPT_PATH="database_script.sql"
        dotnet ef migrations script -o "$SCRIPT_PATH" \
            --project "$INFRA_PROJECT/ERP.Identity.Infrastructure.csproj" \
            --startup-project "$API_PROJECT/ERP.Identity.Api.csproj" \
            --context AuthDbContext
        
        if [ $? -eq 0 ]; then
            echo -e "${GREEN}✓ SQL script generated: $SCRIPT_PATH${NC}"
        else
            echo -e "${RED}✗ Failed to generate script${NC}"
            exit 1
        fi
        ;;
        
    *)
        echo -e "${YELLOW}Usage:${NC}"
        echo "  $0 setup              # Initial setup"
        echo "  $0 migrate [name]     # Create migration"
        echo "  $0 update             # Update to latest migration"
        echo "  $0 reset              # Reset database"
        echo "  $0 list               # List migrations"
        echo "  $0 script             # Generate SQL script"
        echo ""
        echo -e "${YELLOW}Example:${NC}"
        echo "  $0 migrate AddNewColumns"
        ;;
esac

echo ""
echo -e "${GREEN}Done!${NC}"
