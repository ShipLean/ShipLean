using ShipLean.Contracts.Search;
using ShipLean.TestSupport.Contracts.Search;
using ShipLean.TestSupport.Search;

namespace ShipLean.WebApi.Tests.Search.Doubles;

// EmbedderContract run against the fake, so the contract stays proven; the real model runs the same fixture.
[TestFixture]
public class FakeEmbedderContractTests : EmbedderContract
{
    protected override IEmbedder CreateEmbedder() => new FakeEmbedder();
}
