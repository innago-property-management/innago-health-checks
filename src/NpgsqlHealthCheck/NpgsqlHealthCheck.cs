namespace Innago.Shared.HealthChecks.Npgsql;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

using global::Npgsql;

using Innago.Shared.TryHelpers;

internal sealed class NpgsqlHealthCheck : IHealthCheck
{
    private readonly IConfiguration configuration;
    private readonly NpgsqlHealthCheckOptions options;
    private readonly ILogger<NpgsqlHealthCheck> logger;
    private readonly Func<string, string, CancellationToken, Task<HealthCheckResult>> prober;

    internal NpgsqlHealthCheck(
        IConfiguration configuration,
        NpgsqlHealthCheckOptions options,
        ILogger<NpgsqlHealthCheck> logger,
        Func<string, string, CancellationToken, Task<HealthCheckResult>>? prober = null)
    {
        this.configuration = configuration;
        this.options = options;
        this.logger = logger;
        this.prober = prober ?? DefaultProbeAsync;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext? context,
        CancellationToken cancellationToken = default)
    {
        // Strategy 1: Connection string from IConfiguration
        string? connectionString = this.TryStrategy1ConnectionString();

        if (connectionString is not null)
        {
            return await this.ProbeWithCatch(connectionString, cancellationToken);
        }

        // Strategy 2: Vault secrets file
        this.logger.StrategyVaultFile(this.options.VaultSecretsPath);
        Result<VaultSecrets?> vaultResult = VaultSecretsReader.TryRead(this.options.VaultSecretsPath, this.logger);

        if (vaultResult.HasFailed)
        {
            return HealthCheckResult.Unhealthy(
                "Npgsql health check: vault secrets file is present but could not be read or parsed.");
        }

        // HasSucceeded: extract the nullable VaultSecrets
        var vaultFilePresent = false;
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
            return await this.TryStrategy2Vault(secrets!.Value, cancellationToken);
        }

        // Strategy 3: Individual IConfiguration keys (only if vault file absent)
        return await this.TryStrategy3Configuration(cancellationToken);
    }

    private string? TryStrategy1ConnectionString()
    {
        this.logger.StrategyConnectionString();

        if (this.options.ConnectionStringName is not null)
        {
            return this.configuration.GetConnectionString(this.options.ConnectionStringName);
        }

        return this.configuration.GetConnectionString("DefaultConnection")
               ?? this.configuration.GetConnectionString("Database");
    }

    private async Task<HealthCheckResult> TryStrategy2Vault(VaultSecrets secrets, CancellationToken cancellationToken)
    {
        Dictionary<string, string> dict = secrets.Values;

        if (!dict.TryGetValue("db_host", out string? host) || string.IsNullOrWhiteSpace(host))
        {
            return HealthCheckResult.Unhealthy(
                "Npgsql health check: vault secrets file is missing required key 'db_host'.");
        }

        if (!dict.TryGetValue("db_username", out string? username) || string.IsNullOrWhiteSpace(username))
        {
            return HealthCheckResult.Unhealthy(
                "Npgsql health check: vault secrets file is missing required key 'db_username'.");
        }

        if (!dict.TryGetValue("db_password", out string? password) || string.IsNullOrWhiteSpace(password))
        {
            return HealthCheckResult.Unhealthy(
                "Npgsql health check: vault secrets file is missing required key 'db_password'.");
        }

        if (!dict.TryGetValue("db_name", out string? dbName) || string.IsNullOrWhiteSpace(dbName))
        {
            return HealthCheckResult.Unhealthy(
                "Npgsql health check: vault secrets file is missing required key 'db_name'.");
        }

        dict.TryGetValue("db_port", out string? port);

        string cs = this.BuildConnectionString(host, username, password, dbName, port);
        return await this.ProbeWithCatch(cs, cancellationToken);
    }

    private async Task<HealthCheckResult> TryStrategy3Configuration(CancellationToken cancellationToken)
    {
        this.logger.StrategyIConfiguration();

        string? host = this.configuration["db_host"] ?? this.configuration["DB_HOST"];

        if (string.IsNullOrWhiteSpace(host))
        {
            this.logger.NoStrategyFound();

            return HealthCheckResult.Unhealthy(
                "Npgsql health check: no connection string, vault secrets, or configuration keys found.");
        }

        string? username = this.configuration["db_username"] ?? this.configuration["DB_USERNAME"];
        string? password = this.configuration["db_password"] ?? this.configuration["DB_PASSWORD"];
        string? dbName = this.configuration["db_name"] ?? this.configuration["DB_NAME"];
        string? port = this.configuration["db_port"] ?? this.configuration["DB_PORT"];

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(dbName))
        {
            return HealthCheckResult.Unhealthy(
                "Npgsql health check: host found in configuration but required credentials or database name are missing.");
        }

        string cs = this.BuildConnectionString(host, username, password, dbName, port);
        return await this.ProbeWithCatch(cs, cancellationToken);
    }

    private string BuildConnectionString(string host, string username, string password, string dbName, string? portStr)
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Username = username,
            Password = password,
            Database = dbName,
            Port = int.TryParse(portStr, out int p) ? p : 5432,
            Timeout = (int)this.options.ConnectionTimeout.TotalSeconds,
        };

        return builder.ConnectionString;
    }

    private async Task<HealthCheckResult> ProbeWithCatch(string connectionString, CancellationToken cancellationToken)
    {
        try
        {
            return await this.prober(connectionString, this.options.CommandText, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            this.logger.ProbeConnectionFailed(ex);

            return HealthCheckResult.Unhealthy(
                $"Npgsql health check: connection probe failed — {ex.GetType().Name}.");
        }
    }

    private static async Task<HealthCheckResult> DefaultProbeAsync(string connectionString, string commandText, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteScalarAsync(cancellationToken);
        return HealthCheckResult.Healthy("Npgsql health check: connection probe succeeded.");
    }
}
