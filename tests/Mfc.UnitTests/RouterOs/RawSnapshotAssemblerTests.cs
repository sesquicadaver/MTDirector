using System.Text;
using System.Text.Json;
using Mfc.Application.Abstractions.RouterOs;
using Mfc.Application.Common;
using Mfc.Application.Snapshots;
using Mfc.RouterOs.Commands;
using Mfc.RouterOs.Redaction;
using Mfc.RouterOs.Snapshot;
using Mfc.UnitTests.Application.Fakes;
using Xunit;

namespace Mfc.UnitTests.RouterOs;

public sealed class RawSnapshotAssemblerTests
{
    private static readonly JsonSerializerOptions FixtureJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly string[] ForbiddenTokens =
    [
        "password",
        "secret",
        "private-key",
        "private_key",
        "token",
        "api-key",
        "passphrase",
        "psk",
    ];

    [Fact]
    public void AssembleProducesSchemaVersionAndSectionStatus()
    {
        RawSnapshotAssemblyResult result = AssembleSample();

        Assert.Equal(1, result.Document.SchemaVersion);
        Assert.Equal(2, result.Document.Sections.Count);
        Assert.Equal("completed", result.Document.Sections[0].CaptureStatus);
        Assert.Equal("partial_error", result.Document.Sections[1].CaptureStatus);
        Assert.Equal("ROS_TRAP", result.Document.Sections[1].ErrorCode);
        Assert.Contains("not enough permissions", result.Document.Sections[1].ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void PartialErrorsAreNotMaskedAsCompleted()
    {
        RawSectionCaptureInput failed = new()
        {
            SourceMenu = "/ip/firewall/nat/print",
            CommandId = RosReadCommandId.Ipv4Nat,
            CaptureStatus = RawSectionCaptureStatus.Failed,
            ErrorCode = "ROS_TRAP",
            ErrorMessage = "no such command prefix",
            Records = [],
        };

        RawSnapshotAssemblyResult result = RawSnapshotAssembler.Assemble(
            [failed],
            UtcRange());

        Assert.Equal("failed", result.Document.Sections[0].CaptureStatus);
        Assert.Equal("ROS_TRAP", result.Document.Sections[0].ErrorCode);
        Assert.NotEqual("completed", result.Document.Sections[0].CaptureStatus);
    }

    [Fact]
    public void ForbiddenSecretsAreStrippedButUnknownSafePropertiesRemain()
    {
        RawSectionCaptureInput section = new()
        {
            SourceMenu = "/system/identity/print",
            CommandId = RosReadCommandId.SystemIdentity,
            CaptureStatus = RawSectionCaptureStatus.Completed,
            Records =
            [
                new RawRecordInput
                {
                    KnownProperties = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["name"] = "gw1",
                        ["password"] = "should-not-persist",
                        ["secret"] = "also-gone",
                    },
                    UnknownProperties = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["vendor-extra"] = "keep-me",
                        ["private-key"] = "pk-gone",
                        ["token"] = "tok-gone",
                    },
                },
            ],
        };

        RawSnapshotAssemblyResult result = RawSnapshotAssembler.Assemble([section], UtcRange());
        RawSnapshotRecord record = result.Document.Sections[0].Records[0];

        Assert.Equal("gw1", record.Properties["name"]);
        Assert.False(record.Properties.ContainsKey("password"));
        Assert.False(record.Properties.ContainsKey("secret"));
        Assert.Equal("keep-me", record.UnknownProperties["vendor-extra"]);
        Assert.False(record.UnknownProperties.ContainsKey("private-key"));
        Assert.False(record.UnknownProperties.ContainsKey("token"));

        string json = Encoding.UTF8.GetString(result.Utf8Payload);
        Assert.DoesNotContain("should-not-persist", json, StringComparison.Ordinal);
        Assert.DoesNotContain("pk-gone", json, StringComparison.Ordinal);
    }

    [Fact]
    public void RedactionIsCentralizedThroughSensitiveFieldRegistry()
    {
        Assert.True(SensitiveFieldRegistry.IsForbidden("password"));
        Assert.True(SensitiveFieldRegistry.IsForbidden("token"));
        Assert.True(SensitiveFieldRegistry.IsForbidden("private-key"));
        IReadOnlyDictionary<string, string> redacted = SensitiveFieldRegistry.RedactForStorage(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["name"] = "x",
                ["password"] = "y",
            });
        Assert.True(redacted.ContainsKey("name"));
        Assert.False(redacted.ContainsKey("password"));
    }

    [Fact]
    public void LoginSentenceSectionsAreRejected()
    {
        RawSectionCaptureInput login = new()
        {
            SourceMenu = "/login",
            CaptureStatus = RawSectionCaptureStatus.Completed,
            Records = [],
        };

        Assert.Throws<ArgumentException>(() => RawSnapshotAssembler.Assemble([login], UtcRange()));
    }

    [Fact]
    public void CaptureTimestampsAreSeparateFromSectionConfiguration()
    {
        RawSnapshotAssemblyResult result = AssembleSample();
        RawSnapshotWireEnvelope wire = RawSnapshotAssembler.Deserialize(result.Utf8Payload);

        Assert.NotNull(wire.Capture);
        Assert.Equal(result.Timestamps.StartedAtUtc.ToString("O"), wire.Capture.StartedAtUtc);
        Assert.All(wire.Sections, s =>
        {
            Assert.DoesNotContain(
                s.Records.SelectMany(r => r.Properties.Keys),
                key => key.Contains("started", StringComparison.OrdinalIgnoreCase)
                       || key.Contains("completed", StringComparison.OrdinalIgnoreCase));
        });
    }

    [Fact]
    public void SerializationIsDeterministicWithinSchema()
    {
        IReadOnlyList<RawSectionCaptureInput> sections = SampleSections();
        RawSnapshotCaptureTimestamps timestamps = UtcRange();

        byte[] a = RawSnapshotAssembler.Assemble(sections, timestamps).Utf8Payload;
        byte[] b = RawSnapshotAssembler.Assemble(sections, timestamps).Utf8Payload;

        Assert.Equal(a, b);
    }

    [Fact]
    public void OversizedSnapshotThrowsTypedError()
    {
        RawSectionCaptureInput huge = new()
        {
            SourceMenu = "/ip/firewall/filter/print",
            CaptureStatus = RawSectionCaptureStatus.Completed,
            Records =
            [
                new RawRecordInput
                {
                    KnownProperties = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["comment"] = new string('x', 4096),
                    },
                    UnknownProperties = new Dictionary<string, string>(StringComparer.Ordinal),
                },
            ],
        };

        RawSnapshotTooLargeException ex = Assert.Throws<RawSnapshotTooLargeException>(() =>
            RawSnapshotAssembler.Assemble([huge], UtcRange(), maxSnapshotBytes: 64));
        Assert.Equal("SNAPSHOT_TOO_LARGE", RawSnapshotTooLargeException.ErrorCode);
        Assert.True(ex.ActualBytes > ex.MaxBytes);
    }

    [Fact]
    public void SanitizedFixturePassesSecretScanner()
    {
        string path = Path.Combine(
            FindRepoRoot(),
            "tests",
            "Mfc.UnitTests",
            "RouterOs",
            "Fixtures",
            "raw-snapshot.sanitized.json");
        Assert.True(File.Exists(path), $"Missing fixture at {path}");
        string json = File.ReadAllText(path);
        AssertSecretFree(json);

        // Round-trip: assemble equivalent content and scan again.
        RawSnapshotWireEnvelope fixture = JsonSerializer.Deserialize<RawSnapshotWireEnvelope>(
            json,
            FixtureJsonOptions)!;
        Assert.Equal(1, fixture.SchemaVersion);

        List<RawSectionCaptureInput> sections = [];
        foreach (RawSnapshotWireSection section in fixture.Sections)
        {
            sections.Add(new RawSectionCaptureInput
            {
                SourceMenu = section.SourceMenu,
                CommandId = section.CommandId is null
                    ? null
                    : Enum.Parse<RosReadCommandId>(section.CommandId),
                CaptureStatus = section.CaptureStatus switch
                {
                    "completed" => RawSectionCaptureStatus.Completed,
                    "partial_error" => RawSectionCaptureStatus.PartialError,
                    "failed" => RawSectionCaptureStatus.Failed,
                    "unsupported" => RawSectionCaptureStatus.Unsupported,
                    _ => throw new InvalidOperationException(section.CaptureStatus),
                },
                ErrorCode = section.ErrorCode,
                ErrorMessage = section.ErrorMessage,
                Records = section.Records.Select(static r => new RawRecordInput
                {
                    KnownProperties = r.Properties,
                    UnknownProperties = r.UnknownProperties,
                }).ToArray(),
            });
        }

        RawSnapshotAssemblyResult assembled = RawSnapshotAssembler.Assemble(
            sections,
            new RawSnapshotCaptureTimestamps
            {
                StartedAtUtc = DateTimeOffset.Parse(fixture.Capture.StartedAtUtc, System.Globalization.CultureInfo.InvariantCulture),
                CompletedAtUtc = DateTimeOffset.Parse(fixture.Capture.CompletedAtUtc, System.Globalization.CultureInfo.InvariantCulture),
            });
        AssertSecretFree(Encoding.UTF8.GetString(assembled.Utf8Payload));
    }

    [Fact]
    public async Task UseCaseMapsOversizedToSnapshotTooLarge()
    {
        FakeAuthorizationBoundary auth = new();
        AssembleRawSnapshotUseCase useCase = new(auth, new RawSnapshotAssemblerPort());

        ApplicationResult<RawSnapshotView> result = await useCase.ExecuteAsync(
            new AssembleRawSnapshotCommand
            {
                Actor = "a",
                Request = new AssembleRawSnapshotRequest
                {
                    StartedAtUtc = DateTimeOffset.UtcNow,
                    CompletedAtUtc = DateTimeOffset.UtcNow,
                    MaxSnapshotBytes = 32,
                    Sections =
                    [
                        new RawSnapshotSectionDraft
                        {
                            SourceMenu = "/ip/service/print",
                            CaptureStatus = "completed",
                            Records =
                            [
                                new RawSnapshotRecordDraft
                                {
                                    KnownProperties = new Dictionary<string, string>(StringComparer.Ordinal)
                                    {
                                        ["name"] = new string('z', 256),
                                    },
                                    UnknownProperties = new Dictionary<string, string>(StringComparer.Ordinal),
                                },
                            ],
                        },
                    ],
                },
            });

        Assert.True(result.IsFailure);
        Assert.Equal("snapshot_too_large", result.Error!.Code);
    }

    private static void AssertSecretFree(string json)
    {
        foreach (string token in ForbiddenTokens)
        {
            Assert.DoesNotContain($"\"{token}\"", json, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain("/login", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("should-not-persist", json, StringComparison.Ordinal);
    }

    private static RawSnapshotAssemblyResult AssembleSample()
        => RawSnapshotAssembler.Assemble(SampleSections(), UtcRange());

    private static IReadOnlyList<RawSectionCaptureInput> SampleSections()
        =>
        [
            new RawSectionCaptureInput
            {
                SourceMenu = "/ip/service/print",
                CommandId = RosReadCommandId.IpServices,
                CaptureStatus = RawSectionCaptureStatus.PartialError,
                ErrorCode = "ROS_TRAP",
                ErrorMessage = "failure: not enough permissions (trap preserved)",
                Records =
                [
                    new RawRecordInput
                    {
                        KnownProperties = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["name"] = "api-ssl",
                            ["port"] = "8729",
                            ["disabled"] = "false",
                        },
                        UnknownProperties = new Dictionary<string, string>(StringComparer.Ordinal),
                    },
                ],
            },
            new RawSectionCaptureInput
            {
                SourceMenu = "/ip/firewall/filter/print",
                CommandId = RosReadCommandId.Ipv4Filter,
                CaptureStatus = RawSectionCaptureStatus.Completed,
                Records =
                [
                    new RawRecordInput
                    {
                        KnownProperties = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["chain"] = "forward",
                            ["action"] = "accept",
                            ["disabled"] = "false",
                            ["comment"] = "lab-forward",
                        },
                        UnknownProperties = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["vendor-extra"] = "keep-me",
                        },
                    },
                ],
            },
        ];

    private static RawSnapshotCaptureTimestamps UtcRange()
        => new()
        {
            StartedAtUtc = new DateTimeOffset(2026, 8, 9, 5, 0, 0, TimeSpan.Zero),
            CompletedAtUtc = new DateTimeOffset(2026, 8, 9, 5, 0, 1, TimeSpan.Zero),
        };

    private static string FindRepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "MikroTikFirewallController.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
