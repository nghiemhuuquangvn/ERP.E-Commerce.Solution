using ERP.CHB.Application.Dtos;
using ERP.CHB.Application.Interfaces;
using ERP.CHB.Domain.Enums;
using ERP.Shared.Authorization;

namespace ERP.CHB.Infrastructure.Tools;

/// <summary>
/// Endpoint paths below are copied from each service's own controller route attributes
/// (SalesOrdersController, InventoryController, ShipmentsController, ProductsController,
/// PromotionsController) as of 2026-08 — verify against those controllers if a service's
/// routes change, since a stale path here fails silently as a normal-looking tool_result
/// error rather than a build error.
/// </summary>
public class GetMyOrdersTool : IErpTool
{
    private readonly IErpGatewayClient _gateway;
    public GetMyOrdersTool(IErpGatewayClient gateway) => _gateway = gateway;

    public ChatToolDefinition Definition { get; } = new(
        "get_my_orders",
        "List the current authenticated customer's own sales orders (summary: id, status, total, date). Takes no arguments.",
        """{"type":"object","properties":{}}""");

    public IReadOnlyCollection<ConversationChannel> Channels { get; } = new[] { ConversationChannel.Storefront };
    public bool AllowGuest => false;
    public string? RequiredPermission => null;

    public Task<string> ExecuteAsync(UserContext? caller, string? callerJwt, string argumentsJson, CancellationToken ct) =>
        _gateway.GetAsync("/api/v1/orders/my", callerJwt, ct);
}

public class GetOrderByIdTool : IErpTool
{
    private readonly IErpGatewayClient _gateway;
    public GetOrderByIdTool(IErpGatewayClient gateway) => _gateway = gateway;

    public ChatToolDefinition Definition { get; } = new(
        "get_order_by_id",
        "Get full detail (status, line items, shipping, totals) for one sales order by its id.",
        """{"type":"object","properties":{"orderId":{"type":"string","description":"The order's GUID id."}},"required":["orderId"]}""");

    public IReadOnlyCollection<ConversationChannel> Channels { get; } = new[] { ConversationChannel.Storefront, ConversationChannel.Admin };
    public bool AllowGuest => false;
    public string? RequiredPermission => null;

    public Task<string> ExecuteAsync(UserContext? caller, string? callerJwt, string argumentsJson, CancellationToken ct)
    {
        var orderId = ToolArgs.GetRequiredString(argumentsJson, "orderId");
        return _gateway.GetAsync($"/api/v1/orders/{orderId}", callerJwt, ct);
    }
}

public class GetProductInfoTool : IErpTool
{
    private readonly IErpGatewayClient _gateway;
    public GetProductInfoTool(IErpGatewayClient gateway) => _gateway = gateway;

    public ChatToolDefinition Definition { get; } = new(
        "search_products",
        "Search the product catalog by name/keyword. Returns matching products with id, name, price and short description.",
        """{"type":"object","properties":{"query":{"type":"string","description":"Search text, e.g. a product name or keyword."}},"required":["query"]}""");

    public IReadOnlyCollection<ConversationChannel> Channels { get; } = new[] { ConversationChannel.Storefront, ConversationChannel.Admin };
    public bool AllowGuest => true;
    public string? RequiredPermission => null;

    public Task<string> ExecuteAsync(UserContext? caller, string? callerJwt, string argumentsJson, CancellationToken ct)
    {
        var query = ToolArgs.GetRequiredString(argumentsJson, "query");
        return _gateway.GetAsync($"/api/v1/products/search?q={Uri.EscapeDataString(query)}", callerJwt, ct);
    }
}

public class GetInventoryAvailabilityTool : IErpTool
{
    private readonly IErpGatewayClient _gateway;
    public GetInventoryAvailabilityTool(IErpGatewayClient gateway) => _gateway = gateway;

    public ChatToolDefinition Definition { get; } = new(
        "get_stock_availability",
        "Get current stock availability for one product id (does not reflect in-flight soft reservations — treat as a live approximation).",
        """{"type":"object","properties":{"productId":{"type":"string","description":"The product's GUID id."}},"required":["productId"]}""");

    public IReadOnlyCollection<ConversationChannel> Channels { get; } = new[] { ConversationChannel.Storefront, ConversationChannel.Admin };
    public bool AllowGuest => true;
    public string? RequiredPermission => null;

    public Task<string> ExecuteAsync(UserContext? caller, string? callerJwt, string argumentsJson, CancellationToken ct)
    {
        var productId = ToolArgs.GetRequiredString(argumentsJson, "productId");
        return _gateway.GetAsync($"/api/v1/inventory/{productId}/availability", callerJwt, ct);
    }
}

public class GetShipmentTrackingByOrderTool : IErpTool
{
    private readonly IErpGatewayClient _gateway;
    public GetShipmentTrackingByOrderTool(IErpGatewayClient gateway) => _gateway = gateway;

    public ChatToolDefinition Definition { get; } = new(
        "get_shipment_by_order",
        "Get shipment and tracking status for a sales order by the order's id.",
        """{"type":"object","properties":{"orderId":{"type":"string","description":"The order's GUID id."}},"required":["orderId"]}""");

    public IReadOnlyCollection<ConversationChannel> Channels { get; } = new[] { ConversationChannel.Storefront, ConversationChannel.Admin };
    public bool AllowGuest => false;
    public string? RequiredPermission => null;

    public Task<string> ExecuteAsync(UserContext? caller, string? callerJwt, string argumentsJson, CancellationToken ct)
    {
        var orderId = ToolArgs.GetRequiredString(argumentsJson, "orderId");
        return _gateway.GetAsync($"/api/v1/shipments/order/{orderId}", callerJwt, ct);
    }
}

public class GetActivePromotionsTool : IErpTool
{
    private readonly IErpGatewayClient _gateway;
    public GetActivePromotionsTool(IErpGatewayClient gateway) => _gateway = gateway;

    public ChatToolDefinition Definition { get; } = new(
        "get_active_promotions",
        "List all current promotions/discounts. Takes no arguments.",
        """{"type":"object","properties":{}}""");

    public IReadOnlyCollection<ConversationChannel> Channels { get; } = new[] { ConversationChannel.Storefront };
    public bool AllowGuest => true;
    public string? RequiredPermission => null;

    public Task<string> ExecuteAsync(UserContext? caller, string? callerJwt, string argumentsJson, CancellationToken ct) =>
        _gateway.GetAsync("/api/v1/promotions/all", callerJwt, ct);
}

internal static class ToolArgs
{
    public static string GetRequiredString(string argumentsJson, string propertyName)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);
        if (!doc.RootElement.TryGetProperty(propertyName, out var value) || value.ValueKind != System.Text.Json.JsonValueKind.String)
            throw new ArgumentException($"Tool call is missing required string argument '{propertyName}'.");
        return value.GetString()!;
    }
}
