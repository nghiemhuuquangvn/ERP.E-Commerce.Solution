namespace ERP.MFG.Domain.Common;

// Enumerations of the data model (BA spec 01 §7). Stored as smallint; values never change once released.

public enum MakeOrBuy : short { Make = 1, Buy = 2, Both = 3 }

public enum LotSizingPolicy : short { LotForLot = 1, FixedQty = 2, MinMultiple = 3, PeriodOrder = 4 }

public enum IssueMethod : short { Manual = 1, Backflush = 2 }

public enum CostingMethod : short { Actual = 1, Standard = 2 }

/// <summary>Draft -> Approved -> Obsolete for BOM, routing and inspection plans (BR-BOM-02).</summary>
public enum VersionStatus : short { Draft = 1, Approved = 2, Obsolete = 3 }

public enum WorkCenterType : short { Machine = 1, Labor = 2, Subcontract = 3 }

public enum MachineStatus : short { Active = 1, UnderMaintenance = 2, Retired = 3 }

public enum RunTimeBasis : short { PerUnit = 1, PerBatch = 2 }

public enum CalendarExceptionType : short { Holiday = 1, ExtraWorkingDay = 2, ModifiedShifts = 3 }

public enum BomLineType : short { Component = 1, ByProduct = 2, CoProduct = 3 }

public enum ReasonType : short
{
    Scrap = 1, Rework = 2, Downtime = 3, Cancel = 4, Hold = 5, ShortClose = 6, UnplannedMaterial = 7, OverIssue = 8,
}

/// <summary>Downtime reason category (BR-RSN-02) - feeds OEE.</summary>
public enum DowntimeCategory : short { Planned = 1, Unplanned = 2 }

public enum OrderType : short { Standard = 1, Rework = 2, Subcontract = 3 }

/// <summary>Stock documents: a material issue or return (P3-3), a backflush consumption triggered by an operation report (P3-4); receipts join in P3-5.</summary>
public enum StockDocumentType : short { Issue = 1, Return = 2, Backflush = 3 }

/// <summary>Pending = waiting for its backflush document to reach the Warehouse; Posted = applied to the order; Failed = the Warehouse refused its backflush (nothing applied).</summary>
public enum OperationReportStatus : short { Pending = 1, Posted = 2, Failed = 3 }

/// <summary>Pending = not (fully) in the Warehouse yet; Posted = every line is in the Warehouse and the order shows it; Failed = refused or abandoned, nothing left in the Warehouse.</summary>
public enum StockDocumentStatus : short { Pending = 1, Posted = 2, Failed = 3 }

/// <summary>Forward = sending the lines; Rollback = reversing the lines already posted after a refusal or an abandon.</summary>
public enum StockDocumentMode : short { Forward = 1, Rollback = 2 }

public enum StockLineStatus : short { Pending = 1, Posted = 2, Refused = 3, Compensated = 4 }

public enum OrderSourceType : short { Manual = 1, SalesOrder = 2, Mrp = 3, Ncr = 4, ParentOrder = 5 }

/// <summary>BA spec 01 §4.2. Closed and Cancelled are final.</summary>
public enum OrderStatus : short { Draft = 1, Released = 2, InProgress = 3, OnHold = 4, Completed = 5, Closed = 6, Cancelled = 7 }

/// <summary>BA spec 01 §4.3.</summary>
public enum OperationStatus : short { Pending = 1, Ready = 2, Running = 3, Paused = 4, Completed = 5 }
