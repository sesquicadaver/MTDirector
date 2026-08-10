namespace Mfc.Domain.Policy;

/// <summary>
/// Owner scope for policy objects (address/service/zone), including EXCEPTION (Policy Model §11 / §16).
/// Distinct from <see cref="PolicyOwnerScope"/> on policy containers (which never use EXCEPTION).
/// </summary>
public enum PolicyObjectOwnerScope : byte
{
    Company = 0,
    Site = 1,
    Node = 2,
    Exception = 3,
}
