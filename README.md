# Hm.Logging.Providers

Official destination adapters for the [HM Logging Core](https://github.com/holmesmojica-dev/hm-logging) pipeline.

Core owns log validation, normalization, context enrichment, provider orchestration, cancellation flow, and provider-failure isolation. Each provider package depends on Core and adapts the resulting `LogEntry` to exactly one destination. Providers do not depend on Contracts, Service, or one another.

## Package model

Providers are independently consumable and independently versioned. Applications install Core and only the destination packages they need; provider packages will register themselves through chainable `IServiceCollection` methods named `AddLoggingXxx(...)` when their implementations are available.

## Provider status

| Provider | Package | Status |
| --- | --- | --- |
| Console | `HDev.Hm.Logging.Providers.Console` | Foundation only; not published or ready for use |
| Files | — | Planned |
| ElasticSearch | — | Planned; detailed architecture pending |
| EntityFramework | — | Planned; detailed architecture pending |

The repository currently establishes the solution, package, Quality, Delivery, and documentation foundation for Console. It intentionally contains no functional Console provider behavior yet.

## Development

Run the complete local validation baseline:

```powershell
./scripts/validate.ps1
```

Enable the repository-managed pre-commit validation hook:

```powershell
./scripts/install-hooks.ps1
```

Release publication is disabled for the Console release unit until its implementation, tests, and user documentation are complete and explicitly approved.
