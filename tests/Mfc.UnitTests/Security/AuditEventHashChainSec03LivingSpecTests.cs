using Mfc.Infrastructure.Audit;
using Xunit;

namespace Mfc.UnitTests.Security;

/// <summary>Living Spec matrix for SEC-03 (#373) — cryptographically correct audit hash chain.</summary>
public sealed class AuditEventHashChainSec03LivingSpecTests
{
    [Fact]
    public void Ac1HashIncludesPreviousEventHashBytesNotOnlyLength()
    {
        Guid eventId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        const string actor = "ops@test";
        const string action = "sec03.append";
        const string payload = """{"n":1}""";

        byte[] prevA = Enumerable.Repeat((byte)1, 32).ToArray();
        byte[] prevB = Enumerable.Repeat((byte)2, 32).ToArray();
        Assert.Equal(prevA.Length, prevB.Length);

        byte[] hashA = AuditEventHashing.Compute(prevA, eventId, actor, action, payload);
        byte[] hashB = AuditEventHashing.Compute(prevB, eventId, actor, action, payload);
        Assert.False(hashA.AsSpan().SequenceEqual(hashB));
    }

    [Fact]
    public void Ac2GenesisAndChainedHashesDifferWithStableIdentity()
    {
        Guid genesisId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        Guid nextId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        byte[] genesis = AuditEventHashing.Compute(null, genesisId, "a", "act", """{"x":1}""");
        byte[] chained = AuditEventHashing.Compute(genesis, nextId, "a", "act", """{"x":2}""");
        Assert.Equal(32, genesis.Length);
        Assert.False(genesis.AsSpan().SequenceEqual(chained));
    }

    [Fact]
    public void Ac3EventIdIsPartOfPreimage()
    {
        byte[] prev = Enumerable.Repeat((byte)3, 32).ToArray();
        byte[] left = AuditEventHashing.Compute(
            prev,
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "a",
            "act",
            """{"x":1}""");
        byte[] right = AuditEventHashing.Compute(
            prev,
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            "a",
            "act",
            """{"x":1}""");
        Assert.False(left.AsSpan().SequenceEqual(right));
    }

    [Fact]
    public void Ac4RejectsNonSha256LengthPredecessor()
    {
        Assert.Throws<ArgumentException>(() =>
            AuditEventHashing.Compute(
                Enumerable.Repeat((byte)9, 16).ToArray(),
                Guid.NewGuid(),
                "a",
                "act",
                """{"x":1}"""));
    }
}
