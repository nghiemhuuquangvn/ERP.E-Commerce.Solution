using ERP.SHP.Application.Handlers;
using ERP.SHP.Application.Interfaces;
using ERP.SHP.Infrastructure.Carriers;
using ERP.SHP.Infrastructure.Persistence;
using ERP.SHP.Infrastructure.Repositories;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ShippingDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("ShippingDb")
        ?? "Server=(local);Database=ShippingDB;Trusted_Connection=true;"));

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblyContaining<CreateShipmentCommandHandler>());

builder.Services.AddScoped<IShipmentRepository, ShipmentRepository>();
builder.Services.AddScoped<ICarrierRepository, CarrierRepository>();

// Carrier client — GHN when Token configured, else Null stub
if (!string.IsNullOrEmpty(builder.Configuration["Ghn:Token"]))
    builder.Services.AddScoped<ICarrierClient, GhnCarrierClient>();
else
    builder.Services.AddScoped<ICarrierClient, NullCarrierClient>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserContextService, HeaderUserContextService>();
builder.Services.AddScoped<UserContext>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
    c.SwaggerDoc("v1", new() { Title = "Shipping Service API", Version = "v1" }));
builder.Services.AddCors(options =>
    options.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseMiddleware<ERP.Shared.Authorization.UserContextHeaderMiddleware>();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

app.Run();
