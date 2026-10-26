using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Identity;

// An import job that has not created its project yet belongs to no project, so nobody reaches it through IProjectAccess.
[TestFixture]
public class ImportJobAccessTests : ApiFixture
{
    [Test]
    public async Task ProjectAccess_WhenImportJobHasNoProject_ShouldThrowNotFound()
    {
        await using (var connection = await OpenDatabaseAsync())
        {
            await connection.ExecuteAsync("UPDATE import_job SET project_id = NULL WHERE id = @id", new { id = SeedIds.ImportJobs.MobImport });
        }

        var error = await Should.ThrowAsync<ApiException>(() => Host.Services.GetRequiredService<IProjectAccess>()
            .RequireAsync(ProjectItem.ImportJob, SeedIds.ImportJobs.MobImport, ProjectRole.Member));

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }
}
