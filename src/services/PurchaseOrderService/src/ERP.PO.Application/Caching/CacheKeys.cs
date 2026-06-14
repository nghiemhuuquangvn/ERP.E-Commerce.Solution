namespace ERP.PO.Application.Caching;

public static class CacheKeys
{
    public const string PurchaseOrderPrefix = "po";
    public const string SupplierPrefix = "supplier";

    public static string GetPurchaseOrderKey(Guid id) => $"{PurchaseOrderPrefix}:{id}";
    public static string GetPurchaseOrdersBySupplierKey(Guid supplierId) => $"{PurchaseOrderPrefix}:supplier:{supplierId}";
    public static string GetPurchaseOrdersByStatusKey(string status) => $"{PurchaseOrderPrefix}:status:{status}";
    public static string GetSupplierKey(Guid id) => $"{SupplierPrefix}:{id}";
    public static string GetSupplierListKey() => $"{SupplierPrefix}:list";
    public static string GetActiveSupplierKey() => $"{SupplierPrefix}:active";

    public static string InvalidatePurchaseOrderPattern() => $"{PurchaseOrderPrefix}:*";
    public static string InvalidateSupplierPattern() => $"{SupplierPrefix}:*";
}
