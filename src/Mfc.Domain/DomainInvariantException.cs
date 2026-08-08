namespace Mfc.Domain;

/// <summary>
/// Thrown when an inventory aggregate invariant is violated.
/// </summary>
public sealed class DomainInvariantException : Exception
{
    public DomainInvariantException(string message)
        : base(message)
    {
    }
}
