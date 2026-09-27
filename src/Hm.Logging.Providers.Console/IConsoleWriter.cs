namespace Hm.Logging.Providers.Console;

internal interface IConsoleWriter
{
    ValueTask WriteAsync(bool standardError, string value, CancellationToken cancellationToken);
}
