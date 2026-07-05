using ERP.Shared.Authorization;
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
using ERP.SO.Infrastructure.EventBus;
using ERP.SO.Infrastructure.ExternalServices;
using ERP.SO.Infrastructure.Repositories;
using ERP.SO.Infrastructure.Sagas;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

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

// PromotionService client (coupon validation at order creation)
builder.Services.AddHttpClient<IPromotionServiceClient, PromotionServiceClient>();

// Consumes ProductService's product-topic (Kafka) for ProductPriceChangedDomainEvent —
// previously impossible, since that event didn't exist in ProductService until
// 2026-07-03 (docs/business section 3.2).
builder.Services.AddHostedService<ProductPriceChangedConsumer>();

// Authorization & User Context
builder.Services.AddScoped<UserContext>();
builder.Services.AddMiddleware();

// API
builder.Services.AddControllers();
builder.Services.AddSharedLocalization(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();
builder.Services.AddCors(options =>
    options.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseUserContextHeaderMiddleware();
app.UseSharedLocalization();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
