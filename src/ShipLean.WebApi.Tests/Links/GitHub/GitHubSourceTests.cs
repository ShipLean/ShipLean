using ShipLean.Application.Features.Links.GitHub;
using ShipLean.Contracts.Links;
using ShipLean.TestSupport.Contracts.Links;
using ShipLean.TestSupport.Http;
using Shouldly;

namespace ShipLean.WebApi.Tests.Links.GitHub;

// GitHubSource against a stub of the GitHub API on loopback: the shared source contract plus the state mapping, the token and the failure card.
[TestFixture]
public class GitHubSourceTests : LinkCardSourceContract
{
    private const string PullPath = "/repos/acme/storefront/pulls/482";

    private RecordingHttpClientFactory _clients = null!;
    private string? _token;

    [SetUp]
    public void ForgetTheToken()
    {
        _clients = new RecordingHttpClientFactory();
        _token = null;
    }

    protected override ILinkCardSource CreateSource(StubHttpServer server)
        => new GitHubSource(_clients, new FixedIntegrationSettings(new IntegrationSettingsDto(_token, server.BaseAddress.AbsoluteUri, null, null, [])));

    protected override Uri HandledAddress(StubHttpServer server) => new("https://github.com/acme/storefront/pull/482");

    protected override void ServeReadable(StubHttpServer server, Uri address)
        => server.Respond(PullPath, PullJson(state: "open", draft: false, merged: false), "application/json");

    protected override void ServeFailing(StubHttpServer server, Uri address)
        => server.Respond(PullPath, "{}", "application/json", status: 500);

    private static string PullJson(string state, bool draft, bool merged)
        => $$"""{ "number": 482, "title": "Add retry to checkout", "state": "{{state}}", "draft": {{draft.ToString().ToLowerInvariant()}}, "merged": {{merged.ToString().ToLowerInvariant()}}, "user": { "login": "mira" } }""";

    private Task<LinkCardFetch> FetchPullAsync() => CreateSource(Server).FetchAsync(HandledAddress(Server), CancellationToken.None);

    [Test]
    public async Task GitHub_WhenPullMerged_ShouldReturnMerged()
    {
        Server.Respond(PullPath, PullJson(state: "closed", draft: false, merged: true), "application/json");

        var fetch = await FetchPullAsync();

        fetch.State.ShouldBe(LinkState.Merged);
        fetch.Kind.ShouldBe(LinkKind.GithubPull);
        fetch.Title.ShouldBe("Add retry to checkout");
        fetch.Number.ShouldBe(482);
        fetch.Location.ShouldBe("acme/storefront");
        fetch.Author.ShouldBe("mira");
        fetch.PreviewStatus.ShouldBe(LinkPreviewStatus.Ok);
    }

    [Test]
    public async Task GitHub_WhenDraft_ShouldReturnDraft()
    {
        Server.Respond(PullPath, PullJson(state: "open", draft: true, merged: false), "application/json");

        var fetch = await FetchPullAsync();

        fetch.State.ShouldBe(LinkState.Draft);
    }

    [Test]
    public async Task GitHub_WhenPullClosedUnmerged_ShouldReturnClosed()
    {
        Server.Respond(PullPath, PullJson(state: "closed", draft: false, merged: false), "application/json");

        (await FetchPullAsync()).State.ShouldBe(LinkState.Closed);
    }

    [Test]
    public async Task GitHub_WhenIssueOpen_ShouldReturnOpenIssueFromIssuesEndpoint()
    {
        Server.Respond("/repos/acme/storefront/issues/7", """{ "number": 7, "title": "Crash on empty cart", "state": "open", "user": { "login": "omar" } }""", "application/json");

        var fetch = await CreateSource(Server).FetchAsync(new Uri("https://github.com/acme/storefront/issues/7"), CancellationToken.None);

        fetch.Kind.ShouldBe(LinkKind.GithubIssue);
        fetch.State.ShouldBe(LinkState.Open);
        fetch.Title.ShouldBe("Crash on empty cart");
    }

    private GitHubSource SourceFor(Uri apiBase, string? token)
        => new(_clients, new FixedIntegrationSettings(new IntegrationSettingsDto(token, apiBase.AbsoluteUri, null, null, [])));

    [Test]
    public async Task GitHub_WhenTokenSet_ShouldSendBearer()
    {
        await using var https = await HttpsStubServer.StartAsync();
        https.Respond(PullPath, PullJson(state: "open", draft: false, merged: false));

        var fetch = await SourceFor(https.BaseAddress, "ghp_secret").FetchAsync(HandledAddress(Server), CancellationToken.None);

        fetch.PreviewStatus.ShouldBe(LinkPreviewStatus.Ok);
        _clients.Requests.Single().Headers.Authorization!.ToString().ShouldBe("Bearer ghp_secret");
    }

    [Test]
    public async Task GitHub_WhenTokenSetAndApiAddressIsHttp_ShouldReadWithoutAuthorization()
    {
        _token = "ghp_secret";
        ServeReadable(Server, HandledAddress(Server));

        var fetch = await FetchPullAsync();

        fetch.PreviewStatus.ShouldBe(LinkPreviewStatus.Ok);
        _clients.Requests.Single().Headers.Authorization.ShouldBeNull();
    }

    [Test]
    public async Task GitHub_WhenTokenIsMalformed_ShouldReturnUnreachableAndSendNothing()
    {
        await using var https = await HttpsStubServer.StartAsync();
        https.Respond(PullPath, PullJson(state: "open", draft: false, merged: false));

        var fetch = await SourceFor(https.BaseAddress, "ghp_secret\r\nX-Injected: 1").FetchAsync(HandledAddress(Server), CancellationToken.None);

        fetch.PreviewStatus.ShouldBe(LinkPreviewStatus.Unreachable);
        fetch.Number.ShouldBe(482);
        https.Requests.ShouldBeEmpty();
    }

    [Test]
    public async Task GitHub_WhenResponseIsOverTheSizeCap_ShouldReturnUnreachable()
    {
        Server.Respond(PullPath, PullJson(state: "open", draft: false, merged: false).Replace("Add retry", new string('x', GitHubLimits.MaxBody)), "application/json");

        var fetch = await FetchPullAsync();

        fetch.PreviewStatus.ShouldBe(LinkPreviewStatus.Unreachable);
        fetch.Title.ShouldBeNull();
    }

    [Test]
    public async Task GitHub_WhenTitleAndAuthorAreVeryLong_ShouldTruncateThem()
    {
        Server.Respond(PullPath, $$"""{ "title": "{{new string('t', 2000)}}", "state": "open", "user": { "login": "{{new string('a', 500)}}" } }""", "application/json");

        var fetch = await FetchPullAsync();

        fetch.PreviewStatus.ShouldBe(LinkPreviewStatus.Ok);
        fetch.Title!.Length.ShouldBe(GitHubLimits.MaxTitle);
        fetch.Author!.Length.ShouldBe(GitHubLimits.MaxAuthor);
    }

    [Test]
    public async Task GitHub_WhenTokenNotSet_ShouldSendNoAuthorization()
    {
        ServeReadable(Server, HandledAddress(Server));

        await FetchPullAsync();

        _clients.Requests.Single().Headers.Authorization.ShouldBeNull();
    }

    [Test]
    public async Task GitHub_WhenApiFails_ShouldReturnUnreachableWithNumber()
    {
        ServeFailing(Server, HandledAddress(Server));

        var fetch = await FetchPullAsync();

        fetch.PreviewStatus.ShouldBe(LinkPreviewStatus.Unreachable);
        fetch.Number.ShouldBe(482);
        fetch.Location.ShouldBe("acme/storefront");
        fetch.Kind.ShouldBe(LinkKind.GithubPull);
        fetch.Title.ShouldBeNull();
        fetch.State.ShouldBeNull();
    }

    [Test]
    public async Task GitHub_WhenApiAddressRefusesConnection_ShouldReturnUnreachable()
    {
        var source = new GitHubSource(_clients, new FixedIntegrationSettings(new IntegrationSettingsDto(null, "http://127.0.0.1:1", null, null, [])));

        var fetch = await source.FetchAsync(HandledAddress(Server), CancellationToken.None);

        fetch.PreviewStatus.ShouldBe(LinkPreviewStatus.Unreachable);
    }

    [Test]
    public async Task CanHandle_WhenAddressIsNotAPullOrIssue_ShouldBeFalse()
    {
        (await CreateSource(Server).CanHandleAsync(new Uri("https://github.com/acme/storefront"), CancellationToken.None)).ShouldBeFalse();
    }
}
