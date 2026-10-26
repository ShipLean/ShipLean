using Microsoft.Extensions.DependencyInjection;
using ShipLean.AcceptanceTests.Harness;
using Npgsql;
using ShipLean.AcceptanceTests.Pages.Import;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Jira;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Import.Stepper;

// MOB has a seeded Jira connection (no token) and a finished import of MOBILE; the test adds a finished import of the stub's
// PLATFORM and checks step 2 says where it went.
[TestFixture]
public class ImportedBeforeTests : SeededAcceptanceTest
{
    private readonly JiraStubHandler _stub = new();

    protected override Action<IServiceCollection>? ConfigureTestServices => services => ImportStubJira.Pin(services, _stub);

    [Test]
    public async Task Import_WhenProjectImportedBefore_ShouldShowImportedAs()
    {
        await ArrangeFinishedImportAsync();
        var stepper = new ImportStepperPage(Page);
        await stepper.OpenAsync("MOB");
        await Expect(Page.GetByTestId(ImportTestIds.TokenSaved)).ToBeVisibleAsync();
        await Page.GetByTestId(ImportTestIds.Replace).ClickAsync();
        await stepper.SaveConnectionAsync(JiraStubHandler.CloudEmail, JiraStubHandler.CloudToken);
        await stepper.TestConnectionAsync();
        await Expect(stepper.TestResult).ToHaveAttributeAsync("data-ok", "true");
        await stepper.Next.ClickAsync();

        await Expect(stepper.ProjectRows.Filter(new LocatorFilterOptions { HasText = "PLATFORM" })).ToContainTextAsync("Imported as MOB on");
        await Expect(stepper.ImportedAs).ToHaveCountAsync(1);

        await stepper.PickAsync("PLATFORM");
        await stepper.Next.ClickAsync();
        await Expect(Page.GetByTestId(ImportTestIds.TargetRerun)).ToBeVisibleAsync();
        await Expect(stepper.Next).ToBeEnabledAsync();
    }

    private async Task ArrangeFinishedImportAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO import_job (id, connection_id, jira_project_key, project_key, project_id, status, progress, started_at, finished_at, created_by)
            SELECT gen_random_uuid(), job.connection_id, 'PLATFORM', job.project_key, job.project_id, 'done', job.progress,
                   now() - interval '1 hour', now() - interval '50 minutes', job.created_by
            FROM import_job job WHERE job.id = @seed
            """,
            connection);
        command.Parameters.AddWithValue("seed", SeedIds.ImportJobs.MobImport);
        await command.ExecuteNonQueryAsync();
    }
}
