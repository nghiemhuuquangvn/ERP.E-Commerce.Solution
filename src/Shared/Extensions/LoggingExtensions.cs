using ERP.Shared.Observability;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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
    /// <param name="extraOverrides">
    /// Additional per-source-context minimum-level overrides, applied unconditionally (unlike
    /// the built-in Microsoft/System overrides below, which only apply when appsettings.json
    /// doesn't already configure "Serilog:MinimumLevel:Override"). Use this for a source that
    /// floods the centralized log store with low-value noise for one specific service — e.g.
    /// Gateway passes ("Ocelot", Warning) since Ocelot's per-request internal middleware chain
    /// logs several Information-level lines per request that aren't useful once shipped to
    /// LoggingService (they duplicate the single "HTTP ... responded ..." request-log line).
    /// </param>
    /// <summary>
    /// One request-log line per request ("HTTP GET /x responded 400 in 12 ms"). Register it immediately <b>before</b>
    /// <c>UseGlobalExceptionHandling()</c> so the line carries the status the handler wrote (R100). Levels: a 5xx or an
    /// unhandled exception is Error, the container health probe is Verbose (polled every few seconds, no value in the
    /// central log store), everything else Information (R101).
    /// </summary>
    public static WebApplication UseErpRequestLogging(this WebApplication app)
    {
        app.UseSerilogRequestLogging(options => options.GetLevel = RequestLogLevel);
        return app;
    }

    /// <summary>Public so the policy can be tested; see <see cref="UseErpRequestLogging"/>.</summary>
    public static LogEventLevel RequestLogLevel(HttpContext context, double elapsedMs, Exception? ex)
    {
        if (ex != null || context.Response.StatusCode >= 500) return LogEventLevel.Error;
        if (context.Request.Path.StartsWithSegments("/health")) return LogEventLevel.Verbose;
        return LogEventLevel.Information;
    }

    public static WebApplicationBuilder AddSerilogLogging(
        this WebApplicationBuilder builder,
        string serviceName,
        params (string SourceContext, LogEventLevel MinimumLevel)[] extraOverrides)
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

            foreach (var (sourceContext, minimumLevel) in extraOverrides)
            {
                config.MinimumLevel.Override(sourceContext, minimumLevel);
            }
        });

        return builder;
    }
}
