using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Compact;

namespace ComTest.Api.Hosting;

public static class ObservabilityExtensions
{
    private const string ServiceName = "comtest-api";
    private const string NpgsqlMeterName = "Npgsql";

    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        AddLogging(builder);
        AddTelemetry(builder);

        return builder;
    }

    private static void AddLogging(WebApplicationBuilder builder)
    {
        var isDevelopment = builder.Environment.IsDevelopment();
        var seqUrl = builder.Configuration["Seq:Url"];

        builder.Services.AddSerilog((_, logger) =>
        {
            logger
                .ReadFrom.Configuration(builder.Configuration)
                .Enrich.FromLogContext()
                .Enrich.With<SensitiveDataEnricher>()
                .Enrich.WithProperty("Application", ServiceName);

            if (isDevelopment)
            {
                logger.WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture);
            }
            else
            {
                logger.WriteTo.Console(new RenderedCompactJsonFormatter());
            }

            if (!string.IsNullOrEmpty(seqUrl))
            {
                logger.WriteTo.Seq(seqUrl, formatProvider: System.Globalization.CultureInfo.InvariantCulture);
            }
        });
    }

    private static void AddTelemetry(WebApplicationBuilder builder)
    {
        var telemetry = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(ServiceName))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options =>
                    options.Filter = context => !context.Request.Path.StartsWithSegments("/health", StringComparison.Ordinal))
                .AddHttpClientInstrumentation()
                .AddNpgsql())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(NpgsqlMeterName));

        // Export only when a collector is configured, through OTEL_EXPORTER_OTLP_ENDPOINT.
        if (!string.IsNullOrEmpty(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            telemetry.UseOtlpExporter();
        }
    }
}
