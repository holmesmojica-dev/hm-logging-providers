# Hm.Logging.Providers

[![Quality Gate](https://sonarcloud.io/api/project_badges/measure?project=holmesmojica-dev_hm-logging-providers&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=holmesmojica-dev_hm-logging-providers)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=holmesmojica-dev_hm-logging-providers&metric=coverage)](https://sonarcloud.io/summary/new_code?id=holmesmojica-dev_hm-logging-providers)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4)](https://dotnet.microsoft.com/)

Official destination adapters for the [HM Logging Core](https://github.com/holmesmojica-dev/hm-logging)
pipeline.

HM Logging Core owns entry validation and normalization, context enrichment, provider orchestration,
cancellation flow, and provider-failure isolation. This repository supplies destination-specific adapters
that receive those normalized entries, translate them for one destination, and perform the destination write.

## Package model

Each provider is an independently versioned and consumed NuGet package. Applications install Core and only
the providers they need. Registration is explicit through each package's `IServiceCollection` extension;
installing a package does not register Core or its provider automatically.

A normal application composes Core and a provider during service registration. For example, with Console:

```csharp
using Hm.Logging.Extensions;

services
    .AddHmLogging()
    .AddLoggingConsole();
```

Providers do not depend on one another, HM Logging Contracts, or a logging service implementation. Multiple
providers can coexist, and Core dispatches each normalized entry to every registered `ILogProvider`.

## Provider catalog

| Provider | Package | Status |
| --- | --- | --- |
| Console | [`HDev.Hm.Logging.Providers.Console`](https://www.nuget.org/packages/HDev.Hm.Logging.Providers.Console) | Available |
| Files | — | Planned |
| ElasticSearch | — | Planned; detailed architecture pending |
| EntityFramework | — | Planned; detailed architecture pending |

See the [Console provider guide](src/Hm.Logging.Providers.Console/README.md) for its configuration, output,
routing, and operational behavior.

## Development

The solution targets .NET 10. Run the complete local validation baseline, including restore/audit, formatting,
Release build, tests, and release-infrastructure tests:

```powershell
./scripts/validate.ps1 -CollectCoverage
```

Enable the repository-managed pre-commit validation hook:

```powershell
./scripts/install-hooks.ps1
```
