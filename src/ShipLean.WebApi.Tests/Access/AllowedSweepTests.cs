using System.Net;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Fakes.Blobs;
using ShipLean.Contracts.Blobs;
using ShipLean.TestSupport.AccessSweep;
using ShipLean.TestSupport.Blobs;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Access;

// The allowed leg is its own fixture, so its own database: a row that deletes or archives its entity would otherwise
// remove what the refused legs read, and their 404 would pass without any access check. A destructive row points at a
// seed entity that no other row uses (docs/auth.md).
[TestFixture]
public class AllowedSweepTests : ApiFixture
{
    // Seeded attachments are rows only; their bytes go into an in-memory blob store so a download row can succeed.
    protected override Action<IServiceCollection>? ConfigureTestServices => services => services.AddSingleton<IBlobStore, InMemoryBlobStore>();

    [OneTimeSetUp]
    public Task PutSeedAttachmentBlobs() => SeedAttachmentBlobs.PutAllAsync(Host.Services.GetRequiredService<IBlobStore>());

    [TestCaseSource(typeof(SweepCalls), nameof(SweepCalls.AllRows))]
    public async Task AccessSweep_WhenAllowed_ShouldPassAccess(AccessSweepRow row)
    {
        using var response = await SweepCalls.SendAsync(Api.ActingAs(TestUser.Joel), row);

        response.StatusCode.ShouldNotBeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
        ((int)response.StatusCode).ShouldBeLessThan(500);
    }
}
