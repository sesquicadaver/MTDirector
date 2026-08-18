namespace Mfc.Domain.Policy;

/// <summary>
/// Compiles chain-contract default disposition into the explicit root terminal rule
/// (Compiler Spec §22 / M3-06). Default ACCEPT is impossible.
/// </summary>
public static class ChainTerminalCompiler
{
    /// <summary>Builds the single root terminal artifact for a sealed <see cref="ChainContract"/>.</summary>
    public static FilterRuleArtifact Compile(ChainContract contract, uint ordinal = 0)
    {
        ArgumentNullException.ThrowIfNull(contract);
        return Compile(contract.DefaultDisposition, contract.RejectModeValue, ordinal);
    }

    /// <summary>
    /// Maps DROP / REJECT / RETURN_TO_UNMANAGED to one explicit terminal rule
    /// with comment <see cref="CompilerComments.Terminal"/>.
    /// </summary>
    public static FilterRuleArtifact Compile(
        ChainDefaultDisposition disposition,
        RejectMode? rejectMode,
        uint ordinal = 0)
    {
        string action;
        Dictionary<string, string>? parameters = null;
        switch (disposition)
        {
            case ChainDefaultDisposition.Drop:
                if (rejectMode is not null)
                {
                    throw new DomainInvariantException("DROP terminal must not set reject_mode.");
                }

                action = "drop";
                break;

            case ChainDefaultDisposition.Reject:
                if (rejectMode is null)
                {
                    throw new DomainInvariantException("REJECT terminal requires reject_mode.");
                }

                action = "reject";
                parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["reject-with"] = RouterOsCompilerProfile.FormatRejectWith(rejectMode.Value),
                };
                break;

            case ChainDefaultDisposition.ReturnToUnmanaged:
                if (rejectMode is not null)
                {
                    throw new DomainInvariantException("RETURN_TO_UNMANAGED terminal must not set reject_mode.");
                }

                action = "return";
                break;

            default:
                throw new DomainInvariantException(
                    "Default accept is impossible; terminal must be DROP, REJECT, or RETURN_TO_UNMANAGED.");
        }

        return FilterRuleArtifact.Create(
            ordinal,
            action,
            CompilerComments.Terminal,
            structuralRole: "terminal",
            actionParameters: parameters);
    }
}
