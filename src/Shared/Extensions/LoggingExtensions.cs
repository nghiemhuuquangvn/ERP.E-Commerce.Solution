using ERP.Shared.Observability;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace ERP.Shared.Extensions;

public static class LoggingExtensions
{
    /// <summary>
    /// Configures Serilog with structured logging: Console + rolling file + shipping to the
    /// centralized "system-logs-topic" Kafka topic (consumed by LoggingService).
    /// Reads overrides from appsettings.json "Serilog" section; Kafka bootstrap servers from
    /// the "KafkaSettings:BootstrapServers" key already used by every service's message bus.
    /// </summary>
    public static WebApplicationBuilder AddSerilogLogging(this WebApplicationBuilder builder, string serviceName)
    {
        builder.Host.UseSerilog((context, services, config) =>
        {
            var bootstrapServers = context.Configuration.GetValue<string>("KafkaSettings:BootstrapServers")
                ?? "localhost:9092";

            config
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithThreadId()
                .Enrich.WithProperty("Service", serviceName)
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{Service}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
                    restrictedToMinimumLevel: LogEventLevel.Debug)
                .WriteTo.File(
                    path: $"logs/{serviceName}-.log",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 14,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{Service}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
                    restrictedToMinimumLevel: LogEventLevel.Information)
                .WriteTo.KafkaLog(bootstrapServers, serviceName, context.HostingEnvironment.EnvironmentName);

            // Override log levels per namespace if not already in appsettings
            if (!context.Configuration.GetSection("Serilog:MinimumLevel:Override").Exists())
            {
                config
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
                    .MinimumLevel.Override("System", LogEventLevel.Warning);
            }
        });

        return builder;
    }
}
