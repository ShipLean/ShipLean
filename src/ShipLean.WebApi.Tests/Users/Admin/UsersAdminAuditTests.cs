using System.Net;
using System.Text.Json;
using Dapper;
using ShipLean.Contracts.Users;
using ShipLean.Contracts.Users.Admin;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Database;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Tests.Auth;
using ShipLean.WebApi.Tests.Import.Jira;

namespace ShipLean.WebApi.Tests.Users.Admin;

// Every admin write leaves one log line naming the actor, the action and the target, and no secret reaches a log.
[TestFixture]
public class UsersAdminAuditTests : ApiFixture
{
    private readonly LogCapture _logs = new();

    private TextWriter? _console;

    // The host's console sink writes to Console.Out; swapping it before the host starts keeps every log line.
    protected override Task<ClonedDatabase> CloneDatabaseAsync()
    {
        _console = Console.Out;
        Console.SetOut(_logs.Writer);
        return base.CloneDatabaseAsync();
    }

    [OneTimeTearDown]
    public void RestoreConsole()
    {
        if (_console is not null)
        {
            Console.SetOut(_console);
        }
    }

    [Test]
    public async Task Admin_WhenWritingAndAdding_ShouldLogActorActionAndTargetButNoSecret()
    {
        var builder = new AppUserBuilder();
        Guid target;
        await using (var connection = await OpenDatabaseAsync())
        {
            target = await builder.InsertAsync(connection);
        }
        const string newEmail = "audit.new.email@example.test";
        const string addedEmail = "audit.added@example.test";

        using var reset = await Api.PostJsonAsync($"api/admin/users/{target}/one-time-password");
        var password = JsonSerializer.Deserialize<OneTimePasswordDto>(await reset.Content.ReadAsStringAsync(), AuthClient.Json)!.Password;
        using var unlock = await Api.PostJsonAsync($"api/admin/users/{target}/unlock");
        using var promote = await Api.PutJsonAsync($"api/admin/users/{target}/instance-admin", new { isInstanceAdmin = true });
        using var demote = await Api.PutJsonAsync($"api/admin/users/{target}/instance-admin", new { isInstanceAdmin = false });
        using var disable = await Api.PutJsonAsync($"api/admin/users/{target}/disabled", new { disabled = true });
        using var enable = await Api.PutJsonAsync($"api/admin/users/{target}/disabled", new { disabled = false });
        using var email = await Api.PutJsonAsync($"api/admin/users/{target}/email", new { email = newEmail });
        using var add = await Api.PostJsonAsync("api/admin/users", new { email = addedEmail, name = "Audit Added", projectIds = Array.Empty<Guid>() });
        var added = JsonSerializer.Deserialize<AddUserResponse>(await add.Content.ReadAsStringAsync(), AuthClient.Json)!;

        foreach (var response in new[] { reset, unlock, promote, demote, disable, enable, email })
        {
            response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);
        }
        var lines = _logs.Text.Split('\n').Where(l => l.Contains("Instance user admin")).ToList();
        foreach (var action in new[] { "one-time-password", "unlock", "promote", "demote", "disable", "enable", "change-email" })
        {
            lines.ShouldContain(l => l.Contains($"{action} on user {target} by {SeedIds.Users.Joel}"));
        }
        lines.ShouldContain(l => l.Contains($"add on user {added.User.Id} by {SeedIds.Users.Joel}"));
        _logs.Text.ShouldNotContain(password);
        added.OneTimePassword.ShouldNotBeNull();
        _logs.Text.ShouldNotContain(added.OneTimePassword.Password);
        _logs.Text.ShouldNotContain(AppUserBuilder.ArrangedPassword);
        _logs.Text.ShouldNotContain(newEmail);
        _logs.Text.ShouldNotContain(addedEmail);
    }
}
