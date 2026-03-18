**Version:** 1.1 (Post-Delphi Round 1)
**Date:** 2026-03-18
**Changes:** innago-tryhelpers added to both csproj; VaultSecretsReader uses Result<T>; sealed class + Func delegate test seam; HealthCheckRegistration for DI; no CreateChannelAsync; credentials required (no guest/guest in vault path); exception log-not-attach; remove EnableConfigurationBindingGenerator; IgnoreDiConnection + ConnectionStringName options added.

# Implementation Plan: Dynamic Health Checks — RabbitMQ + Npgsql

## Prerequisites

- SPEC.md v1.1 and DESIGN.md v1.1 reviewed and approved (Delphi Round 1 complete).
- Working on a feature branch (not main).
- `dotnet` SDK 9.x available.
- All steps below are independently executable by a coding agent.
- All file paths are relative to the repo root (`/Volumes/Repos/innago-tcp-health-probe/`).
- **TDD discipline:** for each class in Steps 5–6, write the test body for a method BEFORE implementing the production method. The test will fail to compile or fail at runtime until the production code exists. This is the correct TDD sequence.

---

## Step 1: Scaffold the RabbitMQ project

### 1a. Create directory and empty files

```
src/RabbitMqHealthCheck/
  Innago.Shared.HealthChecks.RabbitMq.csproj
  RabbitMqHealthCheckOptions.cs
  RabbitMqHealthCheck.cs
  HealthChecksBuilderExtensions.cs
  VaultSecretsReader.cs
  LogMessages.cs
  BannedSymbols.txt
  PublicAPI.Shipped.txt
  PublicAPI.Unshipped.txt
  README.md
```

### 1b. Write `Innago.Shared.HealthChecks.RabbitMq.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFrameworks>net9.0;net10.0</TargetFrameworks>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <AssemblyName>Innago.Shared.HealthChecks.RabbitMq</AssemblyName>
    <RootNamespace>Innago.Shared.HealthChecks.RabbitMq</RootNamespace>
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
    <PublishAot>true</PublishAot>
    <Title>Innago RabbitMQ Health Check</Title>
    <Description>Tri-strategy RabbitMQ health check: DI IConnection, Vault secrets file, IConfiguration fallback.</Description>
    <Copyright>2025 Innago</Copyright>
    <PackageProjectUrl>https://github.com/innago-property-management/innago-health-checks/src/RabbitMqHealthCheck</PackageProjectUrl>
    <RepositoryUrl>https://github.com/innago-property-management/innago-health-checks</RepositoryUrl>
    <GeneratePackageOnBuild>true</GeneratePackageOnBuild>
    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <!-- No EnableConfigurationBindingGenerator: no IConfiguration.Get<T>() calls here.
         Vault JSON AOT safety handled by VaultSecretsJsonContext (STJ source generation). -->
  </PropertyGroup>

  <PropertyGroup Condition=" '$(Configuration)' == 'Debug' ">
    <DocumentationFile>bin\Debug\$(TargetFramework)\Innago.Shared.HealthChecks.RabbitMq.xml</DocumentationFile>
  </PropertyGroup>

  <PropertyGroup Condition=" '$(Configuration)' == 'Release' ">
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Innago.Shared.TryHelpers" Version="*" />
    <PackageReference Include="JetBrains.Annotations" Version="2024.3.0" />
    <PackageReference Include="Microsoft.CodeAnalysis.BannedApiAnalyzers" Version="3.3.4">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.CodeAnalysis.PublicApiAnalyzers" Version="3.3.4">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.Extensions.Configuration" Version="9.0.14" />
    <PackageReference Include="Microsoft.Extensions.Diagnostics.HealthChecks" Version="9.0.14" />
    <PackageReference Include="RabbitMQ.Client" Version="7.2.1" />
    <PackageReference Include="SonarAnalyzer.CSharp" Version="10.21.0.135717">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <AdditionalFiles Include="PublicAPI.Unshipped.txt" />
    <None Remove="PublicAPI.Shipped.txt" />
    <AdditionalFiles Include="PublicAPI.Shipped.txt" />
    <None Include="README.md" Pack="true" PackagePath="\" />
  </ItemGroup>

</Project>
```

### 1c. Write `BannedSymbols.txt`

```
T:Microsoft.Extensions.Logging.LoggerExtensions; Use LoggerMessage
```

### 1d. Write `PublicAPI.Shipped.txt`

```
#nullable enable
```

### 1e. Write `PublicAPI.Unshipped.txt`

Paste the RabbitMQ unshipped block verbatim from DESIGN.md §PublicAPI.txt Files.

### 1f. Add to solution

```bash
dotnet sln TcpHealthProbe.slnx add src/RabbitMqHealthCheck/Innago.Shared.HealthChecks.RabbitMq.csproj
```

### 1g. Verify scaffold builds

```bash
dotnet build src/RabbitMqHealthCheck/Innago.Shared.HealthChecks.RabbitMq.csproj
```

---

## Step 2: Scaffold the Npgsql project

Mirror Step 1 for `src/NpgsqlHealthCheck/`:
- Assembly/namespace: `Innago.Shared.HealthChecks.Npgsql`
- Replace `RabbitMQ.Client 7.2.1` with `Npgsql 9.0.5`
- `PublicAPI.Unshipped.txt`: Npgsql block from DESIGN.md
- Add to solution:

```bash
dotnet sln TcpHealthProbe.slnx add src/NpgsqlHealthCheck/Innago.Shared.HealthChecks.Npgsql.csproj
```

---

## Step 3: Scaffold the RabbitMQ test project

### 3a. Create `tests/RabbitMqHealthCheck.Tests/`

Files: `RabbitMqHealthCheck.Tests.csproj`, `RabbitMqHealthCheckTests.cs` (stub), `RabbitMqApprovalTests.cs` (stub).

### 3b. Write `RabbitMqHealthCheck.Tests.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFrameworks>net9.0;net10.0</TargetFrameworks>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="autofixture" Version="4.18.1" />
    <PackageReference Include="AwesomeAssertions" Version="*" />
    <PackageReference Include="coverlet.collector" Version="6.0.4">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="JetBrains.Annotations" Version="2024.3.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="Microsoft.Extensions.Configuration" Version="9.0.14" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="9.0.14" />
    <PackageReference Include="Moq" Version="4.20.72" />
    <PackageReference Include="Moq.Analyzers" Version="0.4.2">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="PublicApiGenerator" Version="11.5.4" />
    <PackageReference Include="SonarAnalyzer.CSharp" Version="10.21.0.135717">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Verify.Xunit" Version="*" />
    <!-- xunit v3: verify Verify.Xunit has v3 compat at scaffold time; may need Verify.XunitV3 -->
    <PackageReference Include="xunit.v3" Version="*" />
    <PackageReference Include="Xunit.OpenCategories" Version="*" />
    <!-- verify Xunit.OpenCategories v3 compat at scaffold time -->
    <PackageReference Include="xunit.v3.runner.visualstudio" Version="*">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\RabbitMqHealthCheck\Innago.Shared.HealthChecks.RabbitMq.csproj" />
  </ItemGroup>

  <ItemGroup>
    <Folder Include="ApprovedApi\" />
  </ItemGroup>

  <ItemGroup>
    <AssemblyAttribute Include="System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute" />
  </ItemGroup>

  <ItemGroup>
    <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleToAttribute">
      <_Parameter1>RabbitMqHealthCheck.Tests</_Parameter1>
    </AssemblyAttribute>
  </ItemGroup>

</Project>
```

Note: `InternalsVisibleTo` is added via `AssemblyAttribute` in the **source** `.csproj`, not the test `.csproj`. Move that block to `Innago.Shared.HealthChecks.RabbitMq.csproj`.

### 3c. Add to solution

```bash
dotnet sln TcpHealthProbe.slnx add tests/RabbitMqHealthCheck.Tests/RabbitMqHealthCheck.Tests.csproj
```

---

## Step 4: Scaffold the Npgsql test project

Mirror Step 3 for `tests/NpgsqlHealthCheck.Tests/`. Reference `Innago.Shared.HealthChecks.Npgsql.csproj`. Add `InternalsVisibleTo` to the Npgsql source `.csproj`. Add to solution.

---

## Step 5: Implement core production code — RabbitMQ package

**TDD:** For each method below, write the corresponding test in `RabbitMqHealthCheckTests.cs` first (it will fail), then implement the production code.

### 5a. `VaultSecretsReader.cs`

```csharp
namespace Innago.Shared.HealthChecks.RabbitMq;

using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

using Innago.Shared.TryHelpers;

using Microsoft.Extensions.Logging;

// Intentional copy: see DESIGN.md §Package Structure

[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
internal partial class VaultSecretsJsonContext : JsonSerializerContext { }

internal readonly record struct VaultSecrets(Dictionary<string, string> Values);

internal static class VaultSecretsReader
{
    /// <summary>
    /// Returns:
    ///   HasSucceeded + null         = file was absent; caller may fall through
    ///   HasSucceeded + VaultSecrets = file read and parsed; caller uses .Values
    ///   HasFailed    + Exception    = file present but unreadable/unparseable;
    ///                                 caller must return Unhealthy — do NOT fall through
    /// </summary>
    internal static Result<VaultSecrets?> TryRead(string path, ILogger logger)
    {
        try
        {
            string json = File.ReadAllText(path);
            var dict = JsonSerializer.Deserialize(json, VaultSecretsJsonContext.Default.DictionaryStringString);
            return dict is null
                ? new Result<VaultSecrets?>((VaultSecrets?)null)
                : new Result<VaultSecrets?>(new VaultSecrets(dict));
        }
        catch (FileNotFoundException)
        {
            return new Result<VaultSecrets?>((VaultSecrets?)null);
        }
        catch (Exception ex)
        {
            logger.VaultFileReadError(path, ex);
            return new Result<VaultSecrets?>(ex);
        }
    }
}
```

### 5b. `RabbitMqHealthCheckOptions.cs`

```csharp
namespace Innago.Shared.HealthChecks.RabbitMq;

using JetBrains.Annotations;

/// <summary>Configuration options for the Innago RabbitMQ health check.</summary>
[PublicAPI]
public sealed class RabbitMqHealthCheckOptions
{
    /// <summary>Path to the Vault-injected secrets file. Default: <c>/vault/secrets/appsettings.json</c>.</summary>
    public string VaultSecretsPath { get; set; } = "/vault/secrets/appsettings.json";

    /// <summary>Timeout for opening a test connection. Default: 5 seconds.</summary>
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Health check tags. Default: <c>["ready"]</c>. Use <c>"live"</c> for liveness probes.</summary>
    public string[] Tags { get; set; } = ["ready"];

    /// <summary>Health check registration name. Default: <c>"rabbitmq"</c>.</summary>
    public string Name { get; set; } = "rabbitmq";

    /// <summary>
    /// When <c>true</c>, Strategy 1 (DI <see cref="RabbitMQ.Client.IConnection"/>) is skipped.
    /// Use for services where MassTransit manages <c>IConnection</c> and vault rotation is in effect.
    /// Default: <c>false</c>.
    /// </summary>
    public bool IgnoreDiConnection { get; set; } = false;
}
```

### 5c. `LogMessages.cs`

```csharp
namespace Innago.Shared.HealthChecks.RabbitMq;

using Microsoft.Extensions.Logging;

internal static partial class LogMessages
{
    [LoggerMessage(LogLevel.Debug, Message = "RabbitMQ health check: trying DI IConnection strategy.")]
    public static partial void StrategyDiConnection(this ILogger<RabbitMqHealthCheck> logger);

    [LoggerMessage(LogLevel.Debug, Message = "RabbitMQ health check: DI IConnection found (type: {TypeName}).")]
    public static partial void DiConnectionFound(this ILogger<RabbitMqHealthCheck> logger, string typeName);

    [LoggerMessage(LogLevel.Debug, Message = "RabbitMQ health check: trying vault secrets file at {Path}.")]
    public static partial void StrategyVaultFile(this ILogger<RabbitMqHealthCheck> logger, string path);

    [LoggerMessage(LogLevel.Debug, Message = "RabbitMQ health check: trying IConfiguration fallback.")]
    public static partial void StrategyIConfiguration(this ILogger<RabbitMqHealthCheck> logger);

    [LoggerMessage(LogLevel.Warning, Message = "RabbitMQ health check: vault file at {Path} could not be read or parsed.")]
    public static partial void VaultFileReadError(this ILogger logger, string path, Exception exception);

    [LoggerMessage(LogLevel.Warning, Message = "RabbitMQ health check: connection probe failed.")]
    public static partial void ProbeConnectionFailed(this ILogger<RabbitMqHealthCheck> logger, Exception exception);

    [LoggerMessage(LogLevel.Warning, Message = "RabbitMQ health check: no strategy succeeded — no connection or configuration found.")]
    public static partial void NoStrategyFound(this ILogger<RabbitMqHealthCheck> logger);
}
```

### 5d. `RabbitMqHealthCheck.cs`

```csharp
namespace Innago.Shared.HealthChecks.RabbitMq;

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
            return await GetVaultProbeResult(vaultSecrets.Value.Values, cancellationToken);
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

    private async Task<HealthCheckResult?> GetVaultProbeResult(
        System.Collections.Generic.Dictionary<string, string> secrets,
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
        return await _prober(factory, cancellationToken).ConfigureAwait(false);
    }

    private static HealthCheckResult ResolveFromVaultSecrets(
        System.Collections.Generic.Dictionary<string, string> secrets,
        HealthCheckResult? probeResult)
        => probeResult ?? HealthCheckResult.Unhealthy("Vault strategy produced no result.");

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
        return await _prober(factory, cancellationToken).ConfigureAwait(false);
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
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }
        return null;
    }
}
```

### 5e. `HealthChecksBuilderExtensions.cs`

```csharp
namespace Innago.Shared.HealthChecks.RabbitMq;

using JetBrains.Annotations;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>Extension methods for registering the Innago RabbitMQ health check.</summary>
[PublicAPI]
public static class HealthChecksBuilderExtensions
{
    /// <summary>
    /// Adds the Innago RabbitMQ health check with optional tag override.
    /// Tags default to <c>["ready"]</c>; use <c>"live"</c> for liveness probes.
    /// </summary>
    /// <seealso cref="AddInnagoRabbitMq(IHealthChecksBuilder, Action{RabbitMqHealthCheckOptions})"/>
    public static IHealthChecksBuilder AddInnagoRabbitMq(
        this IHealthChecksBuilder builder,
        string[]? tags = null)
        => AddInnagoRabbitMqCore(builder, options =>
        {
            if (tags is not null)
            {
                options.Tags = tags;
            }
        });

    /// <summary>
    /// Adds the Innago RabbitMQ health check with full options configuration.
    /// </summary>
    /// <seealso cref="AddInnagoRabbitMq(IHealthChecksBuilder, string[])"/>
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
```

### 5f. Build and verify

```bash
dotnet build src/RabbitMqHealthCheck/Innago.Shared.HealthChecks.RabbitMq.csproj
```

---

## Step 6: Implement core production code — Npgsql package

Mirror Step 5 for the Npgsql package. Key differences:

**`NpgsqlHealthCheckOptions.cs`**: Add `CommandText` (default `"SELECT 1"`) and `ConnectionStringName` (default `null`).

**`VaultSecretsReader.cs`**: Identical logic; `VaultSecretsJsonContext` declared in `Innago.Shared.HealthChecks.Npgsql` namespace. Add `// Intentional copy: see DESIGN.md §Package Structure` comment.

**`NpgsqlHealthCheck.cs`** — class signature:

```csharp
internal sealed class NpgsqlHealthCheck : IHealthCheck
{
    internal NpgsqlHealthCheck(
        IConfiguration configuration,
        NpgsqlHealthCheckOptions options,
        ILogger<NpgsqlHealthCheck> logger,
        Func<string, CancellationToken, Task<HealthCheckResult>>? prober = null)
    // Note: no IServiceProvider — NpgsqlHealthCheck has no DI connection strategy
}
```

**Strategy 1 (connection string):**
```csharp
private string? TryStrategy1ConnectionString()
{
    var name = _options.ConnectionStringName;
    if (name is not null)
    {
        return _configuration.GetConnectionString(name);
    }
    return _configuration.GetConnectionString("DefaultConnection")
        ?? _configuration.GetConnectionString("Database");
}
```

**Strategy 2 (vault file):** Keys `db_host`, `db_username`, `db_password`, `db_name`, `db_port`.
Required keys: `db_host`, `db_username`, `db_password`, `db_name` — return `Unhealthy` if any absent.

**Build connection string for strategies 2 and 3:**
```csharp
private string BuildConnectionString(
    string host, string username, string password, string dbName, string? portStr)
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
    // NOTE: never log builder.ConnectionString — it contains plaintext password.
    // For diagnostics, clone builder, set Password = "[redacted]", use that string only.
}
```

**`DefaultProbeAsync`:**
```csharp
private async Task<HealthCheckResult> DefaultProbeAsync(
    string connectionString, CancellationToken cancellationToken)
{
    try
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = _options.CommandText;
        await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return HealthCheckResult.Healthy();
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
```

**`HealthChecksBuilderExtensions.cs`** — DI registration:
```csharp
builder.Services.Configure<HealthCheckServiceOptions>(opts =>
    opts.Registrations.Add(new HealthCheckRegistration(
        options.Name,
        sp => new NpgsqlHealthCheck(
            sp.GetRequiredService<IConfiguration>(),
            options,
            sp.GetRequiredService<ILogger<NpgsqlHealthCheck>>()),
        failureStatus: null,
        tags: options.Tags)));
```

Build and verify before proceeding.

---

## Step 7: Write RabbitMQ unit tests

File: `tests/RabbitMqHealthCheck.Tests/RabbitMqHealthCheckTests.cs`

**Test seam:** construct `RabbitMqHealthCheck` with a lambda for `prober`:
```csharp
var check = new RabbitMqHealthCheck(
    serviceProvider: mockSp.Object,
    configuration: config,
    options: options,
    logger: Mock.Of<ILogger<RabbitMqHealthCheck>>(),
    prober: (factory, ct) => Task.FromResult(HealthCheckResult.Healthy()));
```

**Test list:**

```
Options_Defaults_AreCorrect
Registration_AddsNamedHealthCheck_WithDefaultName
Registration_WithTags_UsesProvidedTags
Registration_WithOptions_UsesOptionsName
Strategy1_ConnectionIsOpen_ReturnsHealthy
Strategy1_ConnectionIsNotOpen_ReturnsUnhealthy
Strategy1_NoConnectionRegistered_FallsThrough
Strategy1_IgnoreDiConnection_SkipsStrategy1
Strategy2_VaultFileAbsent_FallsThroughToStrategy3
Strategy2_VaultFilePresentWithAllKeys_CallsProber
Strategy2_VaultFilePresentHostAbsent_ReturnsUnhealthy
Strategy2_VaultFilePresentUsernameAbsent_ReturnsUnhealthy
Strategy2_VaultFilePresentPasswordAbsent_ReturnsUnhealthy
Strategy2_VaultFileMalformed_ReturnsUnhealthy_DoesNotFallThrough
Strategy3_RabbitMQHostConfigured_CallsProber
Strategy3_MassTransitHostConfigured_CallsProber
Strategy3_RabbitHostConfigured_CallsProber
Strategy3_NoHostConfigured_ReturnsUnhealthy
Strategy3_UsesGuestDefaults_WhenCredentialsAbsent
StrategyOrder_VaultFilePresentBlocksStrategy3
StrategyOrder_DITakesPrecedenceOverVault
NoStrategy_ReturnsUnhealthyWithDescriptiveMessage
Prober_ExceptionIsCaught_ReturnsUnhealthySanitizedMessage
```

For vault file tests: write a temp JSON file to `Path.GetTempFileName()` and pass its path as `VaultSecretsPath`.
For `IConfiguration`: `new ConfigurationBuilder().AddInMemoryCollection(dict).Build()`.
For `IServiceProvider`: `Mock<IServiceProvider>` when testing strategy routing; real `ServiceCollection` for registration tests.

---

## Step 8: Write Npgsql unit tests

Mirror Step 7 for `tests/NpgsqlHealthCheck.Tests/NpgsqlHealthCheckTests.cs`.

Additional test cases specific to Npgsql:
```
Strategy1_ConnectionStringName_UsesNamedKey
Strategy1_ConnectionStringName_Null_TriesDefaultConnectionThenDatabase
Strategy2_VaultFilePresent_DbNameAbsent_ReturnsUnhealthy
```

---

## Step 9: Write approval tests — RabbitMQ

File: `tests/RabbitMqHealthCheck.Tests/RabbitMqApprovalTests.cs`

Follow `TcpHealthProbeServiceApprovalTests.cs` pattern exactly, substituting project folder/name. Run once, rename `.received.txt` to `.verified.txt`, commit.

---

## Step 10: Write approval tests — Npgsql

Mirror Step 9 for `tests/NpgsqlHealthCheck.Tests/NpgsqlApprovalTests.cs`.

---

## Step 11: Write README files

Each README must cover:
1. Package purpose
2. Installation
3. Basic usage (`AddInnagoRabbitMq()` / `AddInnagoNpgsql()`)
4. Options usage
5. Tri-strategy resolution table (from SPEC.md)
6. Configuration keys table (from SPEC.md)
7. **Tag convention:** `"ready"` for readiness probes, `"live"` for liveness probes
8. **MassTransit warning:** If using MassTransit >= 8, set `IgnoreDiConnection = true` OR do not add this package — MassTransit registers its own RabbitMQ health check. Adding both without `IgnoreDiConnection = true` will result in duplicate health check entries.
9. Error handling table (from SPEC.md)
10. **Migration note:** Remove the copy-pasted `DynamicRabbitMqHealthCheck.cs` / `RabbitMqConfigService.cs` / `DynamicNpgSqlHealthCheck.cs` in the same PR that adds this package. Do not run both simultaneously. Verify `/health` output after deployment.

---

## Step 12: Run full test suite and verify

```bash
dotnet test
```

All tests must pass. Fix failures before proceeding.

---

## Step 13: Validation checklist

Before marking the feature complete:

- [ ] `dotnet build --configuration Release` succeeds with zero warnings for all projects
- [ ] `dotnet test` passes all tests
- [ ] PublicApiAnalyzers produce no errors
- [ ] `dotnet pack` produces `.nupkg` for both new packages
- [ ] Approval snapshots committed and up to date
- [ ] No `Console.*` calls (banned)
- [ ] No `LoggerExtensions` calls (use LogMessages source-gen methods only)
- [ ] `VaultSecretsReader.TryRead` uses `VaultSecretsJsonContext` (AOT-safe)
- [ ] No raw `Exception` attached to any `HealthCheckResult`
- [ ] `NpgsqlConnectionStringBuilder.ConnectionString` never appears in log messages
- [ ] `vault file present` correctly blocks Strategy 3 in all code paths
- [ ] `guest/guest` defaults only appear in Strategy 3 (IConfiguration path), never in vault file path

---

## Step 14: Update `.csproj` URLs after repo rename (post-rename only)

After GitHub repo is renamed to `innago-health-checks`: update `RepositoryUrl` and `PackageProjectUrl` in all three source `.csproj` files. Update approval snapshots if assembly metadata appears in verified API text.

---

## Step 15: PR and CI

1. Push branch to origin.
2. Create PR targeting `main`.
3. CI (Oui-Deliver reusable workflow) — no workflow changes needed.
4. Verify both packages build and publish to GitHub Packages.
5. Merge PR.

---

## Parallelism Notes

- Steps 1 and 2 can run in parallel.
- Steps 3 and 4 can run in parallel with each other (and after 1/2).
- Steps 5 and 6 can run in parallel, but within each step, tests for each method precede its implementation (TDD).
- Steps 7–10 are naturally interleaved with 5–6 per the TDD discipline.
- Step 11 (READMEs) can happen at any point.
- Step 12 is the final gate before Step 15.
