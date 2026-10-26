using Confluent.Kafka;
using ERP.PAY.Application.Handlers;
using ERP.PAY.Application.Interfaces;
using ERP.PAY.Application.Services;
using ERP.PAY.Application.Validators;
using ERP.PAY.Infrastructure.BackgroundJobs;
using ERP.PAY.Infrastructure.EventBus;
using ERP.PAY.Infrastructure.Gateway;
using ERP.PAY.Infrastructure.MessageBus;
using ERP.PAY.Infrastructure.Persistence;
using ERP.PAY.Infrastructure.Repositories;
using ERP.PAY.Infrastructure.Services;
using ERP.Shared.Authorization;
using ERP.Shared.Extensions;
using ERP.Shared.Infrastructure.Caching;
using ERP.Shared.Localization;
using ERP.Shared.Messaging;
using ERP.Shared.Security;
using ERP.Shared.Versioning;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<PaymentDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("PaymentDb")
        ?? "Server=(local);Database=PaymentDB;Trusted_Connection=true;"));

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblyContaining<InitiatePaymentCommandHandler>());

builder.Services.AddValidatorsFromAssemblyContaining<InitiatePaymentCommandValidator>();

builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<ISettlementRepository, SettlementRepository>();
builder.Services.AddScoped<ISavedPaymentMethodRepository, SavedPaymentMethodRepository>();
builder.Services.AddScoped<IInstallmentPlanRepository, InstallmentPlanRepository>();
builder.Services.AddScoped<IPaymentUnitOfWork, PaymentUnitOfWork>();
builder.Services.AddScoped<IFraudDetectionService, FraudDetectionService>();

// Payment Settings (System/Tenant two-tier) — storage+CRUD only for now
builder.Services.AddScoped<ISecretProtector, AesSecretProtector>();
builder.Services.AddScoped<IPaymentSettingsService, PaymentSettingsService>();
builder.Services.AddCacheService(builder.Configuration, "pay");
builder.Services.AddHttpClient(); // used by VnPayGateway.RefundAsync (server-to-server POST)

// Kafka producer + Outbox — drains PaymentDbContext.OutboxMessages (already populated
// correctly by PaymentDbContext.SaveChangesAsync) onto the "payment-topic" topic.
var kafkaServers = builder.Configuration["KafkaSettings:BootstrapServers"] ?? "localhost:9092";
var producerConfig = new ProducerConfig
{
    BootstrapServers = kafkaServers,
    AllowAutoCreateTopics = true
};
builder.Services.AddSingleton(new ProducerBuilder<string, string>(producerConfig).Build());
builder.Services.AddScoped<IMessageBus, KafkaMessageBus>();
builder.Services.AddHostedService<OutboxProcessor>();

// RabbitMQ connection — shared single connection, matching the topic exchange model
// SaleOrderService's RabbitMqMessageBus / WarehouseService's consumer already use.
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

// Consumes SaleOrderService's sales_order_events topic exchange (RabbitMQ —
// SaleOrderService does not publish via Kafka) so the existing
// SalesOrderCreatedIntegrationEventHandler (Application/EventHandlers/
// SalesOrderCreatedEventHandler.cs) — previously dead code, since nothing ever
// called IMediator.Publish with this type — actually fires.
builder.Services.AddHostedService<SalesOrderRabbitMqConsumer>();

// Consumes AccountingService's "accounting-topic" for InvoiceCreatedEvent so
// invoice-based (net-terms) payments get a Payment record even when no upfront
// checkout payment was initiated for that order.
builder.Services.AddHostedService<InvoiceCreatedConsumer>();

// Cash on delivery (P1-10): captures a COD payment when ShippingService confirms the delivery.
builder.Services.AddHostedService<ShippingDeliveryConsumer>();

// Payment gateways (P1-10): every gateway is registered; the tenant's own keys decide which ones it can use (PaymentProviderResolver).
builder.Services.AddScoped<IPaymentProvider, VnPayGateway>();
builder.Services.AddScoped<IPaymentProvider, MoMoGateway>();
builder.Services.AddScoped<IPaymentProvider, ZaloPayGateway>();
builder.Services.AddScoped<IPaymentProvider, NullPaymentProvider>();
builder.Services.AddScoped<IPaymentProviderResolver, PaymentProviderResolver>();
builder.Services.AddScoped<IGatewayNotificationService, GatewayNotificationService>();

// Real JWT validation — previously trusted the Gateway's forwarded X-User-*/
// X-Tenant-* headers with no signature verification; see
// AuthenticationExtensions.AddErpJwtAuthentication/JwtClaimsUserContextService.
builder.Services.AddErpJwtAuthentication(builder.Configuration);
builder.Services.AddErpAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserContextService, JwtClaimsUserContextService>();
// Tenant isolation for PaymentDbContext: JWT tenant, Gateway host tenant for anonymous
// requests, or an explicit TenantScope (consumers, VNPay callbacks).
builder.Services.AddRequestTenantProvider();
builder.Services.AddScoped<UserContext>();
builder.Services.AddSingleton(new ModuleIdentity(ModuleCodes.Payment));

builder.Services.AddHealthChecks();
builder.Services.AddControllers();
builder.Services.AddErpApiVersioning();
builder.Services.AddSharedLocalization(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
    c.SwaggerDoc("v1", new() { Title = "Payment Service API", Version = "v1" }));
builder.Services.AddCors(options =>
    options.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

app.UseGlobalExceptionHandling();

// API documentation is for developers only: never expose the endpoint list in Production.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSharedLocalization();
app.UseAuthentication();
app.UseMiddleware<ERP.Shared.Authorization.UserContextHeaderMiddleware>();
app.UseMiddleware<LicenseEnforcementMiddleware>();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapHealthChecks("/health").AllowAnonymous();
app.MapControllers();

app.Run();
