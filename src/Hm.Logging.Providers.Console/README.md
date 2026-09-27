# HM Logging Console Provider

`HDev.Hm.Logging.Providers.Console` writes normalized `HDev.Hm.Logging.Core` entries to stdout and
stderr as human-readable text or structured JSON. Package publication remains disabled until the
release unit is explicitly approved.

## Registration

Register Core and Console independently through `IServiceCollection`:

```csharp
using Hm.Logging.Extensions;
using Hm.Logging.Providers.Console.Configuration;

services
    .AddHmLogging()
    .AddLoggingConsole(options =>
    {
        options.Format = ConsoleOutputFormat.Text;
        options.TimestampFormat = ConsoleTimestampFormat.DateTime;
        options.UseColors = true;
        options.UseStandardErrorForErrors = true;
        options.ExceptionFormat = ConsoleExceptionFormat.Multiline;
    });
```

`AddLoggingConsole` registers one singleton `ILogProvider`. It does not register or replace the Core
logging pipeline, so call `AddHmLogging` separately as shown above.

## Configuration

| Option | Values | Default | Applies to |
| --- | --- | --- | --- |
| `Format` | `Text`, `Json` | `Text` | All output |
| `TimestampFormat` | `Iso8601`, `DateTime`, `Time` | `DateTime` | Text only |
| `UseColors` | `true`, `false` | `true` | Text only |
| `UseStandardErrorForErrors` | `true`, `false` | `true` | Stream routing |
| `ExceptionFormat` | `Multiline`, `Compact` | `Multiline` | Text only |

Text timestamps use UTC and the following representations:

- `Iso8601`: ISO 8601 round-trip format
- `DateTime`: `yyyy-MM-dd HH:mm:ss`
- `Time`: `HH:mm:ss.fff`

With standard-error routing enabled, Error and Critical entries go to stderr; Warning and lower
levels go to stdout. Disabling it sends every level to stdout.

## Output behavior

Text output presents the header first, followed by available trace/correlation values, metadata, and
exception text. Missing optional values are omitted. Metadata keys use ordinal ordering and stable,
culture-independent value formatting. Compact exception formatting changes line breaks in the opaque
exception text to ` | ` without flattening the rest of the entry.

JSON output uses the Core `LogEntry` property names without camel-casing, represents `Level` by name,
and always uses an ISO 8601 main timestamp. Optional null values and empty metadata are omitted.
Metadata retains the normalized value types supplied by Core; temporal values use stable string
representations. Non-finite floating-point values are safely represented as the strings `NaN`,
`Infinity`, and `-Infinity` because JSON has no native non-finite numeric values.

Console writes are cancellation-aware. Destination failures propagate to Core, which retains
responsibility for provider-failure isolation and callbacks.
