using Confluent.Kafka;
using ERP.NTF.Application.Handlers;
using ERP.NTF.Application.Interfaces;
using ERP.NTF.Application.Validators;
using ERP.NTF.Infrastructure.BackgroundJobs;
using ERP.NTF.Infrastructure.Persistence;
using ERP.NTF.Infrastructure.Repositories;
using ERP.NTF.Infrastructure.Services;
using ERP.Shared.Authorization;
using ERP.Shared.Infrastructure.Caching;
using ERP.Shared.Localization;
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

// Kafka producer config (for potential future Outbox)
builder.Services.AddSingleton(new ProducerConfig
{
    BootstrapServers = builder.Configuration["KafkaSettings:BootstrapServers"] ?? "localhost:9092",
    AllowAutoCreateTopics = true
});

// Background consumers
builder.Services.AddHostedService<NotificationDispatchJob>();
builder.Services.AddHostedService<IdentityEventConsumer>();          // identity-topic: UserCreated, TwoFactorCodeGenerated
builder.Services.AddHostedService<NotificationKafkaConsumer>();      // payment-topic, shipping-topic, mst-topic
builder.Services.AddHostedService<SalesOrderRabbitMqConsumer>();     // sales_order_events exchange: OrderCreated, OrderApproved

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserContextService, HeaderUserContextService>();
builder.Services.AddScoped<UserContext>();

builder.Services.AddControllers();
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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseSharedLocalization();
app.UseMiddleware<ERP.Shared.Authorization.UserContextHeaderMiddleware>();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
