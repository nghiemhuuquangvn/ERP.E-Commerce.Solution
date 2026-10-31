using Microsoft.AspNetCore.SignalR;
using Confluent.Kafka;
using ERP.NTF.API.Hubs;
using ERP.NTF.Application.Handlers;
using ERP.NTF.Application.Interfaces;
using ERP.NTF.Application.Validators;
using ERP.NTF.Infrastructure.BackgroundJobs;
using ERP.NTF.Infrastructure.MessageBus;
using ERP.NTF.Infrastructure.Persistence;
using ERP.NTF.Infrastructure.Repositories;
using ERP.NTF.Infrastructure.Services;
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
builder.Services.AddScoped<IRecipientRepository, RecipientRepository>();
builder.Services.AddScoped<ERP.NTF.Application.EventHandlers.OrderNotifier>();
builder.Services.AddScoped<ERP.NTF.Application.EventHandlers.UserNotifier>();

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
builder.Services.AddScoped<ICurrentTenantAccessor, CurrentTenantAccessor>();
// The webhook client: no redirects, a short timeout, and the SSRF rule checked against the ADDRESSES the host resolves to at connect time
// (a name that resolves to an internal address is refused; the static URL rule alone cannot see that).
builder.Services.AddHttpClient("webhook", c => c.Timeout = TimeSpan.FromSeconds(10))
    .ConfigurePrimaryHttpMessageHandler(() => new System.Net.Http.SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        ConnectCallback = async (context, ct) =>
        {
            var addresses = await System.Net.Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, ct);
            if (addresses.Length == 0 || addresses.Any(ERP.Shared.Security.WebhookUrlRules.IsBlockedAddress))
                throw new System.Net.Http.HttpRequestException($"The host '{context.DnsEndPoint.Host}' resolves to a blocked (internal / private) address.");

            var socket = new System.Net.Sockets.Socket(System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, ct);
                return new System.Net.Sockets.NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }
    });

// Bell notifications: real-time push over SignalR for Channel.InApp — mirrors
// ChatBotService's ChatHub/SignalRChatStreamNotifier split. AddMemoryCache +
// IHubTicketStore back the "/hubs" bearer-token-too-large workaround in
// AuthenticationExtensions.AddErpJwtAuthentication (see that file for why).
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IHubTicketStore, HubTicketStore>();
builder.Services.AddScoped<IRealtimeNotifier, SignalRRealtimeNotifier>();
// Re-check token revocation on every hub call (a connection outlives its token check).
builder.Services.AddSignalR(o => o.AddFilter<ERP.Shared.Authorization.TokenRevocation.RevocationHubFilter>());

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
builder.Services.AddErpAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserContextService, JwtClaimsUserContextService>();
// Tenant isolation for NotificationDbContext: JWT tenant, Gateway host tenant for anonymous
// requests, or an explicit TenantScope (consumers, dispatch job). Templates of the platform
// tenant are shared defaults every tenant can read and override.
builder.Services.AddRequestTenantProvider();
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

    // The SignalR hub needs AllowCredentials (browser WS/SSE transports require it),
    // which can't be combined with AllowAnyOrigin — so this is a second, separate
    // policy applied only to the hub via RequireCors, not a replacement for
    // "AllowAll" above (which stays as-is for the REST controllers). Same allowed-origin
    // shape as ChatBotService's "ChatBotCors": config-driven list plus any *.erp.local host.
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
        ?? new[] { "http://localhost:5173", "http://localhost:5174" };
    var allowedOriginSet = new HashSet<string>(allowedOrigins, StringComparer.OrdinalIgnoreCase);
    options.AddPolicy("NotificationHubCors", policy =>
        policy.SetIsOriginAllowed(origin =>
            {
                if (allowedOriginSet.Contains(origin))
                    return true;

                if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                    return false;

                return uri.Host.Equals("erp.local", StringComparison.OrdinalIgnoreCase)
                    || uri.Host.EndsWith(".erp.local", StringComparison.OrdinalIgnoreCase);
            })
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
});
builder.Services.AddHealthChecks();

builder.Services.Configure<HostOptions>(opts =>
    opts.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);

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
app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications").RequireCors("NotificationHubCors");
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();
