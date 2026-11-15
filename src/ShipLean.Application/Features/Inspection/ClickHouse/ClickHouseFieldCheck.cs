using ShipLean.Application.Features.Inspection.ClickHouse.Schema;
using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.ClickHouse;

// Each K1 field: is its attribute present on any trace or log record the exporter wrote in the search window?
public sealed class ClickHouseFieldCheck
{
    private readonly ClickHouseDb db;
    private readonly ClickHouseNames names;
    private readonly ClickHouseSchemaMigrator migrator;
    private readonly IReadOnlyList<(string Name, string Attribute)> fields;
    private readonly int windowDays;

    public ClickHouseFieldCheck(InspectionOptions options, ClickHouseSchemaMigrator migrator)
    {
        db = ClickHouseDb.From(options);
        this.migrator = migrator;
        names = migrator.Names;
        windowDays = options.MaxSearchDays;
        var f = options.Fields;
        fields =
        [
            ("item", f.Item), ("project", f.Project), ("session", f.Session), ("prompt", f.Prompt),
            ("repo", f.Repo), ("branch", f.Branch), ("pr", f.Pr),
        ];
    }

    public async Task<IReadOnlyList<FieldCheck>> CheckAsync(CancellationToken ct)
    {
        try
        {
            if (!await migrator.EnsureAsync(ct))
            {
                return [.. fields.Select(f => new FieldCheck(f.Name, f.Attribute, false))];
            }
            var parameters = new Dictionary<string, object> { ["days"] = windowDays };
            var selects = new List<string>();
            for (var i = 0; i < fields.Count; i++)
            {
                parameters[$"f{i}"] = fields[i].Attribute;
                selects.Add($$"""
                    SELECT {{i}} AS field FROM {{names.Traces}}
                    WHERE Timestamp >= now() - toIntervalDay({days:UInt32}) AND (mapContains(ResourceAttributes, {f{{i}}:String}) OR mapContains(SpanAttributes, {f{{i}}:String})) LIMIT 1
                    """);
                selects.Add($$"""
                    SELECT {{i}} AS field FROM {{names.Logs}}
                    WHERE Timestamp >= now() - toIntervalDay({days:UInt32}) AND (mapContains(ResourceAttributes, {f{{i}}:String}) OR mapContains(LogAttributes, {f{{i}}:String})) LIMIT 1
                    """);
            }
            var rows = await db.QueryAsync($"SELECT DISTINCT field FROM ({string.Join(" UNION ALL ", selects)})", parameters, ct);
            var found = rows.Select(row => Convert.ToInt32(row[0], System.Globalization.CultureInfo.InvariantCulture)).ToHashSet();
            return [.. fields.Select((f, i) => new FieldCheck(f.Name, f.Attribute, found.Contains(i)))];
        }
        catch (Exception ex) when (ClickHouseDb.IsStoreDown(ex, ct) || ex is ClickHouseLayoutException)
        {
            return [.. fields.Select(f => new FieldCheck(f.Name, f.Attribute, false))];
        }
    }
}
