# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What This Is

A .NET 9 NuGet library that exposes a TCP listener as a health probe for console applications — enabling Kubernetes-style liveness checks without HTTP. The library runs as a `BackgroundService` that reflects `HealthCheckService` results via TCP port availability (open = healthy, closed = unhealthy).

## Commands

```bash
# Build
dotnet build HealthChecks.slnx

# Test
dotnet test

# Single test
dotnet test --filter "FullyQualifiedName~TestMethodName"

# Release build (warnings-as-errors, all analyzers active)
dotnet build --configuration Release

# Generate docs (xmldocmd must be installed as tool)
./makedocs.sh

# Package
dotnet pack
```

## Architecture

```
src/TcpHealthProbe/         ← NuGet library (the product)
examples/ConsoleAppWithProbe/ ← Reference implementation (DI, Serilog, OTEL)
tests/UnitTests/            ← xUnit + Moq + FluentAssertions + Verify (approval)
```

### Core Service Flow

`TcpHealthProbeService` (extends `BackgroundService`) polls `HealthCheckService` every `RefreshSeconds` (default 1s):
- **Healthy** → `TcpListener` is started and accepts connections
- **Unhealthy/Degraded** → `TcpListener` is stopped

Configuration via `IConfiguration.GetSection("HealthProbeConfiguration")` or DI-registered `HealthProbeConfiguration` record:
```csharp
public record HealthProbeConfiguration(int Port = 5555, int RefreshSeconds = 1);
```

### Key Conventions

- All logging via `LogMessages.cs` using `LoggerMessage` source generator (zero-allocation, AOT-safe) — never `Console.*`
- `BooleanExtensions.MatchAsync()` used for pattern matching on health status
- `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt` track the public API surface — approval tests (`TcpHealthProbeServiceApprovalTests`) will catch accidental breaking changes
- AOT-compatible: `PublishAot=true`, `EnableConfigurationBindingGenerator=true`
- Release builds are warning-free; SonarAnalyzer, PublicApiAnalyzers, and BannedApiAnalyzers all run

### Testing Patterns

- **Strict mocking**: `MockBehavior.Strict` throughout
- **Approval tests**: `Verify.Xunit` snapshots for public API surface in `tests/UnitTests/ApprovedApi/`
- **FluentAssertions** for assertions, **AutoFixture** for random data

### CI/CD

- `build-publish.yaml`: Build → Test → NuGet publish to GitHub Packages (innago-property-management org)
- `merge-checks.yaml`: License compliance scan on PRs
- `auto-pr.yaml`: Auto-creates PRs for commits to non-main branches
- Publishing uses `--no-gpg-sign` for CI commits (distinguishes from human commits)

## Upcoming Work

`.specify/features/dynamic-health-checks/` documents planned expansion:
- RabbitMQ health check with tri-strategy connection resolution
- PostgreSQL (Npgsql) health check with same tri-strategy pattern
- Eventual repo rename to `innago-health-checks`
