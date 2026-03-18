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
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly RabbitMqHealthCheckOptions _options;
    private readonly ILogger<RabbitMqHealthCheck> _logger;
    private readonly Func<ConnectionFactory, CancellationToken, Task<HealthCheckResult>> _prober;

    internal RabbitMqHealthCheck(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        RabbitMqHealthCheckOptions options,
        ILogger<RabbitMqHealthCheck> logger,
        Func<ConnectionFactory, CancellationToken, Task<HealthCheckResult>>? prober = null)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _options = options;
        _logger = logger;
        _prober = prober ?? DefaultProbeAsync;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // Strategy 1: DI IConnection
        if (!_options.IgnoreDiConnection)
        {
            _logger.StrategyDiConnection();
            var diResult = TryStrategy1DiConnection();
            if (diResult.HasValue)
            {
                return diResult.Value;
            }
        }

        // Strategy 2: Vault secrets file
        _logger.StrategyVaultFile(_options.VaultSecretsPath);
        var vaultRead = VaultSecretsReader.TryRead(_options.VaultSecretsPath, _logger);

        if (vaultRead.HasFailed)
        {
            // File was present but unreadable — do NOT fall through to Strategy 3
            return HealthCheckResult.Unhealthy("Vault file could not be parsed. See application logs for details.");
        }

        var vaultSecrets = (VaultSecrets?)vaultRead;
        if (vaultSecrets is not null)
        {
            // File was present — do NOT fall through to Strategy 3 regardless of outcome
            return await GetVaultProbeResult(vaultSecrets.Value.Values, cancellationToken).ConfigureAwait(false);
        }

        // File was absent — Strategy 3 is permitted
        _logger.StrategyIConfiguration();
        var configResult = await TryStrategy3ConfigurationAsync(cancellationToken).ConfigureAwait(false);
        if (configResult.HasValue)
        {
            return configResult.Value;
        }

        _logger.NoStrategyFound();
        return HealthCheckResult.Unhealthy("No RabbitMQ connection or configuration found.");
    }

    private HealthCheckResult? TryStrategy1DiConnection()
    {
        var connection = _serviceProvider.GetService<IConnection>();
        if (connection is null)
        {
            return null;
        }

        _logger.DiConnectionFound(connection.GetType().Name);
        return connection.IsOpen
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("RabbitMQ connection is not open.");
    }

    private async Task<HealthCheckResult> GetVaultProbeResult(
        Dictionary<string, string> secrets,
        CancellationToken cancellationToken)
    {
        if (!secrets.TryGetValue("rabbit_host", out var host) || string.IsNullOrWhiteSpace(host))
        {
            return HealthCheckResult.Unhealthy("Vault file present but rabbit_host is missing.");
        }

        if (!secrets.TryGetValue("rabbit_username", out var username) || string.IsNullOrWhiteSpace(username))
        {
            return HealthCheckResult.Unhealthy("Vault file present but rabbit_username is missing.");
        }

        if (!secrets.TryGetValue("rabbit_password", out var password) || string.IsNullOrWhiteSpace(password))
        {
            return HealthCheckResult.Unhealthy("Vault file present but rabbit_password is missing.");
        }

        secrets.TryGetValue("rabbit_virtualhost", out var virtualHost);
        secrets.TryGetValue("rabbit_port", out var portStr);

        var factory = BuildFactory(host, username, password, virtualHost, portStr);
        return await SafeProbeAsync(factory, cancellationToken).ConfigureAwait(false);
    }

    private async Task<HealthCheckResult?> TryStrategy3ConfigurationAsync(CancellationToken cancellationToken)
    {
        var host = FirstNonEmpty(
            _configuration["RabbitMQ:Host"],
            _configuration["MassTransit:RabbitMq:Host"],
            _configuration["rabbit_host"]);

        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        var username = FirstNonEmpty(
            _configuration["RabbitMQ:Username"],
            _configuration["MassTransit:RabbitMq:Username"],
            _configuration["rabbit_username"]) ?? "guest";

        var password = FirstNonEmpty(
            _configuration["RabbitMQ:Password"],
            _configuration["MassTransit:RabbitMq:Password"],
            _configuration["rabbit_password"]) ?? "guest";

        var virtualHost = FirstNonEmpty(
            _configuration["RabbitMQ:VirtualHost"],
            _configuration["MassTransit:RabbitMq:VirtualHost"],
            _configuration["rabbit_virtualhost"]);

        var factory = BuildFactory(host, username, password, virtualHost, portStr: null);
        return await SafeProbeAsync(factory, cancellationToken).ConfigureAwait(false);
    }

    private async Task<HealthCheckResult> SafeProbeAsync(
        ConnectionFactory factory,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _prober(factory, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.ProbeConnectionFailed(ex);
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
            RequestedConnectionTimeout = _options.ConnectionTimeout,
        };

        if (int.TryParse(portStr, out var port))
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
            await using var connection = await factory.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
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
            _logger.ProbeConnectionFailed(ex);
            return HealthCheckResult.Unhealthy("Connection probe failed. See application logs for details.");
        }
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
