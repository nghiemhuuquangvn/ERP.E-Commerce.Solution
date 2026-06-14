# Service Scaffolding Template

This file provides the exact template for creating .csproj, Program.cs, and infrastructure files for the remaining 6 services:
- CRMService, PaymentService, ShippingService, POSService, PromotionService, NotificationService, ReportingService

## .csproj Files (Copy & Replace {CODE})

### API .csproj Template
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.0.0" />
    <PackageReference Include="Swashbuckle.AspNetCore" Version="6.4.6" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\..\..\Shared\Core\ERP.Shared.Core.csproj" />
    <ProjectReference Include="..\..\..\..\Shared\Messaging\ERP.Shared.Messaging.csproj" />
    <ProjectReference Include="..\..\..\..\Shared\Contracts\ERP.Shared.Contracts.csproj" />
    <ProjectReference Include="..\ERP.{CODE}.Application\ERP.{CODE}.Application.csproj" />
    <ProjectReference Include="..\ERP.{CODE}.Infrastructure\ERP.{CODE}.Infrastructure.csproj" />
  </ItemGroup>
</Project>
```

### Application .csproj Template
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MediatR" Version="12.2.0" />
    <PackageReference Include="FluentValidation" Version="11.9.1" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\..\..\Shared\Core\ERP.Shared.Core.csproj" />
    <ProjectReference Include="..\..\..\..\Shared\Messaging\ERP.Shared.Messaging.csproj" />
    <ProjectReference Include="..\..\..\..\Shared\Contracts\ERP.Shared.Contracts.csproj" />
    <ProjectReference Include="..\ERP.{CODE}.Domain\ERP.{CODE}.Domain.csproj" />
  </ItemGroup>
</Project>
```

### Domain .csproj Template
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\..\..\Shared\Core\ERP.Shared.Core.csproj" />
  </ItemGroup>
</Project>
```

### Infrastructure .csproj Template
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.0" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.0" />
    <PackageReference Include="Confluent.Kafka" Version="2.3.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\..\..\Shared\Core\ERP.Shared.Core.csproj" />
    <ProjectReference Include="..\..\..\..\Shared\Messaging\ERP.Shared.Messaging.csproj" />
    <ProjectReference Include="..\..\..\..\Shared\Contracts\ERP.Shared.Contracts.csproj" />
    <ProjectReference Include="..\ERP.{CODE}.Domain\ERP.{CODE}.Domain.csproj" />
  </ItemGroup>
</Project>
```

## appsettings.json Template
Replace {ServiceName} and {CODE}:
```json
{
  "ConnectionStrings": {
    "{ServiceName}DB": "Server=localhost;Database={ServiceName}DB;User Id=sa;Password=YourPassword123;TrustServerCertificate=True;Encrypt=False"
  },
  "KafkaSettings": {
    "BootstrapServers": "localhost:9092"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

## Service Codes Reference
| Service | Code | Port | Topic |
|---------|------|------|-------|
| CRMService | CRM | 5006 | crm-topic |
| PaymentService | PAY | 5009 | payment-topic |
| ShippingService | SHP | 5010 | shipping-topic |
| POSService | POS | 5011 | pos-topic |
| PromotionService | PRM | 5012 | promotion-topic |
| NotificationService | NTF | 5013 | notification-topic |
| ReportingService | RPT | 5014 | reporting-topic |

## Kafka Topic Names
Each service publishes to: `{service-name}-topic`
- crm-topic
- payment-topic
- shipping-topic
- pos-topic
- promotion-topic
- notification-topic
- reporting-topic (consumer only, no publishing)

## Program.cs Template
Same for all services - only Swagger title changes:
```csharp
using ERP.{CODE}.Application;
using ERP.{CODE}.Infrastructure;

var builder = WebApplicationBuilder.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddHealthChecks();

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.MapHealthChecks("/health");

var summaries = new[] { "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching" };

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast(
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast")
.WithOpenApi();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
```

## DependencyInjection.cs (Application Layer)
**Same for all services - no changes needed:**
```csharp
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using FluentValidation;
using System.Reflection;

namespace ERP.{CODE}.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddMediatR(config =>
                config.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

            services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

            return services;
        }
    }
}
```

## DependencyInjection.cs (Infrastructure Layer)
Replace {Code} and {ServiceName}:
```csharp
using Confluent.Kafka;
using ERP.{CODE}.Infrastructure.MessageBus;
using ERP.{CODE}.Infrastructure.Persistence;
using ERP.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.{CODE}.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("{ServiceName}DB");
            services.AddDbContext<{ServiceName}DbContext>(options =>
                options.UseSqlServer(connectionString));

            var kafkaServers = configuration["KafkaSettings:BootstrapServers"] ?? "localhost:9092";

            var producerConfig = new ProducerConfig
            {
                BootstrapServers = kafkaServers,
                AllowAutoCreateTopics = true
            };

            services.AddSingleton(new ProducerBuilder<string, string>(producerConfig).Build());
            services.AddScoped<IMessageBus, KafkaMessageBus>();

            services.AddHostedService<OutboxProcessor>();

            return services;
        }
    }
}
```

## DbContext.cs Template
Replace {Code} and {ServiceName}:
```csharp
using ERP.{CODE}.Domain.Entities;
using ERP.Shared.Core.Domain;
using ERP.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Reflection;

namespace ERP.{CODE}.Infrastructure.Persistence
{
    public class {ServiceName}DbContext : DbContext
    {
        // Add DbSet properties for your entities
        public DbSet<OutboxMessage> OutboxMessages { get; set; }

        public {ServiceName}DbContext(DbContextOptions<{ServiceName}DbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

            modelBuilder.Entity<OutboxMessage>()
                .HasIndex(m => m.ProcessedOnUtc)
                .HasFilter("[ProcessedOnUtc] IS NULL");
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var aggregateRoots = ChangeTracker.Entries<BaseEntity>()
                .Where(e => e.Entity.DomainEvents.Count != 0)
                .Select(e => e.Entity)
                .ToList();

            var domainEvents = aggregateRoots
                .SelectMany(aggregate => aggregate.DomainEvents)
                .ToList();

            foreach (var domainEvent in domainEvents)
            {
                var outboxMessage = new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    Type = domainEvent.GetType().FullName ?? domainEvent.GetType().Name,
                    Content = JsonConvert.SerializeObject(domainEvent, new JsonSerializerSettings
                    {
                        TypeNameHandling = TypeNameHandling.All
                    }),
                    OccurredOnUtc = domainEvent.OccurredOn,
                    ProcessedOnUtc = null
                };

                await OutboxMessages.AddAsync(outboxMessage, cancellationToken);
            }

            var result = await base.SaveChangesAsync(cancellationToken);

            foreach (var aggregateRoot in aggregateRoots)
                aggregateRoot.ClearDomainEvents();

            return result;
        }
    }
}
```

## OutboxProcessor.cs
**Copy exactly from AccountingService or ProductService - no changes needed**

## KafkaMessageBus.cs
Replace {code} and {service-name}:
```csharp
// Same as AccountingService version
// Just change "accounting-topic" to "{service-name}-topic"
```

---

## Quick Checklist Per Service

For each of the 6 remaining services:

1. **Create folder structure:**
   ```
   src/services/{ServiceName}/
   └── src/
       ├── ERP.{CODE}.API/
       │   ├── appsettings*.json
       │   ├── Program.cs
       │   └── ERP.{CODE}.API.csproj
       ├── ERP.{CODE}.Application/
       │   ├── DependencyInjection.cs
       │   └── ERP.{CODE}.Application.csproj
       ├── ERP.{CODE}.Domain/
       │   ├── Entities/
       │   └── ERP.{CODE}.Domain.csproj
       └── ERP.{CODE}.Infrastructure/
           ├── DependencyInjection.cs
           ├── Persistence/{ServiceName}DbContext.cs
           ├── BackgroundJobs/OutboxProcessor.cs
           ├── MessageBus/KafkaMessageBus.cs
           └── ERP.{CODE}.Infrastructure.csproj
   ```

2. **Copy files using template above**, replacing {CODE}, {ServiceName}, {service-name}

3. **Add domain entities** specific to service (from README.md in service folder)

4. **Test compilation:** `dotnet build src/services/{ServiceName}/{ServiceName}.sln`

All infrastructure files (Outbox, Kafka, DependencyInjection) are identical across services - just update namespaces and service names.
