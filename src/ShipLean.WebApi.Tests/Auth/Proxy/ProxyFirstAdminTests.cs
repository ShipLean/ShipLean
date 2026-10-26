using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Identity;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Auth.Proxy;

// K6: the first proxy sign-in of SHIPLEAN_ADMIN_EMAIL's owner makes them the instance admin, only while there is none.
// One scenario in order, on a database with no users; the host's SHIPLEAN_ADMIN_EMAIL is set, and the startup bootstrap
// finds nobody to promote.
[TestFixture]
public class ProxyFirstAdminTests : EmptyApiFixture
{
    private const string AdminEmail = "first.admin@example.test";

    private static readonly Guid Admin = Guid.NewGuid();
    private static readonly Guid Other = Guid.NewGuid();

    protected override TestUser SignedInAs => TestUser.Anonymous;

    protected override IReadOnlyDictionary<string, string?> HostSettings => new Dictionary<string, string?>(ProxyApiFixture.LiteralTokenSettings)
    {
        [AdminBootstrap.EmailKey] = AdminEmail,
    };

    private IProxyFirstAdmin FirstAdmin => Host.Services.GetRequiredService<IProxyFirstAdmin>();

    [Test]
    [Order(1)]
    public async Task PromoteIfFirst_WhenEmailDiffers_ShouldDoNothing()
    {
        await using var connection = await OpenDatabaseAsync();
        await new AppUserBuilder().WithId(Other).WithEmail("someone.else@example.test").InsertAsync(connection);
        await new AppUserBuilder().WithId(Admin).WithEmail(AdminEmail).WithPassword(null).InsertAsync(connection);

        (await FirstAdmin.PromoteIfFirstAsync(Other, "someone.else@example.test", CancellationToken.None)).ShouldBeFalse();

        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM app_user WHERE is_admin")).ShouldBe(0);
    }

    [Test]
    [Order(1)]
    public async Task PromoteIfFirst_WhenIdBelongsToAnotherUser_ShouldDoNothing()
    {
        await using var connection = await OpenDatabaseAsync();

        (await FirstAdmin.PromoteIfFirstAsync(Other, AdminEmail, CancellationToken.None)).ShouldBeFalse();

        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM app_user WHERE is_admin")).ShouldBe(0);
    }

    [Test]
    [Order(2)]
    public async Task PromoteIfFirst_WhenNoAdminAndEmailMatches_ShouldPromoteWithoutRotatingStamp()
    {
        await using var connection = await OpenDatabaseAsync();
        var stamp = await connection.ExecuteScalarAsync<string>("SELECT security_stamp FROM app_user WHERE id = @Admin", new { Admin });

        (await FirstAdmin.PromoteIfFirstAsync(Admin, AdminEmail.ToUpperInvariant(), CancellationToken.None)).ShouldBeTrue();

        (await connection.ExecuteScalarAsync<bool>("SELECT is_admin FROM app_user WHERE id = @Admin", new { Admin })).ShouldBeTrue();
        (await connection.ExecuteScalarAsync<string>("SELECT security_stamp FROM app_user WHERE id = @Admin", new { Admin })).ShouldBe(stamp);
    }

    [Test]
    [Order(3)]
    public async Task PromoteIfFirst_WhenAnAdminExists_ShouldDoNothing()
    {
        await using var connection = await OpenDatabaseAsync();

        (await FirstAdmin.PromoteIfFirstAsync(Admin, AdminEmail, CancellationToken.None)).ShouldBeFalse();

        (await connection.ExecuteScalarAsync<bool>("SELECT is_admin FROM app_user WHERE id = @Other", new { Other })).ShouldBeFalse();
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM app_user WHERE is_admin")).ShouldBe(1);
    }
}
