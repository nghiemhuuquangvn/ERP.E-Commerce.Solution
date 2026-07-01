using ERP.PAY.Application.Handlers;
using ERP.PAY.Application.Interfaces;
using ERP.PAY.Application.Validators;
using ERP.PAY.Infrastructure.Gateway;
using ERP.PAY.Infrastructure.Persistence;
using ERP.PAY.Infrastructure.Repositories;
using ERP.Shared.Authorization;
using ERP.Shared.Localization;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

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

// Payment gateway — use VNPay when credentials are configured, fall back to Null stub
var vnpayConfigured = !string.IsNullOrEmpty(builder.Configuration["VnPay:TmnCode"]);
if (vnpayConfigured)
    builder.Services.AddScoped<IPaymentGateway, VnPayGateway>();
else
    builder.Services.AddScoped<IPaymentGateway, NullPaymentGateway>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserContextService, HeaderUserContextService>();
builder.Services.AddScoped<UserContext>();

builder.Services.AddControllers();
builder.Services.AddSharedLocalization(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
    c.SwaggerDoc("v1", new() { Title = "Payment Service API", Version = "v1" }));
builder.Services.AddCors(options =>
    options.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseSharedLocalization();
app.UseMiddleware<ERP.Shared.Authorization.UserContextHeaderMiddleware>();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

app.Run();
