using System.Net;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ShipLean.Contracts.Agents;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Contracts.Agents;
using ShipLean.TestSupport.Mcp;

namespace ShipLean.WebApi.Tests.Agents.Tokens;

[TestFixture]
public class TokenGrantTests : TokenFixture
{
    [Test]
    public async Task Token_WhenCodeAndVerifierMatch_ShouldIssuePair()
    {
        var arranged = await ArrangeAuthorizationAsync();
        var code = await IssueCodeAsync(arranged.AuthorizationId);

        var (response, body) = await TokenAsync(CodeGrant(arranged.ClientId, code));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.ShouldBeTrue();
        body.GetProperty("token_type").GetString().ShouldBe("Bearer");
        body.GetProperty("expires_in").GetInt32().ShouldBeInRange(3500, 3600);
        body.GetProperty("scope").GetString().ShouldBe("shiplean:read shiplean:write");
        body.GetProperty("refresh_token").GetString().ShouldNotBeNullOrEmpty();
        var principal = await ValidateAsync(body.GetProperty("access_token").GetString()!);
        principal.ShouldNotBeNull();
        principal.UserId.ShouldBe(arranged.UserId);
    }

    [Test]
    public async Task Token_WhenCustomSchemeRedirectUri_ShouldIssuePair()
    {
        const string cursor = "cursor://anysphere.cursor-retrieval/oauth/callback";
        Guid userId;
        await using (var connection = await OpenDatabaseAsync())
        {
            userId = await new AppUserBuilder().InsertAsync(connection);
        }
        await using var scope = Host.Services.CreateAsyncScope();
        var clientId = (await scope.ServiceProvider.GetRequiredService<IOAuthClients>().RegisterAsync(new OAuthClientRegistration("Cursor", [cursor]))).ClientId;
        var authorizationId = await scope.ServiceProvider.GetRequiredService<IAgentAuthorizations>().CreateAsync(userId, clientId, ReadWrite.ToHashSet());
        var code = await IssueCodeAsync(authorizationId, redirect: cursor);

        var (response, body) = await TokenAsync(CodeGrant(clientId, code, redirect: cursor));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        body.GetProperty("access_token").GetString().ShouldNotBeNullOrEmpty();
    }

    [Test]
    public async Task Token_WhenVerifierWrong_ShouldRefuse()
    {
        var arranged = await ArrangeAuthorizationAsync();
        var code = await IssueCodeAsync(arranged.AuthorizationId);

        var (response, body) = await TokenAsync(CodeGrant(arranged.ClientId, code, verifier: new string('a', 43)));

        RefusalOf(response, body).ShouldBe("invalid_grant");
        var (again, againBody) = await TokenAsync(CodeGrant(arranged.ClientId, code));
        RefusalOf(again, againBody).ShouldBe("invalid_grant");
    }

    [Test]
    public async Task Token_WhenCodeReused_ShouldRefuseAndRevoke()
    {
        var arranged = await ArrangeAuthorizationAsync();
        var code = await IssueCodeAsync(arranged.AuthorizationId);
        var (first, firstBody) = await TokenAsync(CodeGrant(arranged.ClientId, code));
        first.StatusCode.ShouldBe(HttpStatusCode.OK);

        var (second, secondBody) = await TokenAsync(CodeGrant(arranged.ClientId, code));

        RefusalOf(second, secondBody).ShouldBe("invalid_grant");
        (await ValidateAsync(firstBody.GetProperty("access_token").GetString()!)).ShouldBeNull();
        var (refresh, refreshBody) = await TokenAsync(RefreshGrant(arranged.ClientId, firstBody.GetProperty("refresh_token").GetString()!));
        RefusalOf(refresh, refreshBody).ShouldBe("invalid_grant");
    }

    [Test]
    public async Task Token_WhenRedirectOrResourceMismatch_ShouldRefuse()
    {
        var arranged = await ArrangeAuthorizationAsync();

        var wrongRedirect = await TokenAsync(CodeGrant(arranged.ClientId, await IssueCodeAsync(arranged.AuthorizationId), redirect: "http://127.0.0.1:8123/other"));
        RefusalOf(wrongRedirect.Response, wrongRedirect.Body).ShouldBe("invalid_grant");

        var notMcp = await TokenAsync(CodeGrant(arranged.ClientId, await IssueCodeAsync(arranged.AuthorizationId), resource: "https://shiplean.larkspur.dev/api"));
        RefusalOf(notMcp.Response, notMcp.Body).ShouldBe("invalid_target");

        var issuedForOther = await IssueCodeAsync(arranged.AuthorizationId, resource: "https://shiplean.larkspur.dev/other");
        var differs = await TokenAsync(CodeGrant(arranged.ClientId, issuedForOther));
        RefusalOf(differs.Response, differs.Body).ShouldBe("invalid_grant");
    }

    [Test]
    public async Task Token_ShouldAnswerWorkingBearerForMcp()
    {
        var arranged = await ArrangeAuthorizationAsync();
        var body = await RedeemAsync(arranged);

        await using var mcp = await McpTestClient.ConnectAsync(Host, body.GetProperty("access_token").GetString()!);

        (await mcp.ListToolsAsync()).ShouldNotBeNull();
    }
}
