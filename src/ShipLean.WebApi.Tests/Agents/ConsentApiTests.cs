using System.Net;
using System.Net.Http.Json;
using System.Web;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Fakes.Agents;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport.Contracts.Agents;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Agents;

// The consent page's endpoints over the in-memory consent: pending requests are put in place by the fake's Add, since
// /oauth/authorize, which makes them for real, is not built yet.
[TestFixture]
public class ConsentApiTests : ApiFixture
{
    protected override Action<IServiceCollection>? ConfigureTestServices => PinnedAgentFakes.Pin;

    private const string Redirect = "http://127.0.0.1:8123/callback";
    private const string Issuer = "https://ship.example.test";

    private static readonly string[] Both = ["shiplean:read", "shiplean:write"];

    private Guid Pending(TestUser user, string? state = "st-1", params string[] scopes)
        => Host.Services.GetRequiredService<InMemoryAgentConsent>().Add(
            user.Id!.Value, InMemoryAgentStore.TestAgent.ClientId, Redirect, state, AgentWorld.Challenge,
            scopes.Length == 0 ? Both : scopes, AgentWorld.Resource, Issuer);

    private static async Task<ApiError> ErrorOfAsync(Task<HttpResponseMessage> call, HttpStatusCode status)
    {
        using var response = await call;
        return await ApiClient.ReadApiErrorAsync(response, status);
    }

    [Test]
    public async Task Consent_WhenPending_ShouldDescribeTheRequest()
    {
        var id = Pending(TestUser.Joel);

        var request = await Api.GetJsonAsync<ConsentRequestDto>($"api/agents/consent/{id}");

        request.RequestId.ShouldBe(id);
        request.ClientName.ShouldBe("Test Agent");
        request.Verified.ShouldBeFalse();
        request.ClientHost.ShouldBeNull();
        request.RedirectHost.ShouldBe("127.0.0.1");
        request.RequestedScopes.ShouldBe(Both);
        request.LocalhostOnly.ShouldBeTrue();
    }

    [Test]
    public async Task Consent_WhenUnknownRequest_ShouldBeNotFound()
    {
        (await ErrorOfAsync(Api.GetAsync($"api/agents/consent/{Guid.NewGuid()}"), HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
        (await ErrorOfAsync(Api.PostJsonAsync($"api/agents/consent/{Guid.NewGuid()}", new ConsentDecision(true, [])), HttpStatusCode.NotFound))
            .Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Consent_WhenAnonymous_ShouldAnswer401()
    {
        var id = Pending(TestUser.Joel);
        var anonymous = Api.ActingAs(TestUser.Anonymous);

        (await ErrorOfAsync(anonymous.GetAsync($"api/agents/consent/{id}"), HttpStatusCode.Unauthorized)).Code.ShouldBe(ApiErrorCodes.Unauthorized);
        (await ErrorOfAsync(anonymous.PostJsonAsync($"api/agents/consent/{id}", new ConsentDecision(true, [])), HttpStatusCode.Unauthorized))
            .Code.ShouldBe(ApiErrorCodes.Unauthorized);
        (await Api.GetJsonAsync<ConsentRequestDto>($"api/agents/consent/{id}")).RequestId.ShouldBe(id);
    }

    [Test]
    public async Task Consent_WhenRequestBelongsToAnotherUser_ShouldBeNotFound()
    {
        var id = Pending(TestUser.Mei);

        var unknown = await ErrorOfAsync(Api.GetAsync($"api/agents/consent/{Guid.NewGuid()}"), HttpStatusCode.NotFound);
        var foreign = await ErrorOfAsync(Api.GetAsync($"api/agents/consent/{id}"), HttpStatusCode.NotFound);
        var decided = await ErrorOfAsync(Api.PostJsonAsync($"api/agents/consent/{id}", new ConsentDecision(true, [])), HttpStatusCode.NotFound);

        foreign.ShouldBe(unknown);
        decided.ShouldBe(unknown);
        (await Api.ActingAs(TestUser.Mei).GetJsonAsync<ConsentRequestDto>($"api/agents/consent/{id}")).RequestId.ShouldBe(id);
    }

    [Test]
    public async Task Consent_WhenAllowed_ShouldRedirectWithCodeStateAndIss()
    {
        var id = Pending(TestUser.Priya, "st-9");
        var priya = Api.ActingAs(TestUser.Priya);

        using var response = await priya.PostJsonAsync($"api/agents/consent/{id}", new ConsentDecision(true, ["shiplean:write"]));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = (await response.Content.ReadFromJsonAsync<ConsentResult>())!;
        var query = HttpUtility.ParseQueryString(new Uri(result.RedirectUrl).Query);
        result.RedirectUrl.ShouldStartWith(Redirect + "?");
        query["code"].ShouldNotBeNullOrEmpty();
        query["state"].ShouldBe("st-9");
        query["iss"].ShouldBe(Issuer);
        var agent = (await priya.GetJsonAsync<List<ConnectedAgentDto>>("api/me/agents")).ShouldHaveSingleItem();
        agent.ClientName.ShouldBe("Test Agent");
        agent.Scopes.ShouldBe(["shiplean:read", "shiplean:write"]);
    }

    [Test]
    public async Task Consent_WhenDenied_ShouldRedirectWithAccessDenied()
    {
        var id = Pending(TestUser.Tom, "st-2");
        var tom = Api.ActingAs(TestUser.Tom);

        using var response = await tom.PostJsonAsync($"api/agents/consent/{id}", new ConsentDecision(false, []));

        var result = (await response.Content.ReadFromJsonAsync<ConsentResult>())!;
        var query = HttpUtility.ParseQueryString(new Uri(result.RedirectUrl).Query);
        query["error"].ShouldBe("access_denied");
        query["state"].ShouldBe("st-2");
        query["iss"].ShouldBe(Issuer);
        query["code"].ShouldBeNull();
        (await tom.GetJsonAsync<List<ConnectedAgentDto>>("api/me/agents")).ShouldBeEmpty();
    }

    [Test]
    public async Task Consent_WhenScopeNotRequested_ShouldBeValidation()
    {
        var id = Pending(TestUser.Sam, "st-3", "shiplean:read");

        var error = await ErrorOfAsync(Api.ActingAs(TestUser.Sam).PostJsonAsync($"api/agents/consent/{id}", new ConsentDecision(true, ["shiplean:plan"])), HttpStatusCode.BadRequest);

        error.Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Consent_WhenAllowIsMissing_ShouldBeValidation()
    {
        var id = Pending(TestUser.Kai);

        var error = await ErrorOfAsync(Api.ActingAs(TestUser.Kai).PostJsonAsync($"api/agents/consent/{id}", new { scopes = new[] { "shiplean:read" } }), HttpStatusCode.BadRequest);

        error.Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Consent_WhenDecidedTwice_ShouldBeNotFound()
    {
        var id = Pending(TestUser.Joel);
        using var first = await Api.PostJsonAsync($"api/agents/consent/{id}", new ConsentDecision(false, []));
        first.StatusCode.ShouldBe(HttpStatusCode.OK);

        var error = await ErrorOfAsync(Api.PostJsonAsync($"api/agents/consent/{id}", new ConsentDecision(false, [])), HttpStatusCode.NotFound);

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }
}
