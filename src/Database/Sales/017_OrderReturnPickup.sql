-- ============================================================
-- SalesDB Migration 017 - the way back of a returned order through the carrier (2026-10-28, ROADMAP P1-11d)
--
-- SalesOrders.ReturnApprovedAt        - the approver approved the return (Shipping was asked to book the pickup).
-- SalesOrders.ReturnPickupBooked      - at least one reverse parcel is booked at a carrier: the refund waits until the goods are back.
-- SalesOrders.ReturnGoodsReceivedAt   - every reverse parcel has been received in the warehouse (told by Shipping's ReturnReceived).
-- SalesOrders.ReturnPickupNote        - what Shipping answered (parcels booked, or why none).
-- Idempotent.
-- ============================================================

USE SalesDB;
GO

IF COL_LENGTH('SalesOrders', 'ReturnApprovedAt') IS NULL ALTER TABLE SalesOrders ADD ReturnApprovedAt DATETIME2 NULL;
IF COL_LENGTH('SalesOrders', 'ReturnPickupBooked') IS NULL ALTER TABLE SalesOrders ADD ReturnPickupBooked BIT NOT NULL CONSTRAINT DF_SalesOrders_ReturnPickupBooked DEFAULT 0;
IF COL_LENGTH('SalesOrders', 'ReturnGoodsReceivedAt') IS NULL ALTER TABLE SalesOrders ADD ReturnGoodsReceivedAt DATETIME2 NULL;
IF COL_LENGTH('SalesOrders', 'ReturnPickupNote') IS NULL ALTER TABLE SalesOrders ADD ReturnPickupNote NVARCHAR(1000) NULL;
GO
