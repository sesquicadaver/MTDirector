using System.Reflection;
using System.Text.Json;
using Mfc.Domain;
using Mfc.Domain.Capabilities;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Snapshots;
using Xunit;

namespace Mfc.UnitTests.Snapshots;

public sealed class HashDigestTests
{
    [Fact]
    public void Hash256UsesFixedSha256Length()
    {
        Assert.Equal(32, Hash256.Size);
        Assert.Equal("SHA-256", Hash256.AlgorithmName);

        Hash256 digest = Hash256.Create(Enumerable.Repeat((byte)0xAB, 32).ToArray());
        Assert.Equal(64, digest.ToString().Length);
        Assert.Equal(digest, Hash256.ParseHex(digest.ToString()));
        Assert.Equal(digest, Hash256.ParseHex("0x" + digest.ToString().ToUpperInvariant()));
        Assert.True(digest.Equals((object)Hash256.ParseHex(digest.ToString())));
        Assert.False(digest.Equals(null));
        Assert.False(digest.Equals("nope"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-hex")]
    [InlineData("abcd")]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    public void Hash256RejectsInvalidHexText(string hex)
    {
        Assert.ThrowsAny<Exception>(() => Hash256.ParseHex(hex));
    }

    [Fact]
    public void TypedHashesKeepConfigurationAndObservationSeparate()
    {
        string hex = new string('1', 64);
        ConfigurationHash config = ConfigurationHash.ParseHex(hex);
        ObservationHash observation = ObservationHash.ParseHex(hex);
        SnapshotHash snapshot = SnapshotHash.FromDigest(Hash256.ParseHex(hex));
        CapabilityHash capability = CapabilityHash.FromBytes(Hash256.ParseHex(hex).Bytes);

        Assert.Equal(config.Value, observation.Value);
        Assert.True(config == ConfigurationHash.FromDigest(config.Value));
        Assert.False(config != ConfigurationHash.ParseHex(hex));
        Assert.True(observation == ObservationHash.FromBytes(observation.Value.Bytes));
        Assert.False(observation != ObservationHash.ParseHex(hex));
        Assert.True(snapshot == SnapshotHash.ParseHex(hex));
        Assert.False(snapshot != SnapshotHash.ParseHex(hex));
        Assert.True(capability == CapabilityHash.ParseHex(hex));
        Assert.False(capability != CapabilityHash.ParseHex(hex));
        Assert.True(config.Equals((object)ConfigurationHash.ParseHex(hex)));
        Assert.False(config.Equals(null));
        Assert.True(observation.Equals((object)observation));
        Assert.False(observation.Equals(null));
        Assert.True(snapshot.Equals((object)snapshot));
        Assert.False(snapshot.Equals("x"));
        Assert.True(capability.Equals((object)capability));
        Assert.False(capability.Equals(null));
        Assert.Throws<ArgumentNullException>(() => ConfigurationHash.FromDigest(null!));
        Assert.Throws<ArgumentNullException>(() => ObservationHash.FromDigest(null!));
        Assert.Throws<ArgumentNullException>(() => SnapshotHash.FromDigest(null!));
        Assert.Throws<ArgumentNullException>(() => CapabilityHash.FromDigest(null!));
        Assert.ThrowsAny<Exception>(() => ConfigurationHash.ParseHex("short"));
    }
}

public sealed class SupportStateAndRouterOsVersionTests
{
    [Fact]
    public void SupportStateExposesRequiredValues()
    {
        Assert.Equal(
            new[]
            {
                SupportState.Supported,
                SupportState.ReadOnly,
                SupportState.NeedsRevalidation,
                SupportState.Unsupported,
            },
            Enum.GetValues<SupportState>());
    }

    [Theory]
    [InlineData("7.16.2", 7, 16, 2, null)]
    [InlineData("7.16.2-stable", 7, 16, 2, "stable")]
    [InlineData("7.16.2 (stable)", 7, 16, 2, "stable")]
    [InlineData("6.49", 6, 49, 0, null)]
    public void RouterOsVersionParsesValidForms(string text, int major, int minor, int patch, string? channel)
    {
        RouterOsVersion version = RouterOsVersion.Parse(text);
        Assert.Equal(major, version.Major);
        Assert.Equal(minor, version.Minor);
        Assert.Equal(patch, version.Patch);
        Assert.Equal(channel, version.Channel);
        Assert.Equal(version, RouterOsVersion.Create(major, minor, patch, channel));
        Assert.Equal(version.ToString(), version.ToString());
        Assert.True(version.Equals((object)version));
        Assert.False(version.Equals(null));
        Assert.False(version.Equals("7.16.2"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("v7.16")]
    [InlineData("7")]
    [InlineData("7.16.2.1")]
    [InlineData("7.x.1")]
    [InlineData("7.16.2-bad channel")]
    [InlineData("7.16.2-")]
    [InlineData("a.b")]
    [InlineData("7.16.x")]
    public void RouterOsVersionRejectsInvalidText(string text)
    {
        Assert.ThrowsAny<Exception>(() => RouterOsVersion.Parse(text));
    }

    [Fact]
    public void RouterOsVersionRejectsNegativeAndMismatchedEquality()
    {
        Assert.Throws<DomainInvariantException>(() => RouterOsVersion.Create(-1, 0, 0));
        Assert.Throws<DomainInvariantException>(() => RouterOsVersion.Create(0, -1, 0));
        Assert.Throws<DomainInvariantException>(() => RouterOsVersion.Create(0, 0, -1));

        RouterOsVersion a = RouterOsVersion.Create(7, 16, 2, "stable");
        RouterOsVersion b = RouterOsVersion.Create(7, 16, 2);
        Assert.NotEqual(a, b);
        Assert.NotEqual(a.GetHashCode(), b.GetHashCode());
        Assert.Equal("7.16.2-stable", a.ToString());
        Assert.Equal("7.16.2", b.ToString());
    }
}

public sealed class CapabilityProfileTests
{
    [Fact]
    public void EqualityIsDeterministicAndIndependentOfPackageInputOrder()
    {
        CapabilityProfile left = CreateProfile(packages: ["routing", "ipv6"]);
        CapabilityProfile right = CreateProfile(packages: ["ipv6", "routing"]);

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
        Assert.True(left.Equals((object)right));
        Assert.False(left.Equals(null));
        Assert.False(left.Equals("profile"));
        Assert.NotEqual(left, CreateProfile(packages: ["routing"], ipv6: false));
        Assert.NotEqual(left, CreateProfile(packages: ["routing", "ipv6"], support: SupportState.ReadOnly));
    }

    [Fact]
    public void SerializationRoundTripDoesNotAffectEquality()
    {
        CapabilityProfile original = CreateProfile(packages: ["security"]);
        var dto = new
        {
            Version = original.Version.ToString(),
            Architecture = original.Architecture.Value,
            Model = original.Model.Value,
            Packages = original.Packages.ToArray(),
            original.Ipv6Supported,
            original.VrrpSupported,
            original.BridgeSupported,
            original.ApiSslCertificatePresent,
            SupportState = original.SupportState.ToString(),
            CompatibilityManifestHash = original.CompatibilityManifestHash.ToString(),
        };

        string json = JsonSerializer.Serialize(dto);
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        CapabilityProfile restored = CapabilityProfile.Create(
            RouterOsVersion.Parse(root.GetProperty("Version").GetString()!),
            NonEmptyName.Create(root.GetProperty("Architecture").GetString()!),
            NonEmptyName.Create(root.GetProperty("Model").GetString()!),
            root.GetProperty("Packages").EnumerateArray().Select(e => e.GetString()!),
            root.GetProperty("Ipv6Supported").GetBoolean(),
            root.GetProperty("VrrpSupported").GetBoolean(),
            root.GetProperty("BridgeSupported").GetBoolean(),
            root.GetProperty("ApiSslCertificatePresent").GetBoolean(),
            Enum.Parse<SupportState>(root.GetProperty("SupportState").GetString()!),
            Hash256.ParseHex(root.GetProperty("CompatibilityManifestHash").GetString()!));

        Assert.Equal(original, restored);
    }

    [Fact]
    public void CapabilityProfileRejectsEmptyPackageNamesAndNullArgs()
    {
        Assert.Throws<DomainInvariantException>(() => CreateProfile(packages: ["ok", "  "]));
        Assert.Throws<ArgumentNullException>(() =>
            CapabilityProfile.Create(
                null!,
                NonEmptyName.Create("arm64"),
                NonEmptyName.Create("CCR2004"),
                [],
                true,
                true,
                true,
                true,
                SupportState.Supported,
                Hash256.Create(Enumerable.Repeat((byte)1, 32).ToArray())));
    }

    [Fact]
    public void DomainTypesDoNotExposeCredentialOrRawPayloadFields()
    {
        AssertNoSensitiveMembers(typeof(CapabilityProfile));
        AssertNoSensitiveMembers(typeof(SnapshotMetadata));
        AssertNoSensitiveMembers(typeof(TopologyObservation));
    }

    private static CapabilityProfile CreateProfile(
        string[] packages,
        bool ipv6 = true,
        SupportState support = SupportState.Supported)
        => CapabilityProfile.Create(
            RouterOsVersion.Parse("7.16.2"),
            NonEmptyName.Create("arm64"),
            NonEmptyName.Create("CCR2004"),
            packages,
            ipv6Supported: ipv6,
            vrrpSupported: true,
            bridgeSupported: true,
            apiSslCertificatePresent: true,
            support,
            Hash256.Create(Enumerable.Repeat((byte)9, 32).ToArray()));

    private static void AssertNoSensitiveMembers(Type type)
    {
        string[] forbidden = ["password", "credential", "secret", "raw", "payload", "apiresponse"];
        foreach (MemberInfo member in type.GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            string name = member.Name.ToLowerInvariant();
            Assert.DoesNotContain(forbidden, token => name.Contains(token, StringComparison.Ordinal));
        }
    }
}

public sealed class TopologyObservationTests
{
    [Fact]
    public void EqualityIgnoresInputOrdering()
    {
        DeviceId device = DeviceId.New();
        DateTimeOffset at = DateTimeOffset.UtcNow;
        TopologyObservation left = TopologyObservation.Create(
            NodeId.New(),
            at,
            ["ether2", "ether1"],
            [
                new VrrpRoleObservation(device, IpAddressFamily.IPv4, 10, VrrpMemberObservedState.Backup),
                new VrrpRoleObservation(device, IpAddressFamily.IPv4, 1, VrrpMemberObservedState.Master),
            ]);

        TopologyObservation right = TopologyObservation.Create(
            left.NodeId,
            at,
            ["ether1", "ether2"],
            [
                new VrrpRoleObservation(device, IpAddressFamily.IPv4, 1, VrrpMemberObservedState.Master),
                new VrrpRoleObservation(device, IpAddressFamily.IPv4, 10, VrrpMemberObservedState.Backup),
            ]);

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
        Assert.True(left.Equals((object)right));
        Assert.False(left.Equals(null));
        Assert.False(left.Equals("topo"));

        TopologyObservation otherNode = TopologyObservation.Create(
            NodeId.New(),
            at,
            left.ActiveInterfaceKeys,
            left.VrrpRoles);
        Assert.NotEqual(left, otherNode);

        VrrpRoleObservation role = left.VrrpRoles[0];
        Assert.True(role == left.VrrpRoles[0]);
        Assert.False(role != left.VrrpRoles[0]);
        Assert.True(role.Equals((object)role));
        Assert.False(role.Equals(null));
        Assert.NotEqual(
            role,
            new VrrpRoleObservation(device, IpAddressFamily.IPv6, role.Vrid, VrrpMemberObservedState.Backup));
    }

    [Fact]
    public void RejectsNonUtcAndEmptyInterfaceKeys()
    {
        // Explicit non-zero offset: DateTimeOffset.Now is UTC on CI runners and would not throw.
        DateTimeOffset nonUtc = new(2026, 8, 13, 12, 0, 0, TimeSpan.FromHours(3));
        Assert.Throws<DomainInvariantException>(() =>
            TopologyObservation.Create(
                NodeId.New(),
                nonUtc,
                ["ether1"],
                Array.Empty<VrrpRoleObservation>()));

        Assert.Throws<DomainInvariantException>(() =>
            TopologyObservation.Create(
                NodeId.New(),
                DateTimeOffset.UtcNow,
                [""],
                Array.Empty<VrrpRoleObservation>()));

        Assert.Throws<ArgumentNullException>(() =>
            TopologyObservation.Create(
                NodeId.New(),
                DateTimeOffset.UtcNow,
                null!,
                Array.Empty<VrrpRoleObservation>()));

        Assert.Throws<DomainInvariantException>(() =>
            new VrrpRoleObservation(DeviceId.New(), IpAddressFamily.IPv4, 0, VrrpMemberObservedState.Master));
    }
}

public sealed class SnapshotMetadataTests
{
    [Fact]
    public void CompletedSnapshotKeepsDistinctHashSlots()
    {
        DeviceId deviceId = DeviceId.New();
        ConfigurationHash config = ConfigurationHash.ParseHex(new string('a', 64));
        ObservationHash observation = ObservationHash.ParseHex(new string('b', 64));
        CapabilityHash capability = CapabilityHash.ParseHex(new string('c', 64));
        SnapshotHash snapshot = SnapshotHash.ParseHex(new string('d', 64));

        SnapshotMetadata meta = SnapshotMetadata.CreateCompleted(
            deviceId,
            config,
            observation,
            capability,
            snapshot,
            DateTimeOffset.UtcNow);

        Assert.Equal(SnapshotStatus.Completed, meta.Status);
        Assert.Equal(config, meta.ConfigurationHash);
        Assert.Equal(observation, meta.ObservationHash);
        Assert.Equal(capability, meta.CapabilityHash);
        Assert.Equal(snapshot, meta.SnapshotHash);
        Assert.NotEqual(meta.ConfigurationHash!.Value.Value, meta.ObservationHash!.Value.Value);
        Assert.True(meta.Equals(meta));
        Assert.True(meta.Equals((object)meta));
        Assert.False(meta.Equals(null));
        Assert.False(meta.Equals("meta"));
        _ = meta.GetHashCode();
    }

    [Fact]
    public void FailedSnapshotHasNoHashesAndSnapshotIdIsUnique()
    {
        SnapshotMetadata a = SnapshotMetadata.CreateFailed(DeviceId.New(), DateTimeOffset.UtcNow);
        SnapshotMetadata b = SnapshotMetadata.CreateFailed(a.DeviceId, a.CompletedAtUtc!.Value);

        Assert.Equal(SnapshotStatus.Failed, a.Status);
        Assert.Null(a.ConfigurationHash);
        Assert.Null(a.ObservationHash);
        Assert.NotEqual(a.Id, b.Id);
        Assert.False(a.Equals(b));
        Assert.Contains(SnapshotStatus.Queued, Enum.GetValues<SnapshotStatus>());
        Assert.Contains(SnapshotStatus.Canceled, Enum.GetValues<SnapshotStatus>());
    }

    [Fact]
    public void SnapshotFactoriesRejectNonUtcTimestamps()
    {
        ConfigurationHash config = ConfigurationHash.ParseHex(new string('a', 64));
        ObservationHash observation = ObservationHash.ParseHex(new string('b', 64));
        CapabilityHash capability = CapabilityHash.ParseHex(new string('c', 64));
        SnapshotHash snapshot = SnapshotHash.ParseHex(new string('d', 64));
        // Explicit non-zero offset: DateTimeOffset.Now is UTC on CI runners and would not throw.
        DateTimeOffset local = new(2026, 8, 13, 12, 0, 0, TimeSpan.FromHours(3));

        Assert.Throws<DomainInvariantException>(() =>
            SnapshotMetadata.CreateCompleted(DeviceId.New(), config, observation, capability, snapshot, local));
        Assert.Throws<DomainInvariantException>(() =>
            SnapshotMetadata.CreateFailed(DeviceId.New(), local));
    }

    [Fact]
    public void SnapshotIdRoundTripsThroughGuidText()
    {
        SnapshotId id = SnapshotId.New();
        Assert.Equal(id.Value, Guid.Parse(id.ToString()));
    }
}
