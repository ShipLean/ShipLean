using System.Net;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using Dapper;

namespace ShipLean.WebApi.Tests.Auth;

// The InstanceAdmin policy reads app_user on every request, so demoting an admin takes effect at once.
[TestFixture]
public class InstanceAdminTests : ApiFixture
{
    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
        services.AddControllers().AddApplicationPart(typeof(AuthTestController).Assembly);

    [Test]
    [Order(1)]
    public async Task InstanceAdmin_WhenAdmin_ShouldPass()
    {
        using var response = await Api.GetAsync("/api/test-auth/admin");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    [Order(2)]
    public async Task InstanceAdmin_WhenNotAdmin_ShouldBeForbidden()
    {
        using var response = await Api.ActingAs(TestUser.Priya).GetAsync("/api/test-auth/admin");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Forbidden)).Code.ShouldBe(ApiErrorCodes.Forbidden);
    }

    [Test]
    [Order(3)]
    public async Task InstanceAdmin_WhenAnonymous_ShouldBe401()
    {
        using var response = await Api.ActingAs(TestUser.Anonymous).GetAsync("/api/test-auth/admin");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    [Order(4)]
    public async Task InstanceAdmin_WhenDemotedWithTheSessionStillOpen_ShouldBeForbiddenAtOnce()
    {
        using var client = await AuthClient.SignedInAsync(Host, "tom@shiplean.local", ShipLean.TestSupport.SeedIds.Passwords.Tom);
        (await client.GetAsync("/api/test-auth/admin")).StatusCode.ShouldBe(HttpStatusCode.OK);
        await using (var connection = await OpenDatabaseAsync())
        {
            await connection.ExecuteAsync("UPDATE app_user SET is_admin = false WHERE id = @id", new { id = TestUser.Tom.Id });
        }

        using var response = await client.GetAsync("/api/test-auth/admin");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
