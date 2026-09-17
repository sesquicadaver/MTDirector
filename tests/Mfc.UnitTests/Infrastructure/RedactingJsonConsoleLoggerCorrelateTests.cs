using System.Diagnostics;
using System.Text.Json;
using Mfc.Infrastructure.Persistence.Logging;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Mfc.UnitTests.Infrastructure;

/// <summary>
/// CTRL-LOG-OTEL-CORRELATE-01: redacted JSON console logs include Activity TraceId/SpanId when present.
/// </summary>
public sealed class RedactingJsonConsoleLoggerCorrelateTests
{
    [Fact]
    public void LogIncludesTraceIdAndSpanIdWhenActivityCurrentPresent()
    {
        using Activity activity = new("ctrl-log-otel-correlate-01");
        activity.Start();

        string line = CaptureLogLine(LogLevel.Information, "correlate-me");

        using JsonDocument doc = JsonDocument.Parse(line);
        JsonElement root = doc.RootElement;
        Assert.Equal(activity.TraceId.ToString(), root.GetProperty("traceId").GetString());
        Assert.Equal(activity.SpanId.ToString(), root.GetProperty("spanId").GetString());
        Assert.Equal("correlate-me", root.GetProperty("message").GetString());
    }

    [Fact]
    public void LogOmitsTraceFieldsWhenNoActivity()
    {
        Activity.Current = null;

        string line = CaptureLogLine(LogLevel.Warning, "no-activity");

        using JsonDocument doc = JsonDocument.Parse(line);
        JsonElement root = doc.RootElement;
        Assert.False(root.TryGetProperty("traceId", out _));
        Assert.False(root.TryGetProperty("spanId", out _));
        Assert.Equal("no-activity", root.GetProperty("message").GetString());
    }

    [Fact]
    public void LogStillRedactsSecretsWithTraceFields()
    {
        using Activity activity = new("ctrl-log-otel-correlate-redact");
        activity.Start();

        string line = CaptureLogLine(LogLevel.Error, "Password=secret-value; Host=db.example");

        using JsonDocument doc = JsonDocument.Parse(line);
        JsonElement root = doc.RootElement;
        Assert.Equal(activity.TraceId.ToString(), root.GetProperty("traceId").GetString());
        Assert.Equal(activity.SpanId.ToString(), root.GetProperty("spanId").GetString());
        string message = root.GetProperty("message").GetString() ?? string.Empty;
        Assert.DoesNotContain("secret-value", message, StringComparison.Ordinal);
        Assert.Contains("Password=***", message, StringComparison.Ordinal);
    }

    private static string CaptureLogLine(LogLevel level, string message)
    {
        using StringWriter writer = new();
        TextWriter original = Console.Out;
        Console.SetOut(writer);
        try
        {
            using RedactingJsonConsoleLoggerProvider provider = new();
            ILogger logger = provider.CreateLogger("Mfc.UnitTests.Correlate");
            logger.Log(
                level,
                eventId: default,
                state: message,
                exception: null,
                formatter: static (state, _) => state);
        }
        finally
        {
            Console.SetOut(original);
        }

        string output = writer.ToString().Trim();
        Assert.False(string.IsNullOrWhiteSpace(output));
        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[^1];
    }
}
