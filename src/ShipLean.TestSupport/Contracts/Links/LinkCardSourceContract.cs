using ShipLean.Contracts.Links;
using ShipLean.TestSupport.Http;
using Shouldly;

namespace ShipLean.TestSupport.Contracts.Links;

// Behaviour every ILinkCardSource must have. A source reads through HTTP, so the contract runs it against a StubHttpServer
// on loopback: no external network. The implementation's test class says how to build the source and how to make the stub
// answer (or fail) for an address the source handles.
[TestFixture]
public abstract class LinkCardSourceContract
{
    private StubHttpServer _server = null!;

    protected StubHttpServer Server => _server;

    // The source under test, built to read through the stub server.
    protected abstract ILinkCardSource CreateSource(StubHttpServer server);

    // An address the source handles, whose requests land on the stub server.
    protected abstract Uri HandledAddress(StubHttpServer server);

    // Makes the stub answer so a fetch of the address reads successfully.
    protected abstract void ServeReadable(StubHttpServer server, Uri address);

    // Makes the stub answer so a fetch of the address fails (a server error, as a down host would).
    protected abstract void ServeFailing(StubHttpServer server, Uri address);

    // An address on a host no source handles.
    protected virtual Uri ForeignAddress => new("https://unhandled.example/page");

    [SetUp]
    public async Task StartServer()
    {
        _server = await StubHttpServer.StartAsync();
    }

    [TearDown]
    public async Task StopServer()
    {
        await _server.DisposeAsync();
    }

    [Test]
    public void Order_ShouldBePositive()
    {
        CreateSource(_server).Order.ShouldBeGreaterThan(0);
    }

    [Test]
    public async Task CanHandle_WhenAddressIsOneItHandles_ShouldBeTrue()
    {
        (await CreateSource(_server).CanHandleAsync(HandledAddress(_server), CancellationToken.None)).ShouldBeTrue();
    }

    [Test]
    public async Task CanHandle_WhenAddressIsForeign_ShouldBeFalse()
    {
        (await CreateSource(_server).CanHandleAsync(ForeignAddress, CancellationToken.None)).ShouldBeFalse();
    }

    [Test]
    public async Task Fetch_WhenServerAnswers_ShouldReportOkWithATitle()
    {
        var address = HandledAddress(_server);
        ServeReadable(_server, address);

        var fetch = await CreateSource(_server).FetchAsync(address, CancellationToken.None);

        fetch.PreviewStatus.ShouldBe(LinkPreviewStatus.Ok);
        fetch.Title.ShouldNotBeNullOrWhiteSpace();
    }

    [Test]
    public async Task Fetch_WhenServerFails_ShouldReportUnreachableAndNotThrow()
    {
        var address = HandledAddress(_server);
        ServeFailing(_server, address);

        var fetch = await CreateSource(_server).FetchAsync(address, CancellationToken.None);

        fetch.PreviewStatus.ShouldBe(LinkPreviewStatus.Unreachable);
    }

    [Test]
    public async Task Fetch_WhenServerAnswers_ShouldKeepBlobKeysNotAddresses()
    {
        var address = HandledAddress(_server);
        ServeReadable(_server, address);

        var fetch = await CreateSource(_server).FetchAsync(address, CancellationToken.None);

        (fetch.ImageKey ?? string.Empty).ShouldNotStartWith("http");
        (fetch.FaviconKey ?? string.Empty).ShouldNotStartWith("http");
    }
}
