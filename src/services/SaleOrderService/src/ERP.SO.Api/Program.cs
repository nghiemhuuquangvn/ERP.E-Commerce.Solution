using Confluent.Kafka;
using ERP.Shared.Authorization;
using ERP.Shared.Extensions;
using ERP.Shared.Infrastructure.Caching;
using ERP.Shared.Localization;
using ERP.Shared.Messaging;
using ERP.SO.Api.Extensions;
using ERP.SO.Api.Middleware;
using ERP.SO.Application.Handlers;
using ERP.SO.Application.Interfaces;
using ERP.SO.Application.Sagas;
using ERP.SO.Application.Validators;
using ERP.SO.Infrastructure;
using ERP.SO.Infrastructure.BackgroundJobs;
using ERP.SO.Infrastructure.EventBus;
using ERP.SO.Infrastructure.ExternalServices;
using ERP.SO.Infrastructure.MessageBus;
using ERP.SO.Infrastructure.Repositories;
using ERP.SO.Infrastructure.Sagas;
using ERP.SO.Infrastructure.Services;
using ERP.Shared.Versioning;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<SalesOrderDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SalesDb")
        ?? "Server=(local);Database=SalesDB;Trusted_Connection=true;"));

// Cache
builder.Services.AddCacheService(builder.Configuration, "so");

// MediatR + Validators
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining(typeof(CreateOrderCommandHandler)));
builder.Services.AddValidatorsFromAssemblyContaining(typeof(CreateOrderCommandValidator));

// Repository, UnitOfWork & Message Bus
builder.Services.AddScoped<ISalesOrderRepository, SalesOrderRepository>();
builder.Services.AddScoped<IOrderFulfillmentSagaRepository, OrderFulfillmentSagaRepository>();
builder.Services.AddScoped<ISalesOrderUnitOfWork, SalesOrderUnitOfWork>();
builder.Services.AddScoped<IMessageBus, RabbitMqMessageBus>();
builder.Services.AddScoped<IOrderNumberGenerator, OrderNumberGenerator>();
builder.Services.AddScoped<ICustomerSalesProfileRepository, CustomerSalesProfileRepository>();
builder.Services.AddScoped<ICommissionPlanRepository, CommissionPlanRepository>();
builder.Services.AddScoped<ICommissionRecordRepository, CommissionRecordRepository>();
builder.Services.AddScoped<ISalesQuotationRepository, SalesQuotationRepository>();

// Orchestration saga — Order → Payment → Inventory Reservation → (Approval gate) →
// Shipment. Wired up 2026-07-14: previously registered nowhere, its own state entity
// wasn't even configured in EF Core, and none of its Handle*Async methods were ever
// invoked by anything.
builder.Services.AddScoped<OrderFulfillmentSaga>();

// Cart/Checkout/Customer + Order Settings (System/Tenant two-tier) — storage+CRUD only for now
builder.Services.AddScoped<ICartCheckoutSettingsService, CartCheckoutSettingsService>();
builder.Services.AddScoped<IOrderSettingsService, OrderSettingsService>();

// PromotionService client (coupon validation at order creation)
builder.Services.AddHttpClient<IPromotionServiceClient, PromotionServiceClient>();

// WareHouseService client — synchronous stock availability pre-check before payment is
// charged (see CreateOrderCommandHandler). Previously CreateOrderCommandHandler had no
// inventory awareness at all: an order was created and paid for before the saga ever
// asked WareHouseService to reserve stock, so an out-of-stock order was only caught
// (and refunded) after the customer had already been charged.
builder.Services.AddHttpClient<IWarehouseServiceClient, WarehouseServiceClient>();

// Consumes ProductService's product-topic (Kafka) for ProductPriceChangedDomainEvent —
// previously impossible, since that event didn't exist in ProductService until
// 2026-07-03 (docs/business section 3.2).
builder.Services.AddHostedService<ProductPriceChangedConsumer>();

// Forward-compatible plumbing: invalidates cached Currency Settings when
// SharedMasterDataService publishes a SettingsChangedIntegrationEvent on its "mst-topic".
// Distinct consumer group id so it doesn't compete for offsets with the other consumers'
// "sales-order-service-consumer" group. No visible effect yet — SaleOrderService doesn't
// fetch/cache Currency Settings anywhere today.
builder.Services.AddHostedService<CurrencySettingsConsumer>();

// Isolated Settings-changed Kafka publish path (Outbox -> "sale-order-settings-topic").
// Entirely separate from IMessageBus/RabbitMqMessageBus and the "sales_order_events"
// exchange used by the order-fulfillment saga — this is the first time SaleOrderService
// produces to Kafka, so its own IProducer<string,string> singleton is registered here
// (the existing Kafka consumers above only ever consumed, never produced).
var settingsKafkaSettings = builder.Configuration.GetSection("KafkaSettings");
var settingsBootstrapServers = settingsKafkaSettings.GetValue<string>("BootstrapServers") ?? "localhost:9092";
var settingsProducerConfig = new ProducerConfig
{
    BootstrapServers = settingsBootstrapServers,
    Acks = Acks.All,
    RetryBackoffMs = settingsKafkaSettings.GetValue<int>("RetryBackoffMs", 100),
    MessageTimeoutMs = settingsKafkaSettings.GetValue<int>("MessageTimeoutMs", 30000)
};
builder.Services.AddSingleton(new ProducerBuilder<string, string>(settingsProducerConfig).Build());
builder.Services.AddScoped<ISettingsEventPublisher, KafkaSettingsEventPublisher>();
builder.Services.AddHostedService<SettingsOutboxProcessor>();

// Outbox retrofit for SalesOrder/SalesQuotation domain events (previously published
// directly from command handlers, non-transactionally, right after SaveChanges) — see
// DomainEventOutboxWriter/SalesOrderEventOutboxProcessor.
builder.Services.AddHostedService<SalesOrderEventOutboxProcessor>();

// RabbitMQ connection — shared single connection, matching the topic exchange model
// PaymentService/WarehouseService/ShippingService's consumers already use. SO didn't
// need one before since RabbitMqMessageBus (outbound only) creates its own internally.
builder.Services.AddSingleton<IConnection>(_ =>
{
    var factory = new ConnectionFactory
    {
        HostName = builder.Configuration["RabbitMQ:HostName"] ?? "localhost",
        Port = int.Parse(builder.Configuration["RabbitMQ:Port"] ?? "5672"),
        UserName = builder.Configuration["RabbitMQ:UserName"] ?? "guest",
        Password = builder.Configuration["RabbitMQ:Password"] ?? "guest",
        VirtualHost = builder.Configuration["RabbitMQ:VirtualHost"] ?? "/",
        DispatchConsumersAsync = true
    };
    return factory.CreateConnection();
});

// Feed the saga: PaymentProcessed/PaymentFailed (Kafka payment-topic),
// InventoryReserved/InventoryInsufficient (RabbitMQ warehouse_exchange),
// ShipmentCreated (Kafka shipping-topic).
builder.Services.AddHostedService<PaymentKafkaConsumer>();
builder.Services.AddHostedService<WarehouseRabbitMqConsumer>();
builder.Services.AddHostedService<ShippingKafkaConsumer>();

// Surfaces sagas that haven't progressed within a reasonable time — GetStuckSagasAsync
// existed on the repository already but nothing ever called it.
builder.Services.AddHostedService<SagaRecoveryHostedService>();

// Authorization & User Context — real JWT validation (previously this service trusted
// the Gateway's forwarded X-User-*/X-Tenant-* headers with no signature verification;
// see AuthenticationExtensions.AddErpJwtAuthentication/JwtClaimsUserContextService).
builder.Services.AddErpJwtAuthentication(builder.Configuration);
builder.Services.AddErpAuthorization();
// Tenant must hold a license for this module (claim "licensed_module"); was missing here.
builder.Services.AddSingleton(new ModuleIdentity(ModuleCodes.SaleOrder));
builder.Services.AddMiddleware();

// API
builder.Services.AddControllers();
builder.Services.AddErpApiVersioning();
builder.Services.AddSharedLocalization(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();
builder.Services.AddCors(options =>
    options.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

app.UseGlobalExceptionHandling();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseUserContextHeaderMiddleware();
app.UseMiddleware<LicenseEnforcementMiddleware>();
app.UseSharedLocalization();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();
