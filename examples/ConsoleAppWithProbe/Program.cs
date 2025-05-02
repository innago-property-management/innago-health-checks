using Innago.Shared.HealthChecks.TcpHealthProbe;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Serilog;

using static ConsoleAppWithProbe.ProgramConfiguration;

IHostBuilder builder = Host.CreateDefaultBuilder(args);

builder.UseSerilog((context, provider, loggerConfig) =>
    loggerConfig.ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(provider));

builder.ConfigureServices((context, services) =>
{
    string serviceName = context.Configuration["serviceName"]!;
    string serviceVersion = context.Configuration["serviceVersion"] ?? "0.0.1";
    
    services.AddLogging();
    services.AddHostedService<TcpHealthProbeService>();
    services.AddHealthChecks();

    services.AddOpenTelemetry()
        .ConfigureResource(ConfigureResource(serviceName, serviceVersion))
        .WithTracing(ConfigureTracing(context.Configuration, serviceName))
        .WithMetrics(ConfigureMetrics(serviceName));
});

await builder.RunConsoleAsync();