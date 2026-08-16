using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Grpc.Core;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Services;

namespace Mfc.Desktop.ViewModels;

/// <summary>Policy authoring and review workflow panel (Contracts-only; M2-18).</summary>
public sealed partial class PoliciesViewModel : ObservableObject, IDisposable
{
    private const string DeployResidual =
        "Deploy is gated by N1-06 and is not available from Desktop policy authoring (no Save and Deploy).";

    private readonly IPolicyPanelService _policies;
    private readonly IControllerConnectionService _connection;
    private bool _disposed;
    private byte[]? _contentHash;
    private byte[]? _logicalEffectiveHash;
    private byte[]? _analysisBundleHash;
    private byte[]? _dependencyFingerprint;
    private Guid? _analysisRunId;

    public PoliciesViewModel(IPolicyPanelService policies, IControllerConnectionService connection)
    {
        _policies = policies ?? throw new ArgumentNullException(nameof(policies));
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _connection.StateChanged += OnConnectionStateChanged;

        Families =
        [
            IpAddressFamily.Ipv4,
            IpAddressFamily.Ipv6,
        ];
        Chains =
        [
            PolicyFilterChain.Input,
            PolicyFilterChain.Forward,
            PolicyFilterChain.Output,
        ];
        Stages =
        [
            PolicyPipelineStage.ProtectedControlPlane,
            PolicyPipelineStage.MandatoryPreStateDeny,
            PolicyPipelineStage.StatePrelude,
            PolicyPipelineStage.CompanyDenyExemptions,
            PolicyPipelineStage.CompanyDeny,
            PolicyPipelineStage.SiteDenyExemptions,
            PolicyPipelineStage.SiteDeny,
            PolicyPipelineStage.NodeDenyExemptions,
            PolicyPipelineStage.NodeDeny,
            PolicyPipelineStage.CompanyAllow,
            PolicyPipelineStage.SiteAllow,
            PolicyPipelineStage.NodeAllow,
            PolicyPipelineStage.DefaultDisposition,
        ];
        Effects =
        [
            PolicyRuleEffect.Accept,
            PolicyRuleEffect.Drop,
            PolicyRuleEffect.Reject,
            PolicyRuleEffect.FasttrackAccept,
            PolicyRuleEffect.ExemptDenyStage,
        ];
        RejectModes =
        [
            RejectMode.TcpReset,
            RejectMode.AdminProhibited,
            RejectMode.PortUnreachable,
        ];
        SelectedFamily = IpAddressFamily.Ipv4;
        SelectedChain = PolicyFilterChain.Forward;
        SelectedStage = PolicyPipelineStage.CompanyAllow;
        SelectedEffect = PolicyRuleEffect.Accept;
        AddressFamily = IpAddressFamily.Ipv4;
        ContractFamily = IpAddressFamily.Ipv4;
        ContractChain = PolicyFilterChain.Forward;
        ContractDisposition = "DROP";
        ContractRejectMode = RejectMode.AdminProhibited;
        AnalysisRiskLevelText = "LOW";
        TestsJsonText = "[]";
    }

    public ObservableCollection<PolicyRuleListItem> Rules { get; } = [];

    public ObservableCollection<PolicyAddressObjectListItem> AddressObjects { get; } = [];

    public ObservableCollection<PolicyServiceObjectListItem> ServiceObjects { get; } = [];

    public ObservableCollection<PolicyChainContractListItem> ChainContracts { get; } = [];

    public ObservableCollection<PolicyFindingListItem> Findings { get; } = [];

    public ObservableCollection<PolicyFindingListItem> DiffLines { get; } = [];

    public IReadOnlyList<IpAddressFamily> Families { get; }

    public IReadOnlyList<PolicyFilterChain> Chains { get; }

    public IReadOnlyList<PolicyPipelineStage> Stages { get; }

    public IReadOnlyList<PolicyRuleEffect> Effects { get; }

    public IReadOnlyList<RejectMode> RejectModes { get; }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);

    public bool IsReadOnlyBannerVisible => IsReadOnly;

    public string ReadOnlyBannerText => IsReadOnly
        ? "Revision is read-only (InReview / Approved / terminal). Mutate commands are disabled."
        : string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorText;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReadOnlyBannerVisible))]
    [NotifyPropertyChangedFor(nameof(ReadOnlyBannerText))]
    private bool _isReadOnly;

    [ObservableProperty]
    private string _revisionIdText = string.Empty;

    [ObservableProperty]
    private string _draftNameText = string.Empty;

    [ObservableProperty]
    private string _stateText = string.Empty;

    [ObservableProperty]
    private string _contentHashText = string.Empty;

    [ObservableProperty]
    private string _riskLevelText = string.Empty;

    [ObservableProperty]
    private string _effectiveRiskLevelText = string.Empty;

    [ObservableProperty]
    private string _analysisRunIdText = string.Empty;

    [ObservableProperty]
    private string _diffBaselineRevisionIdText = string.Empty;

    [ObservableProperty]
    private string _composeNodeIdText = string.Empty;

    [ObservableProperty]
    private string _ruleDescriptionText = string.Empty;

    [ObservableProperty]
    private string _reorderRuleIdsText = string.Empty;

    [ObservableProperty]
    private IpAddressFamily _selectedFamily;

    [ObservableProperty]
    private PolicyFilterChain _selectedChain;

    [ObservableProperty]
    private PolicyPipelineStage _selectedStage;

    [ObservableProperty]
    private PolicyRuleEffect _selectedEffect;

    [ObservableProperty]
    private string _addressNameText = string.Empty;

    [ObservableProperty]
    private IpAddressFamily _addressFamily;

    [ObservableProperty]
    private string _addressEntriesText = string.Empty;

    [ObservableProperty]
    private string _serviceNameText = string.Empty;

    [ObservableProperty]
    private string _serviceTcpPortText = "443";

    [ObservableProperty]
    private IpAddressFamily _contractFamily;

    [ObservableProperty]
    private PolicyFilterChain _contractChain;

    [ObservableProperty]
    private string _contractDisposition = "DROP";

    [ObservableProperty]
    private RejectMode _contractRejectMode;

    [ObservableProperty]
    private string _testsJsonText = "[]";

    [ObservableProperty]
    private string _analysisRiskLevelText = "LOW";

    /// <summary>Optional comma-separated source address object UUIDs for AddRule TrafficPredicate (proto field only).</summary>
    [ObservableProperty]
    private string _predicateSourceAddressIdsText = string.Empty;

    /// <summary>Optional comma-separated destination address object UUIDs for AddRule TrafficPredicate.</summary>
    [ObservableProperty]
    private string _predicateDestinationAddressIdsText = string.Empty;

    /// <summary>Optional comma-separated service object UUIDs for AddRule TrafficPredicate.</summary>
    [ObservableProperty]
    private string _predicateServiceIdsText = string.Empty;

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task LoadAsync()
    {
        if (!TryParseRevisionId(out Guid revisionId))
        {
            return;
        }

        await RunBusyAsync(async ct =>
        {
            ApplyState(await _policies.LoadRevisionAsync(revisionId, ct).ConfigureAwait(true));
        }).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task CreateDraftAsync()
    {
        if (string.IsNullOrWhiteSpace(DraftNameText))
        {
            ErrorText = "Enter a draft policy name.";
            return;
        }

        await RunBusyAsync(async ct =>
        {
            ApplyState(await _policies.CreateDraftAsync(DraftNameText.Trim(), PolicyKind.CompanyBaseline, ct)
                .ConfigureAwait(true));
            DraftNameText = string.Empty;
        }).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanMutate))]
    private async Task AddRuleAsync()
    {
        if (!TryRequireMutableRevision(out Guid revisionId, out byte[] hash))
        {
            return;
        }

        await RunBusyAsync(async ct =>
        {
            TrafficPredicate? predicate = BuildPredicateFromProtoFields();
            ApplyState(await _policies.AddRuleAsync(
                    revisionId,
                    hash,
                    SelectedFamily,
                    SelectedChain,
                    SelectedStage,
                    SelectedEffect,
                    RuleDescriptionText.Trim(),
                    predicate,
                    ct)
                .ConfigureAwait(true));
            RuleDescriptionText = string.Empty;
        }).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanMutate))]
    private async Task ReorderRulesAsync()
    {
        if (!TryRequireMutableRevision(out Guid revisionId, out byte[] hash))
        {
            return;
        }

        List<Guid> orderedIds = [];
        foreach (string part in ReorderRuleIdsText.Split(
                     [',', ';', ' ', '\n', '\r'],
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Guid.TryParse(part, out Guid id))
            {
                ErrorText = $"Invalid rule UUID in reorder list: '{part}'.";
                return;
            }

            orderedIds.Add(id);
        }

        if (orderedIds.Count == 0)
        {
            ErrorText = "Enter comma-separated rule UUIDs for the same family/chain/stage.";
            return;
        }

        await RunBusyAsync(async ct =>
        {
            ApplyState(await _policies.ReorderRulesInStageAsync(
                    revisionId,
                    hash,
                    SelectedFamily,
                    SelectedChain,
                    SelectedStage,
                    orderedIds,
                    ct)
                .ConfigureAwait(true));
        }).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanMutate))]
    private async Task UpsertAddressAsync()
    {
        if (!TryRequireMutableRevision(out Guid revisionId, out byte[] hash))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(AddressNameText))
        {
            ErrorText = "Enter an address object name.";
            return;
        }

        await RunBusyAsync(async ct =>
        {
            ApplyState(await _policies.UpsertAddressObjectAsync(
                    revisionId,
                    hash,
                    AddressNameText.Trim(),
                    AddressFamily,
                    AddressEntriesText,
                    objectId: null,
                    ct)
                .ConfigureAwait(true));
            AddressNameText = string.Empty;
            AddressEntriesText = string.Empty;
        }).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanMutate))]
    private async Task UpsertServiceAsync()
    {
        if (!TryRequireMutableRevision(out Guid revisionId, out byte[] hash))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(ServiceNameText))
        {
            ErrorText = "Enter a service object name.";
            return;
        }

        if (!uint.TryParse(ServiceTcpPortText.Trim(), out uint port))
        {
            ErrorText = "Enter a valid TCP port (1..65535).";
            return;
        }

        await RunBusyAsync(async ct =>
        {
            ApplyState(await _policies.UpsertTcpServiceObjectAsync(
                    revisionId,
                    hash,
                    ServiceNameText.Trim(),
                    port,
                    objectId: null,
                    ct)
                .ConfigureAwait(true));
            ServiceNameText = string.Empty;
        }).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanMutate))]
    private async Task ReplaceContractsAsync()
    {
        if (!TryRequireMutableRevision(out Guid revisionId, out byte[] hash))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(ContractDisposition))
        {
            ErrorText = "Enter chain default disposition (DROP / REJECT / RETURN_TO_UNMANAGED).";
            return;
        }

        await RunBusyAsync(async ct =>
        {
            ApplyState(await _policies.ReplaceChainContractsAsync(
                    revisionId,
                    hash,
                    ContractFamily,
                    ContractChain,
                    ContractDisposition.Trim(),
                    ContractRejectMode,
                    ct)
                .ConfigureAwait(true));
        }).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanMutate))]
    private async Task ReplaceTestsAsync()
    {
        if (!TryRequireMutableRevision(out Guid revisionId, out byte[] hash))
        {
            return;
        }

        await RunBusyAsync(async ct =>
        {
            ApplyState(await _policies.ReplaceTestsAsync(revisionId, hash, TestsJsonText ?? string.Empty, ct)
                .ConfigureAwait(true));
        }).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task ValidateAsync()
    {
        if (!TryRequireLoadedRevision(out Guid revisionId, out byte[] hash))
        {
            return;
        }

        await RunBusyAsync(async ct =>
        {
            ApplyState(await _policies.ValidateAsync(revisionId, hash, ct).ConfigureAwait(true));
        }).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task SubmitAsync()
    {
        if (!TryRequireLoadedRevision(out Guid revisionId, out byte[] hash))
        {
            return;
        }

        await RunBusyAsync(async ct =>
        {
            ApplyState(await _policies.SubmitForReviewAsync(revisionId, hash, ct).ConfigureAwait(true));
        }).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task DiffAsync()
    {
        if (!TryParseRevisionId(out Guid afterId))
        {
            return;
        }

        if (!Guid.TryParse(DiffBaselineRevisionIdText.Trim(), out Guid beforeId))
        {
            ErrorText = "Enter a valid baseline revision UUID for DiffPolicyRevisions.";
            return;
        }

        await RunBusyAsync(async ct =>
        {
            PolicyDiffPanelResult diff = await _policies.DiffAsync(beforeId, afterId, ct).ConfigureAwait(true);
            DiffLines.Clear();
            foreach (PolicyFindingListItem line in diff.Lines)
            {
                DiffLines.Add(line);
            }

            if (!string.IsNullOrWhiteSpace(diff.RiskLevel))
            {
                RiskLevelText = diff.RiskLevel;
            }
        }).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task ComposeAsync()
    {
        if (!Guid.TryParse(ComposeNodeIdText.Trim(), out Guid nodeId))
        {
            ErrorText = "Enter a node UUID for ComposeEffectivePolicy (device context residual for NODE_EFFECTIVE).";
            return;
        }

        await RunBusyAsync(async ct =>
        {
            PolicyComposePanelResult compose = await _policies.ComposeAsync(nodeId, ct).ConfigureAwait(true);
            _logicalEffectiveHash = compose.LogicalEffectiveHash;
            Findings.Clear();
            foreach (PolicyFindingListItem finding in compose.Findings)
            {
                Findings.Add(finding);
            }
        }).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task RecordAnalysisAsync()
    {
        if (!TryRequireLoadedRevision(out Guid revisionId, out byte[] hash))
        {
            return;
        }

        byte[] logical = _logicalEffectiveHash ?? hash;
        string risk = string.IsNullOrWhiteSpace(AnalysisRiskLevelText) ? "LOW" : AnalysisRiskLevelText.Trim();
        await RunBusyAsync(async ct =>
        {
            PolicyAnalysisRunListItem run = await _policies.RecordAnalysisRunAsync(
                    revisionId,
                    hash,
                    logical,
                    risk,
                    Findings.ToArray(),
                    ct)
                .ConfigureAwait(true);
            _analysisRunId = run.Id;
            _analysisBundleHash = run.BundleHash;
            _dependencyFingerprint = run.DependencyFingerprint;
            AnalysisRunIdText = run.Id.ToString("D");
            RiskLevelText = run.RiskLevel;
            EffectiveRiskLevelText = run.EffectiveRiskLevel;
        }).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task ApproveAsync()
    {
        if (!TryRequireLoadedRevision(out Guid revisionId, out byte[] hash))
        {
            return;
        }

        if (_analysisRunId is not Guid runId
            || _analysisBundleHash is null
            || _dependencyFingerprint is null)
        {
            ErrorText = "Record an analysis run before Approve (needs run id, bundle hash, dependency fingerprint).";
            return;
        }

        await RunBusyAsync(async ct =>
        {
            await _policies.ApproveAsync(revisionId, runId, hash, _analysisBundleHash, _dependencyFingerprint, ct)
                .ConfigureAwait(true);
            ApplyState(await _policies.LoadRevisionAsync(revisionId, ct).ConfigureAwait(true));
        }).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task BindAsync()
    {
        if (!TryRequireLoadedRevision(out Guid revisionId, out byte[] hash))
        {
            return;
        }

        if (_analysisRunId is not Guid runId || _dependencyFingerprint is null)
        {
            ErrorText = "Record/Approve analysis run before Bind (desired binding ≠ deploy).";
            return;
        }

        await RunBusyAsync(async ct =>
        {
            await _policies.BindAsync(revisionId, runId, hash, _dependencyFingerprint, ct).ConfigureAwait(true);
        }).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanNeverDeploy))]
    private Task DeployAsync()
    {
        ErrorText = DeployResidual;
        return Task.CompletedTask;
    }

    private static bool CanNeverDeploy() => false;

    private bool CanOperate()
        => !IsBusy && _connection.State == ControllerConnectionState.Connected;

    private bool CanMutate()
        => CanOperate() && !IsReadOnly && _contentHash is not null;

    private bool TryParseRevisionId(out Guid revisionId)
    {
        if (!Guid.TryParse(RevisionIdText.Trim(), out revisionId))
        {
            ErrorText = "Enter a valid policy revision UUID.";
            return false;
        }

        return true;
    }

    private bool TryRequireLoadedRevision(out Guid revisionId, out byte[] hash)
    {
        revisionId = default;
        hash = [];
        if (!TryParseRevisionId(out revisionId))
        {
            return false;
        }

        if (_contentHash is null)
        {
            ErrorText = "Load or create a revision first (content hash required for CAS).";
            return false;
        }

        hash = _contentHash;
        return true;
    }

    private bool TryRequireMutableRevision(out Guid revisionId, out byte[] hash)
    {
        if (!TryRequireLoadedRevision(out revisionId, out hash))
        {
            return false;
        }

        if (IsReadOnly)
        {
            ErrorText = "Revision is read-only; mutate commands are disabled.";
            return false;
        }

        return true;
    }

    private TrafficPredicate? BuildPredicateFromProtoFields()
    {
        List<Uuid> sources = ParseUuidList(PredicateSourceAddressIdsText);
        List<Uuid> destinations = ParseUuidList(PredicateDestinationAddressIdsText);
        List<Uuid> services = ParseUuidList(PredicateServiceIdsText);
        if (sources.Count == 0 && destinations.Count == 0 && services.Count == 0)
        {
            return null;
        }

        TrafficPredicate predicate = new();
        if (sources.Count > 0)
        {
            predicate.SourceAddresses = new AddressSelector();
            predicate.SourceAddresses.Include.AddRange(sources);
        }

        if (destinations.Count > 0)
        {
            predicate.DestinationAddresses = new AddressSelector();
            predicate.DestinationAddresses.Include.AddRange(destinations);
        }

        if (services.Count > 0)
        {
            predicate.Services = new ServiceSelector();
            predicate.Services.Include.AddRange(services);
        }

        return predicate;
    }

    private static List<Uuid> ParseUuidList(string text)
    {
        List<Uuid> ids = [];
        if (string.IsNullOrWhiteSpace(text))
        {
            return ids;
        }

        foreach (string part in text.Split(
                     [',', ';', ' ', '\n', '\r'],
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Guid.TryParse(part, out Guid id))
            {
                throw new FormatException($"Invalid UUID in TrafficPredicate selector list: '{part}'.");
            }

            ids.Add(DesktopProtoUuid.FromGuid(id));
        }

        return ids;
    }

    private void ApplyState(PolicyRevisionPanelState state)
    {
        bool revisionChanged = !Guid.TryParse(RevisionIdText, out Guid previousId)
                               || previousId != state.RevisionId;
        bool contentChanged = _contentHash is null
                              || !state.ContentHash.AsSpan().SequenceEqual(_contentHash);
        bool invalidateReview = revisionChanged || contentChanged;

        RevisionIdText = state.RevisionId.ToString("D");
        StateText = state.StateText;
        ContentHashText = state.ContentHashHex;
        TestsJsonText = string.IsNullOrWhiteSpace(state.TestsJson) ? "[]" : state.TestsJson;
        IsReadOnly = state.IsReadOnly;
        _contentHash = state.ContentHash;

        if (invalidateReview)
        {
            Findings.Clear();
            DiffLines.Clear();
            _analysisRunId = null;
            _analysisBundleHash = null;
            _dependencyFingerprint = null;
            _logicalEffectiveHash = null;
            AnalysisRunIdText = string.Empty;
            RiskLevelText = string.Empty;
            EffectiveRiskLevelText = string.Empty;
        }

        Rules.Clear();
        foreach (PolicyRuleListItem rule in state.Rules)
        {
            Rules.Add(rule);
        }

        AddressObjects.Clear();
        foreach (PolicyAddressObjectListItem address in state.AddressObjects)
        {
            AddressObjects.Add(address);
        }

        ServiceObjects.Clear();
        foreach (PolicyServiceObjectListItem service in state.ServiceObjects)
        {
            ServiceObjects.Add(service);
        }

        ChainContracts.Clear();
        foreach (PolicyChainContractListItem contract in state.ChainContracts)
        {
            ChainContracts.Add(contract);
        }

        if (invalidateReview && state.RevisionWarnings.Count > 0)
        {
            foreach (PolicyFindingListItem warning in state.RevisionWarnings)
            {
                Findings.Add(warning);
            }
        }

        NotifyCommands();
    }

    private async Task RunBusyAsync(Func<CancellationToken, Task> action)
    {
        if (_connection.State != ControllerConnectionState.Connected)
        {
            ErrorText = "Connect to Controller first.";
            return;
        }

        IsBusy = true;
        NotifyCommands();
        ErrorText = null;
        try
        {
            await action(CancellationToken.None).ConfigureAwait(true);
        }
        catch (RpcException ex)
        {
            ErrorText = ex.Status.Detail;
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
        }
        finally
        {
            IsBusy = false;
            NotifyCommands();
        }
    }

    private void NotifyCommands()
    {
        LoadCommand.NotifyCanExecuteChanged();
        CreateDraftCommand.NotifyCanExecuteChanged();
        AddRuleCommand.NotifyCanExecuteChanged();
        ReorderRulesCommand.NotifyCanExecuteChanged();
        UpsertAddressCommand.NotifyCanExecuteChanged();
        UpsertServiceCommand.NotifyCanExecuteChanged();
        ReplaceContractsCommand.NotifyCanExecuteChanged();
        ReplaceTestsCommand.NotifyCanExecuteChanged();
        ValidateCommand.NotifyCanExecuteChanged();
        SubmitCommand.NotifyCanExecuteChanged();
        DiffCommand.NotifyCanExecuteChanged();
        ComposeCommand.NotifyCanExecuteChanged();
        RecordAnalysisCommand.NotifyCanExecuteChanged();
        ApproveCommand.NotifyCanExecuteChanged();
        BindCommand.NotifyCanExecuteChanged();
        DeployCommand.NotifyCanExecuteChanged();
    }

    private void OnConnectionStateChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            NotifyCommands();
        }
        else
        {
            Dispatcher.UIThread.Post(NotifyCommands);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _connection.StateChanged -= OnConnectionStateChanged;
    }
}
