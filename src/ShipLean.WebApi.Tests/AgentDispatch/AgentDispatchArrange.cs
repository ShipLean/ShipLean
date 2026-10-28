using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapper;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Refs;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Tests.Instance.AgentConnections;
using ShipLean.WebApi.Tests.ProjectSettings.Api;

namespace ShipLean.WebApi.Tests.AgentDispatch;

// A project of its own for one test, with Joel as its admin: New and Active states per type, "Active" flagged as starting
// an agent, a live connection and a repository (each can be left out). Items are written through the API, so the real events fire.
internal sealed class AgentDispatchArrange
{
    private AgentDispatchArrange(ArrangedProject project, Guid? connectionId)
    {
        Project = project;
        ConnectionId = connectionId;
    }

    public ArrangedProject Project { get; }

    public Guid? ConnectionId { get; }

    public Guid Flagged => Project.State(WorkItemTypeId.Story, "Active");

    public Guid Unflagged => Project.State(WorkItemTypeId.Story, "Resolved");

    public static async Task<AgentDispatchArrange> CreateAsync(NpgsqlConnection database, ApiClient api, bool connection = true, bool repository = true)
    {
        var project = await ArrangedProject.CreateAsync(database, (SeedIds.Users.Joel, ProjectRole.Admin));
        await database.ExecuteAsync("UPDATE state SET starts_agent = true WHERE id = @id", new { id = project.State(WorkItemTypeId.Story, "Active") });
        Guid? connectionId = connection ? (await AgentConnectionsWire.CreateAsync(api, $"Relay {Guid.NewGuid():N}")).Connection.Id : null;
        await database.ExecuteAsync(
            "UPDATE project SET agent_connection_id = @connectionId, repo_url = @repo, repo_base_branch = 'develop' WHERE id = @id",
            new { id = project.Id, connectionId, repo = repository ? "https://github.com/ShipLean/ShipLean.git" : null });
        return new AgentDispatchArrange(project, connectionId);
    }

    public async Task<(Guid Id, string Key, int Revision)> CreateItemAsync(ApiClient api, Guid? stateId = null)
    {
        var body = new JsonObject
        {
            ["projectId"] = Project.Id,
            ["type"] = "story",
            ["title"] = "Add the thing",
            ["changes"] = stateId is null ? null : new JsonObject { ["state"] = new JsonObject { ["value"] = stateId } },
        };
        return await WriteAsync(await api.PostJsonAsync("api/work-items", body));
    }

    public static async Task<(Guid Id, string Key, int Revision)> MoveAsync(ApiClient api, Guid id, int revision, Guid stateId)
    {
        var body = new JsonObject { ["expectedRevision"] = revision, ["changes"] = new JsonObject { ["state"] = new JsonObject { ["value"] = stateId } } };
        return await WriteAsync(await api.PatchJsonAsync($"api/work-items/{id}", body));
    }

    private static async Task<(Guid Id, string Key, int Revision)> WriteAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        var json = JsonNode.Parse(text)!;
        return (json["id"]!.GetValue<Guid>(), json["key"]!.GetValue<string>(), json["revision"]!.GetValue<int>());
    }

    public static async Task<List<SessionRow>> SessionsAsync(NpgsqlConnection database, Guid workItemId)
        => [.. await database.QueryAsync<SessionRow>(
            "SELECT id, state, connection_id AS connectionId, created_by AS createdBy, repo_url AS repoUrl, base_branch AS baseBranch FROM agent_session WHERE work_item_id = @workItemId ORDER BY created_at",
            new { workItemId })];

    public static async Task<List<DeliveryRow>> DeliveriesAsync(NpgsqlConnection database, Guid workItemId)
        => [.. await database.QueryAsync<DeliveryRow>(
            """
            SELECT d.id, d.session_id AS sessionId, d.seq, d.action, d.body::text AS body, d.status, d.attempts
            FROM agent_delivery d JOIN agent_session s ON s.id = d.session_id WHERE s.work_item_id = @workItemId ORDER BY d.created_at, d.seq
            """,
            new { workItemId })];

    public static JsonNode BodyOf(DeliveryRow delivery) => JsonNode.Parse(delivery.Body, documentOptions: new JsonDocumentOptions())!;

    internal sealed record SessionRow(Guid Id, string State, Guid ConnectionId, Guid CreatedBy, string RepoUrl, string BaseBranch);

    internal sealed record DeliveryRow(Guid Id, Guid SessionId, int Seq, string Action, string Body, string Status, int Attempts);
}
