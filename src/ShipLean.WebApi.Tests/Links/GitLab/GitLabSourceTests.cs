using ShipLean.Application.Features.Links.GitLab;
using ShipLean.Contracts.Links;
using ShipLean.TestSupport.Contracts.Links;
using ShipLean.TestSupport.Http;
using ShipLean.WebApi.Tests.Links.GitHub;
using Shouldly;

namespace ShipLean.WebApi.Tests.Links.GitLab;

// GitLabSource against a stub of a GitLab v4 API on loopback: the shared source contract plus the state mapping, the token and the failure card.
[TestFixture]
public class GitLabSourceTests : LinkCardSourceContract
{
    private const string HandledPath = "/payments/vault/-/merge_requests/31";
    private const string MergeRequestPath = "/api/v4/projects/payments%2Fvault/merge_requests/31";

    private RecordingHttpClientFactory _clients = null!;
    private string? _token;
    private bool _configured;

    [SetUp]
    public void ForgetTheSettings()
    {
        _clients = new RecordingHttpClientFactory();
        _token = null;
        _configured = true;
    }

    protected override ILinkCardSource CreateSource(StubHttpServer server)
        => new GitLabSource(_clients, new FixedIntegrationSettings(new IntegrationSettingsDto(null, "https://api.github.com", _configured ? server.BaseAddress.AbsoluteUri : null, _token, [])));

    protected override Uri HandledAddress(StubHttpServer server) => server.UrlOf("/payments/vault/-/merge_requests/31");

    protected override void ServeReadable(StubHttpServer server, Uri address)
        => server.Respond(MergeRequestPath, MergeRequestJson(state: "opened", draft: false), "application/json");

    protected override void ServeFailing(StubHttpServer server, Uri address)
        => server.Respond(MergeRequestPath, "{}", "application/json", status: 500);

    private static string MergeRequestJson(string state, bool draft)
        => $$"""{ "iid": 31, "title": "Rotate vault keys", "state": "{{state}}", "draft": {{draft.ToString().ToLowerInvariant()}}, "author": { "username": "mira" } }""";

    private Task<LinkCardFetch> FetchMergeRequestAsync() => CreateSource(Server).FetchAsync(HandledAddress(Server), CancellationToken.None);

    [Test]
    public async Task GitLab_WhenUrlNotConfigured_ShouldNotHandle()
    {
        _configured = false;

        (await CreateSource(Server).CanHandleAsync(HandledAddress(Server), CancellationToken.None)).ShouldBeFalse();
    }

    [Test]
    public async Task GitLab_WhenMergeRequestOpen_ShouldReturnOpen()
    {
        ServeReadable(Server, HandledAddress(Server));

        var fetch = await FetchMergeRequestAsync();

        fetch.State.ShouldBe(LinkState.Open);
        fetch.Kind.ShouldBe(LinkKind.GitlabMergeRequest);
        fetch.Title.ShouldBe("Rotate vault keys");
        fetch.Number.ShouldBe(31);
        fetch.Author.ShouldBe("mira");
        fetch.PreviewStatus.ShouldBe(LinkPreviewStatus.Ok);
    }

    [TestCase("merged", false, LinkState.Merged)]
    [TestCase("closed", false, LinkState.Closed)]
    [TestCase("opened", true, LinkState.Draft)]
    public async Task GitLab_WhenMergeRequestHasState_ShouldMapIt(string state, bool draft, LinkState expected)
    {
        Server.Respond(MergeRequestPath, MergeRequestJson(state, draft), "application/json");

        (await FetchMergeRequestAsync()).State.ShouldBe(expected);
    }

    [Test]
    public async Task GitLab_WhenIssueClosed_ShouldReturnClosedFromIssuesEndpoint()
    {
        Server.Respond("/api/v4/projects/payments%2Fvault/issues/7", """{ "iid": 7, "title": "Key not rotated", "state": "closed", "author": { "username": "omar" } }""", "application/json");

        var fetch = await CreateSource(Server).FetchAsync(Server.UrlOf("/payments/vault/-/issues/7"), CancellationToken.None);

        fetch.Kind.ShouldBe(LinkKind.GitlabIssue);
        fetch.State.ShouldBe(LinkState.Closed);
    }

    private GitLabSource SourceFor(Uri gitLabBase, string? token)
        => new(_clients, new FixedIntegrationSettings(new IntegrationSettingsDto(null, "https://api.github.com", gitLabBase.AbsoluteUri, token, [])));

    [Test]
    public async Task GitLab_WhenTokenSet_ShouldSendPrivateToken()
    {
        await using var https = await HttpsStubServer.StartAsync();
        https.Respond(MergeRequestPath, MergeRequestJson(state: "opened", draft: false));

        var fetch = await SourceFor(https.BaseAddress, "glpat-secret").FetchAsync(new Uri(https.BaseAddress, HandledPath), CancellationToken.None);

        fetch.PreviewStatus.ShouldBe(LinkPreviewStatus.Ok);
        var request = _clients.Requests.Single();
        request.Headers.GetValues("PRIVATE-TOKEN").ShouldBe(["glpat-secret"]);
        request.Headers.Authorization.ShouldBeNull();
    }

    [Test]
    public async Task GitLab_WhenTokenSetAndGitLabAddressIsHttp_ShouldReadWithoutTheToken()
    {
        _token = "glpat-secret";
        ServeReadable(Server, HandledAddress(Server));

        var fetch = await FetchMergeRequestAsync();

        fetch.PreviewStatus.ShouldBe(LinkPreviewStatus.Ok);
        _clients.Requests.Single().Headers.Contains("PRIVATE-TOKEN").ShouldBeFalse();
    }

    [Test]
    public async Task GitLab_WhenTokenIsMalformed_ShouldReturnUnreachableAndSendNothing()
    {
        await using var https = await HttpsStubServer.StartAsync();
        https.Respond(MergeRequestPath, MergeRequestJson(state: "opened", draft: false));

        var fetch = await SourceFor(https.BaseAddress, "glpat-secret\r\nX-Injected: 1").FetchAsync(new Uri(https.BaseAddress, HandledPath), CancellationToken.None);

        fetch.PreviewStatus.ShouldBe(LinkPreviewStatus.Unreachable);
        fetch.Number.ShouldBe(31);
        https.Requests.ShouldBeEmpty();
    }

    [Test]
    public async Task GitLab_WhenResponseIsOverTheSizeCap_ShouldReturnUnreachable()
    {
        Server.Respond(MergeRequestPath, MergeRequestJson(state: "opened", draft: false).Replace("Rotate", new string('x', GitLabLimits.MaxBody)), "application/json");

        var fetch = await FetchMergeRequestAsync();

        fetch.PreviewStatus.ShouldBe(LinkPreviewStatus.Unreachable);
        fetch.Title.ShouldBeNull();
    }

    [Test]
    public async Task GitLab_WhenTitleAndAuthorAreVeryLong_ShouldTruncateThem()
    {
        Server.Respond(MergeRequestPath, $$"""{ "iid": 31, "title": "{{new string('t', 2000)}}", "state": "opened", "author": { "username": "{{new string('a', 500)}}" } }""", "application/json");

        var fetch = await FetchMergeRequestAsync();

        fetch.PreviewStatus.ShouldBe(LinkPreviewStatus.Ok);
        fetch.Title!.Length.ShouldBe(GitLabLimits.MaxTitle);
        fetch.Author!.Length.ShouldBe(GitLabLimits.MaxAuthor);
    }

    [Test]
    public async Task GitLab_WhenApiFails_ShouldReturnUnreachableWithNumber()
    {
        ServeFailing(Server, HandledAddress(Server));

        var fetch = await FetchMergeRequestAsync();

        fetch.PreviewStatus.ShouldBe(LinkPreviewStatus.Unreachable);
        fetch.Number.ShouldBe(31);
        fetch.Location.ShouldNotBeNullOrEmpty();
        fetch.Title.ShouldBeNull();
    }

    [Test]
    public async Task CanHandle_WhenAddressIsOnAnotherHost_ShouldBeFalse()
    {
        (await CreateSource(Server).CanHandleAsync(new Uri("https://gitlab.other.example/payments/vault/-/merge_requests/31"), CancellationToken.None)).ShouldBeFalse();
    }
}
