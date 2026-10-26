using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Auth;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Contracts.Agents;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Auth;

namespace ShipLean.WebApi.Tests.Agents.Tokens;

// Arranges an authorization and a code through the agent interfaces from the host's services (so it works on any store),
// then speaks to /oauth/token and /oauth/revoke as an agent would: a form, no session. The seed's base_url is
// https://shiplean.larkspur.dev, so its /mcp is the resource.
public abstract class TokenFixture : ApiFixture
{
    protected const string Resource = "https://shiplean.larkspur.dev/mcp";
    protected const string Redirect = AgentWorld.LoopbackRedirect;
    protected static readonly string[] ReadWrite = ["shiplean:read", "shiplean:write"];

    protected override TestUser SignedInAs => TestUser.Anonymous;

    protected record Arranged(Guid UserId, string ClientId, Guid AuthorizationId);

    protected async Task<Arranged> ArrangeAuthorizationAsync(params string[] scopes)
    {
        Guid userId;
        await using (var connection = await OpenDatabaseAsync())
        {
            userId = await new AppUserBuilder().InsertAsync(connection);
        }
        await using var scope = Host.Services.CreateAsyncScope();
        var clientId = (await scope.ServiceProvider.GetRequiredService<IOAuthClients>()
            .RegisterAsync(new OAuthClientRegistration("Token Bot", [Redirect]))).ClientId;
        var authorizationId = await scope.ServiceProvider.GetRequiredService<IAgentAuthorizations>()
            .CreateAsync(userId, clientId, (scopes.Length == 0 ? ReadWrite : scopes).ToHashSet());
        return new Arranged(userId, clientId, authorizationId);
    }

    protected async Task<string> IssueCodeAsync(Guid authorizationId, string resource = Resource, string redirect = Redirect)
    {
        await using var scope = Host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IOAuthCodes>().IssueAsync(authorizationId, AgentWorld.Challenge, redirect, resource);
    }

    protected static Dictionary<string, string> CodeGrant(string clientId, string code, string verifier = AgentWorld.Verifier, string redirect = Redirect, string resource = Resource)
        => new()
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = clientId,
            ["code"] = code,
            ["code_verifier"] = verifier,
            ["redirect_uri"] = redirect,
            ["resource"] = resource,
        };

    protected static Dictionary<string, string> RefreshGrant(string clientId, string refreshToken, string resource = Resource)
        => new()
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = clientId,
            ["refresh_token"] = refreshToken,
            ["resource"] = resource,
        };

    protected async Task<(HttpResponseMessage Response, JsonElement Body)> PostAsync(string path, Dictionary<string, string> form)
    {
        var response = await Client.PostAsync(path, new FormUrlEncodedContent(form));
        var text = await response.Content.ReadAsStringAsync();
        return (response, text.Length == 0 ? default : JsonDocument.Parse(text).RootElement);
    }

    protected Task<(HttpResponseMessage Response, JsonElement Body)> TokenAsync(Dictionary<string, string> form) => PostAsync("/oauth/token", form);

    protected async Task<JsonElement> RedeemAsync(Arranged arranged)
    {
        var (response, body) = await TokenAsync(CodeGrant(arranged.ClientId, await IssueCodeAsync(arranged.AuthorizationId)));
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return body;
    }

    // The error code of a refusal, after checking the status, that no token came with it, and no-store.
    protected static string? RefusalOf(HttpResponseMessage response, JsonElement body, HttpStatusCode status = HttpStatusCode.BadRequest)
    {
        response.StatusCode.ShouldBe(status);
        body.TryGetProperty("access_token", out _).ShouldBeFalse();
        response.Headers.CacheControl!.NoStore.ShouldBeTrue();
        return body.GetProperty("error").GetString();
    }

    protected async Task<AgentPrincipal?> ValidateAsync(string accessToken)
    {
        await using var scope = Host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IAgentTokens>().ValidateAsync(accessToken, Resource);
    }

    // A real password change by the user, through the API, which rotates the security stamp.
    protected async Task ChangePasswordAsync(Guid userId, string newPassword)
    {
        string email;
        await using (var connection = await OpenDatabaseAsync())
        {
            email = await connection.QuerySingleAsync<string>("SELECT email FROM app_user WHERE id = @userId", new { userId });
        }
        using var browser = await AuthClient.SignedInAsync(Host, email, AppUserBuilder.ArrangedPassword);
        using var changed = await browser.PostAsJsonAsync("/api/auth/password", new ChangePasswordRequest(AppUserBuilder.ArrangedPassword, newPassword), AuthClient.Json);
        changed.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    protected async Task ExecuteAsync(string sql, object parameters)
    {
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync(sql, parameters);
    }
}
