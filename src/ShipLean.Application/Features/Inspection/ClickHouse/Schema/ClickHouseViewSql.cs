namespace ShipLean.Application.Features.Inspection.ClickHouse.Schema;

// The statements that build the ShipLean `inspection` database: the two target tables and the SELECTs behind the four
// materialized views (and, run once over old rows, the backfill). K3's LinkRules are translated here: Tagged and ShipleanFile
// from the item attribute (record over resource), FromToolCall from a ShipLean MCP tool call's JSON input.
public sealed class ClickHouseViewSql(ClickHouseNames names)
{
    // Link strength stored as a version so ReplacingMergeTree keeps the strongest: Tagged 3, ShipleanFile 2, FromToolCall 1.
    public const int TaggedVersion = 3;
    public const int FileVersion = 2;
    public const int ToolCallVersion = 1;

    // Added to a backfill's SELECT: only rows inside retention and not newer than the newest row there was when the views went in
    // ({to} is that row's time in nanoseconds since the epoch). Values are parameters.
    private const string Bound = "Timestamp >= now() - toIntervalDay({days:UInt32}) AND Timestamp <= fromUnixTimestamp64Nano({to:Int64}) AND ";

    private const string ItemColumns = "project, item_key, session_id, link_version, seen_at";

    private const string ShipleanTools = "(pick_up|report_task|update_progress|get_brief|get_work_item|hand_back|comment|record_decisions|log_retrospective)";

    public string CreateDatabase => $"CREATE DATABASE IF NOT EXISTS {names.Database}";

    public string CreateMigrations => $"CREATE TABLE IF NOT EXISTS {names.Migrations} (name String, applied_at DateTime64(6, 'UTC') DEFAULT now64(6)) ENGINE = MergeTree ORDER BY name";

    // Older databases made applied_at a DateTime; the newest marker is told apart by microseconds.
    public string ModifyMigrationsTime => $"ALTER TABLE {names.Migrations} MODIFY COLUMN applied_at DateTime64(6, 'UTC') DEFAULT now64(6)";

    // Counts and tokens add up across collector batches: every column aggregates (sum, min, max), so a run split over many
    // inserts merges to the same totals. Never ReplacingMergeTree here (it would keep one batch's totals).
    public string CreateRuns => $"""
        CREATE TABLE IF NOT EXISTS {names.Runs} (
            project String,
            session_id String,
            agent SimpleAggregateFunction(max, String),
            started_by SimpleAggregateFunction(max, String),
            models SimpleAggregateFunction(groupUniqArrayArray, Array(String)),
            started_at SimpleAggregateFunction(min, DateTime64(6, 'UTC')),
            ended_at SimpleAggregateFunction(max, DateTime64(6, 'UTC')),
            last_seen SimpleAggregateFunction(max, DateTime64(6, 'UTC')),
            span_count SimpleAggregateFunction(sum, UInt64),
            log_count SimpleAggregateFunction(sum, UInt64),
            error_count SimpleAggregateFunction(sum, UInt64),
            span_tokens_in SimpleAggregateFunction(sum, UInt64),
            span_tokens_in_n SimpleAggregateFunction(sum, UInt64),
            span_tokens_out SimpleAggregateFunction(sum, UInt64),
            span_tokens_out_n SimpleAggregateFunction(sum, UInt64),
            log_tokens_in SimpleAggregateFunction(sum, UInt64),
            log_tokens_in_n SimpleAggregateFunction(sum, UInt64),
            log_tokens_out SimpleAggregateFunction(sum, UInt64),
            log_tokens_out_n SimpleAggregateFunction(sum, UInt64)
        ) ENGINE = AggregatingMergeTree
        ORDER BY (project, session_id)
        {RunsTtl}
        """;

    // One row per (project, item, session); the strongest link wins through the version.
    public string CreateRunItems => $"""
        CREATE TABLE IF NOT EXISTS {names.RunItems} (
            project String,
            item_key String,
            session_id String,
            link_version UInt8,
            seen_at DateTime('UTC')
        ) ENGINE = ReplacingMergeTree(link_version)
        ORDER BY (project, item_key, session_id)
        {ItemsTtl}
        """;

    public string ModifyRunsTtl => $"ALTER TABLE {names.Runs} MODIFY {RunsTtl} SETTINGS materialize_ttl_after_modify = 0";

    public string ModifyItemsTtl => $"ALTER TABLE {names.RunItems} MODIFY {ItemsTtl} SETTINGS materialize_ttl_after_modify = 0";

    public IReadOnlyList<ViewDefinition> Views { get; } =
    [
        new(names.View("runs_from_traces"), names.Runs, RunsFromTraces(names, ""), RunsFromTraces(names, Bound), "project, session_id, agent, started_by, models, started_at, ended_at, last_seen, span_count, error_count, span_tokens_in, span_tokens_in_n, span_tokens_out, span_tokens_out_n"),
        new(names.View("runs_from_logs"), names.Runs, RunsFromLogs(names, ""), RunsFromLogs(names, Bound), "project, session_id, agent, started_by, models, started_at, ended_at, last_seen, log_count, error_count, log_tokens_in, log_tokens_in_n, log_tokens_out, log_tokens_out_n"),
        new(names.View("run_items_from_traces"), names.RunItems, ItemsFromTraces(names, ""), ItemsFromTraces(names, Bound), ItemColumns),
        new(names.View("run_items_from_logs"), names.RunItems, ItemsFromLogs(names, ""), ItemsFromLogs(names, Bound), ItemColumns),
    ];

    // Changes whenever any view's SELECT or the field names change, so the migrator rebuilds the views and the data in them.
    public string Fingerprint
    {
        get
        {
            var text = string.Join('\n', Views.Select(v => v.Name + v.Select));
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)))[..16].ToLowerInvariant();
        }
    }

    // The columns the views and the reader read, for the layout check. The log's EventName is not among them: older exporter tables lack it,
    // so the reader takes event.name from the attributes alone.
    public static IReadOnlyList<string> TraceColumns { get; } = ["Timestamp", "SpanName", "ServiceName", "ResourceAttributes", "SpanAttributes", "Duration", "StatusCode",
        "TraceId", "SpanId", "ParentSpanId", "SpanKind", "StatusMessage", "Events.Timestamp", "Events.Name", "Events.Attributes"];

    public static IReadOnlyList<string> LogColumns { get; } = ["Timestamp", "ServiceName", "SeverityText", "SeverityNumber", "ResourceAttributes", "LogAttributes", "Body", "TraceId", "SpanId"];

    // Both tables outlive the exporter's own rows (its retention) by this many days, so a run whose spans are gone stays listed for its item
    // and the run page can say it expired instead of not found. The backfill still reads only rows inside retention.
    public const int ExpiredKeepDays = 30;

    private string RunsTtl => $"TTL toDateTime(last_seen) + INTERVAL {names.RetentionDays + ExpiredKeepDays} DAY";

    private string ItemsTtl => $"TTL seen_at + INTERVAL {names.RetentionDays + ExpiredKeepDays} DAY";

    private static string RunsFromTraces(ClickHouseNames n, string bound) => $"""
        WITH mapUpdate(ResourceAttributes, SpanAttributes) AS a
        SELECT
            a[{n.ProjectField}] AS project,
            a[{n.SessionField}] AS session_id,
            ServiceName AS agent,
            a['user.email'] AS started_by,
            if(a['model'] = '', CAST([], 'Array(String)'), [a['model']]) AS models,
            toDateTime64(Timestamp, 6, 'UTC') AS started_at,
            toDateTime64(addNanoseconds(Timestamp, Duration), 6, 'UTC') AS ended_at,
            toDateTime64(Timestamp, 6, 'UTC') AS last_seen,
            toUInt64(1) AS span_count,
            toUInt64(StatusCode = 'Error') AS error_count,
            {Tokens("span", "input_tokens", "in")},
            {Tokens("span", "output_tokens", "out")}
        FROM {n.Traces}
        WHERE {bound}project != '' AND session_id != ''
        """;

    private static string RunsFromLogs(ClickHouseNames n, string bound) => $"""
        WITH mapUpdate(ResourceAttributes, LogAttributes) AS a
        SELECT
            a[{n.ProjectField}] AS project,
            a[{n.SessionField}] AS session_id,
            ServiceName AS agent,
            a['user.email'] AS started_by,
            if(a['model'] = '', CAST([], 'Array(String)'), [a['model']]) AS models,
            toDateTime64(Timestamp, 6, 'UTC') AS started_at,
            toDateTime64(Timestamp, 6, 'UTC') AS ended_at,
            toDateTime64(Timestamp, 6, 'UTC') AS last_seen,
            toUInt64(1) AS log_count,
            toUInt64(SeverityText = 'ERROR' OR (SeverityText = '' AND SeverityNumber BETWEEN 17 AND 20)) AS error_count,
            {Tokens("log", "input_tokens", "in")},
            {Tokens("log", "output_tokens", "out")}
        FROM {n.Logs}
        WHERE {bound}project != '' AND session_id != ''
        """;

    private static string ItemsFromTraces(ClickHouseNames n, string bound) => $"""
        WITH mapUpdate(ResourceAttributes, SpanAttributes) AS a,
            if(mapContains(a, 'tool_name'), a['tool_name'], SpanName) AS tool
        SELECT
            a[{n.ProjectField}] AS project,
            candidate.1 AS item_key,
            a[{n.SessionField}] AS session_id,
            candidate.2 AS link_version,
            toDateTime(Timestamp, 'UTC') AS seen_at
        FROM {n.Traces}
        ARRAY JOIN {Candidates(n)} AS candidate
        WHERE {bound}project != '' AND session_id != '' AND item_key != ''
        """;

    private static string ItemsFromLogs(ClickHouseNames n, string bound) => $"""
        WITH mapUpdate(ResourceAttributes, LogAttributes) AS a,
            a['tool_name'] AS tool
        SELECT
            a[{n.ProjectField}] AS project,
            candidate.1 AS item_key,
            a[{n.SessionField}] AS session_id,
            candidate.2 AS link_version,
            toDateTime(Timestamp, 'UTC') AS seen_at
        FROM {n.Logs}
        ARRAY JOIN {Candidates(n)} AS candidate
        WHERE {bound}project != '' AND session_id != '' AND item_key != ''
        """;

    // The two items a record can name: the tagged one (Tagged, or ShipleanFile when the Cursor hook said so) and the one a ShipLean
    // MCP tool call's input names. `a` is the record's attributes over its resource's and `tool` its tool name. The server is the
    // mcp_server_name attribute, else, only when the tool is mcp_tool (Claude Code's tool_result log), the one inside tool_parameters; empty is absent; the input is the first of
    // tool_input, tool_parameters, input that names an item.
    private static string Candidates(ClickHouseNames n) => $$"""
        [
            (a[{{n.ItemField}}], if(a['shiplean.link_source'] = 'file', {{FileVersion}}, {{TaggedVersion}})),
            (
                if(coalesce(nullIf(a['mcp_server_name'], ''), if(tool = 'mcp_tool', nullIf(JSONExtractString(a['tool_parameters'], 'mcp_server_name'), ''), NULL)) = 'shiplean' OR match(tool, '^(mcp__)?shiplean__{{ShipleanTools}}$'),
                    coalesce(nullIf({{ItemOf("a['tool_input']")}}, ''), nullIf({{ItemOf("a['tool_parameters']")}}, ''), nullIf({{ItemOf("a['input']")}}, ''), ''),
                    ''),
                {{ToolCallVersion}})
        ]
        """;

    // The item, key or task string at the top of a JSON input; empty when there is none.
    private static string ItemOf(string input) => $"""
        multiIf(
                        JSONType({input}, 'item') = 'String', JSONExtractString({input}, 'item'),
                        JSONType({input}, 'key') = 'String', JSONExtractString({input}, 'key'),
                        JSONType({input}, 'task') = 'String', JSONExtractString({input}, 'task'),
                        '')
        """;

    private static string Tokens(string source, string attribute, string direction)
        => $"ifNull(toUInt64OrNull(a['{attribute}']), 0) AS {source}_tokens_{direction}, toUInt64(isNotNull(toUInt64OrNull(a['{attribute}']))) AS {source}_tokens_{direction}_n";
}

// A materialized view: its quoted name, the quoted table it fills, and its SELECT.
public sealed record ViewDefinition(string Name, string Target, string Select, string BackfillSelect, string Columns)
{
    public string Create => $"CREATE MATERIALIZED VIEW IF NOT EXISTS {Name} TO {Target} AS {Select}";

    public string Drop => $"DROP VIEW IF EXISTS {Name}";

    // The same SELECT, run once over old rows with {days} and {to} as parameters.
    public string Backfill => $"INSERT INTO {Target} ({Columns}) {BackfillSelect}";
}
