namespace Mfc.RouterOs.Session;

/// <summary>Session limits and deadlines (Read Adapter Spec §12–13).</summary>
public sealed class RosSessionOptions
{
    public static RosSessionOptions Default { get; } = new();

    public int MaxPendingCommands { get; init; } = 16;

    public TimeSpan DefaultCommandTimeout { get; init; } = TimeSpan.FromSeconds(30);

    public TimeSpan CancelGracePeriod { get; init; } = TimeSpan.FromSeconds(2);

    public int MaxRecordsPerCommand { get; init; } = 10_000;

    public int MaxPayloadBytesPerCommand { get; init; } = 2 * 1024 * 1024;

    public Protocol.ApiSentenceLimits SentenceLimits { get; init; } = Protocol.ApiSentenceLimits.Default;
}
