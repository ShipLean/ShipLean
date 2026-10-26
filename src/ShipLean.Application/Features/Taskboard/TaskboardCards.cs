using Agoda.IoC.Core;
using Dapper;
using Npgsql;
using ShipLean.Contracts.Criteria;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views;
using ShipLean.Contracts.WorkItems.Reads.Tree;
using ShipLean.Core;

namespace ShipLean.Application.Features.Taskboard;

// Turns tree rows into the cards of the Taskboard: tag colours, the first matching style rule and the extra fields, all from the Taskboard's own settings.
[RegisterPerRequest]
public sealed class TaskboardCards(NpgsqlDataSource dataSource, ICriteriaEvaluator criteria, TimeProvider time)
{
    // Fields a card shows on its own lines; an extra field naming one of these is left out.
    private static readonly HashSet<string> OnTheCard = [FieldIds.State, FieldIds.Parent, FieldIds.Assignee, FieldIds.Points, FieldIds.Tags];

    public async Task<IReadOnlyDictionary<Guid, CardDto>> BuildAsync(
        IReadOnlyList<StackRowDto> rows, IReadOnlyList<WorkTreeRollupDto> rollups, BoardSettingsJson settings, CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return new Dictionary<Guid, CardDto>();
        }

        var ids = rows.Select(row => row.Id).ToArray();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var styles = await StylesAsync(connection, ids, settings.StyleRules, cancellationToken);
        var wantsExtras = settings.CardFields.Any(field => field.ExtraFields.Count > 0);
        var extras = wantsExtras ? await ExtrasAsync(connection, ids, cancellationToken) : new Dictionary<Guid, TaskboardCardExtras>();
        return rows.ToDictionary(row => row.Id, row => Card(row, rollups, settings, styles, extras));
    }

    private CardDto Card(
        StackRowDto row, IReadOnlyList<WorkTreeRollupDto> rollups, BoardSettingsJson settings,
        Dictionary<Guid, string> styles, Dictionary<Guid, TaskboardCardExtras> extras)
    {
        var tags = row.Tags.Select(tag => new CardTagDto(tag, settings.TagColours.FirstOrDefault(c => string.Equals(c.Tag, tag, StringComparison.OrdinalIgnoreCase))?.Colour)).ToList();
        var counts = rollups.Where(r => r.ParentId == row.Id).Select(r => new RollupCountDto(r.Type, r.Open, r.Closed)).ToList();
        var setting = settings.CardFields.FirstOrDefault(f => f.Type == row.Type.Id);
        var extra = (setting?.ExtraFields ?? []).Where(id => !OnTheCard.Contains(id))
            .Select(id => Field(id, row, extras.GetValueOrDefault(row.Id)))
            .OfType<CardFieldDto>()
            .Where(field => field.Text is not null || setting!.ShowEmptyFields)
            .ToList();
        return new CardDto(row.Id, row.Key, row.Type, row.Title, row.State, row.Assignee, row.Points, null, false, row.Parent, tags, counts, false, styles.GetValueOrDefault(row.Id), extra, row.Revision);
    }

    // Null for a field the card's type doesn't have (or one that isn't a card field).
    private CardFieldDto? Field(string fieldId, StackRowDto row, TaskboardCardExtras? extras)
    {
        var definition = FieldIds.All.FirstOrDefault(f => f.Id == fieldId);
        if (definition is null || !definition.UsedIn.Contains(FieldUse.CardField) || !definition.AppliesTo.Contains(row.Type.Id))
        {
            return null;
        }

        var text = fieldId switch
        {
            FieldIds.Priority => extras is null ? null : Option(definition, extras.Priority),
            FieldIds.Severity => extras?.Severity is { } severity ? Option(definition, severity) : null,
            FieldIds.Sprint => row.Sprint?.Name,
            FieldIds.CreatedAt => extras is null ? null : DisplayDate.Relative(extras.CreatedAt, time),
            FieldIds.ChangedAt => extras is null ? null : DisplayDate.Relative(extras.ChangedAt, time),
            FieldIds.CreatedBy => extras?.CreatedByName,
            FieldIds.ChangedBy => extras?.ChangedByName,
            _ => null,
        };
        return new CardFieldDto(fieldId, definition.Label, text);
    }

    private static string Option(FieldDefinition definition, int value)
        => definition.Values?.FirstOrDefault(o => o.Value.TryGetInt32(out var number) && number == value)?.Label ?? value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    // The first enabled rule that matches each card, by the same criteria SQL the backlog filters use.
    private async Task<Dictionary<Guid, string>> StylesAsync(NpgsqlConnection connection, Guid[] ids, IReadOnlyList<StyleRuleJson> rules, CancellationToken cancellationToken)
    {
        var styles = new Dictionary<Guid, string>();
        foreach (var rule in rules.Where(r => r.Enabled))
        {
            var sql = criteria.ToSql(rule.Criteria, "w");
            var parameters = new DynamicParameters(new { ids });
            foreach (var (name, value) in sql.Parameters)
            {
                parameters.Add(name, value);
            }

            var query = $"SELECT w.id FROM work_item w WHERE w.id = ANY(@ids) AND ({sql.Where})";
            foreach (var id in await IdsAsync(connection, query, parameters, cancellationToken))
            {
                styles.TryAdd(id, rule.Id);
            }
        }

        return styles;
    }

    private static async Task<IEnumerable<Guid>> IdsAsync(NpgsqlConnection connection, string sql, DynamicParameters parameters, CancellationToken cancellationToken)
        => await connection.QueryAsync<Guid>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));

    private static async Task<Dictionary<Guid, TaskboardCardExtras>> ExtrasAsync(NpgsqlConnection connection, Guid[] ids, CancellationToken cancellationToken)
    {
        var rows = await connection.QueryAsync<TaskboardCardExtras>(new CommandDefinition(
            """
            SELECT w.id, w.priority::int AS priority, w.severity::int AS severity, w.created_at AS createdAt, cb.name AS createdByName,
                   w.changed_at AS changedAt, ub.name AS changedByName
            FROM work_item w
            LEFT JOIN app_user cb ON cb.id = w.created_by
            LEFT JOIN app_user ub ON ub.id = w.changed_by
            WHERE w.id = ANY(@ids)
            """, new { ids }, cancellationToken: cancellationToken));
        return rows.ToDictionary(row => row.Id);
    }
}
