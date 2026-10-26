using ShipLean.Contracts.Links;
using ShipLean.TestSupport.Contracts.Links;
using ShipLean.TestSupport.Http;
using ShipLean.TestSupport.Links;

namespace ShipLean.WebApi.Tests.Links;

// LinkCardSourceContract run against the fake source, so the contract and the stub server stay proven; the real sources run the same contract.
[TestFixture]
public class FakeLinkCardSourceContractTests : LinkCardSourceContract
{
    private FakeLinkCardSource? _source;

    [SetUp]
    public void ForgetTheSource() => _source = null;

    protected override ILinkCardSource CreateSource(StubHttpServer server) => SourceFor(server);

    private FakeLinkCardSource SourceFor(StubHttpServer server) => _source ??= new FakeLinkCardSource(host: server.BaseAddress.Host);

    protected override Uri HandledAddress(StubHttpServer server) => server.UrlOf("/some/page");

    protected override void ServeReadable(StubHttpServer server, Uri address)
    {
        SourceFor(server).Respond(address.AbsoluteUri, FakeLinkCardSource.ReachablePage("Readable"));
    }

    protected override void ServeFailing(StubHttpServer server, Uri address)
    {
        SourceFor(server).Respond(address.AbsoluteUri, FakeLinkCardSource.Unreachable());
    }
}
