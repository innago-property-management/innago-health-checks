namespace Innago.Shared.HealthChecks.Npgsql;

using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

using global::Npgsql;

internal sealed class NpgsqlHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;
    private readonly NpgsqlHealthCheckOptions _options;
    private readonly ILogger<NpgsqlHealthCheck> _logger;
    private readonly Func<string, CancellationToken, Task<HealthCheckResult>> _prober;

    internal NpgsqlHealthCheck(
        IConfiguration configuration,
        NpgsqlHealthCheckOptions options,
        ILogger<NpgsqlHealthCheck> logger,
        Func<string, CancellationToken, Task<HealthCheckResult>>? prober = null)
    {
        _configuration = configuration;
        _options = options;
        _logger = logger;
        _prober = prober ?? DefaultProbeAsync;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext? context,
        CancellationToken cancellationToken = default)
    {
        // Strategy 1: Connection string from IConfiguration
        string? connectionString = TryStrategy1ConnectionString();
        if (connectionString is not null)
        {
            return await ProbeWithCatch(connectionString, cancellationToken);
        }

        // Strategy 2: Vault secrets file
        _logger.StrategyVaultFile(_options.VaultSecretsPath);
        var vaultResult = VaultSecretsReader.TryRead(_options.VaultSecretsPath, _logger);

        if (vaultResult.HasFailed)
        {
            return HealthCheckResult.Unhealthy(
                "Npgsql health check: vault secrets file is present but could not be read or parsed.");
        }

        // HasSucceeded: extract the nullable VaultSecrets
        bool vaultFilePresent = false;
        VaultSecrets? secrets = null;
        vaultResult.IfSucceeded(s =>
        {
            secrets = s;
            if (s is not null)
            {
                vaultFilePresent = true;
            }
        });

        if (vaultFilePresent)
        {
            return await TryStrategy2Vault(secrets!.Value, cancellationToken);
        }

        // Strategy 3: Individual IConfiguration keys (only if vault file absent)
        return await TryStrategy3Configuration(cancellationToken);
    }

    private string? TryStrategy1ConnectionString()
    {
        _logger.StrategyConnectionString();
        if (_options.ConnectionStringName is not null)
        {
            return _configuration.GetConnectionString(_options.ConnectionStringName);
        }

        return _configuration.GetConnectionString("DefaultConnection")
            ?? _configuration.GetConnectionString("Database");
    }

    private async Task<HealthCheckResult> TryStrategy2Vault(VaultSecrets secrets, CancellationToken cancellationToken)
    {
        var dict = secrets.Values;

        if (!dict.TryGetValue("db_host", out var host) || string.IsNullOrWhiteSpace(host))
        {
            return HealthCheckResult.Unhealthy(
                "Npgsql health check: vault secrets file is missing required key 'db_host'.");
        }

        if (!dict.TryGetValue("db_username", out var username) || string.IsNullOrWhiteSpace(username))
        {
            return HealthCheckResult.Unhealthy(
                "Npgsql health check: vault secrets file is missing required key 'db_username'.");
        }

        if (!dict.TryGetValue("db_password", out var password) || string.IsNullOrWhiteSpace(password))
        {
            return HealthCheckResult.Unhealthy(
                "Npgsql health check: vault secrets file is missing required key 'db_password'.");
        }

        if (!dict.TryGetValue("db_name", out var dbName) || string.IsNullOrWhiteSpace(dbName))
        {
            return HealthCheckResult.Unhealthy(
                "Npgsql health check: vault secrets file is missing required key 'db_name'.");
        }

        dict.TryGetValue("db_port", out var port);

        string cs = BuildConnectionString(host, username, password, dbName, port);
        return await ProbeWithCatch(cs, cancellationToken);
    }

    private async Task<HealthCheckResult> TryStrategy3Configuration(CancellationToken cancellationToken)
    {
        _logger.StrategyIConfiguration();

        string? host = _configuration["db_host"] ?? _configuration["DB_HOST"];
        if (string.IsNullOrWhiteSpace(host))
        {
            _logger.NoStrategyFound();
            return HealthCheckResult.Unhealthy(
                "Npgsql health check: no connection string, vault secrets, or configuration keys found.");
        }

        string? username = _configuration["db_username"] ?? _configuration["DB_USERNAME"];
        string? password = _configuration["db_password"] ?? _configuration["DB_PASSWORD"];
        string? dbName = _configuration["db_name"] ?? _configuration["DB_NAME"];
        string? port = _configuration["db_port"] ?? _configuration["DB_PORT"];

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(dbName))
        {
            return HealthCheckResult.Unhealthy(
                "Npgsql health check: host found in configuration but required credentials or database name are missing.");
        }

        string cs = BuildConnectionString(host, username, password, dbName, port);
        return await ProbeWithCatch(cs, cancellationToken);
    }

    private string BuildConnectionString(string host, string username, string password, string dbName, string? portStr)
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Username = username,
            Password = password,
            Database = dbName,
            Port = int.TryParse(portStr, out var p) ? p : 5432,
            Timeout = (int)_options.ConnectionTimeout.TotalSeconds,
        };
        return builder.ConnectionString;
    }

    private async Task<HealthCheckResult> ProbeWithCatch(string connectionString, CancellationToken cancellationToken)
    {
        try
        {
            return await _prober(connectionString, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.ProbeConnectionFailed(ex);
            return HealthCheckResult.Unhealthy(
                $"Npgsql health check: connection probe failed — {ex.GetType().Name}.");
        }
    }

    private static async Task<HealthCheckResult> DefaultProbeAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1";
        await command.ExecuteScalarAsync(cancellationToken);
        return HealthCheckResult.Healthy("Npgsql health check: connection probe succeeded.");
    }
}
