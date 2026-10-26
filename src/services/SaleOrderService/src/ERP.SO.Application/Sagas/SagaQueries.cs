using ERP.Shared.Infrastructure.Exceptions;
using ERP.SO.Application.Interfaces;
using ERP.SO.Domain.Sagas;
using MediatR;
using Microsoft.Extensions.Options;

namespace ERP.SO.Application.Sagas;

/// <summary>One step of the order's processing as the order page shows it (ROADMAP P1-5).</summary>
public class OrderSagaStepDto
{
    /// <summary>Payment, Inventory, Approval, Shipping or Done.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Done, Current, Waiting or Failed.</summary>
    public string State { get; set; } = "Waiting";
}

public class OrderSagaDto
{
    public Guid OrderId { get; set; }

    /// <summary>False for an order that predates the saga: there is nothing to show.</summary>
    public bool Exists { get; set; }

    /// <summary>Running, Completed, Compensating or Failed.</summary>
    public string Status { get; set; } = string.Empty;
    public string CurrentStep { get; set; } = string.Empty;
    public List<OrderSagaStepDto> Steps { get; set; } = new();
    public bool Approved { get; set; }
    public bool InventoryReserved { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; }

    /// <summary>When the saga last moved (the current step started waiting).</summary>
    public DateTime LastUpdatedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    /// <summary>Minutes the current step may wait before its command is published again; null when it never times out.</summary>
    public int? TimeoutMinutes { get; set; }

    /// <summary>The current step has waited longer than its timeout.</summary>
    public bool Stuck { get; set; }

    /// <summary>The error or the reason of the compensation, or the last failed delivery.</summary>
    public string? Message { get; set; }

    /// <summary>The current step can be pushed again by hand.</summary>
    public bool CanRetry { get; set; }
}

public class GetOrderSagaQuery : IRequest<OrderSagaDto>
{
    public Guid OrderId { get; set; }
}

/// <summary>Publishes the command of the saga's current step again, at once (a person's "retry").</summary>
public class RetryOrderSagaCommand : IRequest<OrderSagaDto>
{
    public Guid OrderId { get; set; }
}

public static class OrderSagaMapper
{
    private static readonly string[] Keys = { "Payment", "Inventory", "Approval", "Shipping", "Done" };

    public static OrderSagaDto Map(Guid orderId, OrderFulfillmentSagaState? saga, SagaOptions options, DateTime nowUtc)
    {
        if (saga is null)
            return new OrderSagaDto { OrderId = orderId, Exists = false, MaxRetries = options.MaxRetries };

        var timeout = options.TimeoutFor(saga.CurrentStep);
        var running = saga.Status == SagaStatus.Running;
        var dto = new OrderSagaDto
        {
            OrderId = orderId,
            Exists = true,
            Status = saga.Status.ToString(),
            CurrentStep = saga.CurrentStep.ToString(),
            Approved = saga.Approved,
            InventoryReserved = saga.InventoryReserved,
            RetryCount = saga.RetryCount,
            MaxRetries = options.MaxRetries,
            LastUpdatedAt = saga.LastUpdatedAt,
            CreatedAt = saga.CreatedAt,
            CompletedAt = saga.CompletedAt,
            TimeoutMinutes = timeout.HasValue ? (int)timeout.Value.TotalMinutes : null,
            Stuck = running && timeout.HasValue && nowUtc - saga.LastUpdatedAt >= timeout.Value,
            Message = saga.ErrorMessage,
            CanRetry = running && timeout.HasValue,
        };

        // Which step a failed saga stopped at is not stored: it is the first one whose result is missing.
        var failedAt = saga.Status is SagaStatus.Failed or SagaStatus.Compensating
            ? (saga.PaymentId is null ? "Payment" : !saga.InventoryReserved ? "Inventory" : "Shipping")
            : null;

        bool Done(string key) => key switch
        {
            "Payment" => saga.PaymentId.HasValue,
            "Inventory" => saga.InventoryReserved,
            "Approval" => saga.Approved,
            "Shipping" => saga.ShipmentId.HasValue || saga.Status == SagaStatus.Completed,
            _ => saga.Status == SagaStatus.Completed,
        };

        var current = saga.CurrentStep switch
        {
            SagaStep.PaymentPending => "Payment",
            SagaStep.InventoryPending => "Inventory",
            SagaStep.AwaitingApproval => "Approval",
            SagaStep.ShippingPending => "Shipping",
            _ => null,
        };

        foreach (var key in Keys)
        {
            string state;
            if (failedAt == key) state = "Failed";
            else if (Done(key)) state = "Done";
            else if (running && key == current) state = "Current";
            else state = "Waiting";
            dto.Steps.Add(new OrderSagaStepDto { Key = key, State = state });
        }
        return dto;
    }
}

public class GetOrderSagaQueryHandler : IRequestHandler<GetOrderSagaQuery, OrderSagaDto>
{
    private readonly ISalesOrderUnitOfWork _uow;
    private readonly SagaOptions _options;

    public GetOrderSagaQueryHandler(ISalesOrderUnitOfWork uow, IOptions<SagaOptions> options)
    {
        _uow = uow;
        _options = options.Value;
    }

    public async Task<OrderSagaDto> Handle(GetOrderSagaQuery request, CancellationToken ct)
    {
        var saga = await _uow.Sagas.GetByOrderIdAsync(request.OrderId, ct);
        return OrderSagaMapper.Map(request.OrderId, saga, _options, DateTime.UtcNow);
    }
}

public class RetryOrderSagaCommandHandler : IRequestHandler<RetryOrderSagaCommand, OrderSagaDto>
{
    private readonly ISalesOrderUnitOfWork _uow;
    private readonly OrderFulfillmentSaga _saga;
    private readonly SagaOptions _options;

    public RetryOrderSagaCommandHandler(ISalesOrderUnitOfWork uow, OrderFulfillmentSaga saga, IOptions<SagaOptions> options)
    {
        _uow = uow;
        _saga = saga;
        _options = options.Value;
    }

    public async Task<OrderSagaDto> Handle(RetryOrderSagaCommand request, CancellationToken ct)
    {
        var state = await _uow.Sagas.GetByOrderIdAsync(request.OrderId, ct)
            ?? throw new KeyNotFoundException($"Order {request.OrderId} has no processing record.");
        if (state.Status != SagaStatus.Running)
            throw new BusinessRuleException("Sales", $"The order's processing is {state.Status}: there is nothing to retry.");
        if (_options.TimeoutFor(state.CurrentStep) is null)
            throw new BusinessRuleException("Sales", $"The order is waiting for {state.CurrentStep}: that step is done by a person, not retried.");

        await _saga.HandleStepTimeoutAsync(state.Id, _options, DateTime.UtcNow, force: true, ct);
        var after = await _uow.Sagas.GetByOrderIdAsync(request.OrderId, ct);
        return OrderSagaMapper.Map(request.OrderId, after, _options, DateTime.UtcNow);
    }
}
