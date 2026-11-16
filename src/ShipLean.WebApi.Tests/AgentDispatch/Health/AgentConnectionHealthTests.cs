using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Demos;
using ShipLean.Core;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Tests.Demos;

namespace ShipLean.WebApi.Tests.AgentDispatch.Health;

// C7 and C8: a connection's health and its last-delivery warning, read from agent_delivery_attempt. Rows are seeded with the
// TestSupport builders; times are offsets from the run's clock, in whole seconds because Postgres keeps microseconds.
[TestFixture]
public class AgentConnectionHealthTests : ApiFixture
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    private TimeProvider Clock => Host.Services.GetRequiredService<TimeProvider>();

    private DateTimeOffset Ago(TimeSpan age) => DateTimeOffset.FromUnixTimeSeconds(Clock.GetUtcNow().Add(-age).ToUnixTimeSeconds());

    private async Task<Guid> NewDeliveryAsync(NpgsqlConnection database, Guid connectionId)
    {
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("task").InsertAsync(database);
        var session = await new AgentSessionSeeds(item, connectionId).WithState("failed").WithEndReason(AgentEndReasons.DeliveryFailed).InsertAsync(database);
        return await AgentDeliveryAttemptRows.InsertDeliveryAsync(database, session);
    }

    private static int _attempt;

    private static Task AddAsync(NpgsqlConnection database, Guid delivery, Guid connection, DateTimeOffset at, int? status, string? error)
        => AgentDeliveryAttemptRows.InsertAsync(database, new(delivery, connection, Interlocked.Increment(ref _attempt), at, status, error, 50, null)).ContinueWith(_ => { });

    private Task<AgentConnectionHealthDto> HealthAsync(Guid connectionId)
        => Host.Services.GetRequiredService<IAgentConnectionHealth>().ForConnectionAsync(connectionId, CancellationToken.None);

    private Task<string?> WarningAsync(Guid connectionId)
        => Host.Services.GetRequiredService<IAgentConnectionWarnings>().ForConnectionAsync(connectionId, CancellationToken.None);

    [Test]
    public async Task Health_WhenASuccessAndThreeFailuresOfWhichOneIsOld_ShouldCountOnlyTheLastDay()
    {
        await using var database = await OpenDatabaseAsync();
        var connection = await AgentSessionSeeds.InsertConnectionAsync(database);
        var delivery = await NewDeliveryAsync(database, connection);
        var success = Ago(TimeSpan.FromHours(2));
        var newestFailure = Ago(TimeSpan.FromHours(1));
        await AddAsync(database, delivery, connection, success, 202, null);
        await AddAsync(database, delivery, connection, newestFailure, 502, "Bad gateway.");
        await AddAsync(database, delivery, connection, Ago(TimeSpan.FromHours(5)), null, "No answer within 5 seconds.");
        await AddAsync(database, delivery, connection, Ago(TimeSpan.FromHours(30)), null, "No answer within 5 seconds.");

        var health = await HealthAsync(connection);

        health.ShouldBe(new AgentConnectionHealthDto(
            success, DisplayDate.Relative(success, Clock), 2, newestFailure, DisplayDate.Relative(newestFailure, Clock), "Bad gateway."));
        health.LastDeliveredLabel.ShouldBe("2h ago");
    }

    [Test]
    public async Task Health_WhenTheConnectionHasNoAttempts_ShouldBeEmpty()
    {
        await using var database = await OpenDatabaseAsync();
        var connection = await AgentSessionSeeds.InsertConnectionAsync(database);

        (await HealthAsync(connection)).ShouldBe(new AgentConnectionHealthDto(null, null, 0, null, null, null));
        (await WarningAsync(connection)).ShouldBeNull();
    }

    [Test]
    public async Task Health_WhenAnotherConnectionHasAttempts_ShouldNotCountThem()
    {
        await using var database = await OpenDatabaseAsync();
        var mine = await AgentSessionSeeds.InsertConnectionAsync(database);
        var other = await AgentSessionSeeds.InsertConnectionAsync(database);
        var delivery = await NewDeliveryAsync(database, other);
        await AddAsync(database, delivery, other, Ago(TimeSpan.FromHours(1)), 202, null);
        await AddAsync(database, delivery, other, Ago(TimeSpan.FromMinutes(10)), 500, "Boom.");

        (await HealthAsync(mine)).ShouldBe(new AgentConnectionHealthDto(null, null, 0, null, null, null));
        (await WarningAsync(mine)).ShouldBeNull();
        (await HealthAsync(other)).FailedLast24h.ShouldBe(1);
    }

    [Test]
    public async Task Warning_WhenTheNewestAttemptFailedAfterAnOlderSuccess_ShouldSayWhenItFailed()
    {
        await using var database = await OpenDatabaseAsync();
        var connection = await AgentSessionSeeds.InsertConnectionAsync(database);
        var delivery = await NewDeliveryAsync(database, connection);
        await AddAsync(database, delivery, connection, Ago(TimeSpan.FromHours(5)), 202, null);
        await AddAsync(database, delivery, connection, Ago(TimeSpan.FromHours(3)), null, "No answer within 5 seconds.");

        (await WarningAsync(connection)).ShouldBe("The last delivery to this connection failed 3h ago.");
    }

    [Test]
    public async Task Warning_WhenALaterSuccessFollowsTheFailures_ShouldBeNull()
    {
        await using var database = await OpenDatabaseAsync();
        var connection = await AgentSessionSeeds.InsertConnectionAsync(database);
        var delivery = await NewDeliveryAsync(database, connection);
        await AddAsync(database, delivery, connection, Ago(TimeSpan.FromHours(5)), 202, null);
        await AddAsync(database, delivery, connection, Ago(TimeSpan.FromHours(3)), 503, "Unavailable.");

        (await WarningAsync(connection)).ShouldNotBeNull();

        await AddAsync(database, delivery, connection, Ago(TimeSpan.FromHours(1)), 202, null);

        (await WarningAsync(connection)).ShouldBeNull();
    }

    [Test]
    public async Task Warning_WhenTheNewestAttemptFailed_ShouldShowOnAvailabilityAndOnTheDemoActionsUntilALaterSuccess()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        var connection = arranged.ConnectionId!.Value;
        var item = await arranged.CreateItemAsync(Api);
        var sprintId = await DemoRunArrange.NewSprintAsync(database, arranged.Project.Id);
        var delivery = await NewDeliveryAsync(database, connection);

        async Task<(string? Start, string? Demo)> WarningsAsync()
        {
            var availability = await ReadAsync<AgentStartAvailabilityDto>(await Api.GetAsync($"api/work-items/{item.Id}/agent-session/availability"));
            using var response = await Api.GetAsync($"api/sprints/{sprintId}/demo");
            var text = await response.Content.ReadAsStringAsync();
            response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
            return (availability.Warning, JsonSerializer.Deserialize<SprintDemoDto>(text, Wire)!.Actions.CreateWarning);
        }

        (await WarningsAsync()).ShouldBe((null, null));

        await AddAsync(database, delivery, connection, Ago(TimeSpan.FromHours(3)), 202, null);
        await AddAsync(database, delivery, connection, Ago(TimeSpan.FromHours(2)), 500, "Boom.");

        var failed = "The last delivery to this connection failed 2h ago.";
        (await WarningsAsync()).ShouldBe((failed, failed));

        await AddAsync(database, delivery, connection, Ago(TimeSpan.FromHours(1)), 202, null);

        (await WarningsAsync()).ShouldBe((null, null));
    }

    [Test]
    public async Task HealthEndpoint_WhenAnInstanceAdminReadsAConnection_ShouldServeItsHealth()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        var connection = arranged.ConnectionId!.Value;
        var delivery = await NewDeliveryAsync(database, connection);
        await AddAsync(database, delivery, connection, Ago(TimeSpan.FromHours(4)), 500, "Boom.");

        var health = await ReadAsync<AgentConnectionHealthDto>(await Api.GetAsync($"api/instance/agent-connections/{connection}/health"));

        (health.FailedLast24h, health.LastError, health.LastDeliveredAt).ShouldBe((1, "Boom.", null));
    }
}
