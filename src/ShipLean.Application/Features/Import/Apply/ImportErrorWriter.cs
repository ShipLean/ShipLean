using Dapper;
using Npgsql;
using ShipLean.Contracts.Persisted;

namespace ShipLean.Application.Features.Import.Apply;

// Records a row the import could not write, pending until it is retried or skipped.
public sealed class ImportErrorWriter(NpgsqlDataSource dataSource)
{
    public async Task<Guid> WriteAsync(Guid jobId, ImportStep step, string entity, string sourceKey, string message, CancellationToken cancellationToken)
    {
        var id = Guid.CreateVersion7();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO import_error (id, import_job_id, step, entity, source_key, message) VALUES (@id, @jobId, @step, @entity, @sourceKey, @message)",
            new { id, jobId, step = StepText(step), entity, sourceKey, message }, cancellationToken: cancellationToken));
        return id;
    }

    private static string StepText(ImportStep step) => System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(step.ToString());
}
