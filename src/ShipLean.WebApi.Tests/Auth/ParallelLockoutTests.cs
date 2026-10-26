using System.Net;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ShipLean.Application.Identity;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Auth;

// Parallel wrong passwords are counted one after another (the user row is locked from the read to the commit), so the
// lock lands at the tenth. Its own class: the login limiter is per client address and the host is shared.
[TestFixture]
public class ParallelLockoutTests : ApiFixture
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 24, 9, 0, 0, TimeSpan.Zero));

    protected override TestUser SignedInAs => TestUser.Anonymous;

    protected override Action<IServiceCollection>? ConfigureTestServices => services => services.AddSingleton<TimeProvider>(_clock);

    [Test]
    public async Task Login_WhenFifteenWrongPasswordsInParallel_ShouldStopAtTen()
    {
        var builder = new AppUserBuilder();
        await using (var connection = await OpenDatabaseAsync())
        {
            await builder.InsertAsync(connection);
        }
        using var client = AuthClient.Create(Host);

        var responses = await Task.WhenAll(Enumerable.Range(0, 15).Select(_ => client.LoginAsync(builder.Email, "wrong-password-1")));

        responses.ShouldAllBe(response => response.StatusCode == HttpStatusCode.Unauthorized);
        await using var check = await OpenDatabaseAsync();
        (await check.ExecuteScalarAsync<int>("SELECT failed_sign_in_count FROM app_user WHERE lower(email) = @email", new { email = builder.Email })).ShouldBe(0);
        (await check.ExecuteScalarAsync<DateTimeOffset?>("SELECT locked_until FROM app_user WHERE lower(email) = @email", new { email = builder.Email })).ShouldBe(_clock.GetUtcNow() + TimeSpan.FromMinutes(15));
    }
}
