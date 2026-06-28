using ERP.PRM.Application.Handlers;
using ERP.PRM.Application.Interfaces;
using ERP.PRM.Infrastructure.Persistence;
using ERP.PRM.Infrastructure.Repositories;
using ERP.Shared.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<PromotionDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("PromotionDb")
        ?? "Server=(local);Database=PromotionDB;Trusted_Connection=true;"));

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblyContaining<CreatePromotionCommandHandler>());

builder.Services.AddScoped<IPromotionRepository, PromotionRepository>();
builder.Services.AddScoped<ICouponRepository, CouponRepository>();
builder.Services.AddScoped<ILoyaltyAccountRepository, LoyaltyAccountRepository>();
builder.Services.AddScoped<IPromotionProductCacheRepository, PromotionProductCacheRepository>();

builder.Services.AddScoped<UserContext>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
    c.SwaggerDoc("v1", new() { Title = "Promotion Service API", Version = "v1" }));
builder.Services.AddCors(options =>
    options.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }

app.UseHttpsRedirection();
app.UseUserContextHeaderMiddleware();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();
app.Run();
