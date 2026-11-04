using System.Net;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Fakes.Blobs;
using ShipLean.Contracts.Blobs;
using ShipLean.TestSupport;
using ShipLean.TestSupport.AccessSweep;
using ShipLean.LocalEnvironment.Blobs;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Instance.AgentConnections;
using ShipLean.WebApi.Tests.Retros.Api;

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

    // The demo logo GET answers 404 for a project with no logo, and the allowed leg refuses a 404: SHIP gets one (B1). The DELETE row comes after the GET row.
    [OneTimeSetUp]
    public async Task InsertDemoLogoSweepRow()
    {
        var blobs = Host.Services.GetRequiredService<IBlobStore>();
        const string key = "demo-logos/sweep/logo";
        await blobs.PutAsync(key, new MemoryStream("<svg xmlns=\"http://www.w3.org/2000/svg\"/>"u8.ToArray()), "image/svg+xml", CancellationToken.None);
        await using var connection = await OpenDatabaseAsync();
        await Dapper.SqlMapper.ExecuteAsync(connection,
            "INSERT INTO project_demo_settings (project_id, logo_blob, logo_version) VALUES (@project, @key, 1) ON CONFLICT (project_id) DO UPDATE SET logo_blob = @key",
            new { project = SeedIds.Projects.Ship, key });
    }

    // The seed has no agent connection, and the instance rows for them need one to find (the 404 check): see AgentConnectionSweepIds.
    [OneTimeSetUp]
    public async Task InsertSweepAgentConnections()
    {
        await using var connection = await OpenDatabaseAsync();
        foreach (var id in new[] { AgentConnectionSweepIds.Live, AgentConnectionSweepIds.ToArchive })
        {
            await Dapper.SqlMapper.ExecuteAsync(connection,
                """
                INSERT INTO agent_connection (id, type, name, receiver_url, signing_secret_protected, signing_secret_last4, callback_token_hash, callback_token_last4, created_at, updated_at)
                VALUES (@id, 'linear-compatible', @name, 'https://relay.example.com/hook', 'x', 'xxxx', @hash, 'xxxx', now(), now())
                """,
                new { id, name = $"Sweep {id}", hash = id.ToByteArray() });
        }
    }

    // The point- and theme-keyed retro routes need real rows: a made-up id is a 404 whether or not access is checked.
    [OneTimeSetUp]
    public async Task InsertRetroSweepRows()
    {
        await using var connection = await OpenDatabaseAsync();
        await RetroSweepData.InsertAsync(connection);
    }

    [TestCaseSource(typeof(SweepCalls), nameof(SweepCalls.AllRows))]
    public async Task AccessSweep_WhenAllowed_ShouldPassAccess(AccessSweepRow row)
    {
        using var response = await SweepCalls.SendAsync(Api.ActingAs(TestUser.Joel), row);

        response.StatusCode.ShouldNotBeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
        ((int)response.StatusCode).ShouldBeLessThan(500);
    }
}
