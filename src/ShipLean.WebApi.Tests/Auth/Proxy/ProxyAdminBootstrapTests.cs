using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using ShipLean.Application.Identity;
using ShipLean.Contracts.Users;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Auth.Proxy;

// SHIPLEAN_ADMIN_EMAIL alone makes the first instance admin in proxy mode, on a database with no users. Each test
// starts from an empty app_user.
[TestFixture]
public class ProxyAdminBootstrapTests : EmptyApiFixture
{
    private const string AdminEmail = "proxy.admin@example.test";

    protected override TestUser SignedInAs => TestUser.Anonymous;

    [SetUp]
    public async Task EmptyUsersAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync("DELETE FROM app_user");
    }

    [Test]
    public async Task Bootstrap_WhenProxyModeAndAdminEmailExists_ShouldPromoteAtStartup()
    {
        var id = await ArrangeAsync(b => b.WithEmail(AdminEmail.ToUpperInvariant()).WithPassword(null));
        var stamp = await ScalarAsync<string>("SELECT security_stamp FROM app_user WHERE id = @id", id);

        await BootstrapAsync(AdminEmail, password: null);

        (await ScalarAsync<bool>("SELECT is_admin FROM app_user WHERE id = @id", id)).ShouldBeTrue();
        (await ScalarAsync<string>("SELECT security_stamp FROM app_user WHERE id = @id", id)).ShouldBe(stamp);
    }

    [Test]
    public async Task Bootstrap_WhenProxyModeAndPasswordSet_ShouldIgnoreIt()
    {
        var id = await ArrangeAsync(b => b.WithEmail(AdminEmail).WithPassword(null));

        await BootstrapAsync(AdminEmail, password: "a-long-enough-password-1");

        (await ScalarAsync<bool>("SELECT is_admin FROM app_user WHERE id = @id", id)).ShouldBeTrue();
        (await ScalarAsync<string?>("SELECT password_hash FROM app_user WHERE id = @id", id)).ShouldBeNull();
    }

    [Test]
    public async Task Bootstrap_WhenProxyModeAndUserMissing_ShouldNotCreate()
    {
        await BootstrapAsync(AdminEmail, password: null);

        await using var connection = await OpenDatabaseAsync();
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM app_user")).ShouldBe(0);
    }

    private async Task<Guid> ArrangeAsync(Func<AppUserBuilder, AppUserBuilder> configure)
    {
        await using var connection = await OpenDatabaseAsync();
        return await configure(new AppUserBuilder()).InsertAsync(connection);
    }

    private async Task<T> ScalarAsync<T>(string sql, Guid id)
    {
        await using var connection = await OpenDatabaseAsync();
        return (await connection.ExecuteScalarAsync<T>(sql, new { id }))!;
    }

    private Task BootstrapAsync(string email, string? password)
    {
        var settings = new Dictionary<string, string?>(ProxyApiFixture.LiteralTokenSettings)
        {
            [AdminBootstrap.EmailKey] = email,
            [AdminBootstrap.PasswordKey] = password,
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new AdminBootstrap(
            Host.Services.GetRequiredService<NpgsqlDataSource>(), Host.Services.GetRequiredService<IUserDirectory>(), new Passwords(),
            configuration, NullLogger<AdminBootstrap>.Instance).StartAsync(CancellationToken.None);
    }
}
