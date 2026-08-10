using System.Text;
using Mfc.Domain;
using Mfc.Domain.Inventory;
using Mfc.Domain.Policy;
using Xunit;

namespace Mfc.UnitTests.Policy;

public sealed class ChainContractTests
{
    [Theory]
    [InlineData(ChainDefaultDisposition.Drop, null)]
    [InlineData(ChainDefaultDisposition.Reject, RejectMode.TcpReset)]
    [InlineData(ChainDefaultDisposition.Reject, RejectMode.AdminProhibited)]
    [InlineData(ChainDefaultDisposition.Reject, RejectMode.PortUnreachable)]
    public void SupportsNormativeDispositions(ChainDefaultDisposition disposition, RejectMode? rejectMode)
    {
        ChainContract contract = ChainContract.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Input,
            disposition,
            rejectMode,
            PolicyRuntimeMode.ManagedOnly);
        Assert.Equal(disposition, contract.DefaultDisposition);
        Assert.False(contract.IsCriticalRisk);
    }

    [Fact]
    public void ReturnToUnmanagedRequiresMigrationCoexistenceAndIsCritical()
    {
        Assert.Throws<DomainInvariantException>(() =>
            ChainContract.Create(
                IpAddressFamily.IPv6,
                PolicyFilterChain.Forward,
                ChainDefaultDisposition.ReturnToUnmanaged,
                rejectMode: null,
                PolicyRuntimeMode.ManagedOnly));

        ChainContract contract = ChainContract.Create(
            IpAddressFamily.IPv6,
            PolicyFilterChain.Forward,
            ChainDefaultDisposition.ReturnToUnmanaged,
            rejectMode: null,
            PolicyRuntimeMode.MigrationCoexistence);
        Assert.True(contract.IsCriticalRisk);
    }

    [Fact]
    public void AcceptDefaultDispositionIsImpossible()
    {
        // There is no ACCEPT member on ChainDefaultDisposition; invalid ordinal is rejected.
        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(() =>
            ChainContract.Create(
                IpAddressFamily.IPv4,
                PolicyFilterChain.Output,
                (ChainDefaultDisposition)byte.MaxValue,
                rejectMode: null,
                PolicyRuntimeMode.ManagedOnly));
        Assert.Contains("ACCEPT", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DropMustNotSetRejectModeRejectRequiresRejectMode()
    {
        Assert.Throws<DomainInvariantException>(() =>
            ChainContract.Create(
                IpAddressFamily.IPv4,
                PolicyFilterChain.Input,
                ChainDefaultDisposition.Drop,
                RejectMode.TcpReset,
                PolicyRuntimeMode.ManagedOnly));
        Assert.Throws<DomainInvariantException>(() =>
            ChainContract.Create(
                IpAddressFamily.IPv4,
                PolicyFilterChain.Input,
                ChainDefaultDisposition.Reject,
                rejectMode: null,
                PolicyRuntimeMode.ManagedOnly));
    }

    [Fact]
    public void CompanyBaselineMayDefineContractsOverlaysCannot()
    {
        ChainContractSet set = ChainContractSet.CreateForCompanyBaseline(
            [
                ChainContract.Create(
                    IpAddressFamily.IPv4,
                    PolicyFilterChain.Input,
                    ChainDefaultDisposition.Drop,
                    null,
                    PolicyRuntimeMode.ManagedOnly),
                ChainContract.Create(
                    IpAddressFamily.IPv4,
                    PolicyFilterChain.Forward,
                    ChainDefaultDisposition.Reject,
                    RejectMode.AdminProhibited,
                    PolicyRuntimeMode.ManagedOnly),
            ],
            PolicyRuntimeMode.ManagedOnly);

        PolicyDocument baseline = PolicyDocument.CreateCompanyBaseline(set);
        Assert.Equal(2, baseline.ChainContracts.Count);

        Assert.Throws<DomainInvariantException>(() =>
            new PolicyDocument(
                PolicyKind.SiteOverlay,
                PolicyOwnerScope.Site,
                chainContracts: set));
        Assert.Throws<DomainInvariantException>(() =>
            new PolicyDocument(
                PolicyKind.NodeOverlay,
                PolicyOwnerScope.Node,
                chainContracts: set));

        PolicyDocument site = PolicyDocument.CreateEmpty(PolicyKind.SiteOverlay, PolicyOwnerScope.Site);
        Assert.Throws<DomainInvariantException>(() => site.WithChainContracts(set));
        Assert.Equal(0, site.ChainContracts.Count);
    }

    [Fact]
    public void DuplicateFamilyChainIsRejectedAndOrderIsDeterministic()
    {
        ChainContract a = ChainContract.Create(
            IpAddressFamily.IPv6,
            PolicyFilterChain.Output,
            ChainDefaultDisposition.Drop,
            null,
            PolicyRuntimeMode.ManagedOnly);
        Assert.Throws<DomainInvariantException>(() =>
            ChainContractSet.CreateForCompanyBaseline([a, a], PolicyRuntimeMode.ManagedOnly));

        ChainContractSet set = ChainContractSet.CreateForCompanyBaseline(
            [
                ChainContract.Create(
                    IpAddressFamily.IPv6,
                    PolicyFilterChain.Input,
                    ChainDefaultDisposition.Drop,
                    null,
                    PolicyRuntimeMode.ManagedOnly),
                ChainContract.Create(
                    IpAddressFamily.IPv4,
                    PolicyFilterChain.Forward,
                    ChainDefaultDisposition.Drop,
                    null,
                    PolicyRuntimeMode.ManagedOnly),
                ChainContract.Create(
                    IpAddressFamily.IPv4,
                    PolicyFilterChain.Input,
                    ChainDefaultDisposition.Drop,
                    null,
                    PolicyRuntimeMode.ManagedOnly),
            ],
            PolicyRuntimeMode.ManagedOnly);

        Assert.Equal(
            [
                (IpAddressFamily.IPv4, PolicyFilterChain.Input),
                (IpAddressFamily.IPv4, PolicyFilterChain.Forward),
                (IpAddressFamily.IPv6, PolicyFilterChain.Input),
            ],
            set.Items.Select(c => (c.Family, c.Chain)));
    }

    [Fact]
    public void CanonicalDocumentIncludesFixedPipelineVersionAndOrderedContracts()
    {
        ChainContractSet set = ChainContractSet.CreateForCompanyBaseline(
            [
                ChainContract.Create(
                    IpAddressFamily.IPv4,
                    PolicyFilterChain.Input,
                    ChainDefaultDisposition.Drop,
                    null,
                    PolicyRuntimeMode.ManagedOnly),
                ChainContract.Create(
                    IpAddressFamily.IPv4,
                    PolicyFilterChain.Forward,
                    ChainDefaultDisposition.Reject,
                    RejectMode.TcpReset,
                    PolicyRuntimeMode.ManagedOnly),
            ],
            PolicyRuntimeMode.ManagedOnly);
        byte[] bytes = PolicyCanonicalWriter.Write(PolicyDocument.CreateCompanyBaseline(set));
        string json = Encoding.UTF8.GetString(bytes);

        Assert.Contains("\"pipeline_version\":\"v1\"", json, StringComparison.Ordinal);
        Assert.Contains(
            "\"chain_contracts\":[{\"family\":\"IPv4\",\"chain\":\"INPUT\",\"default_disposition\":\"DROP\"}," +
            "{\"family\":\"IPv4\",\"chain\":\"FORWARD\",\"default_disposition\":\"REJECT\",\"reject_mode\":\"TCP_RESET\"}]",
            json,
            StringComparison.Ordinal);
        Assert.DoesNotContain("ACCEPT", json, StringComparison.Ordinal);
    }
}
