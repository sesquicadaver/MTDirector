using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Infrastructure.Persistence.Snapshots;
using Xunit;

namespace Mfc.UnitTests.Policy;

public sealed class PolicyCanonicalHashTests
{
    [Fact]
    public void ContentHashIsSha256OfExactCanonicalBytesIndependentOfBrotli()
    {
        PolicyDocument document = PolicyDocument.CreateEmpty(
            PolicyKind.CompanyBaseline,
            PolicyOwnerScope.Company);
        byte[] canonical = PolicyCanonicalWriter.Write(document);
        Hash256 expected = Hash256.Create(SHA256.HashData(canonical));

        Assert.Equal(expected.ToString(), PolicyHashing.HashContent(canonical).ToString());
        Assert.Equal(expected.ToString(), PolicyHashing.HashContent(document).ToString());

        BrotliPayloadCodec.EncodedPayload encoded = BrotliPayloadCodec.Encode(canonical);
        Assert.Equal(expected.ToString(), Convert.ToHexString(encoded.PayloadHash).ToLowerInvariant());
        Assert.NotEqual(canonical.Length, encoded.CompressedPayload.Length);

        byte[] roundTrip = BrotliPayloadCodec.DecodeAndVerify(
            encoded.CompressedPayload,
            encoded.Compression,
            encoded.UncompressedSize,
            encoded.PayloadHash);
        Assert.Equal(canonical, roundTrip);
    }

    [Fact]
    public void CanonicalWriterUsesFixedPropertyOrderAndNoWhitespace()
    {
        byte[] bytes = PolicyCanonicalWriter.Write(
            PolicyDocument.CreateEmpty(PolicyKind.CompanyBaseline, PolicyOwnerScope.Company));
        string json = Encoding.UTF8.GetString(bytes);

        Assert.StartsWith("{\"schema\":\"mfc.policy.v1\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain(" ", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", json, StringComparison.Ordinal);
        Assert.Contains("\"policy_kind\":\"COMPANY_BASELINE\"", json, StringComparison.Ordinal);
        Assert.Contains("\"owner_scope\":\"COMPANY\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void DraftEditChangesContentHash()
    {
        PolicyDocument empty = PolicyDocument.CreateEmpty(
            PolicyKind.CompanyBaseline,
            PolicyOwnerScope.Company);
        using JsonDocument rule = JsonDocument.Parse("""{"id":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"}""");
        PolicyDocument withRule = empty.WithRules([rule.RootElement.Clone()]);

        Assert.NotEqual(
            PolicyHashing.HashContent(empty).ToString(),
            PolicyHashing.HashContent(withRule).ToString());
    }
}
