using System.Net;
using Dapper;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.LinearApi;

// The token scheme is the only way into /linear/graphql. The host signs everyone in as Joel (test sign-in), so a 401 here also
// shows the endpoint ignores the ambient sign-in.
[TestFixture]
public class LinearApiAuthTests : ApiFixture
{
    private LinearApiWorld _world = null!;

    [OneTimeSetUp]
    public async Task Arrange()
    {
        await using var database = await OpenDatabaseAsync();
        _world = await LinearApiWorld.CreateAsync(Api, database);
    }

    [Test]
    public async Task Post_WhenAuthorizationIsTheRawToken_ShouldServeTheConnection()
    {
        var (status, body) = await LinearGraphQl.PostAsync(Client, _world.TokenA, LinearGraphQl.Document("viewer"));

        status.ShouldBe(HttpStatusCode.OK, body.ToJsonString());
        body["data"]!["viewer"]!["id"]!.GetValue<string>().ShouldBe(_world.ConnectionA.ToString());
    }

    [Test]
    public async Task Post_WhenAuthorizationIsBearer_ShouldServeTheConnection()
    {
        var (status, body) = await LinearGraphQl.PostAsync(Client, $"Bearer {_world.TokenB}", LinearGraphQl.Document("viewer"));

        status.ShouldBe(HttpStatusCode.OK, body.ToJsonString());
        body["data"]!["viewer"]!["id"]!.GetValue<string>().ShouldBe(_world.ConnectionB.ToString());
    }

    [Test]
    public async Task Post_WhenThereIsNoToken_ShouldBeUnauthorizedDespiteTheTestSignIn()
    {
        var (status, body) = await LinearGraphQl.PostAsync(Client, null, LinearGraphQl.Document("viewer"));

        status.ShouldBe(HttpStatusCode.Unauthorized);
        LinearGraphQl.ErrorCode(body).ShouldBe("AUTHENTICATION_ERROR");
    }

    [TestCase("not-a-token")]
    [TestCase("Bearer not-a-token")]
    [TestCase("Bearer ")]
    public async Task Post_WhenTheTokenIsWrong_ShouldBeUnauthorized(string authorization)
    {
        var (status, _) = await LinearGraphQl.PostAsync(Client, authorization, LinearGraphQl.Document("viewer"));

        status.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Post_WhenTheConnectionIsArchived_ShouldBeUnauthorized()
    {
        await using var database = await OpenDatabaseAsync();
        var archived = await ShipLean.WebApi.Tests.Instance.AgentConnections.AgentConnectionsWire.CreateAsync(Api, $"Archived {Guid.NewGuid():N}");
        var (before, _) = await LinearGraphQl.PostAsync(Client, archived.CallbackToken, LinearGraphQl.Document("viewer"));
        await database.ExecuteAsync("UPDATE agent_connection SET archived_at = now() WHERE id = @id", new { id = archived.Connection.Id });

        var (after, _) = await LinearGraphQl.PostAsync(Client, archived.CallbackToken, LinearGraphQl.Document("viewer"));

        (before, after).ShouldBe((HttpStatusCode.OK, HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Get_WhenNoTokenIsSent_ShouldBeUnauthorizedAndServeNoToolPage()
    {
        using var response = await Client.GetAsync($"{LinearGraphQl.Route}?query=%7Bviewer%7Bid%7D%7D");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
