using System.Net;
using System.Net.Http.Json;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using ShipLean.Application.Identity;
using ShipLean.Contracts.Users;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Auth;

// The first instance admin from SHIPLEAN_ADMIN_EMAIL and SHIPLEAN_ADMIN_PASSWORD, on a database with no users.
[TestFixture]
public class AdminBootstrapTests : EmptyApiFixture
{
    protected override TestUser SignedInAs => TestUser.Anonymous;

    [Test]
    [Order(1)]
    public async Task AdminBootstrap_WhenNoAdmin_ShouldCreateFromEnvironment()
    {
        await BootstrapAsync("First.Admin@Example.test", "first-admin-password");

        using var client = AuthClient.Create(Host);
        using var login = await client.LoginAsync("first.admin@example.test", "first-admin-password");
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        var me = await client.GetFromJsonAsync<MeDto>("/api/me", AuthClient.Json);
        me!.IsInstanceAdmin.ShouldBeTrue();
        me.Name.ShouldBe("First.Admin");
    }

    [Test]
    [Order(2)]
    public async Task AdminBootstrap_WhenAnAdminExists_ShouldDoNothing()
    {
        await BootstrapAsync("second@example.test", "second-admin-password");

        await using var connection = await OpenDatabaseAsync();
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM app_user")).ShouldBe(1);
    }

    [Test]
    [Order(3)]
    public async Task AdminBootstrap_WhenNotConfigured_ShouldDoNothing()
    {
        await BootstrapAsync(string.Empty, string.Empty);

        await using var connection = await OpenDatabaseAsync();
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM app_user")).ShouldBe(1);
    }

    [Test]
    [Order(4)]
    public async Task AdminBootstrap_WhenEmailInvalid_ShouldNameTheSetting()
    {
        var thrown = await Should.ThrowAsync<InvalidOperationException>(() => BootstrapAsync("not-an-email", "third-admin-password"));

        thrown.Message.ShouldContain(AdminBootstrap.EmailKey);
    }

    private Task BootstrapAsync(string email, string password)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [AdminBootstrap.EmailKey] = email,
            [AdminBootstrap.PasswordKey] = password,
        }).Build();
        return new AdminBootstrap(
            Host.Services.GetRequiredService<NpgsqlDataSource>(), Host.Services.GetRequiredService<IUserDirectory>(), new Passwords(),
            configuration, NullLogger<AdminBootstrap>.Instance).StartAsync(CancellationToken.None);
    }
}
