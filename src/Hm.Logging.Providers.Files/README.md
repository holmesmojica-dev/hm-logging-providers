# HM Logging Files Provider

[![NuGet](https://img.shields.io/nuget/vpre/HDev.Hm.Logging.Providers.Files?label=nuget)](https://www.nuget.org/packages/HDev.Hm.Logging.Providers.Files)
[![Quality Gate](https://sonarcloud.io/api/project_badges/measure?project=holmesmojica-dev_hm-logging-providers&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=holmesmojica-dev_hm-logging-providers)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=holmesmojica-dev_hm-logging-providers&metric=coverage)](https://sonarcloud.io/summary/new_code?id=holmesmojica-dev_hm-logging-providers)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](https://github.com/holmesmojica-dev/hm-logging-providers/blob/main/LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4)](https://dotnet.microsoft.com/)

Quality and coverage badges report the repository's main-branch SonarQube Cloud analysis.

`HDev.Hm.Logging.Providers.Files` persists normalized HM Logging entries to local, provider-managed files. It is
useful for applications that need durable logs on a host filesystem, such as console applications, ASP.NET Core
applications, Worker Services, Linux services, scheduled jobs, and environments that collect files through a
separate log shipper or operational process.

Files is a destination adapter for [HM Logging Core](https://github.com/holmesmojica-dev/hm-logging). Core owns
entry validation and normalization, context enrichment, provider orchestration, cancellation flow, and
provider-failure isolation. Files owns only file serialization, naming, rotation, retention, capacity, and the
destination write.

## Installation

Install the latest available release, including preview releases, from NuGet with:

```bash
dotnet add package HDev.Hm.Logging.Providers.Files --prerelease
```

Installing the package does not register HM Logging Core or the Files provider automatically.

## Quick Start

Register Core and Files explicitly during service configuration:

```csharp
using Hm.Logging.Extensions;

builder.Services
    .AddHmLogging()
    .AddLoggingFiles();
```

Inject Core's `ILoggerService` and write entries through the HM Logging pipeline:

```csharp
using Hm.Logging.Abstractions;
using Hm.Logging.Models;

public sealed class CheckoutService(ILoggerService logger)
{
    public Task ConfirmOrderAsync(CancellationToken cancellationToken)
    {
        LogEntry entry = LogEntry.Info("Order accepted") with
        {
            Source = "checkout",
            CorrelationId = "order-1042"
        };

        return logger.LogAsync(entry, cancellationToken: cancellationToken);
    }
}
```

With the defaults, this entry is appended as JSON Lines to
`logs/logs-<current-UTC-date>.jsonl`. The application writes through Core rather than calling the provider
directly, so Core can normalize the entry and coordinate all registered providers.

`AddLoggingFiles` registers one singleton `ILogProvider`; it does not call or replace `AddHmLogging`. Options are
validated and captured during registration for that singleton's lifetime. Files does not provide configuration
hot reload.

## Configuration

Configure every Files option through `AddLoggingFiles`:

```csharp
using Hm.Logging.Extensions;
using Hm.Logging.Providers.Files.Configuration;

builder.Services
    .AddHmLogging()
    .AddLoggingFiles(options =>
    {
        options.DirectoryPath = "/var/log/my-application";
        options.GroupBySource = true;
        options.RetentionDays = 14;
        options.MaximumFileSize = FileSize.FromMB(50);
        options.MaximumTotalSize = FileSize.FromGB(2);
        options.Format = FilesLogFormat.Clef;
    });
```

| Option | Values | Default | Behavior |
| --- | --- | --- | --- |
| `DirectoryPath` | Filesystem path | `logs` | Selects the provider-managed root. Null, empty, or whitespace-only values normalize to `logs`; relative paths are resolved when registration captures the options. |
| `GroupBySource` | `true`, `false` | `false` | Places each Source in its own safe deterministic directory and filename prefix when enabled. |
| `RetentionDays` | `uint` or `null` | `30` | Retains recognized logs by complete UTC date. `null` disables age cleanup; `0` removes every recognized prior-date log. |
| `MaximumFileSize` | `FileSize` or `null` | 100 MB | Sets a strict physical-file limit and enables numbered segments. `null` disables size-based segmentation. |
| `MaximumTotalSize` | `FileSize` or `null` | `null` | Limits the combined size of recognized Files-managed logs. `null` disables total-capacity enforcement. |
| `Format` | `Json`, `Text`, `Clef` | `Json` | Selects HM Logging JSON Lines (`.jsonl`), human-readable Text (`.log`), or Compact Log Event Format JSON Lines (`.clef`). |

`FileSize` stores an unsigned byte count. `FromKB`, `FromMB`, and `FromGB` use decimal SI units: 1 KB is 1,000
bytes, 1 MB is 1,000,000 bytes, and 1 GB is 1,000,000,000 bytes. Values must be greater than zero and must
convert to a whole number of bytes. `FromBytes` is available when an exact byte limit is required.

Unsupported `FilesLogFormat` values and zero-valued file-size limits fail during configuration with
`ArgumentOutOfRangeException`.

### Common configurations

Export CLEF, an open structured event format compatible with monitoring tools such as Seq:

```csharp
builder.Services.AddLoggingFiles(options =>
{
    options.Format = FilesLogFormat.Clef;
});
```

Partition files by source while retaining the default JSON format:

```csharp
builder.Services.AddLoggingFiles(options =>
{
    options.GroupBySource = true;
    options.RetentionDays = 14;
});
```

Use human-readable Text files:

```csharp
builder.Services.AddLoggingFiles(options =>
{
    options.Format = FilesLogFormat.Text;
});
```

Bound both individual files and total managed storage:

```csharp
builder.Services.AddLoggingFiles(options =>
{
    options.MaximumFileSize = FileSize.FromMB(25);
    options.MaximumTotalSize = FileSize.FromGB(1);
});
```

## Output formats

All formats use UTF-8 without a byte-order mark. Each serialized entry ends with a line feed (`\n`). Optional
entry fields are omitted when absent, and empty metadata is omitted. `Json` and `Clef` each write one complete
JSON object per physical line.

| Format | Extension |
| --- | --- |
| `Json` | `.jsonl` |
| `Text` | `.log` |
| `Clef` | `.clef` |

### JSON Lines

JSON is the default format. Each physical line contains one complete compact JSON object, making `.jsonl` files
suitable for streaming readers and log shippers. A representative line is:

```json
{"Message":"Order accepted","Level":"Information","Timestamp":"2026-09-28T10:30:00.0000000Z","Source":"checkout","TraceId":"4bf92f3577b34da6a3ce929d0e0e4736","CorrelationId":"order-1042","Metadata":{"elapsedMs":18,"region":"north"}}
```

JSON uses `LogEntry` property names and semantic level names. Timestamps use the ISO 8601 round-trip format in
UTC. Metadata keys use ordinal ordering, and supported normalized value types retain their JSON types.
`DateTime`, `DateTimeOffset`, and `TimeSpan` metadata use stable string representations. Because JSON has no
native non-finite numbers, `NaN`, positive infinity, and negative infinity are represented as the strings
`"NaN"`, `"Infinity"`, and `"-Infinity"`.

### CLEF

CLEF is the open Compact Log Event Format. Select `FilesLogFormat.Clef` to write newline-delimited CLEF events
to `.clef` files. The extension distinguishes CLEF from the provider's default JSON schema; the content remains
one valid JSON object per line. CLEF-aware tools such as Seq can interpret its reserved properties. For example:

```json
{"@t":"2026-09-28T10:30:00.0000000Z","@m":"Order accepted","@l":"Information","@tr":"4bf92f3577b34da6a3ce929d0e0e4736","CorrelationId":"order-1042","Source":"checkout","Metadata":{"elapsedMs":18,"region":"north"}}
```

Files uses only CLEF fields backed by HM Logging data:

| HM Logging value | CLEF property | Behavior |
| --- | --- | --- |
| `Timestamp` | `@t` | ISO 8601 timestamp in UTC. |
| `Message` | `@m` | The rendered HM Logging message. Files does not invent a message template. |
| `Level` | `@l` | `Trace` maps to `Verbose`, `Critical` maps to `Fatal`, and the remaining levels retain their semantic names. |
| `Exception` | `@x` | Preserves the available textual exception representation, including type, message, and stack trace when supplied by the application. |
| compatible `TraceId` | `@tr` | Exactly 32 hexadecimal characters, not all zero; normalized to lowercase. |
| incompatible `TraceId` | `TraceId` | Preserved completely as an ordinary property; it is never truncated, replaced, or discarded. |
| `CorrelationId` | `CorrelationId` | Preserved when present. |
| `Source` | `Source` | Preserved when present. |
| `Metadata` | `Metadata` | Preserved as a nested object with deterministic key ordering and the same JSON value handling as the default JSON format. |

Core resolves active scopes into the normalized entry before provider dispatch. Scope-derived Source, trace,
correlation, and metadata values therefore appear through the fields above. The current Core `LogEntry` contract
does not carry a separate `Scopes` member, so Files does not invent an independent `Scopes` property.

Files does not emit reserved CLEF properties without corresponding HM Logging semantics, including `@mt`,
`@i`, `@r`, `@sp`, `@ps`, `@st`, `@sc`, `@ra`, and `@sk`.

### Text

Text produces a human-readable `.log` representation. Header values appear first, followed by available
trace/correlation values, metadata, and exception text. For example:

```text
[2026-09-28T10:30:00.0000000Z] [Information] [checkout] Order accepted
TraceId: 4bf92f3577b34da6a3ce929d0e0e4736
CorrelationId: order-1042
Metadata: elapsedMs=18, region="north"
```

Metadata keys use ordinal ordering and values use culture-independent representations. String and character
metadata are JSON-quoted so delimiters and line breaks remain unambiguous. Exception text is appended under an
`Exception:` label and preserves its supplied line structure.

## File organization and naming

The provider's current UTC date selects the active daily file. The default, ungrouped layout is:

```text
logs/logs-2026-09-28.jsonl
logs/logs-2026-09-28.1.jsonl
logs/logs-2026-09-28.2.jsonl
```

The first file has no segment suffix. Rotation adds `.1`, `.2`, and subsequent numeric segments before the
format extension. JSON uses `.jsonl`, Text uses `.log`, and CLEF uses `.clef`, for example:

```text
logs/logs-2026-09-28.clef
logs/logs-2026-09-28.1.clef
```

With `GroupBySource = true`, each Source is converted to a deterministic filesystem-safe directory and filename
prefix:

```text
logs/checkout/checkout-2026-09-28.jsonl
logs/payments-api/payments-api-2026-09-28.jsonl
logs/unknown/unknown-2026-09-28.jsonl
```

Missing, empty, or whitespace-only Sources use `unknown`. Normalization prevents path traversal, handles reserved
filesystem names, and bounds long representations. Different processes should still use separate managed roots;
source normalization is not a cross-process locking mechanism.

## Validated interoperability

The following combinations have been exercised with Files output:

- **Fluent Bit:** ingestion of the default JSON Lines format, including rotated files and files grouped by
  Source.
- **Seq:** native interpretation of the CLEF event representation, including timestamp, severity, rendered
  message, exception text, and compatible TraceId.
- **Grafana Loki with Alloy:** ingestion of the default JSON Lines format, structured filters, and dashboards.
- **Elasticsearch with Kibana and Filebeat:** JSON Lines indexing plus searches and filters over structured
  fields.

Direct integration of the `.clef` files generated by this implementation still requires validation. In
particular, continuous file ingestion into Seq has not been configured by this package. Fluent Bit, Alloy/Loki,
and Filebeat/Elasticsearch can ingest JSON content when their paths and parsers are configured appropriately,
but the validated scenarios above used the default `.jsonl` output rather than `.clef`. Files only produces
files; deployment and lifecycle of ingestion agents remain the consumer's responsibility.

For ungrouped JSON files, configure file agents against a pattern equivalent to:

```text
logs/*.jsonl
```

With `GroupBySource = true`, use a pattern equivalent to:

```text
logs/*/*.jsonl
```

For CLEF, use the corresponding patterns:

```text
logs/*.clef
logs/*/*.clef
```

Adapt the root to the path visible inside the ingestion agent's host or container. These patterns include the
numbered rotated files because their final extension remains `.jsonl` or `.clef`.

### UTC behavior

UTC governs:

- the current daily file and its `yyyy-MM-dd` filename;
- rotation into a new daily stream;
- retention boundaries;
- identification of older dates eligible for capacity cleanup; and
- serialized entry timestamps, after Core normalization.

The provider's current UTC date selects the physical file; an entry's historical timestamp does not redirect it
to an older filename. Maintenance runs lazily on the first write after provider startup and on the first write of
each subsequent UTC date. Files does not use a timer or background worker.

## Storage limits and maintenance

### Maximum file size

`MaximumFileSize` is strict and includes the complete serialized entry plus its terminating line feed. If an
entry fits in an empty file but not in the active segment, Files starts the next numbered segment before writing
it. If the entry itself exceeds the configured maximum, the write is rejected with `InvalidOperationException`;
Files never creates an intentionally oversized segment. Set the option to `null` to keep one physical file per
logical UTC-day stream without size-based segmentation.

### Retention

`RetentionDays` applies to complete UTC dates represented by recognized Files-managed logs. Cleanup preserves
the current and future UTC dates. The default is 30, `null` disables age cleanup, and `0` makes every prior UTC
date eligible. Recognized `.jsonl`, `.log`, and `.clef` files share this provider-managed retention contract;
unrecognized files are not removed.

### Total capacity

`MaximumTotalSize` counts only files matching the Files naming and directory layouts described above, including
JSON Lines, Text, CLEF, and numbered segments. Foreign files under the root are neither counted nor deleted.

When a new entry would exceed the limit, Files deletes eligible older UTC dates as complete groups, oldest first.
It never deletes the current UTC date to admit a write. If deleting older dates is insufficient, or if the entry
itself exceeds the total limit, the write is rejected with `InvalidOperationException`.

The configured root is provider-managed storage. External changes while the provider is running can make its
in-memory capacity accounting temporarily stale; the next daily reconciliation repairs the accounting. Files
never removes the configured root itself.

## Concurrency, lifecycle, and failures

Writes to the same logical file stream are serialized within one provider instance, so complete entries do not
interleave. Independent streams may progress independently, and no global ordering between different Sources or
UTC dates is promised.

Dependency injection normally owns the singleton provider's lifetime. Disposal stops admission of new writes,
waits for already-admitted writes to finish, and then releases synchronization resources.

Files honors cancellation while waiting for and performing destination work. Cancellation propagates through
Core. Filesystem, permission, serialization, and configured-capacity failures are reported to Core, which owns
provider-failure isolation and the optional failure callback. Files does not silently retry failed writes. If
an append fails after changing a file, Files attempts to restore the file's previous length while retaining the
same in-process stream lock, and always reports the original failure.

## Operational limitations

- Coordination and locking are local to one provider instance. Files does not provide distributed or
  cross-process locking; multiple processes must not share the same managed root.
- Files does not add queues, batching, buffering, timers, background workers, retries, or a per-entry `fsync`
  policy.
- Append recovery is best-effort truncation to the length observed immediately before the write. It does not
  provide transactional filesystem semantics or recovery from process termination, operating-system failure,
  storage failure that also prevents truncation, or external modification of the active file.
- The managed root should not be modified externally while the provider is active. When external tooling needs
  to read logs, it should avoid renaming, deleting, or rewriting active Files-managed paths.
- Retention and total-capacity maintenance are write-driven. A process that stops writing does not perform
  periodic cleanup.
