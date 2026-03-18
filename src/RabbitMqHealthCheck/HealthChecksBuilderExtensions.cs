namespace Innago.Shared.HealthChecks.RabbitMq;

using System;

using JetBrains.Annotations;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

/// <summary>Extension methods for registering the Innago RabbitMQ health check.</summary>
[PublicAPI]
public static class HealthChecksBuilderExtensions
{
    /// <summary>
    /// Adds the Innago RabbitMQ health check with default options.
    /// Tags default to <c>["ready"]</c>; use the overload with <see cref="Action{T}"/> for customisation.
    /// </summary>
    public static IHealthChecksBuilder AddInnagoRabbitMq(
        this IHealthChecksBuilder builder)
        => AddInnagoRabbitMqCore(builder, null);

    /// <summary>
    /// Adds the Innago RabbitMQ health check with full options configuration.
    /// </summary>
    public static IHealthChecksBuilder AddInnagoRabbitMq(
        this IHealthChecksBuilder builder,
        Action<RabbitMqHealthCheckOptions> configure)
        => AddInnagoRabbitMqCore(builder, configure);

    private static IHealthChecksBuilder AddInnagoRabbitMqCore(
        IHealthChecksBuilder builder,
        Action<RabbitMqHealthCheckOptions>? configure)
    {
        var options = new RabbitMqHealthCheckOptions();
        configure?.Invoke(options);

        // Use HealthCheckRegistration directly to access IServiceProvider in the factory.
        // This is the same mechanism all AddCheck overloads use internally.
        builder.Services.Configure<HealthCheckServiceOptions>(opts =>
            opts.Registrations.Add(new HealthCheckRegistration(
                options.Name,
                sp => new RabbitMqHealthCheck(
                    sp,
                    sp.GetRequiredService<IConfiguration>(),
                    options,
                    sp.GetRequiredService<ILogger<RabbitMqHealthCheck>>()),
                failureStatus: null,
                tags: options.Tags)));

        return builder;
    }
}
