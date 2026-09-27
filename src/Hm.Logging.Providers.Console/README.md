# HM Logging Console Provider

[![Quality Gate](https://sonarcloud.io/api/project_badges/measure?project=holmesmojica-dev_hm-logging-providers&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=holmesmojica-dev_hm-logging-providers)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=holmesmojica-dev_hm-logging-providers&metric=coverage)](https://sonarcloud.io/summary/new_code?id=holmesmojica-dev_hm-logging-providers)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](https://github.com/holmesmojica-dev/hm-logging-providers/blob/main/LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4)](https://dotnet.microsoft.com/)

Quality and coverage badges report the repository's main-branch SonarQube Cloud analysis.

`HDev.Hm.Logging.Providers.Console` emits normalized HM Logging entries through the application's process
standard output and standard error streams. It is useful for console applications, ASP.NET Core applications,
Worker Services, Linux services, and containerized workloads whose runtime captures stdout/stderr.

This is a destination adapter for [HM Logging Core](https://github.com/holmesmojica-dev/hm-logging), not an
integration with framework-specific visual consoles such as the Unity Editor Console. Core remains responsible
for validation, normalization, context enrichment, orchestration, and provider-failure isolation.

## Installation

Install a published release from NuGet with:

```bash
dotnet add package HDev.Hm.Logging.Providers.Console
```

Installing the package does not register HM Logging Core or the Console provider automatically.

## Registration

Register Core and Console explicitly during service configuration:

```csharp
using Hm.Logging.Extensions;

services
    .AddHmLogging()
    .AddLoggingConsole();
```

The minimal registration uses the Console defaults. Configure only the behavior the application needs:

```csharp
using Hm.Logging.Extensions;
using Hm.Logging.Providers.Console.Configuration;

services
    .AddHmLogging()
    .AddLoggingConsole(options =>
    {
        options.Format = ConsoleOutputFormat.Json;
        options.UseStandardErrorForErrors = true;
    });
```

`AddLoggingConsole` registers Console as a singleton `ILogProvider`; it does not call or replace
`AddHmLogging`. Options are validated and captured during registration for that singleton's lifetime. Console
V1 does not provide configuration hot reload.

## Configuration

| Option | Values | Default | Behavior |
| --- | --- | --- | --- |
| `Format` | `Text`, `Json` | `Text` | Selects the representation emitted for each entry. |
| `TimestampFormat` | `Iso8601`, `DateTime`, `Time` | `DateTime` | Controls only the main Text timestamp. JSON remains ISO 8601. |
| `UseColors` | `true`, `false` | `true` | Adds ANSI level colors to Text. JSON never contains ANSI sequences. |
| `UseStandardErrorForErrors` | `true`, `false` | `true` | Routes Error/Critical to stderr and all lower levels, including Warning, to stdout. `false` sends every level to stdout. |
| `ExceptionFormat` | `Multiline`, `Compact` | `Multiline` | Controls only Text exception presentation. JSON preserves the exception text. |

Text timestamp formats use the normalized UTC entry timestamp:

- `Iso8601`: ISO 8601 round-trip format
- `DateTime`: `yyyy-MM-dd HH:mm:ss`
- `Time`: `HH:mm:ss.fff`

Unsupported enum values fail during configuration with `ArgumentOutOfRangeException`.

## Output

### Text

Text is the default human-readable presentation. It places the available header values first, followed by
trace/correlation values, metadata, and exception text. Missing optional values are omitted. For example:

```text
[2026-09-27 14:05:06] [Information] [checkout] Order accepted
TraceId: 4bf92f3577b34da6a3ce929d0e0e4736
CorrelationId: order-1042
Metadata: elapsedMs=18, region="north"
```

This example illustrates the information presented; exact punctuation and spacing are not a compatibility
contract. Metadata keys use ordinal ordering, and values use stable, culture-independent representations.

When `UseColors` is enabled, Text level names use ANSI colors and reset before subsequent content. Environments
that capture stdout/stderr may preserve ANSI escape sequences; set `UseColors = false` when plain captured logs
are preferable.

`ExceptionFormat.Multiline` preserves the opaque exception text's line structure. `Compact` replaces exception
line breaks with ` | ` separators without flattening the rest of the log entry.

### JSON

JSON uses `LogEntry` property names without camel-casing, semantic level names, and a standardized ISO 8601 main
timestamp regardless of `TimestampFormat`. It omits absent optional fields and empty metadata. A representative
entry is:

```json
{
  "Message": "Order accepted",
  "Level": "Information",
  "Timestamp": "2026-09-27T14:05:06.7890000Z",
  "Source": "checkout",
  "TraceId": "4bf92f3577b34da6a3ce929d0e0e4736",
  "Metadata": {
    "elapsedMs": 18,
    "region": "north"
  }
}
```

The example's top-level property order and whitespace are illustrative, not contractual. Metadata keys are
deterministic and normalized value types are preserved. `DateTime`, `DateTimeOffset`, and `TimeSpan` metadata use
stable string representations. Because JSON has no native non-finite numbers, `NaN`, positive infinity, and
negative infinity are safely represented as the strings `"NaN"`, `"Infinity"`, and `"-Infinity"`.

JSON never emits ANSI color sequences and preserves semantic exception text independently of the Text exception
setting.

## Operational behavior

Each entry is completely rendered before emission. Concurrent entries targeting the same stream are written as
complete logical units without interleaving within one provider instance; stdout and stderr remain independent,
so no global ordering between them is promised.

Console honors cancellation while waiting for and writing to a destination stream. Cancellation and destination
write failures propagate to Core, which owns provider-failure isolation and failure callbacks.

Disposal stops admission of new writes and allows already-admitted writes to finish before releasing provider
resources. Dependency injection normally owns the singleton provider's lifetime and disposal.
