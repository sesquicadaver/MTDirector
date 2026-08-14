namespace Mfc.Domain.Policy;

/// <summary>Rule effect with optional reject mode (Policy Model §26).</summary>
public sealed class RuleEffectSpec
{
    public PolicyRuleEffect Kind { get; }

    public RejectMode? RejectModeValue { get; }

    private RuleEffectSpec(PolicyRuleEffect kind, RejectMode? rejectMode)
    {
        Kind = kind;
        RejectModeValue = rejectMode;
    }

    public static RuleEffectSpec Create(PolicyRuleEffect kind, RejectMode? rejectMode = null)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new DomainInvariantException($"Unknown rule effect '{kind}'.");
        }

        switch (kind)
        {
            case PolicyRuleEffect.Reject:
                if (rejectMode is null)
                {
                    throw new DomainInvariantException("REJECT requires reject_mode.");
                }

                if (!Enum.IsDefined(rejectMode.Value))
                {
                    throw new DomainInvariantException($"Unknown reject mode '{rejectMode}'.");
                }

                break;

            case PolicyRuleEffect.FasttrackAccept:
            case PolicyRuleEffect.ExemptDenyStage:
                if (rejectMode is not null)
                {
                    throw new DomainInvariantException(
                        $"{PolicyPipelineV1.FormatEffect(kind)} forbids reject_mode.");
                }

                break;

            case PolicyRuleEffect.Accept:
            case PolicyRuleEffect.Drop:
                if (rejectMode is not null)
                {
                    throw new DomainInvariantException(
                        $"{PolicyPipelineV1.FormatEffect(kind)} forbids reject_mode.");
                }

                break;

            default:
                throw new DomainInvariantException($"Unknown rule effect '{kind}'.");
        }

        return new RuleEffectSpec(kind, rejectMode);
    }
}
