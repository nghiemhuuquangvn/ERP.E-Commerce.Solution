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
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Caching;
using ERP.Shared.Localization;
using ERP.Shared.Messaging;
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

// Payment gateway — use VNPay when credentials are configured, fall back to Null stub
var vnpayConfigured = !string.IsNullOrEmpty(builder.Configuration["VnPay:TmnCode"]);
if (vnpayConfigured)
    builder.Services.AddScoped<IPaymentGateway, VnPayGateway>();
else
    builder.Services.AddScoped<IPaymentGateway, NullPaymentGateway>();

// Real JWT validation — previously trusted the Gateway's forwarded X-User-*/
// X-Tenant-* headers with no signature verification; see
// AuthenticationExtensions.AddErpJwtAuthentication/JwtClaimsUserContextService.
builder.Services.AddErpJwtAuthentication(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserContextService, JwtClaimsUserContextService>();
builder.Services.AddScoped<UserContext>();

builder.Services.AddHealthChecks();
builder.Services.AddControllers();
builder.Services.AddSharedLocalization(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
    c.SwaggerDoc("v1", new() { Title = "Payment Service API", Version = "v1" }));
builder.Services.AddCors(options =>
    options.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseSharedLocalization();
app.UseAuthentication();
app.UseMiddleware<ERP.Shared.Authorization.UserContextHeaderMiddleware>();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapControllers();

app.Run();
