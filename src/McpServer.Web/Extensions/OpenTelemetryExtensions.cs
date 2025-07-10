using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace McpServer.Web.Extensions;

/// <summary>
/// Extension methods for configuring OpenTelemetry.
/// </summary>
public static class OpenTelemetryExtensions
{
    /// <summary>
    /// Adds OpenTelemetry tracing and metrics.
    /// </summary>
    public static IServiceCollection AddOpenTelemetryObservability(this IServiceCollection services, IConfiguration configuration)
    {
        var serviceName = configuration["OpenTelemetry:ServiceName"] ?? "McpServer";
        var serviceVersion = configuration["OpenTelemetry:ServiceVersion"] ?? "1.0.0";
        var otlpEndpoint = configuration["OpenTelemetry:OtlpEndpoint"];
        var enableConsoleExporter = configuration.GetValue("OpenTelemetry:EnableConsoleExporter", false);
        var enableOtlpExporter = configuration.GetValue("OpenTelemetry:EnableOtlpExporter", !string.IsNullOrEmpty(otlpEndpoint));

        // Configure resource
        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(serviceName: serviceName, serviceVersion: serviceVersion)
            .AddTelemetrySdk()
            .AddAttributes(new Dictionary<string, object>
            {
                ["deployment.environment"] = configuration["ASPNETCORE_ENVIRONMENT"] ?? "Production",
                ["host.name"] = Environment.MachineName
            });

        // Add OpenTelemetry
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName, serviceVersion))
            .WithTracing(tracing =>
            {
                tracing
                    .SetResourceBuilder(resourceBuilder)
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.Filter = (httpContext) =>
                        {
                            // Filter out health checks and metrics endpoints
                            var path = httpContext.Request.Path.Value;
                            if (path == null) return true;
                            
                            return !path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) &&
                                   !path.StartsWith("/api/health", StringComparison.OrdinalIgnoreCase) &&
                                   !path.StartsWith("/api/metrics", StringComparison.OrdinalIgnoreCase);
                        };
                    })
                    .AddHttpClientInstrumentation()
                    .AddSource("McpServer")
                    .AddSource("McpServer.Application")
                    .AddSource("McpServer.Infrastructure");

                // Add console exporter for development
                if (enableConsoleExporter)
                {
                    tracing.AddConsoleExporter();
                }

                // Add OTLP exporter if configured
                if (enableOtlpExporter && !string.IsNullOrEmpty(otlpEndpoint))
                {
                    tracing.AddOtlpExporter(otlpOptions =>
                    {
                        otlpOptions.Endpoint = new Uri(otlpEndpoint);
                        otlpOptions.Protocol = OtlpExportProtocol.Grpc;
                        
                        // Configure headers if provided
                        var headers = configuration["OpenTelemetry:Headers"];
                        if (!string.IsNullOrEmpty(headers))
                        {
                            otlpOptions.Headers = headers;
                        }
                    });
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .SetResourceBuilder(resourceBuilder)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddProcessInstrumentation()
                    .AddMeter("McpServer")
                    .AddMeter("McpServer.Application")
                    .AddMeter("McpServer.Infrastructure");

                // Add console exporter for development
                if (enableConsoleExporter)
                {
                    metrics.AddConsoleExporter();
                }

                // Add OTLP exporter if configured
                if (enableOtlpExporter && !string.IsNullOrEmpty(otlpEndpoint))
                {
                    metrics.AddOtlpExporter(otlpOptions =>
                    {
                        otlpOptions.Endpoint = new Uri(otlpEndpoint);
                        otlpOptions.Protocol = OtlpExportProtocol.Grpc;
                        
                        // Configure headers if provided
                        var headers = configuration["OpenTelemetry:Headers"];
                        if (!string.IsNullOrEmpty(headers))
                        {
                            otlpOptions.Headers = headers;
                        }
                    });
                }
            });

        return services;
    }
}