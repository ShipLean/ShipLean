using System.Data.Common;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapper;
using ShipLean.Contracts.Persisted;

namespace ShipLean.Application.Features.ProjectSettings;

// A removed state must not stay mapped to a board column: its mapping points at the state its items moved to.
internal static class BoardMappingRepair
{
    public static async Task RunAsync(DbTransaction transaction, Guid projectId, IReadOnlyDictionary<Guid, Guid> moved, CancellationToken cancellationToken)
    {
        var connection = transaction.Connection!;
        var boards = await connection.QueryAsync<(Guid Id, string Settings)>(new CommandDefinition(
            "SELECT id, settings::text AS settings FROM board WHERE project_id = @projectId ORDER BY id FOR UPDATE",
            new { projectId }, transaction, cancellationToken: cancellationToken));
        foreach (var (id, text) in boards)
        {
            var settings = JsonSerializer.Deserialize<BoardSettingsJson>(text, PersistedJson.Options)!;
            var changed = false;
            var columns = new List<BoardColumnJson>();
            var lanes = settings.Swimlanes.Select(l => l with { Criteria = l.Criteria is null ? null : Remap(l.Criteria, moved) }).ToList();
            var rules = settings.StyleRules.Select(r => r with { Criteria = Remap(r.Criteria, moved) }).ToList();
            changed |= lanes.Where((l, i) => !JsonEqual(l.Criteria, settings.Swimlanes[i].Criteria)).Any()
                || rules.Where((r, i) => !JsonEqual(r.Criteria, settings.StyleRules[i].Criteria)).Any();
            foreach (var column in settings.Columns)
            {
                var mappings = column.StateMappings.Select(m => moved.TryGetValue(m.StateId, out var target) ? m with { StateId = target } : m).ToList();
                changed |= mappings.Where((m, i) => m != column.StateMappings[i]).Any();
                columns.Add(column with { StateMappings = mappings });
            }

            if (changed)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    "UPDATE board SET settings = @settings::jsonb, updated_at = now() WHERE id = @id",
                    new { id, settings = JsonSerializer.Serialize(settings with { Columns = columns, Swimlanes = lanes, StyleRules = rules }, PersistedJson.Options) }, transaction, cancellationToken: cancellationToken));
            }
        }
    }

    private static readonly HashSet<string> StateFields = [.. FieldIds.All.Where(f => f.Kind == FieldKind.State).Select(f => f.Id)];

    private static bool JsonEqual(CriteriaJson? a, CriteriaJson? b)
        => JsonSerializer.Serialize(a, PersistedJson.Options) == JsonSerializer.Serialize(b, PersistedJson.Options);

    private static CriteriaJson Remap(CriteriaJson criteria, IReadOnlyDictionary<Guid, Guid> moved)
        => criteria with { Clauses = [.. criteria.Clauses.Select(c => StateFields.Contains(c.Field) ? c with { Value = RemapValue(c.Value, moved) } : c)] };

    // A State clause's value is one id (string) or, for "in", an array of ids.
    private static JsonElement RemapValue(JsonElement value, IReadOnlyDictionary<Guid, Guid> moved)
    {
        JsonNode? Id(JsonElement e) => e.ValueKind == JsonValueKind.String && e.TryGetGuid(out var id) && moved.TryGetValue(id, out var target)
            ? JsonValue.Create(target.ToString())
            : JsonSerializer.SerializeToNode(e);
        JsonNode? node = value.ValueKind switch
        {
            JsonValueKind.String => Id(value),
            JsonValueKind.Array => new JsonArray([.. value.EnumerateArray().Select(Id)]),
            _ => null,
        };
        return node is null ? value : JsonSerializer.SerializeToElement(node);
    }
}
