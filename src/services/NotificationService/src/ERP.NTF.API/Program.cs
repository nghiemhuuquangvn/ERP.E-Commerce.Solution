using Confluent.Kafka;
using ERP.NTF.Application.Handlers;
using ERP.NTF.Application.Interfaces;
using ERP.NTF.Application.Validators;
using ERP.NTF.Infrastructure.BackgroundJobs;
using ERP.NTF.Infrastructure.MessageBus;
using ERP.NTF.Infrastructure.Persistence;
using ERP.NTF.Infrastructure.Repositories;
using ERP.NTF.Infrastructure.Services;
using ERP.Shared.Authorization;
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

builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("NotificationDb")
        ?? "Server=(local);Database=NotificationDB;Trusted_Connection=true;"));

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<SendNotificationCommandHandler>();
});

builder.Services.AddValidatorsFromAssemblyContaining<SendNotificationCommandValidator>();

builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<ITemplateRepository, TemplateRepository>();
builder.Services.AddScoped<IPreferenceRepository, PreferenceRepository>();
builder.Services.AddScoped<IWebhookRepository, WebhookRepository>();

// Notification/Email/SMS Settings (System/Tenant two-tier) — storage+CRUD only for now
builder.Services.AddScoped<ISecretProtector, AesSecretProtector>();
builder.Services.AddScoped<INotificationSettingsService, NotificationSettingsService>();
builder.Services.AddScoped<IEmailSettingsService, EmailSettingsService>();
builder.Services.AddScoped<ISmsSettingsService, SmsSettingsService>();

builder.Services.AddHttpClient();

// Email: always SMTP (dev can use MailHog/Papercut on port 25)
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();

// SMS: Twilio when AccountSid configured, else Null stub
if (!string.IsNullOrEmpty(builder.Configuration["Twilio:AccountSid"]))
    builder.Services.AddScoped<ISmsSender, TwilioSmsSender>();
else
    builder.Services.AddScoped<ISmsSender, NullSmsSender>();

// Push: FCM when ServerKey configured, else Null stub
if (!string.IsNullOrEmpty(builder.Configuration["Fcm:ServerKey"]))
    builder.Services.AddScoped<IPushSender, FcmPushSender>();
else
    builder.Services.AddScoped<IPushSender, NullPushSender>();

builder.Services.AddScoped<IWebhookSender, WebhookSender>();

// Redis cache
builder.Services.AddCacheService(builder.Configuration, "ntf");

// RabbitMQ shared connection — needed for SalesOrderRabbitMqConsumer
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

// Kafka producer config (consumers use this same config's BootstrapServers value)
var kafkaProducerConfig = new ProducerConfig
{
    BootstrapServers = builder.Configuration["KafkaSettings:BootstrapServers"] ?? "localhost:9092",
    Acks = Acks.All,
    AllowAutoCreateTopics = true
};
builder.Services.AddSingleton(kafkaProducerConfig);

// Kafka producer + Outbox pattern — publishes to "notification-topic" (e.g. SettingsChangedIntegrationEvent)
builder.Services.AddSingleton(new ProducerBuilder<string, string>(kafkaProducerConfig).Build());
builder.Services.AddScoped<IMessageBus, KafkaMessageBus>();
builder.Services.AddHostedService<OutboxProcessor>();

// Background consumers
builder.Services.AddHostedService<NotificationDispatchJob>();
builder.Services.AddHostedService<IdentityEventConsumer>();          // identity-topic: UserCreated, TwoFactorCodeGenerated
builder.Services.AddHostedService<NotificationKafkaConsumer>();      // payment-topic, shipping-topic, mst-topic
builder.Services.AddHostedService<SalesOrderRabbitMqConsumer>();     // sales_order_events exchange: OrderCreated, OrderApproved

// Real JWT validation — previously trusted the Gateway's forwarded X-User-*/
// X-Tenant-* headers with no signature verification; see
// AuthenticationExtensions.AddErpJwtAuthentication/JwtClaimsUserContextService.
builder.Services.AddErpJwtAuthentication(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserContextService, JwtClaimsUserContextService>();
builder.Services.AddScoped<UserContext>();
builder.Services.AddSingleton(new ModuleIdentity(ModuleCodes.Notification));

builder.Services.AddControllers();
builder.Services.AddErpApiVersioning();
builder.Services.AddSharedLocalization(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Notification Service API", Version = "v1" });
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});
builder.Services.AddHealthChecks();

builder.Services.Configure<HostOptions>(opts =>
    opts.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseSharedLocalization();
app.UseAuthentication();
app.UseMiddleware<ERP.Shared.Authorization.UserContextHeaderMiddleware>();
app.UseMiddleware<LicenseEnforcementMiddleware>();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
