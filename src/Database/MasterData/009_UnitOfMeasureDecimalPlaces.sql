-- ============================================================
-- MasterDataDB Migration 009 - UnitsOfMeasure.DecimalPlaces (Manufacturing, BA spec BR-GEN-06): how many decimals a quantity in this unit may
-- have; ManufacturingService rounds a calculated requirement UP to it. Default 0 (whole units); weight / volume / length / area units that
-- are still at the default get 3 (kg, litre, m, m2 ... are measured, not counted) - a starting point, the shop edits it on the unit screen.
-- Idempotent.
-- ============================================================

USE MasterDataDB;
GO

IF COL_LENGTH('UnitsOfMeasure', 'DecimalPlaces') IS NULL
    ALTER TABLE UnitsOfMeasure ADD DecimalPlaces INT NOT NULL CONSTRAINT DF_UnitsOfMeasure_DecimalPlaces DEFAULT 0;
GO

-- Run once: only rows still at the default and not counted units. A unit edited later is never touched again because the column exists.
IF NOT EXISTS (SELECT 1 FROM sys.extended_properties WHERE name = N'DecimalPlacesBackfilled' AND major_id = OBJECT_ID('UnitsOfMeasure'))
BEGIN
    EXEC('UPDATE UnitsOfMeasure SET DecimalPlaces = 3 WHERE DecimalPlaces = 0 AND UnitType IN (0, 1, 2, 4)');
    EXEC sys.sp_addextendedproperty @name = N'DecimalPlacesBackfilled', @value = N'1', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'UnitsOfMeasure';
END
GO
