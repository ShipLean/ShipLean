using System.Text.Json;
using Dapper;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;

namespace ShipLean.WebApi.Tests.ProjectSettings.Api;

// A project of its own for one test: five states per type (New, Active, Resolved, Closed, Removed), a Stories board whose
// "Doing" column maps the Story and Bug Active states, and the given members.
internal sealed class ArrangedProject
{
    private readonly Dictionary<(WorkItemTypeId Type, string Name), Guid> _states = [];

    private ArrangedProject(Guid id, string key)
    {
        Id = id;
        Key = key;
    }

    public Guid Id { get; }

    public string Key { get; }

    public Guid State(WorkItemTypeId type, string name) => _states[(type, name)];

    public static async Task<ArrangedProject> CreateAsync(NpgsqlConnection connection, params (Guid UserId, ProjectRole Role)[] members)
    {
        var id = Guid.NewGuid();
        var key = new string([.. Enumerable.Range(0, 4).Select(_ => (char)('A' + Random.Shared.Next(26)))]);
        var project = new ArrangedProject(id, key);
        await connection.ExecuteAsync("INSERT INTO project (id, key, name) VALUES (@id, @key, @name)", new { id, key, name = $"Project {key}" });
        foreach (var (userId, role) in members)
        {
            await connection.ExecuteAsync(
                "INSERT INTO project_member (id, project_id, user_id, role) VALUES (@id, @id2, @userId, @role)",
                new { id = Guid.NewGuid(), id2 = id, userId, role = role.ToString().ToLowerInvariant() });
        }

        (string Name, string Category)[] names = [("New", "proposed"), ("Active", "inProgress"), ("Resolved", "resolved"), ("Closed", "completed"), ("Removed", "removed")];
        foreach (var type in Enum.GetValues<WorkItemTypeId>())
        {
            for (var i = 0; i < names.Length; i++)
            {
                var stateId = Guid.NewGuid();
                project._states[(type, names[i].Name)] = stateId;
                await connection.ExecuteAsync(
                    "INSERT INTO state (id, project_id, type, name, category, sort_order) VALUES (@stateId, @id, @type, @name, @category, @order)",
                    new { stateId, id, type = type.ToString().ToLowerInvariant(), name = names[i].Name, category = names[i].Category, order = i + 1 });
            }
        }

        BoardColumnJson[] columns =
        [
            new("col-todo", "To do", null, false, "", [new(WorkItemTypeId.Story, project.State(WorkItemTypeId.Story, "New")), new(WorkItemTypeId.Bug, project.State(WorkItemTypeId.Bug, "New"))]),
            new("col-doing", "Doing", null, false, "", [new(WorkItemTypeId.Story, project.State(WorkItemTypeId.Story, "Active")), new(WorkItemTypeId.Bug, project.State(WorkItemTypeId.Bug, "Active"))]),
        ];
        var settings = new BoardSettingsJson(
            columns, [new SwimlaneJson("lane-default", "Everything else", null, SwimlaneKind.Default, null)], [], [], [], new CardAnnotationsJson(false, false, false, false), null, []);
        await connection.ExecuteAsync(
            "INSERT INTO board (id, project_id, level, settings) VALUES (@boardId, @id, 'stories', @settings::jsonb)",
            new { boardId = Guid.NewGuid(), id, settings = JsonSerializer.Serialize(settings, PersistedJson.Options) });
        return project;
    }
}
