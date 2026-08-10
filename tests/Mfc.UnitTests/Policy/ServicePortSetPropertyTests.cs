using Mfc.Domain.Policy;
using Xunit;

namespace Mfc.UnitTests.Policy;

/// <summary>Property-style checks: port normalize idempotence and term canonical order stability.</summary>
public sealed class ServicePortSetPropertyTests
{
    [Fact]
    public void PortNormalizationIsIdempotentAndOrderIndependent()
    {
        Random rng = new(11);
        for (int trial = 0; trial < 200; trial++)
        {
            List<PortInterval> raw = [];
            int count = rng.Next(1, 10);
            for (int i = 0; i < count; i++)
            {
                int a = rng.Next(0, 200);
                int b = rng.Next(0, 200);
                raw.Add(new PortInterval((ushort)Math.Min(a, b), (ushort)Math.Max(a, b)));
            }

            IReadOnlyList<PortInterval> once = PortSet.Normalize(raw);
            Assert.Equal(once, PortSet.Normalize(once));
            Assert.Equal(once, PortSet.Normalize(raw.OrderBy(_ => rng.Next())));
        }
    }

    [Fact]
    public void TermCanonicalOrderingDoesNotDependOnInputOrder()
    {
        Random rng = new(5);
        for (int trial = 0; trial < 50; trial++)
        {
            List<ServiceTerm> terms =
            [
                ServiceTerm.Create(IpProtocol.Create((byte)rng.Next(1, 20))),
                ServiceTerm.Create(
                    IpProtocol.Create(IpProtocol.Tcp),
                    destinationPorts: PortSet.Create(
                    [
                        new PortInterval((ushort)rng.Next(1, 100), (ushort)rng.Next(100, 200)),
                    ])),
                ServiceTerm.Create(
                    IpProtocol.Create(IpProtocol.Udp),
                    sourcePorts: PortSet.Create([new PortInterval(1, 10)])),
            ];

            IReadOnlyList<ServiceTerm> a = ServiceObject.CanonicalizeTerms(terms);
            IReadOnlyList<ServiceTerm> b = ServiceObject.CanonicalizeTerms(terms.OrderBy(_ => rng.Next()));
            Assert.Equal(a, b);
        }
    }
}
