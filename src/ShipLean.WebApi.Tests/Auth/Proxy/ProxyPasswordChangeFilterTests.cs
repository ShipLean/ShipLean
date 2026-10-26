using System.Net;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Identity;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Auth.Proxy;

// A user left with must_change_password is not locked out of everything in proxy mode.
[TestFixture]
public class ProxyPasswordChangeFilterTests : ApiFixture
{
    protected override IReadOnlyDictionary<string, string?> HostSettings => ProxyApiFixture.LiteralTokenSettings;

    protected override TestUser SignedInAs => TestUser.Mei;

    [Test]
    public async Task PasswordChangeFilter_WhenProxyModeAndChangeRequired_ShouldNotBlock()
    {
        await using (var connection = await OpenDatabaseAsync())
        {
            await connection.ExecuteAsync("UPDATE app_user SET must_change_password = true WHERE id = @id", new { id = SeedIds.Users.Mei });
        }
        Host.Services.GetRequiredService<UserSessions>().Forget(SeedIds.Users.Mei);

        using var response = await Api.GetAsync("api/me");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
