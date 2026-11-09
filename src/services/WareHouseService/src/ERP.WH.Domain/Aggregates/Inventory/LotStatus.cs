namespace ERP.WH.Domain.Aggregates.Inventory;

/// <summary>Whether a lot may be allocated or issued. Stored as TINYINT.</summary>
public enum LotStatus : byte
{
    /// <summary>Normal stock.</summary>
    Available = 0,
    /// <summary>Held for quality control (a production receipt waiting for FQC): counted, but not allocatable.</summary>
    QcHold = 1,
    /// <summary>Withdrawn (damaged, recalled): counted, never allocatable.</summary>
    Blocked = 2
}

/// <summary>Where a lot came from. Stored as TINYINT.</summary>
public enum LotSource : byte
{
    Receipt = 0,
    Production = 1,
    Return = 2
}
