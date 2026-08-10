-- ERP Platform — Initialize all 14 service databases
-- Run as SA against localhost SQL Server before starting Docker services
-- Usage: sqlcmd -S localhost -U sa -P "!Edw@rd!123456!" -i init-databases.sql

USE master;
GO

IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = 'AuthDB')
    CREATE DATABASE AuthDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = 'SalesDB')
    CREATE DATABASE SalesDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = 'PurchasingDB')
    CREATE DATABASE PurchasingDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = 'WarehouseDB')
    CREATE DATABASE WarehouseDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = 'AccountingDB')
    CREATE DATABASE AccountingDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = 'CrmDB')
    CREATE DATABASE CrmDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = 'HrmDB')
    CREATE DATABASE HrmDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = 'ProductDB')
    CREATE DATABASE ProductDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = 'PaymentDB')
    CREATE DATABASE PaymentDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = 'ShippingDB')
    CREATE DATABASE ShippingDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = 'PosDB')
    CREATE DATABASE PosDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = 'PromotionDB')
    CREATE DATABASE PromotionDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = 'NotificationDB')
    CREATE DATABASE NotificationDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = 'ReportingDB')
    CREATE DATABASE ReportingDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = 'ChatBotDB')
    CREATE DATABASE ChatBotDB;
GO

PRINT 'All 14 ERP databases created (or already exist).';
GO
