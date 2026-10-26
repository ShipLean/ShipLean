using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Lanes.Import.Stepper;
using ShipLean.AcceptanceTests.Pages.Import;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Jira;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Import.Mapping;

// A finished import of the stub's PLATFORM into MOB with the mapping it was run with, then the stepper walked to the
// step after the target. The mapping is read from the app (what Jira offers now) so only the rows a test removes are new.
internal static class ImportRerunArrangement
{
    public static async Task<Guid> ArrangeFinishedJobAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO import_job (id, connection_id, jira_project_key, project_key, project_id, status, progress, started_at, finished_at, created_by)
            SELECT @id, job.connection_id, 'PLATFORM', job.project_key, job.project_id, 'done', job.progress,
                   now() - interval '1 hour', now() - interval '50 minutes', job.created_by
            FROM import_job job WHERE job.id = @seed
            """,
            connection);
        var id = Guid.NewGuid();
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("seed", SeedIds.ImportJobs.MobImport);
        await command.ExecuteNonQueryAsync();
        return id;
    }

    public static void PinJira(IServiceCollection services, JiraStubHandler stub) => ImportStubJira.Pin(services, stub);

    // Saves the stub's token on MOB's connection, reads the mapping Jira offers for PLATFORM, drops the rows `forget` names and
    // stores the rest as the job's saved mapping.
    public static async Task SaveMappingAsync(IPage page, ImportStepperPage stepper, string connectionString, Guid jobId, Action<JsonObject> forget)
    {
        await stepper.OpenAsync("MOB");
        await page.GetByTestId(ImportTestIds.Replace).ClickAsync();
        await stepper.SaveConnectionAsync(JiraStubHandler.CloudEmail, JiraStubHandler.CloudToken);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var find = new NpgsqlCommand(
            "SELECT c.id FROM jira_connection c JOIN project p ON p.id = c.project_id WHERE p.key = 'MOB' AND c.archived_at IS NULL", connection);
        var connectionId = (Guid)(await find.ExecuteScalarAsync())!;

        var body = await page.EvaluateAsync<string>(
            """
            async (request) => {
              const response = await fetch('/api/import/mapping', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(request) });
              return await response.text();
            }
            """,
            new { connectionId, jiraProjectKey = "PLATFORM", previousJobId = (Guid?)null });
        var mapping = JsonNode.Parse(body)!.AsObject();
        forget(mapping);

        await Insert(connection, jobId, "issueType", "*", mapping["issueTypes"]!);
        await Insert(connection, jobId, "status", "*", mapping["statuses"]!);
        await Insert(connection, jobId, "user", "*", mapping["users"]!);
        await Insert(connection, jobId, "field", "fields", mapping["fields"]!);
    }

    private static async Task Insert(NpgsqlConnection connection, Guid jobId, string kind, string source, JsonNode json)
    {
        await using var command = new NpgsqlCommand(
            "INSERT INTO import_mapping (id, import_job_id, kind, source_value, target) VALUES (gen_random_uuid(), @job, @kind, @source, @json::jsonb)", connection);
        command.Parameters.AddWithValue("job", jobId);
        command.Parameters.AddWithValue("kind", kind);
        command.Parameters.AddWithValue("source", source);
        command.Parameters.AddWithValue("json", json.ToJsonString());
        await command.ExecuteNonQueryAsync();
    }

    // Step 1 (token saved, connection tested) to step 3 (target) for PLATFORM; the caller presses Next.
    public static async Task WalkToTargetAsync(IPage page, ImportStepperPage stepper)
    {
        await stepper.OpenAsync("MOB");
        await stepper.TestConnectionAsync();
        await Assertions.Expect(stepper.TestResult).ToHaveAttributeAsync("data-ok", "true");
        await stepper.Next.ClickAsync();
        await stepper.PickAsync("PLATFORM");
        await stepper.Next.ClickAsync();
        await Assertions.Expect(page.GetByTestId(ImportTestIds.TargetRerun)).ToBeVisibleAsync();
    }
}
