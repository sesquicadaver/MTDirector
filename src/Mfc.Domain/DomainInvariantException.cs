namespace Mfc.Domain;

/// <summary>
/// Thrown when a domain aggregate invariant is violated.
/// </summary>
public sealed class DomainInvariantException : Exception
{
    public DomainInvariantException(string message)
        : base(message)
    {
    }

    public DomainInvariantException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
