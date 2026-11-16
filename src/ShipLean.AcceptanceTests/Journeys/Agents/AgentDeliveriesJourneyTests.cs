using System.Text.Json;
using Npgsql;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.WorkItem;
using ShipLean.Contracts.Agents;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Journeys.Agents;

// A session ended as failed after two refused tries. Joel (a project admin) opens the item and expands Delivery in the agent
// panel to see both tries. The rows are seeded by SQL, not by the deliverer.
[TestFixture]
public class AgentDeliveriesJourneyTests : SeededAcceptanceTest
{
    [Test]
    public async Task EndedSession_WhenAttemptsFailed_ShouldListThemForAdmin()
    {
        // Arrange: a story with a failed session and two attempt rows.
        var item = await CreateStoryAsync();
        var sent = TimeProvider.System.GetUtcNow().AddMinutes(-10);
        await using (var database = new NpgsqlConnection(ConnectionString))
        {
            await database.OpenAsync();
            var connectionId = await AgentSessionSeeds.InsertConnectionAsync(database);
            var sessionId = await new AgentSessionSeeds(item.Id, connectionId)
                .WithState("failed")
                .WithEndReason(AgentEndReasons.DeliveryFailed)
                .WithEndDetail("The receiver answered 503. (2 tries)")
                .InsertAsync(database);
            var deliveryId = await AgentDeliveryAttemptRows.InsertDeliveryAsync(database, sessionId);
            await AgentDeliveryAttemptRows.InsertAsync(database, new AgentDeliveryAttemptCreate(deliveryId, connectionId, 1, sent, 502, "The receiver answered 502.", 120, sent.AddSeconds(30)));
            await AgentDeliveryAttemptRows.InsertAsync(database, new AgentDeliveryAttemptCreate(deliveryId, connectionId, 2, sent.AddSeconds(30), 503, "The receiver answered 503.", 90, null));
        }

        // Act: open the item and expand the delivery list.
        await new WorkItemScreen(Page).OpenAsync("SHIP", item.Key);
        var panel = Page.GetByTestId(AgentPanelTestIds.Panel);
        await Expect(panel).ToBeVisibleAsync();
        var deliveries = panel.GetByTestId(AgentPanelTestIds.SessionDeliveries);
        await deliveries.GetByTestId(AgentPanelTestIds.SessionDeliveriesToggle).ClickAsync(); // collapsed, the list is just its summary line

        // Assert: both tries, oldest first, with what the receiver answered.
        var rows = deliveries.GetByTestId(AgentPanelTestIds.SessionDeliveryRow);
        await Expect(rows).ToHaveCountAsync(2);
        await Expect(rows.Nth(0)).ToContainTextAsync("attempt 1");
        await Expect(rows.Nth(0)).ToContainTextAsync("The receiver answered 502.");
        await Expect(rows.Nth(1)).ToContainTextAsync("attempt 2");
        await Expect(rows.Nth(1)).ToContainTextAsync("The receiver answered 503.");
    }

    private async Task<(Guid Id, string Key)> CreateStoryAsync()
    {
        var options = new APIRequestContextOptions
        {
            Method = "POST",
            Headers = new Dictionary<string, string> { ["X-ShipLean-Client"] = "agent-deliveries-journey" },
            DataObject = new { projectId = SeedIds.Projects.Ship, type = "story", title = "Deliveries are listed" },
        };
        var response = await Page.Context.APIRequest.FetchAsync("/api/work-items", options);
        var text = await response.TextAsync();
        response.Ok.ShouldBeTrue(text);
        var json = JsonDocument.Parse(text).RootElement;
        return (json.GetProperty("id").GetGuid(), json.GetProperty("key").GetString()!);
    }
}
