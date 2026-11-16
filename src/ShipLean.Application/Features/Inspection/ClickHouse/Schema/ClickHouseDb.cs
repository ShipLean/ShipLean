using System.Data.Common;
using ClickHouse.Client.ADO;
using ClickHouse.Client.ADO.Parameters;
using ClickHouse.Client.Utility;

namespace ShipLean.Application.Features.Inspection.ClickHouse.Schema;

// ClickHouse over the HTTP interface (the connection string says Protocol=http). Every value goes as a typed parameter named in
// the statement as {name:Type}; the statement text itself only ever holds QueryText-quoted names and the fixed view SQL.
public sealed class ClickHouseDb(string connectionString)
{
    public static ClickHouseDb From(InspectionOptions options)
        => new(options.ClickHouseConnection ?? throw new InvalidOperationException("SHIPLEAN_INSPECTION_CLICKHOUSE_CONNECTION is not set."));

    // The store did not answer: refused, a server error, or an HTTP timeout (which surfaces as TaskCanceledException while the
    // caller's own token is not cancelled).
    public static bool IsStoreDown(Exception ex, CancellationToken ct)
        => ex is HttpRequestException or TimeoutException
            || (ex is global::ClickHouse.Client.ClickHouseServerException server && !IsSchemaError(server))
            || (ex is TaskCanceledException && !ct.IsCancellationRequested);

    // The server rejected the statement for a column, table or database it does not have: a layout problem, not a store that is down.
    public static bool IsSchemaError(Exception ex)
        => ex is global::ClickHouse.Client.ClickHouseServerException { ErrorCode: 16 or 47 or 60 or 81 };

    public async Task ExecuteAsync(string statement, IReadOnlyDictionary<string, object>? parameters, CancellationToken ct)
    {
        await using var connection = new ClickHouseConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = Command(connection, statement, parameters);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<object?[]>> QueryAsync(string statement, IReadOnlyDictionary<string, object>? parameters, CancellationToken ct)
    {
        await using var connection = new ClickHouseConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = Command(connection, statement, parameters);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<object?[]>();
        while (await reader.ReadAsync(ct))
        {
            var row = new object?[reader.FieldCount];
            for (var i = 0; i < row.Length; i++)
            {
                row[i] = await reader.IsDBNullAsync(i, ct) ? null : reader.GetValue(i);
            }
            rows.Add(row);
        }
        return rows;
    }

    public async Task<T> ScalarAsync<T>(string statement, IReadOnlyDictionary<string, object>? parameters, CancellationToken ct)
    {
        var rows = await QueryAsync(statement, parameters, ct);
        return (T)Convert.ChangeType(rows[0][0]!, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static DbCommand Command(ClickHouseConnection connection, string statement, IReadOnlyDictionary<string, object>? parameters)
    {
        var command = connection.CreateCommand();
        command.CommandText = statement;
        foreach (var (name, value) in parameters ?? new Dictionary<string, object>())
        {
            if (value is Typed typed)
            {
                command.AddParameter(name, typed.Type, typed.Value);
            }
            else
            {
                command.AddParameter(name, value);
            }
        }
        return command;
    }

    // A parameter whose ClickHouse type is stated (microsecond times), not inferred from the .NET value.
    public sealed record Typed(string Type, object Value)
    {
        public static Typed Time(DateTime utc) => new("DateTime64(6, 'UTC')", DateTime.SpecifyKind(utc, DateTimeKind.Utc));

        public static Typed Time(DateTimeOffset at) => Time(at.UtcDateTime);
    }
}
