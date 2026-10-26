using System.Net;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ShipLean.Application.Identity;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Auth;

// Ten wrong passwords in a row lock the account for 15 minutes, counted on the app's clock (pinned here).
[TestFixture]
public class LockoutTests : ApiFixture
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 24, 9, 0, 0, TimeSpan.Zero));

    protected override TestUser SignedInAs => TestUser.Anonymous;

    protected override Action<IServiceCollection>? ConfigureTestServices => services => services.AddSingleton<TimeProvider>(_clock);

    [Test]
    public async Task Login_WhenTenFailures_ShouldLockFor15Minutes()
    {
        var builder = new AppUserBuilder();
        await using (var connection = await OpenDatabaseAsync())
        {
            await builder.InsertAsync(connection);
        }
        using var client = AuthClient.Create(Host);
        for (var i = 0; i < SignInService.LockoutFailures; i++)
        {
            (await client.LoginAsync(builder.Email, "wrong-password-1")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }
        await using (var connection = await OpenDatabaseAsync())
        {
            var lockedUntil = await connection.ExecuteScalarAsync<DateTimeOffset>("SELECT locked_until FROM app_user WHERE lower(email) = @email", new { email = builder.Email });
            lockedUntil.ShouldBe(_clock.GetUtcNow() + TimeSpan.FromMinutes(15));
        }

        using var whileLocked = await client.LoginAsync(builder.Email, AppUserBuilder.ArrangedPassword);
        _clock.Advance(TimeSpan.FromMinutes(14));
        using var stillLocked = await client.LoginAsync(builder.Email, AppUserBuilder.ArrangedPassword);
        _clock.Advance(TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(1));
        using var afterwards = await client.LoginAsync(builder.Email, AppUserBuilder.ArrangedPassword);

        whileLocked.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        stillLocked.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        afterwards.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task Login_WhenAFewFailuresThenSuccess_ShouldResetTheCounter()
    {
        var builder = new AppUserBuilder();
        await using (var connection = await OpenDatabaseAsync())
        {
            await builder.InsertAsync(connection);
        }
        using var client = AuthClient.Create(Host);
        for (var i = 0; i < SignInService.LockoutFailures - 1; i++)
        {
            await client.LoginAsync(builder.Email, "wrong-password-1");
        }
        (await client.LoginAsync(builder.Email, AppUserBuilder.ArrangedPassword)).StatusCode.ShouldBe(HttpStatusCode.OK);

        await client.LoginAsync(builder.Email, "wrong-password-1");

        await using var check = await OpenDatabaseAsync();
        (await check.ExecuteScalarAsync<int>("SELECT failed_sign_in_count FROM app_user WHERE lower(email) = @email", new { email = builder.Email })).ShouldBe(1);
        (await check.ExecuteScalarAsync<DateTimeOffset?>("SELECT last_sign_in_at FROM app_user WHERE lower(email) = @email", new { email = builder.Email })).ShouldBe(_clock.GetUtcNow());
    }
}
