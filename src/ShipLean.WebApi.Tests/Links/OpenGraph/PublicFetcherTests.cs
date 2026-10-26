using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Links.PublicFetch;
using ShipLean.Application.Http;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Http;
using Shouldly;

namespace ShipLean.WebApi.Tests.Links.OpenGraph;

// PublicFetcher as the app registers it, against a stub server on loopback. Loopback is let through (the stub lives there);
// every other address meets the real rules, and "private.test" resolves to a private address. The deadline is short so the
// hung-host case finishes quickly; the body cap is the production 1 MB.
[TestFixture]
public class PublicFetcherTests : EmptyApiFixture
{
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromSeconds(1);

    private StubHttpServer _server = null!;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.AddSingleton<IAddressCheck, LoopbackAllowedAddressCheck>();
        services.AddSingleton<IHostResolver>(new FixedHostResolver(new Dictionary<string, string> { ["private.test"] = "10.0.0.5" }));
        services.AddSingleton(PublicFetchOptions.Default with { Timeout = ShortTimeout });
    };

    [OneTimeSetUp]
    public async Task StartStubServer()
    {
        _server = await StubHttpServer.StartAsync();
    }

    [OneTimeTearDown]
    public async Task StopStubServer()
    {
        await _server.DisposeAsync();
    }

    private async Task<PublicFetchResult> FetchAsync(Uri address)
    {
        using var scope = Host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<PublicFetcher>().FetchAsync(address, "text/html", CancellationToken.None);
    }

    private static Dictionary<string, string> RedirectTo(string location) => new() { ["Location"] = location };

    [Test]
    public async Task Fetch_WhenPageAnswers_ShouldReturnTheBody()
    {
        _server.Respond("/plain", "<p>hello</p>");

        var result = await FetchAsync(_server.UrlOf("/plain"));

        result.Status.ShouldBe(PublicFetchStatus.Ok);
        System.Text.Encoding.UTF8.GetString(result.Body).ShouldBe("<p>hello</p>");
        result.ContentType.ShouldBe("text/html");
        result.Truncated.ShouldBeFalse();
    }

    [Test]
    public async Task Fetch_WhenRedirectedWithinTheLimit_ShouldFollowToTheLastAddress()
    {
        _server.Respond("/hop1", string.Empty, status: 302, headers: RedirectTo("/hop2"));
        _server.Respond("/hop2", string.Empty, status: 301, headers: RedirectTo("/hop3"));
        _server.Respond("/hop3", string.Empty, status: 307, headers: RedirectTo(_server.UrlOf("/landed").AbsoluteUri));
        _server.Respond("/landed", "landed");

        var result = await FetchAsync(_server.UrlOf("/hop1"));

        result.Status.ShouldBe(PublicFetchStatus.Ok);
        result.Address.ShouldBe(_server.UrlOf("/landed"));
    }

    [Test]
    public async Task Fetch_WhenRedirectedMoreThanThreeTimes_ShouldBeUnreachable()
    {
        _server.Respond("/loop1", string.Empty, status: 302, headers: RedirectTo("/loop2"));
        _server.Respond("/loop2", string.Empty, status: 302, headers: RedirectTo("/loop3"));
        _server.Respond("/loop3", string.Empty, status: 302, headers: RedirectTo("/loop4"));
        _server.Respond("/loop4", string.Empty, status: 302, headers: RedirectTo("/loop5"));
        _server.Respond("/loop5", "never read");

        var result = await FetchAsync(_server.UrlOf("/loop1"));

        result.Status.ShouldBe(PublicFetchStatus.Unreachable);
        _server.Requests.ShouldNotContain("GET /loop5");
    }

    [Test]
    public async Task Fetch_WhenRedirectToPrivate_ShouldRefuse()
    {
        _server.Respond("/to-private", string.Empty, status: 302, headers: RedirectTo("http://private.test/admin"));

        var result = await FetchAsync(_server.UrlOf("/to-private"));

        result.Status.ShouldBe(PublicFetchStatus.Private);
        result.Body.ShouldBeEmpty();
    }

    [TestCase("http://169.254.169.254/latest/meta-data/")]
    [TestCase("http://[::ffff:10.0.0.1]/")]
    [TestCase("http://private.test/")]
    public async Task Fetch_WhenHostIsPrivate_ShouldRefuse(string address)
    {
        var result = await FetchAsync(new Uri(address));

        result.Status.ShouldBe(PublicFetchStatus.Private);
    }

    [TestCase("file:///etc/passwd")]
    [TestCase("gopher://127.0.0.1:6379/_INFO")]
    public async Task Fetch_WhenRedirectLeavesHttp_ShouldBeUnreachable(string location)
    {
        var path = $"/off-http-{Guid.NewGuid():N}";
        _server.Respond(path, string.Empty, status: 302, headers: RedirectTo(location));

        var result = await FetchAsync(_server.UrlOf(path));

        result.Status.ShouldBe(PublicFetchStatus.Unreachable);
    }

    [Test]
    public async Task Fetch_WhenBodyOver1Mb_ShouldStopAtCap()
    {
        _server.Respond("/huge", new byte[3 * 1024 * 1024], "text/html");

        var result = await FetchAsync(_server.UrlOf("/huge"));

        result.Status.ShouldBe(PublicFetchStatus.Ok);
        result.Body.Length.ShouldBe(1024 * 1024);
        result.Truncated.ShouldBeTrue();
    }

    [Test]
    public async Task Fetch_WhenSlow_ShouldTimeOutAt5s()
    {
        using var silent = new SilentServer();
        var clock = Stopwatch.StartNew();

        var result = await FetchAsync(silent.UrlOf("/hangs"));

        result.Status.ShouldBe(PublicFetchStatus.Unreachable);
        clock.Elapsed.ShouldBeGreaterThanOrEqualTo(ShortTimeout - TimeSpan.FromMilliseconds(50));
        clock.Elapsed.ShouldBeLessThan(ShortTimeout + TimeSpan.FromSeconds(3));
    }

    [Test]
    public void Defaults_ShouldBeFiveSecondsOneMegabyteThreeRedirects()
    {
        PublicFetchOptions.Default.ShouldBe(new PublicFetchOptions(TimeSpan.FromSeconds(5), 1024 * 1024, 3));
    }
}
