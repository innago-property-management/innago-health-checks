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
    /// <param name="builder">The health checks builder.</param>
    extension(IHealthChecksBuilder builder)
    {
        /// <summary>Adds the Innago Npgsql health check with default options.</summary>
        /// <returns>The <see cref="IHealthChecksBuilder"/> for chaining.</returns>
        public IHealthChecksBuilder AddInnagoNpgsql()
        {
            return Register(builder, new NpgsqlHealthCheckOptions());
        }

        /// <summary>Adds the Innago Npgsql health check with a configuration action.</summary>
        /// <param name="configure">Action to configure <see cref="NpgsqlHealthCheckOptions"/>.</param>
        /// <returns>The <see cref="IHealthChecksBuilder"/> for chaining.</returns>
        public IHealthChecksBuilder AddInnagoNpgsql(Action<NpgsqlHealthCheckOptions> configure)
        {
            var options = new NpgsqlHealthCheckOptions();
            configure(options);
            return Register(builder, options);
        }
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
