using ERP.SO.Application.Handlers;
using ERP.SO.Application.Validators;
using ERP.SO.Infrastructure;
using ERP.SO.Infrastructure.Repositories;
using ERP.Shared.Authorization;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Database configuration
builder.Services.AddDbContext<SalesOrderDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SalesDb") ?? "Server=(local);Database=SalesDB;Trusted_Connection=true;"));

// MediatR + Validators
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining(typeof(CreateOrderCommandHandler)));
builder.Services.AddValidatorsFromAssemblyContaining(typeof(CreateOrderCommandValidator));

// Repository & Message Bus
builder.Services.AddScoped<ISalesOrderRepository, SalesOrderRepository>();
builder.Services.AddScoped<IMessageBus, RabbitMqMessageBus>();

// Authorization & User Context
builder.Services.AddScoped<UserContext>();
builder.Services.AddMiddleware();

// API Configuration
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseUserContextHeaderMiddleware();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

app.Run();
