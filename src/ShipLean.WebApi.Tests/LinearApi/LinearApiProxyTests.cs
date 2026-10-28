using System.Net;
using ShipLean.TestSupport.Fixtures;
using Dapper;
using Npgsql;
using ShipLean.Contracts.Agents;
using ShipLean.TestSupport;

namespace ShipLean.WebApi.Tests.LinearApi;

// Behind an identity proxy that reads Authorization as its own JWT, the endpoint still takes the connection's token: the proxy
// identity is never read on /linear/graphql and the CSRF guard does not apply.
[TestFixture]
public class LinearApiProxyTests : ProxyApiFixture
{
    protected override bool UseFakeReader => false;

    protected override IReadOnlyDictionary<string, string?> ProxySettings => new Dictionary<string, string?>
    {
        ["SHIPLEAN_AUTH_PROXY_TOKEN_HEADER"] = "Authorization",
        ["SHIPLEAN_AUTH_PROXY_TOKEN_ISSUER"] = "https://idp.example.test",
        ["SHIPLEAN_AUTH_PROXY_TOKEN_JWKS_URL"] = "http://127.0.0.1:9/jwks",
    };

    [Test]
    public async Task Post_WhenAuthorizationIsAConnectionToken_ShouldServeItInProxyMode()
    {
        await using var database = await OpenDatabaseAsync();
        var issued = await IssueAsync(database);

        var (status, body) = await LinearGraphQl.PostAsync(Client, issued.Token, LinearGraphQl.Document("viewer"));
        var (bearerStatus, _) = await LinearGraphQl.PostAsync(Client, $"Bearer {issued.Token}", LinearGraphQl.Document("viewer"));
        var (wrongStatus, _) = await LinearGraphQl.PostAsync(Client, "Bearer nope", LinearGraphQl.Document("viewer"));

        status.ShouldBe(HttpStatusCode.OK, body.ToJsonString());
        body["data"]!["viewer"]!["id"]!.GetValue<string>().ShouldBe(issued.Id.ToString());
        (bearerStatus, wrongStatus).ShouldBe((HttpStatusCode.OK, HttpStatusCode.Unauthorized));
    }

    // The proxy host has no signed-in admin to call the admin API, so the row is written as the admin API writes it.
    private static async Task<(Guid Id, string Token)> IssueAsync(NpgsqlConnection database)
    {
        var id = Guid.NewGuid();
        var token = $"lin_{Guid.NewGuid():N}";
        await database.ExecuteAsync(
            """
            INSERT INTO agent_connection (id, type, name, receiver_url, signing_secret_protected, signing_secret_last4, callback_token_hash, callback_token_last4,
                                          created_by, created_at, updated_by, updated_at)
            VALUES (@id, 'linear-compatible', @name, 'https://relay.example.com/hook', 'x', 'xxxx', @hash, 'xxxx', @joel, now(), @joel, now())
            """,
            new { id, name = $"Proxy {id:N}", hash = IAgentConnections.HashCallbackToken(token), joel = SeedIds.Users.Joel });
        return (id, token);
    }
}
