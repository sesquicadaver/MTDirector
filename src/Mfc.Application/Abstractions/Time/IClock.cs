namespace Mfc.Application.Abstractions.Time;

/// <summary>UTC clock for expiry and other application-time decisions (M2-08).</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

/// <summary>Production clock using <see cref="DateTimeOffset.UtcNow"/>.</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
