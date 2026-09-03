using System.Net;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Google.Protobuf.Reflection;
using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Abstractions.ConnectionProfiles;
using Mfc.Application.Audit;
using Mfc.Application.Common;
using Mfc.Application.Deployment;
using Mfc.Application.Models;
using Mfc.Controller.Authorization;
using Mfc.Domain.Capabilities;
using Mfc.Domain.Deployment;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Infrastructure.Persistence.Entities;
using Mfc.Infrastructure.Persistence.Logging;
using Mfc.RouterOs.Deployment;
using Mfc.RouterOs.Transport;
using Mfc.UnitTests.Application.Fakes;
using Mfc.UnitTests.Deployment;
using Mfc.UnitTests.Onboarding;
using Xunit;
using CertificateTrustMode = Mfc.Domain.Inventory.CertificateTrustMode;
using DomainDevice = Mfc.Domain.Inventory.Device;
using DomainNode = Mfc.Domain.Inventory.Node;
using DomainOnboardingFacts = Mfc.Domain.Onboarding.OnboardingDevicePrerequisiteFacts;
using ProtoDeploymentService = Mfc.Contracts.Mfc.V1.DeploymentService;
using ProtoDeviceConnectionSummary = Mfc.Contracts.Mfc.V1.DeviceConnectionSummary;

namespace Mfc.UnitTests.Security;

/// <summary>
/// Living Spec matrix for Issue Set M6-08 AC 1–10 (E2E Spec §47 / §52 security + integrity).
/// Pure/domain/desktop/reflection + scripted TLS validation — no live CHR, no production secrets.
/// AC 11–14 (PostgreSQL backup/restore) live in IntegrationTests
/// <c>SecurityBackupRestoreAcceptanceTests</c>.
/// Reuses / mirrors existing suites: <see cref="ApiSslConnectionTests"/>,
/// <see cref="OnboardingPrerequisiteLivingSpecTests"/>,
/// <see cref="ConnectionProfileViewAndRedactionTests"/>,
/// <see cref="DeploymentFaultSecurityAcceptanceLivingSpecTests"/> AC11–12,
/// ArchitectureBoundary Desktop bans.
/// </summary>
public sealed class SecurityBackupRestoreLivingSpecTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 21, 14, 30, 0, TimeSpan.Zero);

    // ── AC 1 ──────────────────────────────────────────────────────────────────────

    /// <summary>Invalid CA chain, SAN mismatch, and SPKI pin mismatch are rejected with typed codes.</summary>
    [Fact]
    public void Ac1InvalidCaSanAndSpkiAreRejected()
    {
        (X509Certificate2 trustedCa, X509Certificate2 server) = CreateCertPair(sanHost: "127.0.0.1");
        (X509Certificate2 foreignCa, _) = CreateCertPair(sanHost: "127.0.0.1");
        using (trustedCa)
        using (server)
        using (foreignCa)
        using (SecretLease password = new("x"u8))
        {
            // Wrong CA (foreign root) → CertificateMismatch
            X509Certificate2Collection foreignRoots = [foreignCa];
            ApiSslConnectOptions wrongCa = BaseOptions(
                password,
                CertificateTrustMode.InternalCa,
                host: "127.0.0.1",
                trustedRoots: foreignRoots);
            Assert.False(ApiSslCertificateValidator.Validate(
                server, chain: null, SslPolicyErrors.RemoteCertificateChainErrors, wrongCa, out ApiSslException? caError));
            Assert.Equal(ApiSslErrors.CertificateMismatch, caError!.Code);

            // SAN mismatch → HostnameMismatch
            X509Certificate2Collection trustedRoots = [trustedCa];
            ApiSslConnectOptions sanMismatch = BaseOptions(
                password,
                CertificateTrustMode.InternalCa,
                host: "203.0.113.50",
                trustedRoots: trustedRoots);
            Assert.False(ApiSslCertificateValidator.Validate(
                server, chain: null, SslPolicyErrors.RemoteCertificateNameMismatch, sanMismatch, out ApiSslException? sanError));
            Assert.Equal(ApiSslErrors.HostnameMismatch, sanError!.Code);

            // SPKI pin mismatch → CertificateMismatch
            ApiSslConnectOptions spkiMismatch = BaseOptions(
                password,
                CertificateTrustMode.SpkiPin,
                host: "127.0.0.1",
                pinnedSpki: Hash256.Create(Enumerable.Repeat((byte)0xEE, 32).ToArray()));
            Assert.False(ApiSslCertificateValidator.Validate(
                server, chain: null, SslPolicyErrors.None, spkiMismatch, out ApiSslException? spkiError));
            Assert.Equal(ApiSslErrors.CertificateMismatch, spkiError!.Code);
        }
    }

    // ── AC 2 ──────────────────────────────────────────────────────────────────────

    /// <summary>Plain API (8728) is blocked on connect and as an onboarding prerequisite.</summary>
    [Fact]
    public async Task Ac2PlainApiIsBlocked()
    {
        using SecretLease password = new("x"u8);
        ApiSslException ex = await Assert.ThrowsAsync<ApiSslException>(() =>
            AuthenticatedRosConnection.ConnectAsync(new ApiSslConnectOptions
            {
                Host = "127.0.0.1",
                Port = 8728,
                Username = "ro",
                Password = password,
                TrustMode = CertificateTrustMode.SpkiPin,
                PinnedSpkiSha256 = Hash256.Create(Enumerable.Repeat((byte)1, 32).ToArray()),
            }));
        Assert.Equal(ApiSslErrors.PlainApiForbidden, ex.Code);

        DomainNode node = OnboardingTestFactory.RouterWithDevice(out DomainDevice device);
        OnboardingDevicePrerequisiteFacts facts = ValidPrerequisiteFacts(device.Id) with
        {
            PlainApi = OnboardingIpServiceFacts.Create(found: true, disabled: false, port: 8728),
        };
        OnboardingPrerequisiteResult result = OnboardingPrerequisiteValidator.Validate(
            node,
            new Dictionary<DeviceId, OnboardingDevicePrerequisiteFacts> { [device.Id] = facts });
        Assert.Contains(result.Findings, static f => f.Code == OnboardingCodes.PlainApiEnabled);
    }

    // ── AC 3 ──────────────────────────────────────────────────────────────────────

    /// <summary>Default RouterOS groups (flag or named defaults) are rejected.</summary>
    [Fact]
    public void Ac3DefaultRouterOsGroupIsRejected()
    {
        DomainNode node = OnboardingTestFactory.RouterWithDevice(out DomainDevice device);
        OnboardingDevicePrerequisiteFacts flagged = ValidPrerequisiteFacts(device.Id) with
        {
            ReadAccount = OnboardingServiceAccountFacts.Create(
                "mfc-read",
                "mfc-read-group",
                isDefaultGroup: true,
                policies: ["api", "read"],
                addressPrefixes: ["10.0.0.0/24"]),
        };
        Assert.Contains(
            OnboardingPrerequisiteValidator.Validate(
                    node,
                    new Dictionary<DeviceId, OnboardingDevicePrerequisiteFacts> { [device.Id] = flagged })
                .Findings,
            static f => f.Code == OnboardingCodes.ReadAccountInvalid);

        OnboardingDevicePrerequisiteFacts namedDefault = ValidPrerequisiteFacts(device.Id) with
        {
            DeploymentAccount = OnboardingServiceAccountFacts.Create(
                "mfc-deploy",
                "full",
                isDefaultGroup: false,
                policies: ["api", "read", "write", "test"],
                addressPrefixes: ["10.0.0.0/24"]),
        };
        Assert.Contains(
            OnboardingPrerequisiteValidator.Validate(
                    node,
                    new Dictionary<DeviceId, OnboardingDevicePrerequisiteFacts> { [device.Id] = namedDefault })
                .Findings,
            static f => f.Code == OnboardingCodes.DeployAccountInvalid);
    }

    // ── AC 4 ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Desktop never receives RouterOS credentials: view/proto surfaces have no password fields;
    /// Desktop assembly has no RouterOS / Domain / Infrastructure references.
    /// </summary>
    [Fact]
    public void Ac4DesktopNeverReceivesCredentials()
    {
        PropertyInfo[] viewProps = typeof(ConnectionProfileView).GetProperties(
            BindingFlags.Instance | BindingFlags.Public);
        Assert.DoesNotContain(
            viewProps,
            static p => p.Name.Contains("Password", StringComparison.OrdinalIgnoreCase)
                        || p.Name.Contains("Plain", StringComparison.OrdinalIgnoreCase)
                        || p.Name.Contains("SecretText", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(viewProps, static p => p.Name == nameof(ConnectionProfileView.SecretReference));

        MessageDescriptor summary = ProtoDeviceConnectionSummary.Descriptor;
        Assert.DoesNotContain(
            summary.Fields.InDeclarationOrder(),
            static f => f.Name.Contains("password", StringComparison.OrdinalIgnoreCase)
                        || f.Name.Contains("secret", StringComparison.OrdinalIgnoreCase));

        Assembly desktop = typeof(Mfc.Desktop.App).Assembly;
        Assert.DoesNotContain(
            desktop.GetReferencedAssemblies(),
            static a => string.Equals(a.Name, "Mfc.RouterOs", StringComparison.Ordinal)
                        || string.Equals(a.Name, "Mfc.Domain", StringComparison.Ordinal)
                        || string.Equals(a.Name, "Mfc.Infrastructure", StringComparison.Ordinal));
    }

    // ── AC 5 ──────────────────────────────────────────────────────────────────────

    /// <summary>Encrypted secret entity has no plaintext credential members (DB column surface).</summary>
    [Fact]
    public void Ac5DbEntityHasNoPlaintextCredentials()
    {
        PropertyInfo[] props = typeof(EncryptedSecretEntity).GetProperties(
            BindingFlags.Instance | BindingFlags.Public);
        string[] names = props.Select(static p => p.Name).ToArray();
        Assert.Contains(nameof(EncryptedSecretEntity.Ciphertext), names);
        Assert.Contains(nameof(EncryptedSecretEntity.WrappedDek), names);
        Assert.DoesNotContain(names, static n => n.Contains("Plain", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, static n => n.Contains("Password", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, static n => n.Contains("SecretText", StringComparison.OrdinalIgnoreCase));
        Assert.All(
            props,
            static p => Assert.True(
                p.PropertyType != typeof(string) || p.Name == nameof(EncryptedSecretEntity.Algorithm),
                $"Unexpected string property '{p.Name}' on encrypted_secrets."));
    }

    // ── AC 6 ──────────────────────────────────────────────────────────────────────

    /// <summary>Logs redact secrets; audit chain hashing excludes credential field names by contract.</summary>
    [Fact]
    public void Ac6LogsAndAuditContainNoSecrets()
    {
        const string raw = """Password=super-secret; {"password":"super-secret"} """;
        string redacted = RedactingJsonConsoleLoggerProvider.RedactForTests(raw);
        Assert.DoesNotContain("super-secret", redacted, StringComparison.Ordinal);
        Assert.Contains("Password=***", redacted, StringComparison.OrdinalIgnoreCase);

        ApiSslException loginFail = new(ApiSslErrors.AuthenticationFailed, "RouterOS rejected login credentials.");
        Assert.DoesNotContain("super-secret", loginFail.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password=", loginFail.ToString(), StringComparison.OrdinalIgnoreCase);

        // Mirror EfAuditEventWriter.AssertNoCredentialLeak: sensitive JSON property names must be rejected.
        Assert.Throws<InvalidOperationException>(() => AssertAuditPayloadHasNoCredentialFields(
            """{"password":"x"}"""));
        Assert.Throws<InvalidOperationException>(() => AssertAuditPayloadHasNoCredentialFields(
            """{"secret":"x"}"""));
        AssertAuditPayloadHasNoCredentialFields("""{"pin":"aabb"}""");
    }

    // ── AC 7 ──────────────────────────────────────────────────────────────────────

    /// <summary>RBAC bypass is impossible: denied permission fails closed before data access.</summary>
    [Fact]
    public async Task Ac7RbacBypassIsImpossible()
    {
        FakeAuthorizationBoundary auth = new();
        auth.DeniedPermissions.Add(ApplicationPermissions.AuditRead);
        auth.DeniedPermissions.Add(ApplicationPermissions.InventoryWrite);
        ListAuditEventsUseCase useCase = new(auth, new EmptyAuditStore());
        ApplicationResult<IReadOnlyList<AuditEventView>> denied = await useCase.ExecuteAsync(
            new ListAuditEventsQuery { Actor = "attacker", PageSize = 10 });
        Assert.True(denied.IsFailure);
        Assert.Equal("forbidden", denied.Error!.Code);

        UnauthorizedAccessException inventoryDenied = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            auth.EnsureAllowedAsync("attacker", ApplicationPermissions.InventoryWrite));
        Assert.Contains("inventory.write", inventoryDenied.Message, StringComparison.Ordinal);

        DenyAllAuthorizationBoundary denyAll = new();
        UnauthorizedAccessException productionDeny = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            denyAll.EnsureAllowedAsync("attacker", ApplicationPermissions.DeploymentWrite));
        Assert.Contains("deployment.write", productionDeny.Message, StringComparison.Ordinal);
    }

    // ── AC 8 ──────────────────────────────────────────────────────────────────────

    /// <summary>Arbitrary RouterOS path injection is impossible (mirrors M4-13 AC12).</summary>
    [Fact]
    public void Ac8ArbitraryRouterOsPathInjectionIsImpossible()
    {
        string[] forbiddenPathSubstrings =
        [
            "/move",
            "filter/remove",
            "address-list/remove",
            "address-list/set",
            "script/run",
        ];
        foreach (DeploymentWritePath path in Enum.GetValues<DeploymentWritePath>())
        {
            if (path == DeploymentWritePath.Ping)
            {
                continue;
            }

            string fixedPath = DeploymentWritePaths.Fixed(path);
            foreach (string forbidden in forbiddenPathSubstrings)
            {
                Assert.DoesNotContain(forbidden, fixedPath, StringComparison.OrdinalIgnoreCase);
            }
        }

        Assert.Throws<InvalidOperationException>(() => DeploymentWritePaths.Fixed((DeploymentWritePath)byte.MaxValue));

        Assembly routerOs = typeof(Mfc.RouterOs.AssemblyMarker).Assembly;
        string[] forbiddenNamespaces =
        [
            "Mfc.RouterOs.Write",
            "Mfc.RouterOs.Scripting",
            "Mfc.RouterOs.Terminal",
            "Mfc.RouterOs.GenericCommands",
        ];
        foreach (string ns in forbiddenNamespaces)
        {
            Type[] hits = routerOs.GetTypes()
                .Where(t => string.Equals(t.Namespace, ns, StringComparison.Ordinal)
                            || (t.Namespace is not null
                                && t.Namespace.StartsWith(ns + ".", StringComparison.Ordinal)))
                .ToArray();
            Assert.True(
                hits.Length == 0,
                $"Forbidden namespace '{ns}' is present: {string.Join(", ", hits.Select(static t => t.FullName))}");
        }
    }

    // ── AC 9 ──────────────────────────────────────────────────────────────────────

    /// <summary>Script injection is impossible: no script_source/raw_command surfaces; watchdog has no credentials.</summary>
    [Fact]
    public void Ac9ScriptInjectionIsImpossible()
    {
        string[] forbidden = ["password", "script_source", "raw_command", "force_apply", "executecommand"];
        foreach (DescriptorBase item in WalkDescriptor(ProtoDeploymentService.Descriptor.File))
        {
            string name = (item switch
            {
                MethodDescriptor m => m.Name,
                FieldDescriptor f => f.Name,
                MessageDescriptor msg => msg.Name,
                _ => string.Empty,
            }).ToLowerInvariant();
            foreach (string kw in forbidden)
            {
                Assert.DoesNotContain(kw, name, StringComparison.OrdinalIgnoreCase);
            }
        }

        DomainNode node = DeploymentTestFactory.RouterWithDevice(out _);
        DeploymentPlan plan = DeploymentTestFactory.PlanFor(node, T0);
        DeviceDeploymentPlan devicePlan = plan.DevicePlans[0];
        string script = DeploymentWatchdogScript.Render(
            devicePlan.OldAnchorTargets,
            devicePlan.NewAnchorTargets,
            devicePlan.AnchorRollbackOrder);
        Assert.DoesNotContain("password", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("user=", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/system/script/run", script, StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain(
            Enum.GetValues<DeploymentWritePath>().Select(DeploymentWritePaths.Fixed),
            static p => p.Contains("script/run", StringComparison.OrdinalIgnoreCase));
    }

    // ── AC 10 ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Audit tampering is detected via hash-chain recompute (same preimage as EfAuditEventWriter).
    /// Application-layer update/delete rejection is covered by Integration BootstrapPersistenceTests.
    /// </summary>
    [Fact]
    public void Ac10AuditTamperingIsDetected()
    {
        const string actor = "admin@test";
        const string action = "bootstrap.self-check";
        const string payload = """{"ok":true}""";
        byte[] first = ComputeAuditEventHash(previous: null, Guid.Parse("11111111-1111-1111-1111-111111111111"), actor, action, payload);
        byte[] second = ComputeAuditEventHash(previous: first, Guid.Parse("22222222-2222-2222-2222-222222222222"), actor, action, """{"ok":true,"n":2}""");

        Assert.Equal(32, first.Length);
        Assert.Equal(32, second.Length);
        Assert.False(first.AsSpan().SequenceEqual(second));

        byte[] tampered = ComputeAuditEventHash(previous: null, Guid.Parse("11111111-1111-1111-1111-111111111111"), actor, action, """{"ok":false}""");
        Assert.False(first.AsSpan().SequenceEqual(tampered));

        // Chain continuity: different predecessor *bytes* (same length) produce a different next hash.
        byte[] otherPrev = Enumerable.Repeat((byte)7, 32).ToArray();
        Guid eventId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        byte[] fromFirst = ComputeAuditEventHash(previous: first, eventId, actor, action, payload);
        byte[] fromOther = ComputeAuditEventHash(previous: otherPrev, eventId, actor, action, payload);
        Assert.False(fromFirst.AsSpan().SequenceEqual(fromOther));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    /// <summary>Mirrors <c>AuditEventHashing.Compute</c> / <c>EfAuditEventWriter</c> event hash preimage (SEC-03).</summary>
    internal static byte[] ComputeAuditEventHash(
        byte[]? previous,
        Guid eventId,
        string actor,
        string action,
        string payloadJson)
        => Mfc.Infrastructure.Audit.AuditEventHashing.Compute(previous, eventId, actor, action, payloadJson);

    /// <summary>Mirrors <c>EfAuditEventWriter.AssertNoCredentialLeak</c> for Living Spec without a DB.</summary>
    private static void AssertAuditPayloadHasNoCredentialFields(string payloadJson)
    {
        using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(payloadJson);
        ForbidSensitiveNames(doc.RootElement);

        static void ForbidSensitiveNames(System.Text.Json.JsonElement element)
        {
            switch (element.ValueKind)
            {
                case System.Text.Json.JsonValueKind.Object:
                    foreach (System.Text.Json.JsonProperty property in element.EnumerateObject())
                    {
                        string name = property.Name.ToLowerInvariant();
                        if (name.Contains("password", StringComparison.Ordinal)
                            || name.Contains("ciphertext", StringComparison.Ordinal)
                            || name is "pwd" or "credential" or "secret" or "plaintext")
                        {
                            throw new InvalidOperationException(
                                "Audit payload must not contain credential-related fields.");
                        }

                        ForbidSensitiveNames(property.Value);
                    }

                    break;
                case System.Text.Json.JsonValueKind.Array:
                    foreach (System.Text.Json.JsonElement item in element.EnumerateArray())
                    {
                        ForbidSensitiveNames(item);
                    }

                    break;
            }
        }
    }

    private static ApiSslConnectOptions BaseOptions(
        SecretLease password,
        CertificateTrustMode mode,
        string host,
        X509Certificate2Collection? trustedRoots = null,
        Hash256? pinnedSpki = null)
        => new()
        {
            Host = host,
            Port = ApiSslConnectOptions.ApiSslPort,
            Username = "ro",
            Password = password,
            TrustMode = mode,
            TrustedRootCertificates = trustedRoots,
            CertificateRevocationMode = X509RevocationMode.NoCheck,
            PinnedSpkiSha256 = pinnedSpki,
        };

    private static (X509Certificate2 Ca, X509Certificate2 Server) CreateCertPair(string sanHost)
    {
        using RSA caKey = RSA.Create(2048);
        CertificateRequest caRequest = new(
            "CN=Mfc M6-08 CA",
            caKey,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        caRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        X509Certificate2 ca = caRequest.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-30),
            DateTimeOffset.UtcNow.AddYears(1));

        using RSA serverKey = RSA.Create(2048);
        CertificateRequest serverRequest = new(
            $"CN={sanHost}",
            serverKey,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        serverRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        serverRequest.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false));
        SubjectAlternativeNameBuilder san = new();
        if (IPAddress.TryParse(sanHost, out IPAddress? ip))
        {
            san.AddIpAddress(ip);
        }
        else
        {
            san.AddDnsName(sanHost);
        }

        serverRequest.CertificateExtensions.Add(san.Build());
        using X509Certificate2 ephemeral = serverRequest.Create(
            ca,
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(30),
            RandomNumberGenerator.GetBytes(8));
        X509Certificate2 server = ephemeral.CopyWithPrivateKey(serverKey);
        return (
            X509CertificateLoader.LoadPkcs12(ca.Export(X509ContentType.Pfx), password: null),
            X509CertificateLoader.LoadPkcs12(server.Export(X509ContentType.Pfx), password: null));
    }

    private static DomainOnboardingFacts ValidPrerequisiteFacts(DeviceId deviceId)
        => DomainOnboardingFacts.Create(
            deviceId,
            CapabilityProfile.Create(
                RouterOsVersion.Create(7, 16, 2, "stable"),
                NonEmptyName.Create("x86_64"),
                NonEmptyName.Create("CHR"),
                packages: ["routeros", "ipv6"],
                ipv6Supported: true,
                vrrpSupported: true,
                bridgeSupported: true,
                apiSslCertificatePresent: true,
                SupportState.Supported,
                Hash256.Create(SHA256.HashData("manifest"u8))),
            exactSupportedBuild: true,
            OnboardingIpServiceFacts.Create(found: true, disabled: true, port: 8728),
            OnboardingIpServiceFacts.Create(
                found: true,
                disabled: false,
                port: 8729,
                certificate: "mfc-api",
                maxSessions: 4),
            OnboardingServiceAccountFacts.Create(
                "mfc-read",
                "mfc-read-group",
                isDefaultGroup: false,
                policies: ["api", "read"],
                addressPrefixes: ["10.0.0.0/24"]),
            OnboardingServiceAccountFacts.Create(
                "mfc-deploy",
                "mfc-deploy-group",
                isDefaultGroup: false,
                policies: ["api", "read", "write", "test"],
                addressPrefixes: ["10.0.0.0/24"]),
            OnboardingDeviceModeFacts.Create(schedulerEnabled: true, flagged: false));

    private static IEnumerable<DescriptorBase> WalkDescriptor(FileDescriptor file)
    {
        foreach (MessageDescriptor message in file.MessageTypes)
        {
            foreach (DescriptorBase item in WalkMessage(message))
            {
                yield return item;
            }
        }

        foreach (ServiceDescriptor service in file.Services)
        {
            foreach (MethodDescriptor method in service.Methods)
            {
                yield return method;
            }
        }
    }

    private static IEnumerable<DescriptorBase> WalkMessage(MessageDescriptor message)
    {
        yield return message;
        foreach (FieldDescriptor field in message.Fields.InDeclarationOrder())
        {
            yield return field;
        }

        foreach (MessageDescriptor nested in message.NestedTypes)
        {
            foreach (DescriptorBase child in WalkMessage(nested))
            {
                yield return child;
            }
        }
    }

    private sealed class EmptyAuditStore : Mfc.Application.Abstractions.Audit.IAuditEventReadStore
    {
        public Task<IReadOnlyList<Mfc.Application.Abstractions.Audit.AuditEventRecord>> ListNewestAsync(
            int limit,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Mfc.Application.Abstractions.Audit.AuditEventRecord>>([]);
    }
}
