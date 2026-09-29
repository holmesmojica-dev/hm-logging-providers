namespace Hm.Logging.Providers.Files.Configuration;

/// <summary>
/// Represents a strictly positive file-size limit as an unsigned number of bytes.
/// </summary>
/// <remarks>
/// Use the factory method whose unit matches the configured limit. The KB, MB, and GB factories use decimal SI
/// units and accept fractional values only when the conversion produces a whole number of bytes.
/// </remarks>
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
    /// Gets the exact size represented by this value, in bytes.
    /// </summary>
    public ulong Bytes { get; }

    /// <summary>
    /// Creates a file size from a positive byte count.
    /// </summary>
    /// <param name="bytes">The number of bytes. The value must be greater than zero.</param>
    /// <returns>A file size representing exactly <paramref name="bytes"/> bytes.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="bytes"/> is zero.</exception>
    public static FileSize FromBytes(ulong bytes)
    {
        ArgumentOutOfRangeException.ThrowIfZero(bytes);
        return new FileSize(bytes);
    }

    /// <summary>
    /// Creates a file size from decimal SI kilobytes, where one KB is 1,000 bytes.
    /// </summary>
    /// <param name="kilobytes">The positive number of decimal SI kilobytes to convert.</param>
    /// <returns>The converted file size in whole bytes.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="kilobytes"/> is not positive or does not convert to a whole number of bytes.
    /// </exception>
    /// <exception cref="OverflowException">
    /// Thrown when <paramref name="kilobytes"/> converts to more than <see cref="ulong.MaxValue"/> bytes.
    /// </exception>
    public static FileSize FromKB(decimal kilobytes)
    {
        return FromDecimalUnit(kilobytes, Kilobyte, nameof(kilobytes));
    }

    /// <summary>
    /// Creates a file size from decimal SI megabytes, where one MB is 1,000,000 bytes.
    /// </summary>
    /// <param name="megabytes">The positive number of decimal SI megabytes to convert.</param>
    /// <returns>The converted file size in whole bytes.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="megabytes"/> is not positive or does not convert to a whole number of bytes.
    /// </exception>
    /// <exception cref="OverflowException">
    /// Thrown when <paramref name="megabytes"/> converts to more than <see cref="ulong.MaxValue"/> bytes.
    /// </exception>
    public static FileSize FromMB(decimal megabytes)
    {
        return FromDecimalUnit(megabytes, Megabyte, nameof(megabytes));
    }

    /// <summary>
    /// Creates a file size from decimal SI gigabytes, where one GB is 1,000,000,000 bytes.
    /// </summary>
    /// <param name="gigabytes">The positive number of decimal SI gigabytes to convert.</param>
    /// <returns>The converted file size in whole bytes.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="gigabytes"/> is not positive or does not convert to a whole number of bytes.
    /// </exception>
    /// <exception cref="OverflowException">
    /// Thrown when <paramref name="gigabytes"/> converts to more than <see cref="ulong.MaxValue"/> bytes.
    /// </exception>
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
