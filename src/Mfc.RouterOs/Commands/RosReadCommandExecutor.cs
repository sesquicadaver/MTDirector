using System.Text;
using Mfc.RouterOs.Protocol;
using Mfc.RouterOs.Redaction;
using Mfc.RouterOs.Session;

namespace Mfc.RouterOs.Commands;

/// <summary>
/// Executes allowlisted RouterOS read commands by <see cref="RosReadCommandId"/> only.
/// Callers cannot supply a free-form path or UI-built query filter.
/// </summary>
public sealed class RosReadCommandExecutor
{
    public const string TrapErrorCode = "ROS_TRAP";
    public const string FatalErrorCode = "API_FATAL";
    public const string LimitExceededCode = "ROS_READ_LIMIT";

    /// <summary>
    /// Runs a typed read command on an authenticated session.
    /// <c>!trap</c> becomes a typed error; <c>!fatal</c> leaves the session faulted.
    /// </summary>
    public static async Task<RosReadCommandResult> ExecuteAsync(
        RosSession session,
        RosReadCommandId commandId,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        RosReadCommandDefinition definition = RosReadCommandRegistry.Get(commandId);

        List<(string Name, string Value)> attributes = [];
        foreach ((string name, string value) in definition.QueryProfile.PrintArguments)
        {
            attributes.Add((name, value));
        }

        (string Name, string Value)[] apiAttributes =
        [
            ("proplist", definition.PropertyProfile.ProplistValue),
        ];

        RosCommandResult sessionResult = await session.ExecuteAsync(
            definition.FixedPath,
            attributes.Count == 0 ? null : attributes,
            apiAttributes,
            definition.QueryProfile.QueryWords.Count == 0 ? null : definition.QueryProfile.QueryWords,
            timeout,
            cancellationToken).ConfigureAwait(false);

        return MapResult(definition, session, sessionResult);
    }

    private static RosReadCommandResult MapResult(
        RosReadCommandDefinition definition,
        RosSession session,
        RosCommandResult sessionResult)
    {
        bool sessionInvalidated = session.IsFaulted
            || sessionResult.Lifecycle == RosCommandLifecycle.Faulted
            || (sessionResult.Error?.Code == FatalErrorCode);

        if (sessionResult.Records.Count > definition.MaxRecords)
        {
            return Fail(
                definition,
                sessionResult,
                sessionInvalidated,
                LimitExceededCode,
                $"Command '{definition.Id}' exceeded max records ({definition.MaxRecords}).");
        }

        int payloadBytes = 0;
        List<RosReadRecord> records = new(sessionResult.Records.Count);
        foreach (RosSentence sentence in sessionResult.Records)
        {
            payloadBytes += sentence.PayloadBytes;
            if (payloadBytes > definition.MaxPayloadBytes)
            {
                return Fail(
                    definition,
                    sessionResult,
                    sessionInvalidated,
                    LimitExceededCode,
                    $"Command '{definition.Id}' exceeded max payload bytes ({definition.MaxPayloadBytes}).");
            }

            records.Add(MapRecord(definition.PropertyProfile, sentence));
        }

        if (sessionResult.Traps.Count > 0)
        {
            string message = SummarizeTraps(sessionResult.Traps);
            return new RosReadCommandResult
            {
                CommandId = definition.Id,
                Lifecycle = sessionResult.Lifecycle,
                Records = records,
                SessionInvalidated = sessionInvalidated,
                Error = new RosReadCommandError
                {
                    Code = TrapErrorCode,
                    Message = message,
                    Traps = sessionResult.Traps,
                },
            };
        }

        if (sessionResult.Error is not null
            || sessionResult.Lifecycle is not RosCommandLifecycle.Completed)
        {
            return new RosReadCommandResult
            {
                CommandId = definition.Id,
                Lifecycle = sessionResult.Lifecycle,
                Records = records,
                SessionInvalidated = sessionInvalidated,
                Error = new RosReadCommandError
                {
                    Code = sessionResult.Error?.Code ?? sessionResult.Lifecycle.ToString(),
                    Message = sessionResult.Error?.Message ?? $"Command ended with {sessionResult.Lifecycle}.",
                    Traps = sessionResult.Traps,
                },
            };
        }

        return new RosReadCommandResult
        {
            CommandId = definition.Id,
            Lifecycle = RosCommandLifecycle.Completed,
            Records = records,
            SessionInvalidated = false,
            Error = null,
        };
    }

    private static RosReadRecord MapRecord(RosPropertyProfile profile, RosSentence sentence)
    {
        Dictionary<string, string> known = new(StringComparer.Ordinal);
        Dictionary<string, string> raw = new(StringComparer.Ordinal);

        foreach (RosAttributeEntry attribute in sentence.Attributes)
        {
            string name = Encoding.ASCII.GetString(attribute.Name.Span);
            if (SensitiveFieldRegistry.IsForbidden(name))
            {
                // Defense in depth: never store forbidden attributes even if the device returns them.
                continue;
            }

            string value = Encoding.UTF8.GetString(attribute.Value.Span);
            if (profile.TryGet(name, out _))
            {
                known[name] = value;
            }
            else
            {
                raw[name] = value;
            }
        }

        return new RosReadRecord
        {
            KnownProperties = known,
            RawProperties = raw,
        };
    }

    private static RosReadCommandResult Fail(
        RosReadCommandDefinition definition,
        RosCommandResult sessionResult,
        bool sessionInvalidated,
        string code,
        string message)
        => new()
        {
            CommandId = definition.Id,
            Lifecycle = RosCommandLifecycle.LimitExceeded,
            Records = Array.Empty<RosReadRecord>(),
            SessionInvalidated = sessionInvalidated,
            Error = new RosReadCommandError
            {
                Code = code,
                Message = message,
                Traps = sessionResult.Traps,
            },
        };

    private static string SummarizeTraps(IReadOnlyList<RosTrap> traps)
    {
        List<string> parts = [];
        foreach (RosTrap trap in traps)
        {
            string? category = null;
            string? message = null;
            foreach (RosAttributeEntry attribute in trap.Attributes)
            {
                string name = Encoding.ASCII.GetString(attribute.Name.Span);
                string value = SensitiveFieldRegistry.RedactForLog(
                    name,
                    Encoding.UTF8.GetString(attribute.Value.Span));
                if (string.Equals(name, "category", StringComparison.Ordinal))
                {
                    category = value;
                }
                else if (string.Equals(name, "message", StringComparison.Ordinal))
                {
                    message = value;
                }
            }

            parts.Add($"category={category ?? "?"}; message={message ?? "?"}");
        }

        return parts.Count == 0 ? "RouterOS returned !trap." : string.Join(" | ", parts);
    }
}

/// <summary>One !re row: known allowlisted properties plus unknown raw bag.</summary>
public sealed class RosReadRecord
{
    public required IReadOnlyDictionary<string, string> KnownProperties { get; init; }

    public required IReadOnlyDictionary<string, string> RawProperties { get; init; }
}

/// <summary>Typed read-command error (trap, fatal, limit, session fault).</summary>
public sealed class RosReadCommandError
{
    public required string Code { get; init; }

    public required string Message { get; init; }

    public required IReadOnlyList<RosTrap> Traps { get; init; }
}

/// <summary>Outcome of an allowlisted read execution.</summary>
public sealed class RosReadCommandResult
{
    public required RosReadCommandId CommandId { get; init; }

    public required RosCommandLifecycle Lifecycle { get; init; }

    public required IReadOnlyList<RosReadRecord> Records { get; init; }

    public required bool SessionInvalidated { get; init; }

    public RosReadCommandError? Error { get; init; }

    public bool IsSuccess => Error is null && Lifecycle == RosCommandLifecycle.Completed;
}
