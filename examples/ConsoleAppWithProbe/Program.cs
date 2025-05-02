using Innago.Shared.HealthChecks.TcpHealthProbe;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Serilog;

IHostBuilder builder = Host.CreateDefaultBuilder(args);

builder.UseSerilog((context, provider, loggerConfig) =>
    loggerConfig.ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(provider));

builder.ConfigureServices((_, services) =>
{
    services.AddLogging();
    services.AddHostedService<TcpHealthProbeService>();
    services.AddHealthChecks();
});

await builder.RunConsoleAsync();
