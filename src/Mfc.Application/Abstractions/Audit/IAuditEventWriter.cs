namespace Mfc.Application.Abstractions.Audit;

/// <summary>Append-only audit sink. Payloads must never contain credentials.</summary>
public interface IAuditEventWriter
{
    Task AppendAsync(
        string actor,
        string action,
        string payloadJson,
        CancellationToken cancellationToken = default);
}
