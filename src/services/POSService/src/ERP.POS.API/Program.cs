using ERP.POS.Application.Handlers;
using ERP.POS.Application.Interfaces;
using ERP.POS.Infrastructure.Persistence;
using ERP.POS.Infrastructure.Repositories;
using ERP.Shared.Authorization;
using ERP.Shared.Localization;
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
builder.Services.AddScoped<IPosProductCacheRepository, PosProductCacheRepository>();
builder.Services.AddScoped<IPosPromotionCacheRepository, PosPromotionCacheRepository>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserContextService, HeaderUserContextService>();
builder.Services.AddScoped<UserContext>();
builder.Services.AddControllers();
builder.Services.AddSharedLocalization(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
    c.SwaggerDoc("v1", new() { Title = "POS Service API", Version = "v1" }));
builder.Services.AddCors(options =>
    options.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }

app.UseHttpsRedirection();
app.UseSharedLocalization();
app.UseMiddleware<ERP.Shared.Authorization.UserContextHeaderMiddleware>();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();
app.Run();
