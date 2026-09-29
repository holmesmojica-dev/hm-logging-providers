# Hm.Logging Files Provider

`HDev.Hm.Logging.Providers.Files` persists normalized HM Logging entries to provider-managed files. HM Logging
Core remains responsible for validation, normalization, context enrichment, provider orchestration, and
provider-failure isolation.

## Installation

Install the package from NuGet when a Files release is available:

```bash
dotnet add package HDev.Hm.Logging.Providers.Files --prerelease
```

## Registration

Register Core and Files explicitly:

```csharp
using Hm.Logging.Extensions;

services
    .AddHmLogging()
    .AddLoggingFiles();
```

Configure storage when the defaults are not appropriate:

```csharp
using Hm.Logging.Extensions;
using Hm.Logging.Providers.Files.Configuration;

services.AddLoggingFiles(options =>
{
    options.DirectoryPath = "/var/log/my-application";
    options.GroupBySource = true;
    options.RetentionDays = 14;
    options.MaximumFileSize = FileSize.FromMB(50);
    options.MaximumTotalSize = FileSize.FromGB(2);
    options.Format = FilesLogFormat.Json;
});
```

`AddLoggingFiles` registers one singleton `ILogProvider`. Configuration is captured during registration and is
not hot-reloaded.

## Defaults

| Option | Default |
| --- | --- |
| `DirectoryPath` | `logs` |
| `GroupBySource` | `false` |
| `RetentionDays` | `30` |
| `MaximumFileSize` | 100 MB (decimal SI) |
| `MaximumTotalSize` | `null` |
| `Format` | `Json` |

Null, empty, or whitespace-only `DirectoryPath` values normalize to `logs`. `FileSize` uses bytes internally;
KB, MB, and GB factories use decimal SI units.

## Output and naming

JSON is UTF-8 without BOM and uses JSON Lines: one complete object followed by `\n` for each entry. Text output
is also UTF-8 without BOM. UTC controls maintenance, logical dates, and filenames:

```text
logs/logs-2026-09-28.jsonl
logs/logs-2026-09-28.1.jsonl
```

With `GroupBySource = true`, Sources map to deterministic safe directory and filename prefixes. Missing Sources
use `unknown`:

```text
logs/payments/payments-2026-09-28.jsonl
logs/unknown/unknown-2026-09-28.jsonl
```

The maximum file size is strict and includes the complete serialized entry and line terminator. An entry larger
than the configured limit is rejected. Otherwise, an entry that cannot fit in the current segment starts the next
numbered segment.

## Retention and capacity

Maintenance runs lazily on the first write of each UTC day and on the first write after startup. Retention removes
complete recognized UTC days; foreign files are not deleted. A configured total-size limit applies only to
recognized Files-managed logs. To admit a write, eligible older days are deleted oldest-first, but the current UTC
day is never deleted for capacity.

The configured root is provider-managed storage. External modifications while the provider is active may make
in-memory capacity accounting temporarily stale; the next daily reconciliation repairs it. The root itself is
never automatically removed.

## Operational boundaries

Writes to the same logical file stream are serialized within one provider instance. Files does not provide a
distributed lock for multiple processes sharing a path. It does not add background workers, timers, queues,
batching, retries, or per-entry `fsync`. Cancellation and filesystem or capacity failures propagate to Core.
