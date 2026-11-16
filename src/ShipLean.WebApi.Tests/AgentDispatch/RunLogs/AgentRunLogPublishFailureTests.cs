using System.Net;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Events;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Usage;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Usage;

namespace ShipLean.WebApi.Tests.AgentDispatch.RunLogs;

// AttachmentStore publishes attachmentsChanged after the run log's transaction commits. When that publish throws, the log is already stored,
// so the link must stay spent (a second POST would archive the first log) and the upload is counted exactly once.
// Mutation: give the link back on any exception.
[TestFixture]
public class AgentRunLogPublishFailureTests : ApiFixture
{
    protected override Action<IServiceCollection>? ConfigureTestServices => services
        => services.AddSingleton<IProjectEvents>(sp => new ThrowingAttachmentEvents(ActivatorUtilities.CreateInstance<InMemoryProjectEvents>(sp)));

    [Test]
    public async Task Upload_WhenThePublishAfterTheCommitThrows_ShouldKeepTheLinkSpentAndCountTheUploadOnce()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentRunLogArrange.StartAsync(database, Api);

        using var first = await Client.SendAsync(AgentRunLogArrange.Upload(arranged.PathAndQuery, AgentRunLogArrange.Text("first")));
        var storedId = await arranged.RunLogIdAsync(database);
        using var second = await Client.SendAsync(AgentRunLogArrange.Upload(arranged.PathAndQuery, AgentRunLogArrange.Text("second")));

        first.StatusCode.ShouldBe(HttpStatusCode.InternalServerError, "the publish threw after the commit");
        storedId.ShouldNotBeNull("the first log was stored");
        second.StatusCode.ShouldBe(HttpStatusCode.NotFound, "the link was spent by the stored upload");
        (await arranged.RunLogIdAsync(database)).ShouldBe(storedId);
        (await database.ExecuteScalarAsync<bool>("SELECT archived_at IS NULL FROM attachment WHERE id = @id", new { id = storedId })).ShouldBeTrue();
        ((RecordingUsageEvents)Host.Services.GetRequiredService<IUsageEvents>()).Events
            .Count(e => e.Name == UsageEventNames.AgentSessionRunLogUploaded && e.ProjectId == arranged.ProjectId).ShouldBe(1);
    }
}
