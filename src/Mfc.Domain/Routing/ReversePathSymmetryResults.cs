namespace Mfc.Domain.Routing;

/// <summary>Reverse-path symmetry classification (M7.1 Spec §12).</summary>
public static class ReversePathSymmetryResults
{
    public const string Symmetric = "SYMMETRIC";

    public const string AsymmetricExpected = "ASYMMETRIC_EXPECTED";

    public const string AsymmetricUnexpected = "ASYMMETRIC_UNEXPECTED";

    public const string ReversePathMissing = "REVERSE_PATH_MISSING";

    public const string Indeterminate = "INDETERMINATE";
}
