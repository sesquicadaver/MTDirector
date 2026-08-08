using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Mfc.Infrastructure.Persistence.Logging;

/// <summary>
/// JSON console logger that redacts connection-string and credential-shaped secrets from messages and exceptions.
/// </summary>
public sealed class RedactingJsonConsoleLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new RedactingJsonConsoleLogger(categoryName);

    public void Dispose()
    {
    }

    private sealed class RedactingJsonConsoleLogger : ILogger
    {
        private readonly string _categoryName;

        public RedactingJsonConsoleLogger(string categoryName) => _categoryName = categoryName;

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            string message = Redact(formatter(state, exception));
            string? exceptionText = exception is null ? null : Redact(exception.ToString());

            var payload = new Dictionary<string, object?>
            {
                ["timestamp"] = DateTimeOffset.UtcNow.ToString("O"),
                ["level"] = logLevel.ToString(),
                ["category"] = _categoryName,
                ["eventId"] = eventId.Id,
                ["message"] = message,
            };

            if (exceptionText is not null)
            {
                payload["exception"] = exceptionText;
            }

            Console.WriteLine(JsonSerializer.Serialize(payload));
        }

        internal static string Redact(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            string result = text;
            result = Regex.Replace(
                result,
                "(?i)(Password|Pwd)\\s*=\\s*[^;\\s]+",
                "$1=***");
            result = Regex.Replace(
                result,
                "(?i)(Host|Username|User ID|Database)\\s*=\\s*[^;\\s]+",
                match => match.Groups[1].Value + "=***");
            result = Regex.Replace(
                result,
                "(?i)(\"|')?(password|pwd|secret|credential)(\"|')?\\s*[:=]\\s*(\"|')[^\"']+(\"|')",
                "$1$2$3:***");
            return result;
        }
    }

    /// <summary>Exposes redaction for tests without logging.</summary>
    public static string RedactForTests(string text)
        => RedactingJsonConsoleLogger.Redact(text);
}
