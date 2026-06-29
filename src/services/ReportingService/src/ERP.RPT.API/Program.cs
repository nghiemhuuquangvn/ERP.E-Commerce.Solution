using ERP.RPT.Application.Handlers;
using ERP.RPT.Application.Interfaces;
using ERP.RPT.Infrastructure.EventConsumers;
using ERP.RPT.Infrastructure.Persistence;
using ERP.RPT.Infrastructure.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ReportingDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("ReportingDb")
        ?? "Server=(local);Database=ReportingDB;Trusted_Connection=true;"));

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblyContaining<GetSalesDashboardQueryHandler>());

builder.Services.AddScoped<IReportingRepository, ReportingRepository>();

// Register Kafka consumers — each runs as a BackgroundService
builder.Services.AddReportingKafkaConsumers();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
    options.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

builder.Services.AddHealthChecks()
    .AddDbContextCheck<ReportingDbContext>("reporting-db");

var app = builder.Build();

if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapControllers();
app.Run();
