using Google.Protobuf;
using Google.Protobuf.Reflection;
using Mfc.Contracts.Mfc.V1;
using Mfc.Controller.Grpc;
using Xunit;

namespace Mfc.UnitTests.Contracts;

/// <summary>Contract round-trip / forward-compat checks for M1-26 snapshot/diff protos.</summary>
public sealed class SnapshotProtoContractTests
{
    [Fact]
    public void DiffEntryWithUnknownEnumValuesRoundTrips()
    {
        DiffEntry entry = new()
        {
            SectionId = "firewall.ipv4.filter",
            Domain = (DiffDomain)42,
            Confidence = (MatchConfidence)77,
            RecordKey = "fwc:rule:aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
        };
        entry.Changes.Add((DiffChange)99);
        entry.Changes.Add(DiffChange.Modified);
        entry.FieldDiffs.Add(new FieldDiff
        {
            FieldName = "action",
            Before = new CanonicalValue { StringValue = "accept" },
            After = new CanonicalValue { StringValue = "drop" },
        });

        DiffEntry parsed = DiffEntry.Parser.ParseFrom(entry.ToByteArray());
        Assert.Equal((DiffDomain)42, parsed.Domain);
        Assert.Equal((MatchConfidence)77, parsed.Confidence);
        Assert.Contains((DiffChange)99, parsed.Changes);
        Assert.Contains(DiffChange.Modified, parsed.Changes);
        Assert.Equal("action", parsed.FieldDiffs[0].FieldName);
        Assert.Equal("drop", parsed.FieldDiffs[0].After.StringValue);
    }

    [Fact]
    public void Sha256AndUuidRoundTripAsFixedLengthBytes()
    {
        Guid original = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");
        Uuid uuid = new() { Value = ByteString.CopyFrom(original.ToByteArray(bigEndian: true)) };
        Uuid uuid2 = Uuid.Parser.ParseFrom(uuid.ToByteArray());
        Assert.Equal(16, uuid2.Value.Length);
        Assert.Equal(original, new Guid(uuid2.Value.Span, bigEndian: true));

        byte[] digest = SnapshotProtoMapper.HexToSha256Bytes(new string('a', 64));
        Assert.Equal(32, digest.Length);
        Sha256 sha = new() { Value = ByteString.CopyFrom(digest) };
        Sha256 sha2 = Sha256.Parser.ParseFrom(sha.ToByteArray());
        Assert.Equal(32, sha2.Value.Length);
        Assert.Equal(digest, sha2.Value.ToByteArray());
    }

    [Fact]
    public void SnapshotSectionPageRoundTripsRecords()
    {
        SnapshotSectionPage page = new()
        {
            CaptureId = new Uuid { Value = ByteString.CopyFrom(Guid.NewGuid().ToByteArray(bigEndian: true)) },
            SectionId = "system.identity",
            Ordered = false,
            NextPageToken = "10",
        };
        page.Records.Add(new SnapshotRecord
        {
            StableKey = "identity|router",
            Configuration =
            {
                new CanonicalField
                {
                    Name = "name",
                    Value = new CanonicalValue { StringValue = "router" },
                },
            },
        });

        SnapshotSectionPage parsed = SnapshotSectionPage.Parser.ParseFrom(page.ToByteArray());
        Assert.Equal("system.identity", parsed.SectionId);
        Assert.Single(parsed.Records);
        Assert.Equal("router", parsed.Records[0].Configuration[0].Value.StringValue);
        Assert.Equal("10", parsed.NextPageToken);
    }

    [Fact]
    public void SnapshotServiceDescriptorExposesVerticalSliceRpcs()
    {
        ServiceDescriptor descriptor = SnapshotService.Descriptor;
        string[] methods = descriptor.Methods.Select(m => m.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Assert.Equal(
            [
                "CompareSnapshots",
                "GetSnapshotSection",
                "GetSnapshotSummary",
                "ListCaptures",
                "StartCapture",
                "WatchCapture",
            ],
            methods);
        Assert.DoesNotContain("CaptureSnapshot", methods);
        Assert.DoesNotContain("WatchSnapshotCapture", methods);
        Assert.DoesNotContain("ListSnapshots", methods);
        Assert.DoesNotContain("GetRawSnapshot", methods);
    }

    [Fact]
    public void SnapshotResponseMessagesHaveNoPasswordFields()
    {
        MessageDescriptor[] descriptors =
        [
            SnapshotSummary.Descriptor,
            SnapshotSectionPage.Descriptor,
            SnapshotRecord.Descriptor,
            DiffPage.Descriptor,
            DiffEntry.Descriptor,
            CaptureProgress.Descriptor,
            StartCaptureResponse.Descriptor,
            ListCapturesResponse.Descriptor,
        ];

        foreach (MessageDescriptor descriptor in descriptors)
        {
            Assert.DoesNotContain(
                descriptor.Fields.InDeclarationOrder(),
                f => f.Name.Contains("password", StringComparison.OrdinalIgnoreCase)
                     || f.Name.Contains("secret", StringComparison.OrdinalIgnoreCase)
                     || f.Name.Contains("cipher", StringComparison.OrdinalIgnoreCase)
                     || f.Name.Contains("raw_payload", StringComparison.OrdinalIgnoreCase));
        }
    }
}
