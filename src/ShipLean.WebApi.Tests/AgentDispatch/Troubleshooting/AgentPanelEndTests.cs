using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.AgentDispatch.Troubleshooting;

// The Agent panel's end fields (C3 as amended by D9, C4): every member sees why a session ended; a deliveryFailed detail, every
// delivery attempt and the run log are for project admins only. Sessions and attempts are seeded with the TestSupport builders, never through the writers. Each
// test owns a project with Joel as admin and Priya as member.
[TestFixture]
public class AgentPanelEndTests : ApiFixture
{
    private const string ReceiverDetail = "The receiver answered 502 BadGateway at relay-host-7.internal:8443. (2 tries)";

    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    private static readonly DateTimeOffset Sent = new(2026, 11, 16, 3, 0, 0, TimeSpan.Zero);

    private async Task<AgentSessionDto> SessionAsync(TestUser user, Guid itemId, Guid sessionId)
    {
        using var response = await Api.ActingAs(user).GetAsync($"api/work-items/{itemId}/agent-sessions");
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonSerializer.Deserialize<AgentPanelDto>(text, Wire)!.Sessions.Single(s => s.Id == sessionId);
    }

    private async Task<(Guid ItemId, Guid ConnectionId)> ArrangeAsync(NpgsqlConnection database)
    {
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        await database.ExecuteAsync(
            "INSERT INTO project_member (id, project_id, user_id, role) VALUES (@id, @projectId, @userId, 'member')",
            new { id = Guid.NewGuid(), projectId = arranged.Project.Id, userId = SeedIds.Users.Priya });
        var item = await arranged.CreateItemAsync(Api);
        return (item.Id, arranged.ConnectionId!.Value);
    }

    private static async Task SeedTwoAttemptsAsync(NpgsqlConnection database, Guid sessionId, Guid connectionId)
    {
        var deliveryId = await AgentDeliveryAttemptRows.InsertDeliveryAsync(database, sessionId);
        await AgentDeliveryAttemptRows.InsertAsync(database, new AgentDeliveryAttemptCreate(deliveryId, connectionId, 1, Sent, 502, ReceiverDetail, 120, Sent.AddMinutes(1)));
        await AgentDeliveryAttemptRows.InsertAsync(database, new AgentDeliveryAttemptCreate(deliveryId, connectionId, 2, Sent.AddMinutes(1), 502, ReceiverDetail, 90, null));
    }

    [Test]
    public async Task Sessions_WhenDeliveryFailed_ShouldShowTheDetailAndAttemptsToAdminsOnly()
    {
        await using var database = await OpenDatabaseAsync();
        var (itemId, connectionId) = await ArrangeAsync(database);
        var sessionId = await new AgentSessionSeeds(itemId, connectionId)
            .WithState("failed").WithEndReason(AgentEndReasons.DeliveryFailed).WithEndDetail(ReceiverDetail).InsertAsync(database);
        await SeedTwoAttemptsAsync(database, sessionId, connectionId);

        var admin = await SessionAsync(TestUser.Joel, itemId, sessionId);
        var member = await SessionAsync(TestUser.Priya, itemId, sessionId);

        admin.EndReason.ShouldBe(AgentEndReasons.DeliveryFailed);
        admin.EndDetail.ShouldBe(ReceiverDetail);
        admin.Deliveries.Select(d => (d.Action, d.Attempt, d.HttpStatus)).ShouldBe([("created", 1, 502), ("created", 2, 502)]);
        member.EndReason.ShouldBe(AgentEndReasons.DeliveryFailed);
        member.EndText.ShouldNotBeNullOrWhiteSpace();
        member.EndText.ShouldNotContain(ReceiverDetail);
        member.EndText.ShouldNotContain("relay-host-7");
        member.EndDetail.ShouldBeNull();
        member.Deliveries.ShouldBeEmpty();
    }

    [Test]
    public async Task Sessions_WhenAgentErrorAfterAFailedThenSuccessfulAttempt_ShouldShowTheAttemptsToAdminsOnly()
    {
        await using var database = await OpenDatabaseAsync();
        var (itemId, connectionId) = await ArrangeAsync(database);
        var sessionId = await new AgentSessionSeeds(itemId, connectionId)
            .WithState("failed").WithEndReason(AgentEndReasons.AgentError).WithEndDetail("dotnet build failed: CS0103").InsertAsync(database);
        var deliveryId = await AgentDeliveryAttemptRows.InsertDeliveryAsync(database, sessionId, status: "sent");
        await AgentDeliveryAttemptRows.InsertAsync(database, new AgentDeliveryAttemptCreate(deliveryId, connectionId, 1, Sent, null, "Connection refused (relay-host-7.internal:8443)", 15, Sent.AddMinutes(1)));
        await AgentDeliveryAttemptRows.InsertAsync(database, new AgentDeliveryAttemptCreate(deliveryId, connectionId, 2, Sent.AddMinutes(1), 202, null, 80, null));

        var admin = await SessionAsync(TestUser.Joel, itemId, sessionId);
        var member = await SessionAsync(TestUser.Priya, itemId, sessionId);

        admin.Deliveries.Select(d => (d.Attempt, d.HttpStatus, d.Error)).ShouldBe([(1, null, "Connection refused (relay-host-7.internal:8443)"), (2, 202, null)]);
        member.EndDetail.ShouldBe("dotnet build failed: CS0103");
        member.Deliveries.ShouldBeEmpty();
    }

    [Test]
    public async Task Sessions_WhenTheAgentReportedAnError_ShouldShowTheDetailToMembers()
    {
        await using var database = await OpenDatabaseAsync();
        var (itemId, connectionId) = await ArrangeAsync(database);
        var sessionId = await new AgentSessionSeeds(itemId, connectionId)
            .WithState("failed").WithEndReason(AgentEndReasons.AgentError).WithEndDetail("dotnet build failed: CS0103").InsertAsync(database);

        var member = await SessionAsync(TestUser.Priya, itemId, sessionId);

        member.EndReason.ShouldBe(AgentEndReasons.AgentError);
        member.EndDetail.ShouldBe("dotnet build failed: CS0103");
        member.EndText.ShouldNotBeNullOrWhiteSpace();
    }

    [Test]
    public async Task Sessions_WhenTheSessionIsStale_ShouldFillTheEndFieldsAndLeaveEndedLabelNull()
    {
        await using var database = await OpenDatabaseAsync();
        var (itemId, connectionId) = await ArrangeAsync(database);
        var sessionId = await new AgentSessionSeeds(itemId, connectionId)
            .WithState("stale").WithEndReason(AgentEndReasons.Stale).WithEndDetail("No activity for 30 minutes since 2026-11-16T02:30:00Z.").InsertAsync(database);

        var member = await SessionAsync(TestUser.Priya, itemId, sessionId);

        member.Live.ShouldBeFalse();
        member.EndedLabel.ShouldBeNull();
        member.EndReason.ShouldBe(AgentEndReasons.Stale);
        member.EndText.ShouldNotBeNullOrWhiteSpace();
        member.EndDetail.ShouldBe("No activity for 30 minutes since 2026-11-16T02:30:00Z.");
    }

    [Test]
    public async Task Sessions_WhenTheSessionIsLive_ShouldLeaveTheEndFieldsEmpty()
    {
        await using var database = await OpenDatabaseAsync();
        var (itemId, connectionId) = await ArrangeAsync(database);
        var sessionId = await new AgentSessionSeeds(itemId, connectionId).WithState("working").InsertAsync(database);
        await SeedTwoAttemptsAsync(database, sessionId, connectionId);

        var admin = await SessionAsync(TestUser.Joel, itemId, sessionId);

        (admin.EndReason, admin.EndText, admin.EndDetail, admin.RunLog).ShouldBe((null, null, null, null));
        admin.Deliveries.ShouldBeEmpty();
    }
}
