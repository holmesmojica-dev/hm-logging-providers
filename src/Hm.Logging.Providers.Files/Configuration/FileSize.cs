namespace Hm.Logging.Providers.Files.Configuration;

/// <summary>
/// Represents a strictly positive file size using an unsigned byte count.
/// </summary>
public readonly record struct FileSize
{
    private const decimal Kilobyte = 1_000m;
    private const decimal Megabyte = 1_000_000m;
    private const decimal Gigabyte = 1_000_000_000m;

    private FileSize(ulong bytes)
    {
        Bytes = bytes;
    }

    /// <summary>
    /// Gets the size in bytes.
    /// </summary>
    public ulong Bytes { get; }

    /// <summary>
    /// Creates a file size from a positive byte count.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="bytes"/> is zero.</exception>
    public static FileSize FromBytes(ulong bytes)
    {
        ArgumentOutOfRangeException.ThrowIfZero(bytes);
        return new FileSize(bytes);
    }

    /// <summary>
    /// Creates a file size from decimal SI kilobytes, where one KB is 1,000 bytes.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value does not produce a positive whole byte count.</exception>
    /// <exception cref="OverflowException">Thrown when the converted byte count exceeds <see cref="ulong.MaxValue"/>.</exception>
    public static FileSize FromKB(decimal kilobytes)
    {
        return FromDecimalUnit(kilobytes, Kilobyte, nameof(kilobytes));
    }

    /// <summary>
    /// Creates a file size from decimal SI megabytes, where one MB is 1,000,000 bytes.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value does not produce a positive whole byte count.</exception>
    /// <exception cref="OverflowException">Thrown when the converted byte count exceeds <see cref="ulong.MaxValue"/>.</exception>
    public static FileSize FromMB(decimal megabytes)
    {
        return FromDecimalUnit(megabytes, Megabyte, nameof(megabytes));
    }

    /// <summary>
    /// Creates a file size from decimal SI gigabytes, where one GB is 1,000,000,000 bytes.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value does not produce a positive whole byte count.</exception>
    /// <exception cref="OverflowException">Thrown when the converted byte count exceeds <see cref="ulong.MaxValue"/>.</exception>
    public static FileSize FromGB(decimal gigabytes)
    {
        return FromDecimalUnit(gigabytes, Gigabyte, nameof(gigabytes));
    }

    private static FileSize FromDecimalUnit(decimal value, decimal multiplier, string parameterName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "File size must be greater than zero.");
        }

        decimal bytes = checked(value * multiplier);
        return bytes != decimal.Truncate(bytes)
            ? throw new ArgumentOutOfRangeException(parameterName, value, "File size must resolve to a whole number of bytes.")
            : FromBytes(checked((ulong)bytes));
    }
}
