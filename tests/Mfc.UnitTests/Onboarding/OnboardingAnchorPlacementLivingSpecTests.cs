using System.Reflection;
using Mfc.Application.Onboarding;
using Mfc.Domain;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Policy;
using Xunit;

namespace Mfc.UnitTests.Onboarding;

/// <summary>
/// Living Spec matrix for Issue Set M5-04 AC 1–10 (Onboarding Spec §20–§21 / §58).
/// </summary>
public sealed class OnboardingAnchorPlacementLivingSpecTests
{
    [Fact]
    public void Ac1OnlyBeforeStaticRuleAndAppendAreSupported()
    {
        AnchorPlacementIntent append = AnchorPlacementIntent.Append(IpAddressFamily.IPv4, FilterBuiltInContext.Input);
        Assert.Equal(AnchorPlacementMode.Append, append.Mode);
        Hash256 fp = FilterRuleFingerprint.Compute(StaticAccept(0, "ref"));
        AnchorPlacementIntent before = AnchorPlacementIntent.BeforeStaticRule(
            IpAddressFamily.IPv4,
            FilterBuiltInContext.Input,
            fp,
            0);
        Assert.Equal(AnchorPlacementMode.BeforeStaticRule, before.Mode);
        Assert.Throws<DomainInvariantException>(() =>
            AnchorPlacementIntent.Create(IpAddressFamily.IPv4, FilterBuiltInContext.Input, (AnchorPlacementMode)99));
        Assert.Throws<DomainInvariantException>(() =>
            AnchorPlacement.Create(IpAddressFamily.IPv4, FilterBuiltInContext.Input, (AnchorPlacementMode)99, 0));
    }

    [Fact]
    public void Ac2DynamicRuleCannotBeReference()
    {
        ActualFilterRule dyn = Rule("input", 0, "accept", comment: "dyn", dynamic: true);
        Hash256 fp = FilterRuleFingerprint.Compute(dyn);
        AnchorPlacementPlanResult result = PlanAnchorPlacementUseCase.Execute(
            AnchorPlacementIntent.BeforeStaticRule(IpAddressFamily.IPv4, FilterBuiltInContext.Input, fp, 0),
            [dyn]);
        Assert.Contains(result.Findings, static f => f.Code == OnboardingCodes.AnchorReferenceDynamic);
        Assert.Null(result.Placement);
    }

    [Fact]
    public void Ac3FingerprintAndOccurrenceRankAreFixed()
    {
        ActualFilterRule first = StaticAccept(0, "dup");
        ActualFilterRule second = StaticAccept(1, "dup");
        Hash256 fp = FilterRuleFingerprint.Compute(first);
        Assert.Equal(fp.ToString(), FilterRuleFingerprint.Compute(second).ToString());
        AnchorPlacementPlanResult result = PlanAnchorPlacementUseCase.Execute(
            AnchorPlacementIntent.BeforeStaticRule(IpAddressFamily.IPv4, FilterBuiltInContext.Input, fp, 1),
            [first, second]);
        Assert.True(result.Passed);
        Assert.NotNull(result.Placement);
        Assert.Equal(fp.ToString(), result.Placement.ReferenceRuleFingerprint!.ToString());
        Assert.Equal(1u, result.Placement.ReferenceOccurrenceRank);
        Assert.Equal(1u, result.Placement.ExpectedAnchorOrdinal);
        Assert.Equal(1u, FilterRuleFingerprint.OccurrenceRank([first, second], second, fp));
        Assert.DoesNotContain(typeof(ActualFilterRule).GetProperties(), static p => p.Name is "Id" or "ItemId" or ".id");
        Assert.Null(typeof(AnchorPlacement).GetProperty("RouterOsId"));
        Assert.Null(typeof(AnchorPlacement).GetProperty("ItemId"));
    }

    [Fact]
    public void Ac4PredecessorAndSuccessorContextAreChecked()
    {
        ActualFilterRule pred = StaticAccept(0, "pred");
        ActualFilterRule succ = StaticAccept(1, "succ");
        Hash256 succFp = FilterRuleFingerprint.Compute(succ);
        AnchorPlacementPlanResult planned = PlanAnchorPlacementUseCase.Execute(
            AnchorPlacementIntent.BeforeStaticRule(IpAddressFamily.IPv4, FilterBuiltInContext.Input, succFp, 0),
            [pred, succ]);
        Assert.True(planned.Passed);
        Assert.Equal(FilterRuleFingerprint.Compute(pred).ToString(), planned.Placement!.ExpectedPredecessorFingerprint!.ToString());
        Assert.Equal(succFp.ToString(), planned.Placement.ExpectedSuccessorFingerprint!.ToString());

        ActualFilterRule swappedPred = StaticAccept(0, "other-pred");
        AnchorPlacementPlanResult stale = PlanAnchorPlacementUseCase.Revalidate(
            planned.Placement,
            [swappedPred, succ]);
        Assert.Contains(stale.Findings, static f => f.Code == OnboardingCodes.AnchorPlacementStale);
    }

    [Fact]
    public void Ac5PlacementBeforeGuardIsForbidden()
    {
        ActualFilterRule guard = Guard(0);
        ActualFilterRule after = StaticAccept(1, "after-guard");
        Hash256 fp = FilterRuleFingerprint.Compute(guard);
        AnchorPlacementPlanResult beforeGuard = PlanAnchorPlacementUseCase.Execute(
            AnchorPlacementIntent.BeforeStaticRule(IpAddressFamily.IPv4, FilterBuiltInContext.Input, fp, 0),
            [guard, after]);
        Assert.Contains(beforeGuard.Findings, static f => f.Code == OnboardingCodes.AnchorBeforeGuard);

        Hash256 afterFp = FilterRuleFingerprint.Compute(after);
        AnchorPlacementPlanResult afterGuard = PlanAnchorPlacementUseCase.Execute(
            AnchorPlacementIntent.BeforeStaticRule(IpAddressFamily.IPv4, FilterBuiltInContext.Input, afterFp, 0),
            [guard, after]);
        Assert.True(afterGuard.Passed);
        Assert.Equal(1u, afterGuard.Placement!.ExpectedAnchorOrdinal);
    }

    [Fact]
    public void Ac6PlacementAfterUnconditionalTerminalIsBlocked()
    {
        ActualFilterRule drop = Rule("input", 0, "drop");
        AnchorPlacementPlanResult afterDrop = PlanAnchorPlacementUseCase.Execute(
            AnchorPlacementIntent.Append(IpAddressFamily.IPv4, FilterBuiltInContext.Input),
            [drop]);
        Assert.Contains(afterDrop.Findings, static f => f.Code == OnboardingCodes.AnchorUnreachable);

        ActualFilterRule accept = Rule("input", 0, "accept");
        AnchorPlacementPlanResult afterAccept = PlanAnchorPlacementUseCase.Execute(
            AnchorPlacementIntent.Append(IpAddressFamily.IPv4, FilterBuiltInContext.Input),
            [accept]);
        Assert.Contains(afterAccept.Findings, static f => f.Code == OnboardingCodes.AnchorUnreachable);
    }

    [Fact]
    public void JumpAndUnknownMatcherAreContextIndeterminate()
    {
        ActualFilterRule jump = ActualFilterRule.Create(
            IpAddressFamily.IPv4,
            "input",
            0,
            "jump",
            jumpTarget: "custom");
        AnchorPlacementPlanResult afterJump = PlanAnchorPlacementUseCase.Execute(
            AnchorPlacementIntent.Append(IpAddressFamily.IPv4, FilterBuiltInContext.Input),
            [jump]);
        Assert.Contains(afterJump.Findings, static f => f.Code == OnboardingCodes.AnchorContextIndeterminate);

        ActualFilterRule unknown = ActualFilterRule.Create(
            IpAddressFamily.IPv4,
            "input",
            0,
            "accept",
            unknownMatchers: new Dictionary<string, string>(StringComparer.Ordinal) { ["nth"] = "1,1" });
        AnchorPlacementPlanResult afterUnknown = PlanAnchorPlacementUseCase.Execute(
            AnchorPlacementIntent.Append(IpAddressFamily.IPv4, FilterBuiltInContext.Input),
            [unknown]);
        Assert.Contains(afterUnknown.Findings, static f => f.Code == OnboardingCodes.AnchorContextIndeterminate);
    }

    [Fact]
    public void Ac7AutomaticBestPositionSelectionIsAbsent()
    {
        Assert.Null(typeof(AnchorPlacementPlanner).GetMethod("Suggest"));
        Assert.Null(typeof(AnchorPlacementPlanner).GetMethod("SuggestBest"));
        Assert.Null(typeof(AnchorPlacementPlanner).GetMethod("AutoPlace"));
        Assert.Null(typeof(PlanAnchorPlacementUseCase).GetMethod("Suggest"));
        Assert.DoesNotContain(
            typeof(AnchorPlacementPlanner).GetMethods(BindingFlags.Public | BindingFlags.Static),
            static m => m.Name.Contains("Best", StringComparison.Ordinal)
                        || m.Name.Contains("Auto", StringComparison.Ordinal)
                        || m.Name.Contains("Suggest", StringComparison.Ordinal));
    }

    [Fact]
    public void Ac8RouterOsIdIsNotStored()
    {
        AnchorPlacementPlanResult result = PlanAnchorPlacementUseCase.Execute(
            AnchorPlacementIntent.Append(IpAddressFamily.IPv4, FilterBuiltInContext.Input),
            [StaticAccept(0, "only")]);
        Assert.True(result.Passed);
        Assert.Null(typeof(AnchorPlacement).GetProperty("RouterOsId"));
        Assert.Null(typeof(AnchorPlacementPreview).GetProperty("RouterOsId"));
        Assert.Null(typeof(AnchorPlacementIntent).GetProperty("RouterOsId"));
        Assert.DoesNotContain(
            typeof(ActualFilterRule).GetProperties(),
            static p => p.Name.Contains("Id", StringComparison.OrdinalIgnoreCase)
                        && p.Name != nameof(ActualFilterRule.Family));
        Assert.Equal("mfc.onboarding.filter_rule.v1", FilterRuleFingerprint.Prefix);
    }

    [Fact]
    public void Ac9FilterOrderChangeInvalidatesPlan()
    {
        ActualFilterRule a = StaticAccept(0, "a");
        ActualFilterRule b = StaticAccept(1, "b");
        Hash256 bFp = FilterRuleFingerprint.Compute(b);
        AnchorPlacementPlanResult planned = PlanAnchorPlacementUseCase.Execute(
            AnchorPlacementIntent.BeforeStaticRule(IpAddressFamily.IPv4, FilterBuiltInContext.Input, bFp, 0),
            [a, b]);
        Assert.True(planned.Passed);

        ActualFilterRule bFirst = StaticAccept(0, "b");
        ActualFilterRule aSecond = StaticAccept(1, "a");
        AnchorPlacementPlanResult stale = PlanAnchorPlacementUseCase.Revalidate(
            planned.Placement!,
            [bFirst, aSecond]);
        Assert.Contains(stale.Findings, static f => f.Code == OnboardingCodes.AnchorPlacementStale);
    }

    [Fact]
    public void Ac10DesktopPreviewExposesExactBeforeAfterPosition()
    {
        ActualFilterRule pred = StaticAccept(0, "before-marker");
        ActualFilterRule succ = StaticAccept(1, "after-marker");
        Hash256 succFp = FilterRuleFingerprint.Compute(succ);
        AnchorPlacementPlanResult result = PlanAnchorPlacementUseCase.Execute(
            AnchorPlacementIntent.BeforeStaticRule(IpAddressFamily.IPv4, FilterBuiltInContext.Input, succFp, 0),
            [pred, succ]);
        Assert.NotNull(result.Preview);
        Assert.Equal("before-marker", result.Preview.BeforeLabel);
        Assert.Equal("after-marker", result.Preview.AfterLabel);
        Assert.Equal(1u, result.Preview.ExpectedAnchorOrdinal);

        AnchorPlacementPlanResult append = PlanAnchorPlacementUseCase.Execute(
            AnchorPlacementIntent.Append(IpAddressFamily.IPv4, FilterBuiltInContext.Output),
            [StaticAccept(0, "tail", "output")]);
        Assert.NotNull(append.Preview);
        Assert.Equal("tail", append.Preview.BeforeLabel);
        Assert.Equal(string.Empty, append.Preview.AfterLabel);
        Assert.Equal(1u, append.Preview.ExpectedAnchorOrdinal);
    }

    [Fact]
    public void MissingReferenceIsAnchorReferenceMissing()
    {
        Hash256 missing = FilterRuleFingerprint.Compute(StaticAccept(0, "absent"));
        AnchorPlacementPlanResult result = PlanAnchorPlacementUseCase.Execute(
            AnchorPlacementIntent.BeforeStaticRule(IpAddressFamily.IPv4, FilterBuiltInContext.Input, missing, 0),
            [StaticAccept(0, "present")]);
        Assert.Contains(result.Findings, static f => f.Code == OnboardingCodes.AnchorReferenceMissing);
    }

    private static ActualFilterRule Guard(int ordinal)
        => Rule(
            "input",
            ordinal,
            "accept",
            comment: "mfc:guard:v1:0123456789abcdef:4:i:0",
            known: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["protocol"] = "tcp",
                ["dst-port"] = "8729",
            });

    private static ActualFilterRule StaticAccept(int ordinal, string comment, string chain = "input")
        => Rule(
            chain,
            ordinal,
            "accept",
            comment: comment,
            known: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["src-address"] = "10.0.0.0/8",
            });

    private static ActualFilterRule Rule(
        string chain,
        int ordinal,
        string action,
        string? comment = null,
        bool dynamic = false,
        IReadOnlyDictionary<string, string>? known = null)
        => ActualFilterRule.Create(
            IpAddressFamily.IPv4,
            chain,
            ordinal,
            action,
            dynamic: dynamic,
            comment: comment,
            knownMatchers: known);
}
