using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.AgentDispatch;

// Start agent and its availability probe. Every test arranges a project of its own and an item that no transition covers.
[TestFixture]
public class AgentStartApiTests : ApiFixture
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonSerializer.Deserialize<T>(text, Wire)!;
    }

    [Test]
    public async Task Start_WhenConnectionAndRepositoryAreSet_ShouldOpenASessionAndQueueCreated()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        var item = await arranged.CreateItemAsync(Api);

        using var response = await Api.PostJsonAsync($"api/work-items/{item.Id}/agent-session");

        var started = await ReadAsync<AgentSessionStartedDto>(response);
        started.State.ShouldBe("submitted");
        var session = (await AgentDispatchArrange.SessionsAsync(database, item.Id)).ShouldHaveSingleItem();
        session.Id.ShouldBe(started.SessionId);
        session.State.ShouldBe("submitted");
        var delivery = (await AgentDispatchArrange.DeliveriesAsync(database, item.Id)).ShouldHaveSingleItem();
        (delivery.Action, delivery.Status, delivery.Seq).ShouldBe(("created", "pending", 1));
        AgentDispatchArrange.BodyOf(delivery)["agentSession"]!["issue"]!["identifier"]!.GetValue<string>().ShouldBe(item.Key);
    }

    [Test]
    public async Task Start_WhenASessionIsLive_ShouldBeConflictAndAddNothing()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        var item = await arranged.CreateItemAsync(Api);
        using var first = await Api.PostJsonAsync($"api/work-items/{item.Id}/agent-session");
        first.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var second = await Api.PostJsonAsync($"api/work-items/{item.Id}/agent-session");

        (await ApiClient.ReadApiErrorAsync(second, HttpStatusCode.Conflict)).Code.ShouldBe(ApiErrorCodes.Conflict);
        (await AgentDispatchArrange.SessionsAsync(database, item.Id)).ShouldHaveSingleItem();
        (await AgentDispatchArrange.DeliveriesAsync(database, item.Id)).ShouldHaveSingleItem();
    }

    [Test]
    public async Task Start_WhenTheLastSessionEnded_ShouldOpenANewOne()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        var item = await arranged.CreateItemAsync(Api);
        using var first = await Api.PostJsonAsync($"api/work-items/{item.Id}/agent-session");
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        await database.ExecuteAsync("UPDATE agent_session SET state = 'failed', ended_at = now() WHERE work_item_id = @id", new { id = item.Id });

        using var second = await Api.PostJsonAsync($"api/work-items/{item.Id}/agent-session");

        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await AgentDispatchArrange.SessionsAsync(database, item.Id)).Count.ShouldBe(2);
    }

    [Test]
    public async Task Start_WhenThereIsNoConnection_ShouldBeAValidationErrorNamingIt()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api, connection: false);
        var item = await arranged.CreateItemAsync(Api);

        using var response = await Api.PostJsonAsync($"api/work-items/{item.Id}/agent-session");

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Error.ShouldContain("agent connection");
        (await AgentDispatchArrange.SessionsAsync(database, item.Id)).ShouldBeEmpty();
    }

    [Test]
    public async Task Start_WhenTheConnectionIsArchived_ShouldBeAValidationErrorNamingIt()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        await database.ExecuteAsync("UPDATE agent_connection SET archived_at = now() WHERE id = @id", new { id = arranged.ConnectionId });
        var item = await arranged.CreateItemAsync(Api);

        using var response = await Api.PostJsonAsync($"api/work-items/{item.Id}/agent-session");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Error.ShouldContain("agent connection");
    }

    [Test]
    public async Task Start_WhenThereIsNoRepository_ShouldBeAValidationErrorNamingIt()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api, repository: false);
        var item = await arranged.CreateItemAsync(Api);

        using var response = await Api.PostJsonAsync($"api/work-items/{item.Id}/agent-session");

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Error.ShouldContain("repository");
    }

    [Test]
    public async Task Start_WhenTheItemDoesNotExist_ShouldBeNotFound()
    {
        using var response = await Api.PostJsonAsync($"api/work-items/{Guid.NewGuid()}/agent-session");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Availability_ShouldTellWhetherAndWhyNot()
    {
        await using var database = await OpenDatabaseAsync();
        var ready = await AgentDispatchArrange.CreateAsync(database, Api);
        var noConnection = await AgentDispatchArrange.CreateAsync(database, Api, connection: false);
        var noRepository = await AgentDispatchArrange.CreateAsync(database, Api, repository: false);
        var readyItem = await ready.CreateItemAsync(Api);
        var noConnectionItem = await noConnection.CreateItemAsync(Api);
        var noRepositoryItem = await noRepository.CreateItemAsync(Api);

        async Task<AgentStartAvailabilityDto> AvailabilityAsync(Guid id) => await ReadAsync<AgentStartAvailabilityDto>(await Api.GetAsync($"api/work-items/{id}/agent-session/availability"));

        (await AvailabilityAsync(readyItem.Id)).ShouldBe(new AgentStartAvailabilityDto(true, null, string.Empty));
        (await AvailabilityAsync(noConnectionItem.Id)).Reason.ShouldBe(AgentStartBlock.NoConnection);
        (await AvailabilityAsync(noRepositoryItem.Id)).Reason.ShouldBe(AgentStartBlock.NoRepository);
        using var started = await Api.PostJsonAsync($"api/work-items/{readyItem.Id}/agent-session");
        started.StatusCode.ShouldBe(HttpStatusCode.OK);
        var live = await AvailabilityAsync(readyItem.Id);
        (live.CanStart, live.Reason).ShouldBe((false, AgentStartBlock.LiveSession));
        live.Message.ShouldNotBeNullOrEmpty();
    }
}
