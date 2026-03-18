**Version:** 1.1 (Post-Delphi Round 1)
**Date:** 2026-03-18
**Changes:** Add innago-tryhelpers dependency; VaultSecretsReader returns Result<T>; sealed class with Func delegate test seam; HealthCheckRegistration for DI; no CreateChannelAsync; credentials required (no guest/guest in vault path); exception logging not attachment; remove EnableConfigurationBindingGenerator; add IgnoreDiConnection / ConnectionStringName options.

# Technical Design: Dynamic Health Checks — RabbitMQ + Npgsql

---

## Research Summary

### Dependency versions selected

| Package | Version | Justification |
|---------|---------|--------------|
| `RabbitMQ.Client` | 7.2.1 | Latest stable as of 2026-03-18. Published 2026-02-25. Targets net8.0 / netstandard2.0. |
| `Npgsql` | 9.0.5 | Latest stable in the net9.0-aligned 9.x line. Published 2026-03-12. |
| `Microsoft.Extensions.Diagnostics.HealthChecks` | 9.0.14 | Matches TcpHealthProbe version. |
| `Innago.Shared.TryHelpers` | latest | Provides `Result<T>` for discriminated vault read result. Internal Innago package. |

### RabbitMQ.Client 7.x API notes

- `IConnection.IsOpen` still exists (bool property) — used by Strategy 1 and by strategies 2/3 after `CreateConnectionAsync`.
- Synchronous `CreateModel()` is **gone**. New API: `await connection.CreateChannelAsync(...)`. However, health probe does **not** open a channel — `IsOpen` after `CreateConnectionAsync` is sufficient proof of broker reachability and authentication. See §Tri-Strategy Implementation Detail.
- `IChannel` / `IConnection` both implement `IAsyncDisposable`. Always `await using`.
- `ConnectionFactory.CreateConnectionAsync()` is the async factory method.

### Npgsql 9.x API notes

Fully compatible with .NET 9. Core API unchanged: `NpgsqlConnection`, `OpenAsync`, `CreateCommand`. `NpgsqlConnectionStringBuilder.ConnectionString` includes plaintext password — never log or surface this string; use a sanitized copy for any diagnostic output.

### Community package evaluation

Both `AspNetCore.HealthChecks.Rabbitmq` and `AspNetCore.HealthChecks.NpgSql` (Xabaril, 9.0.0) are well-maintained but do not implement the vault file strategy. Write from scratch. The Xabaril Npgsql `SELECT 1` probe pattern is adopted.

### AOT-compatible vault file JSON reading

`[JsonSerializable(typeof(Dictionary<string, string>))]` source-generated context. No reflection. Duplicated in each package for independent deployability.

---

## Package Structure

```
src/
  RabbitMqHealthCheck/
    Innago.Shared.HealthChecks.RabbitMq.csproj
    HealthChecksBuilderExtensions.cs      (public)
    RabbitMqHealthCheckOptions.cs         (public)
    RabbitMqHealthCheck.cs                (internal sealed)
    VaultSecretsReader.cs                 (internal static)
    LogMessages.cs                        (internal static partial)
    BannedSymbols.txt
    PublicAPI.Shipped.txt
    PublicAPI.Unshipped.txt
    README.md

  NpgsqlHealthCheck/
    Innago.Shared.HealthChecks.Npgsql.csproj
    HealthChecksBuilderExtensions.cs      (public)
    NpgsqlHealthCheckOptions.cs           (public)
    NpgsqlHealthCheck.cs                  (internal sealed)
    VaultSecretsReader.cs                 (internal static — intentional copy, see §VaultSecretsReader)
    LogMessages.cs                        (internal static partial)
    BannedSymbols.txt
    PublicAPI.Shipped.txt
    PublicAPI.Unshipped.txt
    README.md
```

`VaultSecretsReader` is duplicated to keep each package independently deployable with no cross-assembly dependencies. Add comment: `// Intentional copy: see DESIGN.md §Package Structure`.

---

## Class Diagrams (text)

### RabbitMQ package

```
IHealthCheck
  └── RabbitMqHealthCheck (internal sealed)
        fields:
          _options: RabbitMqHealthCheckOptions
          _serviceProvider: IServiceProvider
          _configuration: IConfiguration
          _logger: ILogger<RabbitMqHealthCheck>
          _prober: Func<ConnectionFactory, CancellationToken, Task<HealthCheckResult>>
        constructor:
          internal RabbitMqHealthCheck(IServiceProvider, IConfiguration, RabbitMqHealthCheckOptions,
              ILogger<RabbitMqHealthCheck>,
              Func<ConnectionFactory, CancellationToken, Task<HealthCheckResult>>? prober = null)
          // prober defaults to DefaultProbeAsync; override in tests via constructor injection
        methods:
          CheckHealthAsync(HealthCheckContext, CancellationToken) -> Task<HealthCheckResult>
          TryStrategy1_DiConnection()           -> HealthCheckResult?
          TryStrategy2_VaultFile()              -> Task<HealthCheckResult?>   (uses Result<T> from VaultSecretsReader)
          TryStrategy3_IConfiguration()         -> Task<HealthCheckResult?>
          DefaultProbeAsync(ConnectionFactory, CancellationToken) -> Task<HealthCheckResult>

RabbitMqHealthCheckOptions (public sealed)
  VaultSecretsPath: string   = "/vault/secrets/appsettings.json"
  ConnectionTimeout: TimeSpan = 5s
  Tags: string[]             = ["ready"]
  Name: string               = "rabbitmq"
  IgnoreDiConnection: bool   = false

HealthChecksBuilderExtensions (public static)
  AddInnagoRabbitMq(builder, tags?) -> IHealthChecksBuilder
  AddInnagoRabbitMq(builder, Action<RabbitMqHealthCheckOptions>) -> IHealthChecksBuilder

VaultSecretsReader (internal static)
  TryRead(path, logger) -> Result<Dictionary<string,string>?>
  // Success(null)  = file absent → caller falls through
  // Success(dict)  = file parsed ok → caller uses it
  // Failure(ex)    = file present but unreadable → caller returns Unhealthy

LogMessages (internal static partial)
  [LoggerMessage] events for strategy selection, vault errors, probe failures
```

### Npgsql package

```
IHealthCheck
  └── NpgsqlHealthCheck (internal sealed)
        fields:
          _options: NpgsqlHealthCheckOptions
          _configuration: IConfiguration
          _logger: ILogger<NpgsqlHealthCheck>
          _prober: Func<string, CancellationToken, Task<HealthCheckResult>>
        constructor:
          internal NpgsqlHealthCheck(IConfiguration, NpgsqlHealthCheckOptions,
              ILogger<NpgsqlHealthCheck>,
              Func<string, CancellationToken, Task<HealthCheckResult>>? prober = null)
        methods:
          CheckHealthAsync(HealthCheckContext, CancellationToken) -> Task<HealthCheckResult>
          TryStrategy1_ConnectionString() -> string?
          TryStrategy2_VaultFile()        -> Result<string?>     (uses VaultSecretsReader)
          TryStrategy3_IndividualKeys()   -> string?
          DefaultProbeAsync(string connectionString, CancellationToken) -> Task<HealthCheckResult>

NpgsqlHealthCheckOptions (public sealed)
  VaultSecretsPath: string    = "/vault/secrets/appsettings.json"
  ConnectionTimeout: TimeSpan = 5s
  Tags: string[]              = ["ready"]
  Name: string                = "npgsql"
  CommandText: string         = "SELECT 1"
  ConnectionStringName: string? = null
```

Note: `NpgsqlHealthCheck` does not receive `IServiceProvider` — it has no DI connection strategy.

---

## VaultSecretsReader Contract

```csharp
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
internal partial class VaultSecretsJsonContext : JsonSerializerContext { }

// Intentional copy: see DESIGN.md §Package Structure

internal readonly record struct VaultSecrets(Dictionary<string, string> Values);

internal static class VaultSecretsReader
{
    // Returns:
    //   Absent()      → file was not present; caller may fall through to next strategy
    //   Ok(secrets)   → file was read and parsed; caller uses secrets
    //   Failed(msg)   → file was present but unreadable/unparseable;
    //                   caller must return Unhealthy — do NOT fall through
    // Result<VaultSecrets?> three states:
    //   HasSucceeded + null         → file was absent; caller falls through
    //   HasSucceeded + VaultSecrets → file parsed; caller uses .Values
    //   HasFailed    + Exception    → file present but unreadable/unparseable;
    //                                 caller logs ex, returns Unhealthy — do NOT fall through
    internal static Result<VaultSecrets?> TryRead(string path, ILogger logger);
}
```

Implementation: use a single `try { File.ReadAllText(...) } catch (FileNotFoundException) { return new Result<...>((Dictionary<string,string>?)null); }` — eliminates TOCTOU from a `File.Exists` + `ReadAllText` two-step.

---

## Tri-Strategy Implementation Detail

### Vault gate

Both health checks track whether the vault file was present. If `VaultSecretsReader.TryRead` returns a `Failure` or a `Success(non-null dict)` (file was present), Strategy 3 is never attempted. Only a `Success(null)` (file was absent) permits Strategy 3.

### RabbitMQ CheckHealthAsync pseudo-code

```
CheckHealthAsync:
  vaultFileWasPresent = false

  // Strategy 1: DI IConnection
  if not _options.IgnoreDiConnection:
    log.StrategyDiConnection()
    conn = _serviceProvider.GetService<IConnection>()
    if conn != null:
      log.DiConnectionFound(conn.GetType().Name)
      if conn.IsOpen: return Healthy()
      return Unhealthy("RabbitMQ connection is not open.")

  // Strategy 2: Vault file
  log.StrategyVaultFile(_options.VaultSecretsPath)
  vaultResult = VaultSecretsReader.TryRead(_options.VaultSecretsPath, _logger)
  if vaultResult.HasFailed:
    vaultFileWasPresent = true
    log.ProbeConnectionFailed(vaultResult as Exception)
    return Unhealthy("Vault file could not be parsed. See application logs for details.")
  if vaultResult value is non-null:
    vaultFileWasPresent = true
    secrets = vaultResult value
    host = secrets["rabbit_host"]
    if host is null/empty: return Unhealthy("Vault file present but rabbit_host is missing.")
    username = secrets["rabbit_username"]
    if username is null/empty: return Unhealthy("Vault file present but rabbit_username is missing.")
    password = secrets["rabbit_password"]
    if password is null/empty: return Unhealthy("Vault file present but rabbit_password is missing.")
    secrets.TryGetValue("rabbit_virtualhost", out virtualHost)
    secrets.TryGetValue("rabbit_port", out portStr)
    factory = BuildFactory(host, username, password, virtualHost, portStr)
    return await _prober(factory, cancellationToken)

  // Strategy 3: IConfiguration — only if vault file was absent
  if vaultFileWasPresent: [unreachable — covered above]
  log.StrategyIConfiguration()
  host = FirstNonEmpty(_config["RabbitMQ:Host"], _config["MassTransit:RabbitMq:Host"], _config["rabbit_host"])
  if host is null/empty: return Unhealthy("No RabbitMQ connection or configuration found.")
  username = FirstNonEmpty(_config["RabbitMQ:Username"], _config["MassTransit:RabbitMq:Username"], _config["rabbit_username"])
  password = FirstNonEmpty(_config["RabbitMQ:Password"], _config["MassTransit:RabbitMq:Password"], _config["rabbit_password"])
  virtualHost = FirstNonEmpty(_config["RabbitMQ:VirtualHost"], _config["MassTransit:RabbitMq:VirtualHost"], _config["rabbit_virtualhost"])
  factory = BuildFactory(host, username ?? "guest", password ?? "guest", virtualHost, portStr: null)
  return await _prober(factory, cancellationToken)

DefaultProbeAsync(factory, cancellationToken):
  try:
    await using conn = await factory.CreateConnectionAsync(cancellationToken)
    // IsOpen check sufficient — no channel needed, see §Design Notes
    if not conn.IsOpen: return Unhealthy("RabbitMQ connection is not open.")
    return Healthy()
  catch ex:
    _logger.ProbeConnectionFailed(ex)   // full exception to structured log only
    return Unhealthy("Connection probe failed. See application logs for details.")
```

### Npgsql CheckHealthAsync pseudo-code

```
CheckHealthAsync:
  // Strategy 1: named connection string from IConfiguration
  log.StrategyConnectionString()
  connStr = FirstNonEmpty(
    _config.GetConnectionString(_options.ConnectionStringName ?? "DefaultConnection"),
    _options.ConnectionStringName == null ? _config.GetConnectionString("Database") : null)
  if connStr != null: return await _prober(connStr, cancellationToken)

  // Strategy 2: Vault file
  log.StrategyVaultFile(_options.VaultSecretsPath)
  vaultResult = VaultSecretsReader.TryRead(_options.VaultSecretsPath, _logger)
  vaultFileWasPresent = vaultResult.HasFailed || vaultResult value is non-null
  if vaultResult.HasFailed:
    log.ProbeConnectionFailed(vaultResult as Exception)
    return Unhealthy("Vault file could not be parsed. See application logs for details.")
  if vaultResult value is non-null:
    secrets = vaultResult value
    host = secrets["db_host"]
    if host is null/empty: return Unhealthy("Vault file present but db_host is missing.")
    username = secrets["db_username"]
    if username is null/empty: return Unhealthy("Vault file present but db_username is missing.")
    password = secrets["db_password"]
    if password is null/empty: return Unhealthy("Vault file present but db_password is missing.")
    dbName = secrets["db_name"]
    if dbName is null/empty: return Unhealthy("Vault file present but db_name is missing.")
    secrets.TryGetValue("db_port", out portStr)
    connStr = BuildConnectionString(host, username, password, dbName, portStr)
    return await _prober(connStr, cancellationToken)

  // Strategy 3: only if vault file was absent
  log.StrategyIConfiguration()
  host = FirstNonEmpty(_config["db_host"], _config["DB_HOST"])
  if host is null/empty: return Unhealthy("No Npgsql connection string or configuration found.")
  connStr = BuildConnectionString(host,
    _config["db_username"], _config["db_password"],
    FirstNonEmpty(_config["db_name"], _config["DB_NAME"]),
    FirstNonEmpty(_config["db_port"], _config["DB_PORT"], "5432"))
  return await _prober(connStr, cancellationToken)

DefaultProbeAsync(connStr, cancellationToken):
  try:
    await using conn = new NpgsqlConnection(connStr)
    await conn.OpenAsync(cancellationToken)
    using cmd = conn.CreateCommand()
    cmd.CommandText = _options.CommandText
    await cmd.ExecuteScalarAsync(cancellationToken)
    return Healthy()
  catch ex:
    _logger.ProbeConnectionFailed(ex)
    return Unhealthy("Connection probe failed. See application logs for details.")
```

### Design Notes

**Why no `CreateChannelAsync`:** Opening a channel adds an AMQP round-trip, a broker-side resource allocation, and connection churn visible in broker metrics. A successful `CreateConnectionAsync` already proves TCP handshake, TLS (if configured), AMQP protocol handshake, and credential authentication — the full failure mode set. Channel creation would additionally test broker channel limits, which indicates broker load, not broker unavailability. Using `IsOpen` after connect is consistent with Strategy 1 (DI `IConnection`) and aligns the definition of "healthy" across all three strategies.

**Exception attachment:** Raw `Exception` objects are never attached to `HealthCheckResult`. Full exceptions including stack traces and inner exceptions are logged via `ILogger` at `Warning` level. Health check results carry only a sanitized description string. This prevents credential leakage via `IHealthCheckPublisher` implementations (e.g., Loki, OTel exporters).

**Password sanitization for Npgsql:** When building connection strings, never log or surface the raw `NpgsqlConnectionStringBuilder.ConnectionString`. For any diagnostic use, clone the builder, set `Password = "[redacted]"`, use that string only.

---

## DI Registration Detail

`AddInnagoRabbitMqCore` uses `HealthCheckRegistration` directly (the internal mechanism all `AddCheck` overloads use), providing `IServiceProvider` access:

```csharp
private static IHealthChecksBuilder AddInnagoRabbitMqCore(
    IHealthChecksBuilder builder,
    Action<RabbitMqHealthCheckOptions>? configure,
    string[]? tags)
{
    var options = new RabbitMqHealthCheckOptions();
    configure?.Invoke(options);
    if (tags is not null) { options.Tags = tags; }

    // Warn if a health check with this name is already registered
    var existing = builder.Services
        .Where(d => d.ServiceType == typeof(HealthCheckRegistration))
        .Select(d => d.ImplementationInstance as HealthCheckRegistration)
        .Any(r => r?.Name == options.Name);
    if (existing)
    {
        // Log via startup logging — use ILogger<HealthChecksBuilderExtensions> via ServiceDescriptor
        // or simply document this check is best-effort (static context)
    }

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
```

The duplicate-name warning at registration time is best-effort (static context has no `ILogger`). Emit it via `Debug.WriteLine` or document in README; a runtime startup log from the health check itself on first probe is an alternative.

---

## .csproj Structure

### `Innago.Shared.HealthChecks.RabbitMq.csproj`

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
    <!-- No EnableConfigurationBindingGenerator: no IConfiguration.Get<T>() calls in this assembly.
         Vault JSON AOT safety is handled by VaultSecretsJsonContext (STJ source generation). -->
  </PropertyGroup>

  <PropertyGroup Condition=" '$(Configuration)' == 'Debug' ">
    <DocumentationFile>bin\Debug\net9.0\Innago.Shared.HealthChecks.RabbitMq.xml</DocumentationFile>
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

Npgsql `.csproj` is identical replacing `RabbitMQ.Client 7.2.1` with `Npgsql 9.0.5`.

---

## PublicAPI.txt Files

### `PublicAPI.Shipped.txt` (both packages — initially empty)

```
#nullable enable
```

### `PublicAPI.Unshipped.txt` — RabbitMQ package

```
#nullable enable
Innago.Shared.HealthChecks.RabbitMq.HealthChecksBuilderExtensions
static Innago.Shared.HealthChecks.RabbitMq.HealthChecksBuilderExtensions.AddInnagoRabbitMq(this Microsoft.Extensions.DependencyInjection.IHealthChecksBuilder! builder, string[]? tags = null) -> Microsoft.Extensions.DependencyInjection.IHealthChecksBuilder!
static Innago.Shared.HealthChecks.RabbitMq.HealthChecksBuilderExtensions.AddInnagoRabbitMq(this Microsoft.Extensions.DependencyInjection.IHealthChecksBuilder! builder, System.Action<Innago.Shared.HealthChecks.RabbitMq.RabbitMqHealthCheckOptions!>! configure) -> Microsoft.Extensions.DependencyInjection.IHealthChecksBuilder!
Innago.Shared.HealthChecks.RabbitMq.RabbitMqHealthCheckOptions
Innago.Shared.HealthChecks.RabbitMq.RabbitMqHealthCheckOptions.RabbitMqHealthCheckOptions() -> void
Innago.Shared.HealthChecks.RabbitMq.RabbitMqHealthCheckOptions.ConnectionTimeout.get -> System.TimeSpan
Innago.Shared.HealthChecks.RabbitMq.RabbitMqHealthCheckOptions.ConnectionTimeout.set -> void
Innago.Shared.HealthChecks.RabbitMq.RabbitMqHealthCheckOptions.IgnoreDiConnection.get -> bool
Innago.Shared.HealthChecks.RabbitMq.RabbitMqHealthCheckOptions.IgnoreDiConnection.set -> void
Innago.Shared.HealthChecks.RabbitMq.RabbitMqHealthCheckOptions.Name.get -> string!
Innago.Shared.HealthChecks.RabbitMq.RabbitMqHealthCheckOptions.Name.set -> void
Innago.Shared.HealthChecks.RabbitMq.RabbitMqHealthCheckOptions.Tags.get -> string[]!
Innago.Shared.HealthChecks.RabbitMq.RabbitMqHealthCheckOptions.Tags.set -> void
Innago.Shared.HealthChecks.RabbitMq.RabbitMqHealthCheckOptions.VaultSecretsPath.get -> string!
Innago.Shared.HealthChecks.RabbitMq.RabbitMqHealthCheckOptions.VaultSecretsPath.set -> void
```

### `PublicAPI.Unshipped.txt` — Npgsql package

```
#nullable enable
Innago.Shared.HealthChecks.Npgsql.HealthChecksBuilderExtensions
static Innago.Shared.HealthChecks.Npgsql.HealthChecksBuilderExtensions.AddInnagoNpgsql(this Microsoft.Extensions.DependencyInjection.IHealthChecksBuilder! builder, string[]? tags = null) -> Microsoft.Extensions.DependencyInjection.IHealthChecksBuilder!
static Innago.Shared.HealthChecks.Npgsql.HealthChecksBuilderExtensions.AddInnagoNpgsql(this Microsoft.Extensions.DependencyInjection.IHealthChecksBuilder! builder, System.Action<Innago.Shared.HealthChecks.Npgsql.NpgsqlHealthCheckOptions!>! configure) -> Microsoft.Extensions.DependencyInjection.IHealthChecksBuilder!
Innago.Shared.HealthChecks.Npgsql.NpgsqlHealthCheckOptions
Innago.Shared.HealthChecks.Npgsql.NpgsqlHealthCheckOptions.NpgsqlHealthCheckOptions() -> void
Innago.Shared.HealthChecks.Npgsql.NpgsqlHealthCheckOptions.CommandText.get -> string!
Innago.Shared.HealthChecks.Npgsql.NpgsqlHealthCheckOptions.CommandText.set -> void
Innago.Shared.HealthChecks.Npgsql.NpgsqlHealthCheckOptions.ConnectionStringName.get -> string?
Innago.Shared.HealthChecks.Npgsql.NpgsqlHealthCheckOptions.ConnectionStringName.set -> void
Innago.Shared.HealthChecks.Npgsql.NpgsqlHealthCheckOptions.ConnectionTimeout.get -> System.TimeSpan
Innago.Shared.HealthChecks.Npgsql.NpgsqlHealthCheckOptions.ConnectionTimeout.set -> void
Innago.Shared.HealthChecks.Npgsql.NpgsqlHealthCheckOptions.Name.get -> string!
Innago.Shared.HealthChecks.Npgsql.NpgsqlHealthCheckOptions.Name.set -> void
Innago.Shared.HealthChecks.Npgsql.NpgsqlHealthCheckOptions.Tags.get -> string[]!
Innago.Shared.HealthChecks.Npgsql.NpgsqlHealthCheckOptions.Tags.set -> void
Innago.Shared.HealthChecks.Npgsql.NpgsqlHealthCheckOptions.VaultSecretsPath.get -> string!
Innago.Shared.HealthChecks.Npgsql.NpgsqlHealthCheckOptions.VaultSecretsPath.set -> void
```

---

## LogMessages Pattern

```csharp
internal static partial class LogMessages
{
    [LoggerMessage(LogLevel.Debug, "RabbitMQ health check: trying DI IConnection strategy.")]
    public static partial void StrategyDiConnection(this ILogger<RabbitMqHealthCheck> logger);

    [LoggerMessage(LogLevel.Debug, "RabbitMQ health check: DI IConnection found (type: {TypeName}).")]
    public static partial void DiConnectionFound(this ILogger<RabbitMqHealthCheck> logger, string typeName);

    [LoggerMessage(LogLevel.Debug, "RabbitMQ health check: trying vault secrets file at {Path}.")]
    public static partial void StrategyVaultFile(this ILogger<RabbitMqHealthCheck> logger, string path);

    [LoggerMessage(LogLevel.Debug, "RabbitMQ health check: trying IConfiguration fallback.")]
    public static partial void StrategyIConfiguration(this ILogger<RabbitMqHealthCheck> logger);

    [LoggerMessage(LogLevel.Warning, "RabbitMQ health check: vault file at {Path} could not be read or parsed.")]
    public static partial void VaultFileReadError(this ILogger logger, string path, Exception exception);

    [LoggerMessage(LogLevel.Warning, "RabbitMQ health check: connection probe failed.")]
    public static partial void ProbeConnectionFailed(this ILogger<RabbitMqHealthCheck> logger, Exception exception);

    [LoggerMessage(LogLevel.Warning, "RabbitMQ health check: no strategy succeeded.")]
    public static partial void NoStrategyFound(this ILogger<RabbitMqHealthCheck> logger);
}
```

`BannedSymbols.txt`: `T:Microsoft.Extensions.Logging.LoggerExtensions; Use LoggerMessage`

---

## Test Approach

### Unit tests

Framework: **xunit.v3** + **AwesomeAssertions** (drop-in FluentAssertions replacement, better licensing) + Moq + AutoFixture. Both test projects target `net9.0;net10.0`. Verify `Verify.Xunit` and `Xunit.OpenCategories` v3 compatibility at scaffold time — may need `Verify.XunitV3` or updated package versions.

**Test seam:** `RabbitMqHealthCheck` is `internal sealed`. The constructor accepts an optional `Func<ConnectionFactory, CancellationToken, Task<HealthCheckResult>>? prober`. Tests inject a lambda to control `DefaultProbeAsync` behaviour without subclassing or Moq virtual method overrides. No `InternalsVisibleTo` is required beyond what is already needed for the `internal` class itself.

Test categories per class:
1. Options defaults
2. Extension method registration (via `ServiceCollection` + `HealthCheckServiceOptions`)
3. Strategy 1 — DI IConnection (open, closed, absent, `IgnoreDiConnection = true`)
4. Strategy 2 — Vault file (absent → fallthrough, present+valid, present+malformed → Unhealthy, present+missing host key → Unhealthy, present+missing credentials → Unhealthy)
5. Strategy 3 — IConfiguration (each key pattern, none → Unhealthy)
6. Strategy ordering (DI before vault, vault before config, vault present blocks config)
7. `prober` injection — verify factory parameters match vault/config values
8. Approval tests for public API surface

For vault file tests: write a temp JSON file to `Path.GetTempPath()`, pass its path as `VaultSecretsPath`.
For IConfiguration tests: `new ConfigurationBuilder().AddInMemoryCollection(dict).Build()`.

### Integration tests (lower priority)

Testcontainers for RabbitMQ and PostgreSQL. Write temp vault files. These are in separate `*.IntegrationTests` projects, excluded from the standard CI gate.

---

## CI Changes

The existing Oui-Deliver reusable workflow discovers all `.csproj` files with `GeneratePackageOnBuild=true`. Adding the two new projects to the solution file is sufficient — no workflow changes needed.
