using Mfc.Domain;
using Mfc.Domain.Inventory;
using Mfc.Domain.Policy;
using Xunit;

namespace Mfc.UnitTests.Policy;

public sealed class ActualFilterAnalysisTests
{
    [Fact]
    public void Ac1BoundedFilterControlFlowGraphIsBuilt()
    {
        ActualFilterAnalysisResult result = Analyze(
            Jump("forward", 0, "helper"),
            Rule("helper", 0, "return"));
        Assert.Contains(result.Graph.Nodes, n => n.Kind == ActualFilterGraphNodeKind.Rule);
        Assert.Contains(result.Graph.Edges, e => e.Kind == ActualFilterGraphEdgeKind.Jump);
        Assert.Contains(result.Graph.Edges, e => e.Kind == ActualFilterGraphEdgeKind.Return);
        Assert.True(result.Graph.Nodes.Count <= ActualFilterAnalysisCodes.MaxGraphNodes);
    }

    [Fact]
    public void Ac2JumpAndReturnAreSupported()
    {
        ActualFilterRule jump = Jump("forward", 0, "helper");
        ActualFilterRule after = Rule("forward", 1, "drop", comment: "fwc:anchor:ipv4:forward");
        ActualFilterRule helper = Rule("helper", 0, "log");
        ActualFilterRule ret = Rule("helper", 1, "return");
        ActualFilterAnalysisResult result = Analyze(jump, after, helper, ret);
        Assert.Contains(result.Graph.Edges, e => e.Kind == ActualFilterGraphEdgeKind.Jump);
        Assert.DoesNotContain(result.Findings, f => f.Code == ActualFilterAnalysisCodes.JumpCycle);
    }

    [Fact]
    public void Ac3JumpCyclesAreDetected()
    {
        ActualFilterAnalysisResult result = Analyze(
            Jump("forward", 0, "a"),
            Jump("a", 0, "b"),
            Jump("b", 0, "a"));
        Assert.Contains(result.Findings, f =>
            f.Code == ActualFilterAnalysisCodes.JumpCycle
            && f.Severity == ActualFilterAnalysisCodes.SeverityBlocker);
    }

    [Fact]
    public void Ac4DepthAndNodeLimitsAreApplied()
    {
        List<ActualFilterRule> depth = [Jump("forward", 0, "j0")];
        for (int i = 0; i < ActualFilterAnalysisCodes.MaxJumpDepth; i++)
        {
            depth.Add(Jump($"j{i}", 0, $"j{i + 1}"));
        }

        ActualFilterAnalysisResult deep = Analyze(depth.ToArray());
        Assert.Contains(deep.Findings, f => f.Code == ActualFilterAnalysisCodes.DepthLimit);

        List<ActualFilterRule> nodes = [];
        for (int i = 0; i <= ActualFilterAnalysisCodes.MaxGraphNodes; i++)
        {
            nodes.Add(Rule("forward", i, "log"));
        }

        ActualFilterAnalysisResult overflow = Analyze(nodes.ToArray());
        Assert.Contains(overflow.Findings, f => f.Code == ActualFilterAnalysisCodes.AnalysisIndeterminate);

        List<ActualFilterRule> chains = [Rule("forward", 0, "log")];
        for (int i = 0; i < ActualFilterAnalysisCodes.MaxChains; i++)
        {
            chains.Add(Rule($"c{i}", 0, "drop"));
        }

        ActualFilterAnalysisResult tooMany = Analyze(chains.ToArray());
        Assert.Contains(tooMany.Findings, f => f.Code == ActualFilterAnalysisCodes.AnalysisIndeterminate);
    }

    [Fact]
    public void Ac5PreAnchorAcceptBypassIsDetected()
    {
        ActualFilterAnalysisResult result = Analyze(
            Rule("forward", 0, "accept"),
            Anchor(1));
        Assert.Contains(result.Findings, f =>
            f.Code == ActualFilterAnalysisCodes.PreAnchorAcceptBypasses
            && f.Severity == ActualFilterAnalysisCodes.SeverityBlocker
            && f.Ordinal == 0);
    }

    [Fact]
    public void Ac6PreAnchorDropShadowIsDetected()
    {
        ActualFilterAnalysisResult result = Analyze(
            Rule("forward", 0, "drop"),
            Anchor(1));
        Assert.Contains(result.Findings, f => f.Code == ActualFilterAnalysisCodes.PreAnchorDropShadows);
    }

    [Fact]
    public void Ac7PreAnchorFastTrackBypassIsDetected()
    {
        ActualFilterAnalysisResult result = Analyze(
            Rule("forward", 0, "fasttrack-connection"),
            Anchor(1));
        Assert.Contains(result.Findings, f => f.Code == ActualFilterAnalysisCodes.PreAnchorFasttrackBypasses);
    }

    [Fact]
    public void Ac8DynamicPreAnchorRuleIsMarked()
    {
        ActualFilterAnalysisResult result = Analyze(
            Rule("forward", 0, "accept", dynamic: true),
            Anchor(1));
        Assert.Contains(result.Findings, f => f.Code == ActualFilterAnalysisCodes.PreAnchorDynamicRule);
        Assert.Contains(result.Findings, f => f.Code == ActualFilterAnalysisCodes.PreAnchorAcceptBypasses);
    }

    [Fact]
    public void Ac9UnsupportedMatcherOrActionIsIndeterminate()
    {
        ActualFilterRule tarpit = ActualFilterRule.Create(
            IpAddressFamily.IPv4,
            "forward",
            0,
            "tarpit");
        ActualFilterRule timed = ActualFilterRule.Create(
            IpAddressFamily.IPv4,
            "forward",
            1,
            "accept",
            unknownMatchers: new Dictionary<string, string>(StringComparer.Ordinal) { ["time"] = "sunrise-sunset" });
        ActualFilterAnalysisResult actions = Analyze(tarpit, Anchor(2));
        Assert.Contains(actions.Findings, f => f.Code == ActualFilterAnalysisCodes.UnknownAction);
        Assert.Contains(actions.Findings, f => f.Code == ActualFilterAnalysisCodes.PreAnchorIndeterminate);

        ActualFilterAnalysisResult matchers = Analyze(timed, Anchor(2));
        Assert.Contains(matchers.Findings, f => f.Code == ActualFilterAnalysisCodes.UnknownMatcher);
        Assert.Contains(matchers.Findings, f => f.Code == ActualFilterAnalysisCodes.PreAnchorIndeterminate);
    }

    [Fact]
    public void Ac10PostAnchorContextIsAnalyzedOnlyForReturnToUnmanaged()
    {
        ActualFilterRule post = Rule("forward", 2, "tarpit");
        ActualFilterAnalysisResult dropped = Analyze(Anchor(0), Rule("forward", 1, "log"), post);
        Assert.False(dropped.PostAnchorAnalyzed);
        Assert.DoesNotContain(dropped.Findings, f => f.Ordinal == 2 && f.Code == ActualFilterAnalysisCodes.UnknownAction);

        ChainContractSet returning = ChainContractSet.CreateForCompanyBaseline(
            [
                ChainContract.Create(
                    IpAddressFamily.IPv4,
                    PolicyFilterChain.Forward,
                    ChainDefaultDisposition.ReturnToUnmanaged,
                    rejectMode: null,
                    PolicyRuntimeMode.MigrationCoexistence),
            ],
            PolicyRuntimeMode.MigrationCoexistence);
        ActualFilterAnalysisResult migrated = ActualFilterAnalysis.Analyze(
            [Anchor(0), Rule("forward", 1, "log"), post],
            returning);
        Assert.True(migrated.PostAnchorAnalyzed);
        Assert.Contains(migrated.Findings, f =>
            f.Ordinal == 2 && f.Code == ActualFilterAnalysisCodes.UnknownAction);
        Assert.DoesNotContain(migrated.Findings, f => f.Code == ActualFilterAnalysisCodes.PreAnchorAcceptBypasses);
    }

    [Fact]
    public void Ac11RouterOsImplicitAcceptIsNotManagedDefault()
    {
        ActualFilterAnalysisResult result = Analyze();
        Assert.False(result.UsesRouterOsImplicitAcceptAsManagedDefault);
        Assert.Contains(
            result.Graph.Nodes,
            n => n.Kind == ActualFilterGraphNodeKind.RouterOsImplicitAccept);
        Assert.DoesNotContain(result.Findings, f =>
            f.Message.Contains("managed default", StringComparison.OrdinalIgnoreCase)
            && f.Message.Contains("accept", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Ac12ActualContextHashEntersAnalysisContext()
    {
        ActualFilterRule[] rules = [Rule("forward", 0, "drop"), Anchor(1)];
        ActualFilterAnalysisResult first = Analyze(rules);
        ActualFilterAnalysisResult second = Analyze(rules);
        Assert.Equal(first.ActualContextHash.ToString(), second.ActualContextHash.ToString());
        Assert.Equal(
            ActualFilterAnalysis.HashAnalysisContext(first.ActualContextHash).ToString(),
            first.AnalysisContextHash.ToString());
        Assert.Equal(32, first.ActualContextHash.Bytes.Length);

        ActualFilterAnalysisResult changed = Analyze(Rule("forward", 0, "accept"), Anchor(1));
        Assert.NotEqual(first.ActualContextHash.ToString(), changed.ActualContextHash.ToString());
        Assert.NotEqual(first.AnalysisContextHash.ToString(), changed.AnalysisContextHash.ToString());
    }

    [Fact]
    public void DisabledPreAnchorAcceptIsIgnored()
    {
        ActualFilterAnalysisResult result = Analyze(
            Rule("forward", 0, "accept", disabled: true),
            Anchor(1));
        Assert.DoesNotContain(result.Findings, f => f.Code == ActualFilterAnalysisCodes.PreAnchorAcceptBypasses);
    }

    [Fact]
    public void ControllerOwnedPreAnchorIsNotUnmanagedBypass()
    {
        ActualFilterAnalysisResult result = Analyze(
            Rule("forward", 0, "accept", comment: "fwc:guard:api-ssl"),
            Anchor(1));
        Assert.DoesNotContain(result.Findings, f => f.Code == ActualFilterAnalysisCodes.PreAnchorAcceptBypasses);
    }

    [Fact]
    public void GuardDoesNotHideLaterUnmanagedPreAnchorBypass()
    {
        ActualFilterAnalysisResult result = Analyze(
            Rule("forward", 0, "accept", comment: "fwc:guard:api-ssl"),
            Rule("forward", 1, "accept"),
            Rule("forward", 2, "drop"),
            Rule("forward", 3, "fasttrack-connection"),
            Rule("forward", 4, "log", dynamic: true),
            Anchor(5));
        Assert.Contains(result.Findings, f => f.Code == ActualFilterAnalysisCodes.PreAnchorAcceptBypasses && f.Ordinal == 1);
        Assert.Contains(result.Findings, f => f.Code == ActualFilterAnalysisCodes.PreAnchorDropShadows && f.Ordinal == 2);
        Assert.Contains(result.Findings, f => f.Code == ActualFilterAnalysisCodes.PreAnchorFasttrackBypasses && f.Ordinal == 3);
        Assert.Contains(result.Findings, f => f.Code == ActualFilterAnalysisCodes.PreAnchorDynamicRule && f.Ordinal == 4);
    }

    [Fact]
    public void JumpToEmptyBuiltinIsPreAnchorAcceptBypass()
    {
        ActualFilterAnalysisResult result = Analyze(
            Jump("forward", 0, "input"),
            Anchor(1));
        Assert.Contains(result.Findings, f =>
            f.Code == ActualFilterAnalysisCodes.PreAnchorAcceptBypasses
            && f.Chain == "input");
    }

    [Fact]
    public void ReturnEdgeTargetsSuccessorAfterJump()
    {
        ActualFilterAnalysisResult result = Analyze(
            Jump("forward", 0, "helper"),
            Rule("forward", 1, "drop"),
            Rule("helper", 0, "return"));
        Assert.Contains(
            result.Graph.Edges,
            e => e.Kind == ActualFilterGraphEdgeKind.Return && e.ToId.EndsWith(":1", StringComparison.Ordinal));
    }

    [Fact]
    public void ControllerOwnedJumpIntoManagedIsOpaquePipeline()
    {
        ActualFilterAnalysisResult result = Analyze(
            Rule("forward", 0, "jump", jumpTarget: "fwc.forward.rev1", comment: "fwc:rule:owned"),
            Anchor(1));
        Assert.Contains(
            result.Graph.Nodes,
            n => n.Kind == ActualFilterGraphNodeKind.ManagedPipeline && n.Ordinal == 0);
        Assert.DoesNotContain(result.Findings, f => f.Code == ActualFilterAnalysisCodes.AnalysisIndeterminate);
    }

    [Fact]
    public void UnmanagedJumpIntoManagedIsIndeterminate()
    {
        ActualFilterAnalysisResult result = Analyze(
            Jump("forward", 0, "fwc.forward.rev1"),
            Anchor(1));
        Assert.Contains(result.Findings, f => f.Code == ActualFilterAnalysisCodes.AnalysisIndeterminate);
        Assert.Contains(result.Findings, f => f.Code == ActualFilterAnalysisCodes.PreAnchorIndeterminate);
    }

    [Fact]
    public void PreAnchorJumpInheritsAcceptBypass()
    {
        ActualFilterAnalysisResult result = Analyze(
            Jump("forward", 0, "helper"),
            Rule("helper", 0, "accept"),
            Anchor(1));
        Assert.Contains(result.Findings, f =>
            f.Code == ActualFilterAnalysisCodes.PreAnchorAcceptBypasses
            && f.Chain == "helper");
    }

    [Fact]
    public void JumpEdgeTargetsFirstRuleOrdinal()
    {
        ActualFilterAnalysisResult result = Analyze(
            Jump("forward", 0, "helper"),
            Rule("helper", 5, "return"));
        Assert.Contains(
            result.Graph.Edges,
            e => e.Kind == ActualFilterGraphEdgeKind.Jump && e.ToId.EndsWith(":5", StringComparison.Ordinal));
    }

    [Fact]
    public void DuplicateAnchorsAreIndeterminate()
    {
        ActualFilterAnalysisResult result = Analyze(Anchor(0), Anchor(1));
        Assert.Contains(result.Findings, f => f.Code == ActualFilterAnalysisCodes.AnalysisIndeterminate);
    }

    [Fact]
    public void MissingJumpTargetIsIndeterminate()
    {
        ActualFilterAnalysisResult result = Analyze(Jump("forward", 0, "missing"), Anchor(1));
        Assert.Contains(result.Findings, f => f.Code == ActualFilterAnalysisCodes.AnalysisIndeterminate);
        Assert.Contains(result.Findings, f => f.Code == ActualFilterAnalysisCodes.PreAnchorIndeterminate);
    }

    [Fact]
    public void SelfJumpIsCycle()
    {
        ActualFilterAnalysisResult result = Analyze(Jump("forward", 0, "forward"));
        Assert.Contains(result.Findings, f => f.Code == ActualFilterAnalysisCodes.JumpCycle);
    }

    [Fact]
    public void PreAnchorRejectShadowsPolicy()
    {
        ActualFilterAnalysisResult result = Analyze(Rule("forward", 0, "reject"), Anchor(1));
        Assert.Contains(result.Findings, f => f.Code == ActualFilterAnalysisCodes.PreAnchorDropShadows);
        Assert.True(result.HasBlockers);
    }

    [Fact]
    public void Ipv6ImplicitAcceptIsNotManagedDefault()
    {
        ActualFilterAnalysisResult result = ActualFilterAnalysis.Analyze(
            [
                ActualFilterRule.Create(IpAddressFamily.IPv6, "forward", 0, "log"),
            ],
            ChainContractSet.CreateForCompanyBaseline(
                [
                    ChainContract.Create(
                        IpAddressFamily.IPv6,
                        PolicyFilterChain.Forward,
                        ChainDefaultDisposition.Drop,
                        rejectMode: null,
                        PolicyRuntimeMode.ManagedOnly),
                ],
                PolicyRuntimeMode.ManagedOnly));
        Assert.False(result.UsesRouterOsImplicitAcceptAsManagedDefault);
        Assert.Contains(
            result.Graph.Nodes,
            n => n.Kind == ActualFilterGraphNodeKind.RouterOsImplicitAccept && n.Family == IpAddressFamily.IPv6);
    }

    [Fact]
    public void MarkerAndRuleInvariantsHold()
    {
        Assert.True(ActualFilterMarker.IsAnchor("note fwc:anchor:ipv4:forward"));
        Assert.True(ActualFilterMarker.IsUnmanaged(null));
        Assert.True(ActualFilterMarker.IsManagedChainName("mfc.input.rev1"));
        Assert.False(ActualFilterAnalysisCodes.IsFailedPrecondition(string.Empty));
        Assert.True(ActualFilterAnalysisCodes.IsFailedPrecondition(ActualFilterAnalysisCodes.UnknownMatcher));
        Assert.Throws<DomainInvariantException>(() =>
            ActualFilterRule.Create((IpAddressFamily)99, "forward", 0, "drop"));
        Assert.Throws<DomainInvariantException>(() =>
            ActualFilterRule.Create(IpAddressFamily.IPv4, "  ", 0, "drop"));
        Assert.Throws<DomainInvariantException>(() =>
            ActualFilterRule.Create(IpAddressFamily.IPv4, "forward", -1, "drop"));
    }

    private static ActualFilterAnalysisResult Analyze(params ActualFilterRule[] rules)
        => ActualFilterAnalysis.Analyze(
            rules,
            ChainContractSet.CreateForCompanyBaseline(
                [
                    ChainContract.Create(
                        IpAddressFamily.IPv4,
                        PolicyFilterChain.Forward,
                        ChainDefaultDisposition.Drop,
                        rejectMode: null,
                        PolicyRuntimeMode.ManagedOnly),
                ],
                PolicyRuntimeMode.ManagedOnly));

    private static ActualFilterRule Anchor(int ordinal)
        => Rule(
            "forward",
            ordinal,
            "jump",
            jumpTarget: "fwc.forward.rev1",
            comment: "fwc:anchor:ipv4:forward");

    private static ActualFilterRule Jump(string chain, int ordinal, string target)
        => Rule(chain, ordinal, "jump", jumpTarget: target);

    private static ActualFilterRule Rule(
        string chain,
        int ordinal,
        string action,
        bool disabled = false,
        bool dynamic = false,
        string? jumpTarget = null,
        string? comment = null)
        => ActualFilterRule.Create(
            IpAddressFamily.IPv4,
            chain,
            ordinal,
            action,
            disabled: disabled,
            dynamic: dynamic,
            jumpTarget: jumpTarget,
            comment: comment);
}
