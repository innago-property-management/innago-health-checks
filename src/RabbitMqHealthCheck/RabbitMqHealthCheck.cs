namespace Innago.Shared.HealthChecks.RabbitMq;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Innago.Shared.TryHelpers;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

using RabbitMQ.Client;

internal sealed class RabbitMqHealthCheck : IHealthCheck
{
    private readonly IServiceProvider serviceProvider;
    private readonly IConfiguration configuration;
    private readonly RabbitMqHealthCheckOptions options;
    private readonly ILogger<RabbitMqHealthCheck> logger;
    private readonly Func<ConnectionFactory, CancellationToken, Task<HealthCheckResult>> prober;

    internal RabbitMqHealthCheck(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        RabbitMqHealthCheckOptions options,
        ILogger<RabbitMqHealthCheck> logger,
        Func<ConnectionFactory, CancellationToken, Task<HealthCheckResult>>? prober = null)
    {
        this.serviceProvider = serviceProvider;
        this.configuration = configuration;
        this.options = options;
        this.logger = logger;
        this.prober = prober ?? this.DefaultProbeAsync;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // Strategy 1: DI IConnection
        if (!this.options.IgnoreDiConnection)
        {
            this.logger.StrategyDiConnection();
            HealthCheckResult? diResult = this.TryStrategy1DiConnection();

            if (diResult.HasValue)
            {
                return diResult.Value;
            }
        }

        // Strategy 2: Vault secrets file
        this.logger.StrategyVaultFile(this.options.VaultSecretsPath);
        Result<VaultSecrets?> vaultRead = VaultSecretsReader.TryRead(this.options.VaultSecretsPath, this.logger);

        if (vaultRead.HasFailed)
        {
            // File was present but unreadable — do NOT fall through to Strategy 3
            return HealthCheckResult.Unhealthy("Vault file could not be parsed. See application logs for details.");
        }

        var vaultSecrets = (VaultSecrets?)vaultRead;

        if (vaultSecrets is not null)
        {
            // File was present — do NOT fall through to Strategy 3 regardless of outcome
            return await this.GetVaultProbeResult(vaultSecrets.Value.Values, cancellationToken).ConfigureAwait(false);
        }

        // File was absent — Strategy 3 is permitted
        this.logger.StrategyIConfiguration();
        HealthCheckResult? configResult = await this.TryStrategy3ConfigurationAsync(cancellationToken).ConfigureAwait(false);

        if (configResult.HasValue)
        {
            return configResult.Value;
        }

        this.logger.NoStrategyFound();
        return HealthCheckResult.Unhealthy("No RabbitMQ connection or configuration found.");
    }

    private HealthCheckResult? TryStrategy1DiConnection()
    {
        var connection = this.serviceProvider.GetService<IConnection>();

        if (connection is null)
        {
            return null;
        }

        this.logger.DiConnectionFound(connection.GetType().Name);

        return connection.IsOpen
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("RabbitMQ connection is not open.");
    }

    private async Task<HealthCheckResult> GetVaultProbeResult(
        IReadOnlyDictionary<string, string> secrets,
        CancellationToken cancellationToken)
    {
        if (!secrets.TryGetValue("rabbit_host", out string? host) || string.IsNullOrWhiteSpace(host))
        {
            return HealthCheckResult.Unhealthy("Vault file present but rabbit_host is missing.");
        }

        if (!secrets.TryGetValue("rabbit_username", out string? username) || string.IsNullOrWhiteSpace(username))
        {
            return HealthCheckResult.Unhealthy("Vault file present but rabbit_username is missing.");
        }

        if (!secrets.TryGetValue("rabbit_password", out string? password) || string.IsNullOrWhiteSpace(password))
        {
            return HealthCheckResult.Unhealthy("Vault file present but rabbit_password is missing.");
        }

        secrets.TryGetValue("rabbit_virtualhost", out string? virtualHost);
        secrets.TryGetValue("rabbit_port", out string? portStr);

        ConnectionFactory factory = this.BuildFactory(host, username, password, virtualHost, portStr);
        return await this.SafeProbeAsync(factory, cancellationToken).ConfigureAwait(false);
    }

    private async Task<HealthCheckResult?> TryStrategy3ConfigurationAsync(CancellationToken cancellationToken)
    {
        string? host = FirstNonEmpty(this.configuration["RabbitMQ:Host"],
            this.configuration["MassTransit:RabbitMq:Host"],
            this.configuration["rabbit_host"]);

        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        string username = FirstNonEmpty(this.configuration["RabbitMQ:Username"],
            this.configuration["MassTransit:RabbitMq:Username"],
            this.configuration["rabbit_username"]) ?? "guest";

        string password = FirstNonEmpty(this.configuration["RabbitMQ:Password"],
            this.configuration["MassTransit:RabbitMq:Password"],
            this.configuration["rabbit_password"]) ?? "guest";

        string? virtualHost = FirstNonEmpty(this.configuration["RabbitMQ:VirtualHost"],
            this.configuration["MassTransit:RabbitMq:VirtualHost"],
            this.configuration["rabbit_virtualhost"]);

        ConnectionFactory factory = this.BuildFactory(host, username, password, virtualHost, portStr: null);
        return await this.SafeProbeAsync(factory, cancellationToken).ConfigureAwait(false);
    }

    private async Task<HealthCheckResult> SafeProbeAsync(
        ConnectionFactory factory,
        CancellationToken cancellationToken)
    {
        try
        {
            return await this.prober(factory, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            this.logger.ProbeConnectionFailed(ex);
            return HealthCheckResult.Unhealthy("Connection probe failed. See application logs for details.");
        }
    }

    private ConnectionFactory BuildFactory(
        string host,
        string username,
        string password,
        string? virtualHost,
        string? portStr)
    {
        var factory = new ConnectionFactory
        {
            HostName = host,
            UserName = username,
            Password = password,
            VirtualHost = virtualHost ?? "/",
            RequestedConnectionTimeout = this.options.ConnectionTimeout,
        };

        if (int.TryParse(portStr, out int port))
        {
            factory.Port = port;
        }

        return factory;
    }

    private async Task<HealthCheckResult> DefaultProbeAsync(
        ConnectionFactory factory,
        CancellationToken cancellationToken)
    {
        try
        {
            await using IConnection connection = await factory.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);

            // Connection.IsOpen after CreateConnectionAsync proves TCP, TLS, AMQP handshake, and auth.
            // No channel needed — see DESIGN.md §Design Notes.
            return connection.IsOpen
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("RabbitMQ connection is not open.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            this.logger.ProbeConnectionFailed(ex);
            return HealthCheckResult.Unhealthy("Connection probe failed. See application logs for details.");
        }
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}