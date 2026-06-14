using ERP.PRD.Application;
using ERP.PRD.Infrastructure;
using ERP.Shared.Core.Domain;
using ERP.Shared.Messaging;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

// Add Swagger/OpenAPI
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Product Service API",
        Version = "v1",
        Description = "Product catalog and pricing management microservice",
        Contact = new OpenApiContact
        {
            Name = "ERP E-Commerce Solution Team",
            Email = "support@erp-ecommerce.local"
        }
    });
});

builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("*")
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddHealthChecks();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/openapi/v1.json", "Product Service API v1");
    });
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
