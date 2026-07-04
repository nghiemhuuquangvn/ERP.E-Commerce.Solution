using ERP.NTF.Application.Handlers;
using ERP.NTF.Application.Interfaces;
using ERP.NTF.Application.Validators;
using ERP.NTF.Infrastructure.BackgroundJobs;
using ERP.NTF.Infrastructure.Persistence;
using ERP.NTF.Infrastructure.Repositories;
using ERP.NTF.Infrastructure.Services;
using ERP.Shared.Authorization;
using ERP.Shared.Localization;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;


// External channel senders registered below — Twilio for SMS, FCM for push

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

// TwilioSmsSender/FcmPushSender inject IHttpClientFactory — without this registration,
// DI resolution throws InvalidOperationException the moment Twilio:AccountSid/Fcm:ServerKey
// are configured (masked in dev today since both fall back to Null* stubs when unset).
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

builder.Services.AddHostedService<NotificationDispatchJob>();
builder.Services.AddHostedService<ERP.NTF.Infrastructure.BackgroundJobs.IdentityEventConsumer>();

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

app.Run();
