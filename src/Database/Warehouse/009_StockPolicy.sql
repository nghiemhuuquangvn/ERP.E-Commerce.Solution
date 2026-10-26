-- ============================================================
-- WarehouseDB Migration 009 — Replenishment stock policy (2026-10-26, ROADMAP P1-9)
--
-- Inventories gets MinStock / MaxStock / SafetyStock and LowStockAlertedAt.
-- Existing rows keep their meaning: Min = ReorderPoint, Max = ReorderPoint + ReorderQuantity, Safety = 0.
-- Idempotent.
-- ============================================================

USE WarehouseDB;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF COL_LENGTH('Inventories', 'MinStock') IS NULL
    ALTER TABLE Inventories ADD MinStock INT NOT NULL CONSTRAINT DF_Inventories_MinStock DEFAULT 0 WITH VALUES;
IF COL_LENGTH('Inventories', 'MaxStock') IS NULL
    ALTER TABLE Inventories ADD MaxStock INT NOT NULL CONSTRAINT DF_Inventories_MaxStock DEFAULT 0 WITH VALUES;
IF COL_LENGTH('Inventories', 'SafetyStock') IS NULL
    ALTER TABLE Inventories ADD SafetyStock INT NOT NULL CONSTRAINT DF_Inventories_SafetyStock DEFAULT 0 WITH VALUES;
IF COL_LENGTH('Inventories', 'LowStockAlertedAt') IS NULL
    ALTER TABLE Inventories ADD LowStockAlertedAt DATETIME2 NULL;
GO

-- Rows that predate the policy (both zero) take it from the reorder levels.
UPDATE Inventories SET MinStock = ReorderPoint, MaxStock = ReorderPoint + ReorderQuantity
WHERE MinStock = 0 AND MaxStock = 0;
GO
