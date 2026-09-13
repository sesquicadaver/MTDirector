using System.Globalization;
using Mfc.RouterOs.Discovery;

namespace Mfc.RouterOs.Deployment;

/// <summary>
/// Parses RouterOS <c>/system/clock</c> discovery into a <see cref="DateTimeOffset"/> for watchdog deadlines
/// (Safe Deployment Spec §25 / AUDIT-DEP-02).
/// </summary>
public static class RouterOsClockParser
{
    /// <summary>
    /// Builds wall-clock time from RouterOS date/time/gmt-offset fields.
    /// Accepts MikroTik <c>MMM/dd/yyyy</c> and ISO <c>yyyy-MM-dd</c> dates.
    /// </summary>
    public static DateTimeOffset Parse(SystemClockDiscovery clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        if (string.IsNullOrWhiteSpace(clock.Date) || string.IsNullOrWhiteSpace(clock.Time))
        {
            throw new InvalidOperationException("RouterOS clock date/time is required for watchdog arming.");
        }

        if (!TryParseDate(clock.Date.Trim(), out DateOnly date))
        {
            throw new InvalidOperationException($"Unrecognized RouterOS clock date '{clock.Date}'.");
        }

        if (!TimeOnly.TryParseExact(
                clock.Time.Trim(),
                ["HH:mm:ss", "H:mm:ss", "HH:mm", "H:mm"],
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out TimeOnly time))
        {
            throw new InvalidOperationException($"Unrecognized RouterOS clock time '{clock.Time}'.");
        }

        TimeSpan offset = ParseGmtOffset(clock.GmtOffset);
        DateTime local = date.ToDateTime(time, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, offset);
    }

    private static bool TryParseDate(string raw, out DateOnly date)
    {
        if (DateOnly.TryParseExact(
                raw,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out date))
        {
            return true;
        }

        // RouterOS prints lowercase month abbreviations (sep/13/2026).
        string monthTitle = raw.Length >= 3
            ? char.ToUpperInvariant(raw[0]) + raw[1..].ToLowerInvariant()
            : raw;
        return DateOnly.TryParseExact(
            monthTitle,
            ["MMM/dd/yyyy", "MMM/d/yyyy"],
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces,
            out date);
    }

    private static TimeSpan ParseGmtOffset(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return TimeSpan.Zero;
        }

        string normalized = raw.Trim();
        if (normalized.StartsWith("gmt", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[3..].Trim();
        }

        if (normalized.StartsWith('+'))
        {
            normalized = normalized[1..];
        }

        if (normalized.Equals("00:00", StringComparison.Ordinal)
            || normalized.Equals("0", StringComparison.Ordinal))
        {
            return TimeSpan.Zero;
        }

        if (TimeSpan.TryParse(normalized, CultureInfo.InvariantCulture, out TimeSpan parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException($"Unrecognized RouterOS gmt-offset '{raw}'.");
    }
}
