namespace Hm.Logging.Providers.Console.Configuration;

internal sealed record ConsoleProviderSettings(
    ConsoleOutputFormat Format,
    ConsoleTimestampFormat TimestampFormat,
    bool UseColors,
    bool UseStandardErrorForErrors,
    ConsoleExceptionFormat ExceptionFormat)
{
    internal static ConsoleProviderSettings FromOptions(ConsoleProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        Validate(options.Format, nameof(options.Format));
        Validate(options.TimestampFormat, nameof(options.TimestampFormat));
        Validate(options.ExceptionFormat, nameof(options.ExceptionFormat));

        return new ConsoleProviderSettings(
            options.Format,
            options.TimestampFormat,
            options.UseColors,
            options.UseStandardErrorForErrors,
            options.ExceptionFormat);
    }

    private static void Validate<TEnum>(TEnum value, string optionName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(
                optionName,
                value,
                $"The Console provider option '{optionName}' has unsupported value '{value}'.");
        }
    }
}
