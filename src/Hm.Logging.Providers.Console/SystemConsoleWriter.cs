namespace Hm.Logging.Providers.Console;

internal sealed class SystemConsoleWriter : IConsoleWriter
{
    public ValueTask WriteAsync(bool standardError, string value, CancellationToken cancellationToken)
    {
        TextWriter writer = standardError ? System.Console.Error : System.Console.Out;
        return new ValueTask(writer.WriteAsync(value.AsMemory(), cancellationToken));
    }
}
