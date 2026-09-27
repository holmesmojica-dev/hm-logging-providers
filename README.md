# Hm.Logging.Providers

Official destination adapters for the [HM Logging Core](https://github.com/holmesmojica-dev/hm-logging) pipeline.

Core owns log validation, normalization, context enrichment, provider orchestration, cancellation flow, and provider-failure isolation. Each provider package depends on Core and adapts the resulting `LogEntry` to exactly one destination. Providers do not depend on Contracts, Service, or one another.

## Package model

Providers are independently consumable and independently versioned. Applications install Core and only the destination packages they need; provider packages will register themselves through chainable `IServiceCollection` methods named `AddLoggingXxx(...)` when their implementations are available.

## Provider status

| Provider | Package | Status |
| --- | --- | --- |
| Console | `HDev.Hm.Logging.Providers.Console` | V1 implemented; publication remains disabled pending approval |
| Files | — | Planned |
| ElasticSearch | — | Planned; detailed architecture pending |
| EntityFramework | — | Planned; detailed architecture pending |

Console V1 provides Text and Json output, configurable text timestamps and exception presentation,
deterministic structured metadata, ANSI text colors, and severity-based stdout/stderr routing. See the
[Console package documentation](src/Hm.Logging.Providers.Console/README.md) for configuration and usage.

## Development

Run the complete local validation baseline:

```powershell
./scripts/validate.ps1
```

Enable the repository-managed pre-commit validation hook:

```powershell
./scripts/install-hooks.ps1
```

Release publication remains disabled for the Console release unit until it is explicitly approved.
