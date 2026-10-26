using System.Net;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Users;
using ShipLean.Contracts.Users.Admin;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Tests.Auth;

namespace ShipLean.WebApi.Tests.Users.Admin;

// Adding a user issues the one-time password last, so a request that dies earlier leaves a user with no password,
// which Set up sign-in recovers.
[TestFixture]
public class UsersAdminAddRecoveryTests : ApiFixture
{
    private sealed class FailingEvents : IProjectEvents
    {
        public Task PublishAsync(ProjectEvent projectEvent) => throw new InvalidOperationException("The event bus is down.");

        public IDisposable Subscribe(Guid? projectId, Func<ProjectEvent, Task> handler) => new Nothing();

        private sealed class Nothing : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.AddSingleton<IProjectEvents, FailingEvents>();
    };

    [Test]
    public async Task Add_WhenItFailsBeforeThePasswordIsIssued_ShouldLeaveARecoverableUserWithNoPassword()
    {
        const string email = "half.added@example.test";

        using var failed = await Api.PostJsonAsync("api/admin/users", new { email, name = "Half Added", projectIds = new[] { SeedIds.Projects.Mob } });

        failed.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        await using var connection = await OpenDatabaseAsync();
        var row = await connection.QuerySingleAsync<(Guid Id, string? Hash)>("SELECT id, password_hash FROM app_user WHERE lower(email) = @email", new { email });
        row.Hash.ShouldBeNull();
        using var retry = await Api.PostJsonAsync($"api/admin/users/{row.Id}/one-time-password");
        var issued = JsonSerializer.Deserialize<OneTimePasswordDto>(await retry.Content.ReadAsStringAsync(), AuthClient.Json)!;
        using var client = AuthClient.Create(Host);
        (await client.LoginAsync(email, issued.Password)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
