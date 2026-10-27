using ERP.Shared.Authorization;
using ERP.SO.Application.Commands;
using ERP.SO.Application.DTOs;
using ERP.SO.Application.Interfaces;
using ERP.SO.Application.Sagas;
using ERP.SO.Domain.Entities;
using ERP.SO.Domain.ValueObjects;
using MediatR;

namespace ERP.SO.Application.Handlers
{
    public interface ISalesOrderRepository
    {
        Task<SalesOrder?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task AddAsync(SalesOrder order, CancellationToken ct = default);
        Task UpdateAsync(SalesOrder order, CancellationToken ct = default);
        Task<List<SalesOrder>> GetByCustomerIdAsync(Guid customerId, CancellationToken ct = default);
        Task<List<SalesOrder>> GetAllAsync(int skip = 0, int take = 20, CancellationToken ct = default);
        Task<int> GetTotalCountAsync(Guid? customerId = null, OrderStatus? status = null, CancellationToken ct = default);
        Task<List<SalesOrder>> GetByStatusAsync(OrderStatus status, CancellationToken ct = default);
        Task<List<SalesOrder>> GetOpenOrdersContainingProductAsync(Guid productId, CancellationToken ct = default);
    }

    public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, SalesOrderDto>
    {
        private readonly ISalesOrderUnitOfWork _uow;
        private readonly IPromotionServiceClient _promotionServiceClient;
        private readonly IWarehouseServiceClient _warehouseServiceClient;
        private readonly OrderFulfillmentSaga _saga;
        private readonly IUserContextService _userContextService;
        private readonly IOrderNumberGenerator _orderNumberGenerator;

        public CreateOrderCommandHandler(
            ISalesOrderUnitOfWork uow,
            IPromotionServiceClient promotionServiceClient,
            IWarehouseServiceClient warehouseServiceClient,
            OrderFulfillmentSaga saga,
            IUserContextService userContextService,
            IOrderNumberGenerator orderNumberGenerator)
        {
            _uow = uow;
            _promotionServiceClient = promotionServiceClient;
            _warehouseServiceClient = warehouseServiceClient;
            _saga = saga;
            _userContextService = userContextService;
            _orderNumberGenerator = orderNumberGenerator;
        }

        public async Task<SalesOrderDto> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
        {
            var address = new Address(
                request.ShippingAddress.Street,
                request.ShippingAddress.City,
                request.ShippingAddress.State,
                request.ShippingAddress.PostalCode,
                request.ShippingAddress.Country);

            var lineItems = request.LineItems
                .Select(li => new OrderLineItem(li.ProductId, li.ProductName, li.Quantity, li.UnitPrice))
                .ToList();

            // Stock pre-check — must run before the order is persisted and before
            // InitiatePaymentCommand fires. Fails closed: any line we can't confirm as
            // available (including a WareHouseService outage) rejects order creation
            // entirely, rather than charging the customer and finding out via the async
            // Reserve -> InventoryInsufficientEvent -> refund+cancel saga path afterward.
            // That saga path (OrderFulfillmentSaga.HandleInventoryInsufficientAsync) stays
            // as the safety net for the residual race between this check and the actual
            // reservation, not as the primary detection mechanism.
            foreach (var item in lineItems)
            {
                var availability = await _warehouseServiceClient.CheckAvailabilityAsync(item.ProductId, item.Quantity, cancellationToken);
                if (!availability.IsAvailable)
                {
                    throw new InvalidOperationException(
                        $"Insufficient stock for product {item.ProductId}: requested {item.Quantity}, available {availability.AvailableQuantity}." +
                        (availability.ErrorMessage != null ? $" ({availability.ErrorMessage})" : string.Empty));
                }
            }

            var tenantId = new Guid(_userContextService.GetCurrentUser().TenantId);
            var orderNo = await _orderNumberGenerator.NextAsync(tenantId, "SO", cancellationToken);
            var orderSource = string.IsNullOrWhiteSpace(request.OrderSource)
                ? OrderSource.Manual
                : Enum.Parse<OrderSource>(request.OrderSource, ignoreCase: true);

            // The payment method must be one the platform knows; whether this shop really offers it is checked when the customer starts the payment.
            var paymentMethod = NormalizePaymentMethod(request.PaymentMethod);

            var salesOrder = SalesOrder.Create(
                request.CustomerId,
                request.CustomerName,
                request.CustomerEmail,
                address,
                lineItems,
                request.Notes,
                orderNo,
                orderSource,
                request.DepartmentId,
                request.SalesRepId,
                request.QuotationId,
                paymentMethod,
                NormalizePhone(request.CustomerPhone));

            if (!string.IsNullOrWhiteSpace(request.CouponCode))
            {
                var validation = await _promotionServiceClient.ValidateCouponAsync(request.CouponCode, salesOrder.TotalAmount, cancellationToken);
                if (validation.IsValid && validation.PromotionId.HasValue)
                    salesOrder.ApplyDiscount(validation.PromotionId.Value, validation.DiscountAmount);
                else
                    salesOrder.AddInternalNote($"Coupon '{request.CouponCode}' not applied: {validation.ErrorMessage}", "system:promotion-validation");
            }

            await _uow.Orders.AddAsync(salesOrder, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);

            await _saga.HandleOrderCreatedAsync(salesOrder.Id, tenantId, salesOrder.TotalAmount, cancellationToken,
                salesOrder.CustomerId, salesOrder.OrderNo, salesOrder.PaymentMethod);

            return MapToDto(salesOrder);
        }

        /// <summary>A phone number as people type it: digits with an optional leading +, spaces, dots, dashes and brackets removed. Null when empty; refused when it is not a phone number.</summary>
        public static string? NormalizePhone(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return null;
            var digits = new string(phone.Where(c => char.IsDigit(c) || c == '+').ToArray());
            var plain = digits.TrimStart('+');
            if (plain.Length < 8 || plain.Length > 15 || phone.Any(c => !(char.IsDigit(c) || " +-.()".Contains(c))))
                throw new ERP.Shared.Infrastructure.Exceptions.BusinessRuleException("Sales", $"'{phone}' is not a valid phone number.");
            return digits;
        }

        private static readonly string[] PaymentMethods = { "COD", "VNPay", "MoMo", "ZaloPay", "Unspecified" };

        /// <summary>Canonical spelling of a known payment method; null when none was given. An unknown one is refused (a business-rule error, HTTP 400).</summary>
        public static string? NormalizePaymentMethod(string? method)
        {
            if (string.IsNullOrWhiteSpace(method)) return null;
            return PaymentMethods.FirstOrDefault(m => string.Equals(m, method.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? throw new ERP.Shared.Infrastructure.Exceptions.BusinessRuleException("Sales", $"Unknown payment method '{method}'. Use one of: {string.Join(", ", PaymentMethods)}.");
        }

        private SalesOrderDto MapToDto(SalesOrder order)
        {
            return new SalesOrderDto
            {
                OrderId = order.Id,
                CustomerId = order.CustomerId,
                CustomerName = order.CustomerName,
                CustomerEmail = order.CustomerEmail,
                Status = order.Status.ToString(),
                TotalAmount = order.TotalAmount,
                OrderDate = order.OrderDate,
                ApprovedAt = order.ApprovedAt,
                ShippedAt = order.ShippedAt,
                DeliveredAt = order.DeliveredAt,
                CancelledAt = order.CancelledAt,
                Notes = order.Notes,
                OrderNo = order.OrderNo,
                OrderSource = order.OrderSource.ToString(),
                PaymentMethod = order.PaymentMethod,
                DepartmentId = order.DepartmentId,
                SalesRepId = order.SalesRepId,
                QuotationId = order.QuotationId,
                PromotionId = order.PromotionId,
                DiscountAmount = order.DiscountAmount,
                ShippingAddress = new AddressDto
                {
                    Street = order.ShippingAddress.Street,
                    City = order.ShippingAddress.City,
                    State = order.ShippingAddress.State,
                    PostalCode = order.ShippingAddress.PostalCode,
                    Country = order.ShippingAddress.Country
                },
                LineItems = order.LineItems
                    .Select(li => new OrderLineItemDto
                    {
                        ProductId = li.ProductId,
                        ProductName = li.ProductName,
                        Quantity = li.Quantity,
                        UnitPrice = li.UnitPrice
                    })
                    .ToList()
            };
        }
    }

    public class ApproveOrderCommandHandler : IRequestHandler<ApproveOrderCommand, SalesOrderDto>
    {
        private readonly ISalesOrderUnitOfWork _uow;
        private readonly OrderFulfillmentSaga _saga;
        private readonly ICustomerSalesProfileRepository _profiles;

        public ApproveOrderCommandHandler(ISalesOrderUnitOfWork uow, OrderFulfillmentSaga saga, ICustomerSalesProfileRepository profiles)
        {
            _uow = uow;
            _saga = saga;
            _profiles = profiles;
        }

        public async Task<SalesOrderDto> Handle(ApproveOrderCommand request, CancellationToken cancellationToken)
        {
            var order = await _uow.Orders.GetByIdAsync(request.OrderId, cancellationToken)
                ?? throw new InvalidOperationException($"Order {request.OrderId} not found");

            // Credit-limit check: only enforced once a CustomerSalesProfile exists for this
            // customer — no profile means "no limit configured", not "blocked".
            var profile = await _profiles.GetByCustomerIdAsync(order.CustomerId, cancellationToken);
            if (profile != null && profile.CurrentBalance + order.TotalAmount > profile.CreditLimit)
                throw new InvalidOperationException(
                    $"Order exceeds customer's credit limit ({profile.CreditLimit:C}); current open balance {profile.CurrentBalance:C} + this order {order.TotalAmount:C}.");

            order.Approve();
            await _uow.Orders.UpdateAsync(order, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);

            if (profile != null)
            {
                profile.IncreaseExposure(order.TotalAmount);
                await _profiles.UpdateAsync(profile, cancellationToken);
            }

            await _saga.HandleOrderApprovedAsync(order.Id, cancellationToken);

            return MapToDto(order);
        }

        private SalesOrderDto MapToDto(SalesOrder order)
        {
            return new SalesOrderDto
            {
                OrderId = order.Id,
                CustomerId = order.CustomerId,
                CustomerName = order.CustomerName,
                CustomerEmail = order.CustomerEmail,
                Status = order.Status.ToString(),
                TotalAmount = order.TotalAmount,
                OrderDate = order.OrderDate,
                ApprovedAt = order.ApprovedAt,
                ShippedAt = order.ShippedAt,
                DeliveredAt = order.DeliveredAt,
                CancelledAt = order.CancelledAt,
                Notes = order.Notes,
                OrderNo = order.OrderNo,
                OrderSource = order.OrderSource.ToString(),
                PaymentMethod = order.PaymentMethod,
                DepartmentId = order.DepartmentId,
                SalesRepId = order.SalesRepId,
                QuotationId = order.QuotationId,
                PromotionId = order.PromotionId,
                DiscountAmount = order.DiscountAmount,
                ShippingAddress = new AddressDto
                {
                    Street = order.ShippingAddress.Street,
                    City = order.ShippingAddress.City,
                    State = order.ShippingAddress.State,
                    PostalCode = order.ShippingAddress.PostalCode,
                    Country = order.ShippingAddress.Country
                },
                LineItems = order.LineItems
                    .Select(li => new OrderLineItemDto
                    {
                        ProductId = li.ProductId,
                        ProductName = li.ProductName,
                        Quantity = li.Quantity,
                        UnitPrice = li.UnitPrice
                    })
                    .ToList()
            };
        }
    }

    public class ShipOrderCommandHandler : IRequestHandler<ShipOrderCommand, SalesOrderDto>
    {
        private readonly ISalesOrderUnitOfWork _uow;

        public ShipOrderCommandHandler(ISalesOrderUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<SalesOrderDto> Handle(ShipOrderCommand request, CancellationToken cancellationToken)
        {
            var order = await _uow.Orders.GetByIdAsync(request.OrderId, cancellationToken)
                ?? throw new InvalidOperationException($"Order {request.OrderId} not found");

            order.Ship(request.TrackingNumber, request.Carrier);
            await _uow.Orders.UpdateAsync(order, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);

            return MapToDto(order);
        }

        private SalesOrderDto MapToDto(SalesOrder order)
        {
            return new SalesOrderDto
            {
                OrderId = order.Id,
                CustomerId = order.CustomerId,
                CustomerName = order.CustomerName,
                CustomerEmail = order.CustomerEmail,
                Status = order.Status.ToString(),
                TotalAmount = order.TotalAmount,
                OrderDate = order.OrderDate,
                ApprovedAt = order.ApprovedAt,
                ShippedAt = order.ShippedAt,
                DeliveredAt = order.DeliveredAt,
                CancelledAt = order.CancelledAt,
                Notes = order.Notes,
                OrderNo = order.OrderNo,
                OrderSource = order.OrderSource.ToString(),
                PaymentMethod = order.PaymentMethod,
                DepartmentId = order.DepartmentId,
                SalesRepId = order.SalesRepId,
                QuotationId = order.QuotationId,
                PromotionId = order.PromotionId,
                DiscountAmount = order.DiscountAmount,
                ShippingAddress = new AddressDto
                {
                    Street = order.ShippingAddress.Street,
                    City = order.ShippingAddress.City,
                    State = order.ShippingAddress.State,
                    PostalCode = order.ShippingAddress.PostalCode,
                    Country = order.ShippingAddress.Country
                },
                LineItems = order.LineItems
                    .Select(li => new OrderLineItemDto
                    {
                        ProductId = li.ProductId,
                        ProductName = li.ProductName,
                        Quantity = li.Quantity,
                        UnitPrice = li.UnitPrice
                    })
                    .ToList()
            };
        }
    }

    public class CancelOrderCommandHandler : IRequestHandler<CancelOrderCommand, SalesOrderDto>
    {
        private readonly ISalesOrderUnitOfWork _uow;
        private readonly ICustomerSalesProfileRepository _profiles;

        public CancelOrderCommandHandler(ISalesOrderUnitOfWork uow, ICustomerSalesProfileRepository profiles)
        {
            _uow = uow;
            _profiles = profiles;
        }

        public async Task<SalesOrderDto> Handle(CancelOrderCommand request, CancellationToken cancellationToken)
        {
            var order = await _uow.Orders.GetByIdAsync(request.OrderId, cancellationToken)
                ?? throw new InvalidOperationException($"Order {request.OrderId} not found");

            // Exposure was only added at Approve time — only release it if this order had
            // actually reached Approved (ApprovedAt set) before being cancelled.
            var wasApproved = order.ApprovedAt.HasValue;

            order.Cancel(request.Reason);
            await _uow.Orders.UpdateAsync(order, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);

            if (wasApproved)
            {
                var profile = await _profiles.GetByCustomerIdAsync(order.CustomerId, cancellationToken);
                if (profile != null)
                {
                    profile.DecreaseExposure(order.TotalAmount);
                    await _profiles.UpdateAsync(profile, cancellationToken);
                }
            }

            return MapToDto(order);
        }

        private SalesOrderDto MapToDto(SalesOrder order)
        {
            return new SalesOrderDto
            {
                OrderId = order.Id,
                CustomerId = order.CustomerId,
                CustomerName = order.CustomerName,
                CustomerEmail = order.CustomerEmail,
                Status = order.Status.ToString(),
                TotalAmount = order.TotalAmount,
                OrderDate = order.OrderDate,
                ApprovedAt = order.ApprovedAt,
                ShippedAt = order.ShippedAt,
                DeliveredAt = order.DeliveredAt,
                CancelledAt = order.CancelledAt,
                Notes = order.Notes,
                OrderNo = order.OrderNo,
                OrderSource = order.OrderSource.ToString(),
                PaymentMethod = order.PaymentMethod,
                DepartmentId = order.DepartmentId,
                SalesRepId = order.SalesRepId,
                QuotationId = order.QuotationId,
                PromotionId = order.PromotionId,
                DiscountAmount = order.DiscountAmount,
                ShippingAddress = new AddressDto
                {
                    Street = order.ShippingAddress.Street,
                    City = order.ShippingAddress.City,
                    State = order.ShippingAddress.State,
                    PostalCode = order.ShippingAddress.PostalCode,
                    Country = order.ShippingAddress.Country
                },
                LineItems = order.LineItems
                    .Select(li => new OrderLineItemDto
                    {
                        ProductId = li.ProductId,
                        ProductName = li.ProductName,
                        Quantity = li.Quantity,
                        UnitPrice = li.UnitPrice
                    })
                    .ToList()
            };
        }
    }
}
