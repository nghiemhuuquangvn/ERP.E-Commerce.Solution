using Confluent.Kafka;
using ERP.POS.Application.EventHandlers;
using ERP.POS.Application.Handlers;
using ERP.POS.Application.Interfaces;
using ERP.POS.Infrastructure.BackgroundJobs;
using ERP.POS.Infrastructure.MessageBus;
using ERP.POS.Infrastructure.Persistence;
using ERP.POS.Infrastructure.Repositories;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Caching;
using ERP.Shared.Localization;
using ERP.Shared.Messaging;
using MediatR;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<POSDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("PosDb")
        ?? "Server=(local);Database=PosDB;Trusted_Connection=true;"));

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblyContaining<OpenTransactionCommandHandler>());

builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<ICashShiftRepository, CashShiftRepository>();
builder.Services.AddScoped<ITerminalRepository, TerminalRepository>();
builder.Services.AddScoped<IPosProductCacheRepository, PosProductCacheRepository>();
builder.Services.AddScoped<IPosPromotionCacheRepository, PosPromotionCacheRepository>();
builder.Services.AddScoped<IPosUnitOfWork, PosUnitOfWork>();
builder.Services.AddCacheService(builder.Configuration, "pos");

// Inbound consumer: product-topic + promotion-topic + payment-topic (Kafka) —
// previously dead handler classes below (ProductPriceChangedForPosHandler and
// PaymentProcessedForPosHandler were never registered/dispatched).
builder.Services.AddHostedService<PosKafkaConsumer>();
builder.Services.AddScoped<ProductCreatedForPosHandler>();
builder.Services.AddScoped<ProductPriceChangedForPosHandler>();
builder.Services.AddScoped<PromotionCreatedForPosHandler>();
builder.Services.AddScoped<PaymentProcessedForPosHandler>();

// Outbox drain job — POSDbContext.SaveChangesAsync already wrote domain events
// (TransactionCreatedEvent, CashShiftClosedEvent) into OutboxMessages correctly, but
// no processor ever exised to relay them to Kafka, so they sat there unpublished
// forever. This is what makes "real-time inventory deduction" possible at all —
// WarehouseService still needs its own consumer for "pos-topic" to actually act on it.
var kafkaServers = builder.Configuration["KafkaSettings:BootstrapServers"] ?? "localhost:9092";
var producerConfig = new ProducerConfig { BootstrapServers = kafkaServers, AllowAutoCreateTopics = true };
builder.Services.AddSingleton(new ProducerBuilder<string, string>(producerConfig).Build());
builder.Services.AddScoped<IMessageBus, KafkaMessageBus>();
builder.Services.AddHostedService<OutboxProcessor>();

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
    c.SwaggerDoc("v1", new() { Title = "POS Service API", Version = "v1" }));
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
