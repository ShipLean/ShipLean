using System.Net;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Fakes.Blobs;
using ShipLean.Contracts.Blobs;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport.AccessSweep;
using ShipLean.TestSupport.Blobs;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Access;

// Every declared row, called as the callers that must be refused (the allowed leg is AllowedSweepTests, on a database of its own). Kai is in no project, Mei is a SHIP member, Joel is SHIP's admin and
// an instance admin. A row's call goes to the real app; the access rule must answer before anything else about the call.
[TestFixture]
public class AccessSweepTests : ApiFixture
{
    // The seeded attachments' bytes are in an in-memory blob store, as in the allowed leg: without them a download answers
    // 404 whether or not access is checked, and the refused leg would prove nothing for that route.
    protected override Action<IServiceCollection>? ConfigureTestServices => services => services.AddSingleton<IBlobStore, InMemoryBlobStore>();

    [OneTimeSetUp]
    public Task PutSeedAttachmentBlobs() => SeedAttachmentBlobs.PutAllAsync(Host.Services.GetRequiredService<IBlobStore>());

    [TestCaseSource(typeof(SweepCalls), nameof(SweepCalls.ProjectRows))]
    public async Task AccessSweep_WhenNotMember_ShouldBeNotFound(AccessSweepRow row)
    {
        using var response = await SweepCalls.SendAsync(Api.ActingAs(TestUser.Kai), row);

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [TestCaseSource(typeof(SweepCalls), nameof(SweepCalls.AdminOnlyProjectRows))]
    public async Task AccessSweep_WhenBelowMinimum_ShouldBeForbidden(AccessSweepRow row)
    {
        using var response = await SweepCalls.SendAsync(Api.ActingAs(TestUser.Mei), row);

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Forbidden)).Code.ShouldBe(ApiErrorCodes.Forbidden);
    }

    [Test]
    public void AccessSweep_WhenRowsListed_ShouldEachMatchARoute()
    {
        var stale = ApiActions.Unmatched(AccessSweepRows.All(), ApiActions.Of(Host.Services));

        stale.ShouldBeEmpty();
    }
}
