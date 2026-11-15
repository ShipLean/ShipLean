using ShipLean.Application.Features.Inspection.Query;

namespace ShipLean.Application.Features.Inspection.ClickHouse.Schema;

// Every name the ClickHouse parts put into a statement, built once from the instance settings. Database and table names are
// validated and quoted by QueryText.Identifier; attribute names (which appear inside the materialized views' definitions, where
// a query parameter cannot go) are quoted as string literals by QueryText.ClickHouseString. Values always go as query parameters.
public sealed class ClickHouseNames
{
    public const string InspectionDatabase = "inspection";

    public ClickHouseNames(InspectionOptions options, string inspectionDatabase = InspectionDatabase)
    {
        Traces = $"{QueryText.Identifier(options.ClickHouseDatabase)}.{QueryText.Identifier(options.ClickHouseTracesTable)}";
        Logs = $"{QueryText.Identifier(options.ClickHouseDatabase)}.{QueryText.Identifier(options.ClickHouseLogsTable)}";
        ExporterDatabase = options.ClickHouseDatabase;
        TracesTable = options.ClickHouseTracesTable;
        LogsTable = options.ClickHouseLogsTable;
        Database = QueryText.Identifier(inspectionDatabase);
        DatabaseName = inspectionDatabase;
        Runs = $"{Database}.`runs`";
        RunItems = $"{Database}.`run_items`";
        Migrations = $"{Database}.`migrations`";
        RetentionDays = options.RetentionDays > 0
            ? options.RetentionDays
            : throw new ArgumentOutOfRangeException(nameof(options), "The ClickHouse retention days must be at least 1.");
        Fields = options.Fields;
        ItemField = QueryText.ClickHouseString(options.Fields.Item);
        ProjectField = QueryText.ClickHouseString(options.Fields.Project);
        SessionField = QueryText.ClickHouseString(options.Fields.Session);
    }

    public string Traces { get; }

    public string Logs { get; }

    public string ExporterDatabase { get; }

    public string TracesTable { get; }

    public string LogsTable { get; }

    // The ShipLean-owned database (quoted), its tables (quoted) and its name (plain, for system table lookups).
    public string Database { get; }

    public string DatabaseName { get; }

    public string Runs { get; }

    public string RunItems { get; }

    public string Migrations { get; }

    public int RetentionDays { get; }

    public InspectionFieldNames Fields { get; }

    // The configured attribute names as ClickHouse string literals.
    public string ItemField { get; }

    public string ProjectField { get; }

    public string SessionField { get; }

    // A view's quoted name in the ShipLean database.
    public string View(string name) => $"{Database}.{QueryText.Identifier(name)}";
}
