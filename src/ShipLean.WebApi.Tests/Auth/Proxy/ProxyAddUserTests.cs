using System.Net;
using System.Net.Http.Json;
using Dapper;
using ShipLean.Contracts.Users.Admin;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Auth.Proxy;

// Adding a user in proxy mode creates them with no one-time password (K8).
[TestFixture]
public class ProxyAddUserTests : ApiFixture
{
    protected override IReadOnlyDictionary<string, string?> HostSettings => ProxyApiFixture.LiteralTokenSettings;

    [Test]
    public async Task AddUser_WhenProxyMode_ShouldCreateWithoutOneTimePassword()
    {
        using var response = await Api.PostJsonAsync("api/admin/users", new { email = "new.person@example.test", name = "New Person", projectIds = Array.Empty<Guid>() });

        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        var added = System.Text.Json.JsonSerializer.Deserialize<AddUserResponse>(text, AuthClient.Json)!;
        added.OneTimePassword.ShouldBeNull();
        added.User.Email.ShouldBe("new.person@example.test");
        using var list = await Api.GetAsync("api/admin/users");
        var listed = (await list.Content.ReadFromJsonAsync<AdminUsersDto>(AuthClient.Json))!;
        listed.Users.ShouldContain(u => u.Id == added.User.Id);
        await using var connection = await OpenDatabaseAsync();
        (await connection.ExecuteScalarAsync<bool>("SELECT must_change_password FROM app_user WHERE id = @id", new { id = added.User.Id })).ShouldBeFalse();
    }
}
