-- ============================================================
-- PosDB Migration 007 — Transactions.PaymentMethod INT -> NVARCHAR(50) (ROADMAP P0-9)
-- 001 declared the column INT (0=Cash, 1=CreditCard, 2=EWallet) but the entity stores the method as text
-- ("Cash", "Mixed", ...): completing any sale failed with "Conversion failed when converting the nvarchar
-- value 'Cash' to data type int". Existing codes are converted to their names. Idempotent.
-- ============================================================

USE PosDB;
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Transactions') AND name = 'PaymentMethod' AND system_type_id = TYPE_ID('int'))
   AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Transactions') AND name = 'PaymentMethod_New')
    ALTER TABLE Transactions ADD PaymentMethod_New NVARCHAR(50) NULL;
GO

-- Separate batches: the UPDATE must compile after the ALTER above, so it runs as dynamic SQL.
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Transactions') AND name = 'PaymentMethod_New')
   AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Transactions') AND name = 'PaymentMethod' AND system_type_id = TYPE_ID('int'))
BEGIN
    EXEC (N'UPDATE Transactions SET PaymentMethod_New = CASE PaymentMethod WHEN 0 THEN N''Cash'' WHEN 1 THEN N''CreditCard'' WHEN 2 THEN N''EWallet'' ELSE CAST(PaymentMethod AS NVARCHAR(50)) END');
    ALTER TABLE Transactions DROP COLUMN PaymentMethod;
END
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Transactions') AND name = 'PaymentMethod_New')
    EXEC sp_rename 'Transactions.PaymentMethod_New', 'PaymentMethod', 'COLUMN';
GO
