namespace Innago.Shared.HealthChecks.Npgsql;

using System;

using JetBrains.Annotations;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

/// <summary>Extension methods to register the Innago Npgsql health check.</summary>
[PublicAPI]
public static class HealthChecksBuilderExtensions
{
    /// <summary>Adds the Innago Npgsql health check with default options.</summary>
    /// <param name="builder">The health checks builder.</param>
    /// <returns>The <see cref="IHealthChecksBuilder"/> for chaining.</returns>
    public static IHealthChecksBuilder AddInnagoNpgsql(
        this IHealthChecksBuilder builder)
    {
        return Register(builder, new NpgsqlHealthCheckOptions());
    }

    /// <summary>Adds the Innago Npgsql health check with a configuration action.</summary>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="configure">Action to configure <see cref="NpgsqlHealthCheckOptions"/>.</param>
    /// <returns>The <see cref="IHealthChecksBuilder"/> for chaining.</returns>
    public static IHealthChecksBuilder AddInnagoNpgsql(
        this IHealthChecksBuilder builder,
        Action<NpgsqlHealthCheckOptions> configure)
    {
        var options = new NpgsqlHealthCheckOptions();
        configure(options);
        return Register(builder, options);
    }

    private static IHealthChecksBuilder Register(IHealthChecksBuilder builder, NpgsqlHealthCheckOptions options)
    {
        builder.Services.AddSingleton(new HealthCheckRegistration(
            options.Name,
            sp =>
            {
                var configuration = sp.GetRequiredService<IConfiguration>();
                var logger = sp.GetRequiredService<ILogger<NpgsqlHealthCheck>>();
                return new NpgsqlHealthCheck(configuration, options, logger);
            },
            failureStatus: null,
            tags: options.Tags));

        return builder;
    }
}
