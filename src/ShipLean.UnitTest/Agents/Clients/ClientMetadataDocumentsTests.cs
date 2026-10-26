using System.Net;
using Microsoft.Extensions.Time.Testing;
using ShipLean.Application.Features.Agents.Clients;
using ShipLean.Contracts.Agents;

namespace ShipLean.UnitTest.Agents.Clients;

// The real fetcher, with the network replaced by a stub handler: every content and transport rule runs for real. The
// address guard has its own tests (PinnedConnectorTests).
public sealed class ClientMetadataDocumentsTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 24, 9, 0, 0, TimeSpan.Zero));
    private StubHandler _handler = null!;
    private ClientMetadataOptions _options = ClientMetadataOptions.Default;

    private IClientMetadataDocuments Real => new ClientMetadataDocuments(new HandlerHttpClientFactory(_handler), _clock, _options);

    [SetUp]
    public void ServeValidDocument() => _handler = Serving(MetadataDocuments.Json());

    [TearDown]
    public void DisposeHandler() => _handler.Dispose();

    private static StubHandler Serving(string json) => new((_, _) => Task.FromResult(MetadataDocuments.Ok(json)));

    [Test]
    public async Task Cimd_WhenValid_ShouldResolveNameAndHost()
    {
        var document = await Real.FetchAsync(MetadataDocuments.Url);

        document.ShouldNotBeNull();
        document.Client.ClientName.ShouldBe("Review Bot");
        document.Client.ClientHost.ShouldBe("client.example.test");
        document.Client.Kind.ShouldBe(OAuthClientKind.MetadataDocument);
        document.Client.RedirectUris.Count.ShouldBe(2);
    }

    [Test]
    public async Task Cimd_WhenClientIdDiffersFromUrl_ShouldRefuse()
    {
        _handler = Serving(MetadataDocuments.Json("https://client.example.test/oauth/other.json"));

        (await Real.FetchAsync(MetadataDocuments.Url)).ShouldBeNull();
    }

    [Test]
    public async Task Cimd_WhenRedirected_ShouldRefuse()
    {
        _handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Redirect)
        {
            Headers = { Location = new Uri("https://other.example.test/oauth/metadata.json") },
        }));

        (await Real.FetchAsync(MetadataDocuments.Url)).ShouldBeNull();
        _handler.Requests.ShouldBe(1);
    }

    [TestCase("declared")]
    [TestCase("chunked")]
    [TestCase("slow")]
    public async Task Cimd_WhenOverSizeOrSlow_ShouldRefuse(string kind)
    {
        var big = MetadataDocuments.Json(extra: new() { ["padding"] = new string('x', 70_000) });
        _options = _options with { Timeout = TimeSpan.FromMilliseconds(200) };
        _handler = kind switch
        {
            "declared" => Serving(big),
            "chunked" => new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new UnseekableStream(System.Text.Encoding.UTF8.GetBytes(big))),
            })),
            _ => new StubHandler(async (_, token) =>
            {
                await Task.Delay(Timeout.Infinite, token);
                return MetadataDocuments.Ok(MetadataDocuments.Json());
            }),
        };

        (await Real.FetchAsync(MetadataDocuments.Url)).ShouldBeNull();
    }

    [TestCase("client_secret")]
    [TestCase("jwks")]
    public async Task Cimd_WhenSecretDeclared_ShouldRefuse(string property)
    {
        _handler = Serving(MetadataDocuments.Json(extra: new() { [property] = "s3cret" }));

        (await Real.FetchAsync(MetadataDocuments.Url)).ShouldBeNull();
    }

    [TestCase("client_secret_basic")]
    [TestCase("client_secret_post")]
    [TestCase("private_key_jwt")]
    public async Task Cimd_WhenAuthMethodIsNotNone_ShouldRefuse(string method)
    {
        _handler = Serving(MetadataDocuments.Json(extra: new() { ["token_endpoint_auth_method"] = method }));

        (await Real.FetchAsync(MetadataDocuments.Url)).ShouldBeNull();
    }

    [Test]
    public async Task Cimd_WhenAuthMethodIsNone_ShouldAccept()
    {
        _handler = Serving(MetadataDocuments.Json(extra: new() { ["token_endpoint_auth_method"] = "none" }));

        (await Real.FetchAsync(MetadataDocuments.Url)).ShouldNotBeNull();
    }

    [Test]
    public async Task Cimd_WhenRedirectIsPrivateUseScheme_ShouldAccept()
    {
        _handler = Serving(MetadataDocuments.Json(extra: new() { ["redirect_uris"] = new[] { "cursor://anysphere.cursor-retrieval/oauth/callback" } }));

        (await Real.FetchAsync(MetadataDocuments.Url))!.Client.RedirectUris.ShouldBe(["cursor://anysphere.cursor-retrieval/oauth/callback"]);
    }

    [TestCase("http://evil.example.test/cb")]
    [TestCase("javascript:alert(1)")]
    [TestCase("https://user@evil.example.test/cb")]
    public async Task Cimd_WhenRedirectIsNotLoopbackOrHttps_ShouldRefuse(string redirect)
    {
        _handler = Serving(MetadataDocuments.Json(extra: new() { ["redirect_uris"] = new[] { redirect } }));

        (await Real.FetchAsync(MetadataDocuments.Url)).ShouldBeNull();
    }

    [Test]
    public async Task Cimd_WhenNotJson_ShouldRefuse()
    {
        _handler = Serving("<html>not json</html>");

        (await Real.FetchAsync(MetadataDocuments.Url)).ShouldBeNull();
    }

    [Test]
    public async Task Fetch_WhenSecondCallWithinFloor_ShouldServeFromCache()
    {
        var documents = Real;
        await documents.FetchAsync(MetadataDocuments.Url);
        _clock.Advance(TimeSpan.FromMinutes(4));
        await documents.FetchAsync(MetadataDocuments.Url);
        _clock.Advance(TimeSpan.FromMinutes(2));
        await documents.FetchAsync(MetadataDocuments.Url);

        _handler.Requests.ShouldBe(2);
    }

    [Test]
    public async Task Fetch_WhenMaxAgeBeyondCeiling_ShouldExpireAtCeiling()
    {
        _handler = new StubHandler((_, _) =>
        {
            var response = MetadataDocuments.Ok(MetadataDocuments.Json());
            response.Headers.CacheControl = new() { MaxAge = TimeSpan.FromDays(30) };
            return Task.FromResult(response);
        });

        var document = await Real.FetchAsync(MetadataDocuments.Url);

        document!.ExpiresAt.ShouldBe(_clock.GetUtcNow().AddHours(24));
    }

    [Test]
    public async Task Fetch_WhenMaxAgeBelowFloor_ShouldExpireAtFloor()
    {
        _handler = new StubHandler((_, _) =>
        {
            var response = MetadataDocuments.Ok(MetadataDocuments.Json());
            response.Headers.CacheControl = new() { MaxAge = TimeSpan.FromSeconds(1) };
            return Task.FromResult(response);
        });

        var document = await Real.FetchAsync(MetadataDocuments.Url);

        document!.ExpiresAt.ShouldBe(_clock.GetUtcNow().AddMinutes(5));
    }

    [Test]
    public async Task Fetch_WhenRefused_ShouldNotFetchAgainWithinFloor()
    {
        _handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
        var documents = Real;

        (await documents.FetchAsync(MetadataDocuments.Url)).ShouldBeNull();
        (await documents.FetchAsync(MetadataDocuments.Url)).ShouldBeNull();
        _handler.Requests.ShouldBe(1);

        _clock.Advance(TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(1)));
        (await documents.FetchAsync(MetadataDocuments.Url)).ShouldBeNull();
        _handler.Requests.ShouldBe(2);
    }

    [Test]
    public async Task Fetch_WhenCacheFull_ShouldEvictNearestExpiryAndKeepCachingRefusals()
    {
        _options = _options with { MaxCachedUrls = 2 };
        _handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
        var documents = Real;
        string Url(string name) => $"https://client.example.test/oauth/{name}.json";

        await documents.FetchAsync(Url("a"));
        _clock.Advance(TimeSpan.FromMinutes(1));
        await documents.FetchAsync(Url("b"));
        _clock.Advance(TimeSpan.FromMinutes(1));
        await documents.FetchAsync(Url("c"));
        _handler.Requests.ShouldBe(3);

        await documents.FetchAsync(Url("c"));
        await documents.FetchAsync(Url("b"));
        _handler.Requests.ShouldBe(3);
        await documents.FetchAsync(Url("a"));
        _handler.Requests.ShouldBe(4);
    }

    [Test]
    public async Task Fetch_WhenTooManyInFlight_ShouldRefuseTheExtraWithoutFetching()
    {
        _options = _options with { MaxConcurrentFetches = 2 };
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var bothStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        StubHandler? handler = null;
        handler = new StubHandler(async (_, _) =>
        {
            if (handler!.Requests == 2)
            {
                bothStarted.SetResult();
            }
            await gate.Task;
            return MetadataDocuments.Ok(MetadataDocuments.Json());
        });
        _handler = handler;
        var documents = Real;

        var first = documents.FetchAsync(MetadataDocuments.Url);
        var second = documents.FetchAsync(MetadataDocuments.Url);
        await bothStarted.Task;
        var third = await documents.FetchAsync("https://client.example.test/oauth/third.json");

        third.ShouldBeNull();
        handler.Requests.ShouldBe(2);
        gate.SetResult();
        (await first).ShouldNotBeNull();
        (await second).ShouldNotBeNull();
        (await documents.FetchAsync(MetadataDocuments.Url)).ShouldNotBeNull();
    }
}
