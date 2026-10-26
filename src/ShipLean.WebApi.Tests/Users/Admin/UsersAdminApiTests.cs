using System.Net;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Users;
using ShipLean.Contracts.Users.Admin;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Auth;
using ShipLean.WebApi.Tests.Sprints.Api;

namespace ShipLean.WebApi.Tests.Users.Admin;

// The instance Users tab. Joel is the seeded instance admin and the admin of SHIP, MOB and EMPT, a plain member of PAY.
// Each test arranges users of its own, because the class shares one database.
[TestFixture]
public class UsersAdminApiTests : ApiFixture
{
    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonSerializer.Deserialize<T>(text, AuthClient.Json)!;
    }

    private async Task<(Guid Id, string Email)> ArrangeAsync(Func<AppUserBuilder, AppUserBuilder> configure)
    {
        var builder = configure(new AppUserBuilder());
        await using var connection = await OpenDatabaseAsync();
        return (await builder.InsertAsync(connection), builder.Email);
    }

    private async Task<AdminUserDto> RowAsync(Guid id)
        => (await ReadAsync<AdminUsersDto>(await Api.GetAsync("api/admin/users"))).Users.Single(u => u.Id == id);

    private async Task<T> ScalarAsync<T>(string sql, object parameters)
    {
        await using var connection = await OpenDatabaseAsync();
        return (await connection.ExecuteScalarAsync<T>(sql, parameters))!;
    }

    [Test]
    public async Task Admin_WhenNotInstanceAdmin_ShouldBeForbiddenAndChangeNothing()
    {
        var target = await ArrangeAsync(b => b);
        var mei = Api.ActingAs(TestUser.Mei);
        var id = target.Id;

        var responses = new[]
        {
            await mei.GetAsync("api/admin/users"),
            await mei.PostJsonAsync("api/admin/users", new { email = "mei.adds@example.test", name = "Mei Adds", projectIds = Array.Empty<Guid>() }),
            await mei.PostJsonAsync($"api/admin/users/{id}/one-time-password"),
            await mei.PostJsonAsync($"api/admin/users/{id}/unlock"),
            await mei.PutJsonAsync($"api/admin/users/{id}/disabled", new { disabled = true }),
            await mei.PutJsonAsync($"api/admin/users/{id}/instance-admin", new { isInstanceAdmin = true }),
            await mei.PutJsonAsync($"api/admin/users/{id}/email", new { email = "mei.changed@example.test" }),
        };

        foreach (var response in responses)
        {
            (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Forbidden)).Code.ShouldBe(ApiErrorCodes.Forbidden);
            response.Dispose();
        }
        var row = await RowAsync(id);
        row.Status.ShouldBe(UserStatus.Active);
        row.IsInstanceAdmin.ShouldBeFalse();
        row.Email.ShouldBe(target.Email);
        (await ScalarAsync<int>("SELECT count(*)::int FROM app_user WHERE lower(email) IN ('mei.adds@example.test')", new { })).ShouldBe(0);
    }

    [Test]
    public async Task List_WhenRead_ShouldShowStatusProjectsAndLastSignIn()
    {
        var signedIn = await ArrangeAsync(b => b.WithName("Listed Active"));
        var never = await ArrangeAsync(b => b.WithName("Listed Never").WithPassword(null));
        await using (var connection = await OpenDatabaseAsync())
        {
            await connection.ExecuteAsync("UPDATE app_user SET last_sign_in_at = now() WHERE id = @id", new { id = signedIn.Id });
        }

        var users = await ReadAsync<AdminUsersDto>(await Api.GetAsync("api/admin/users"));

        users.Users.Single(u => u.Id == signedIn.Id).LastSignIn.ShouldNotBeNull();
        users.Users.Single(u => u.Id == never.Id).LastSignIn.ShouldBeNull();
        users.Users.Single(u => u.Id == never.Id).Status.ShouldBe(UserStatus.NoPassword);
        var joel = users.Users.Single(u => u.Id == SeedIds.Users.Joel);
        joel.IsInstanceAdmin.ShouldBeTrue();
        joel.Projects.Single(p => p.Id == SeedIds.Projects.Ship).Role.ShouldBe(ProjectRole.Admin);
        joel.Projects.Single(p => p.Id == SeedIds.Projects.Pay).Role.ShouldBe(ProjectRole.Member);
        users.AdministeredProjects.Select(p => p.Id).ShouldBe([SeedIds.Projects.Empt, SeedIds.Projects.Mob, SeedIds.Projects.Ship], ignoreOrder: true);
    }

    [Test]
    public async Task List_WhenAdminIsTheOnlyActiveOne_ShouldFlagTheProject()
    {
        var users = await ReadAsync<AdminUsersDto>(await Api.GetAsync("api/admin/users"));

        var joel = users.Users.Single(u => u.Id == SeedIds.Users.Joel);
        joel.Projects.Single(p => p.Id == SeedIds.Projects.Ship).SoleActiveAdmin.ShouldBeTrue();
        joel.Projects.Single(p => p.Id == SeedIds.Projects.Pay).SoleActiveAdmin.ShouldBeFalse();
    }

    [Test]
    public async Task Add_WhenCreated_ShouldReturnTheOneTimePasswordOnlyOnce()
    {
        using var watch = new ProjectEventWatch(Host.Services.GetRequiredService<IProjectEvents>(), SeedIds.Projects.Mob);

        using var response = await Api.PostJsonAsync("api/admin/users", new { email = "New.Person@Example.test", name = "New Person", isInstanceAdmin = true, projectIds = new[] { SeedIds.Projects.Mob } });
        var added = await ReadAsync<AddUserResponse>(response);

        added.OneTimePassword.ShouldNotBeNull();
        added.OneTimePassword.Password.ShouldNotBeNullOrWhiteSpace();
        added.OneTimePassword.UserId.ShouldBe(added.User.Id);
        added.User.Status.ShouldBe(UserStatus.OneTimePassword);
        added.User.IsInstanceAdmin.ShouldBeTrue();
        added.User.Projects.Select(p => (p.Id, p.Role)).ShouldBe([(SeedIds.Projects.Mob, ProjectRole.Member)]);
        await watch.NextAsync<ProjectChanged>(e => e.ProjectId == SeedIds.Projects.Mob);
        using var later = await Api.GetAsync("api/admin/users");
        (await later.Content.ReadAsStringAsync()).ShouldNotContain(added.OneTimePassword.Password);
        using var client = AuthClient.Create(Host);
        using var signIn = await client.LoginAsync("new.person@example.test", added.OneTimePassword.Password);
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task Add_WhenProjectNotAdministered_ShouldRefuseAndCreateNobody()
    {
        using var member = await Api.PostJsonAsync("api/admin/users", new { email = "refused.member@example.test", name = "Refused", projectIds = new[] { SeedIds.Projects.Pay } });
        using var stranger = await Api.PostJsonAsync("api/admin/users", new { email = "refused.stranger@example.test", name = "Refused", projectIds = new[] { Guid.NewGuid() } });

        (await ApiClient.ReadApiErrorAsync(member, HttpStatusCode.Forbidden)).Code.ShouldBe(ApiErrorCodes.Forbidden);
        (await ApiClient.ReadApiErrorAsync(stranger, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
        (await ScalarAsync<int>("SELECT count(*)::int FROM app_user WHERE email LIKE 'refused.%'", new { })).ShouldBe(0);
    }

    [TestCase("\u202Eevil")]
    [TestCase("Joel\u200BDickson")]
    [TestCase("Ada\u2066Lovelace\u2069")]
    [TestCase("Ada\u0007Lovelace")]
    public async Task Add_WhenNameHasControlOrInvisibleCharacters_ShouldBeValidationAndCreateNobody(string name)
    {
        using var response = await Api.PostJsonAsync("api/admin/users", new { email = "invisible.name@example.test", name, projectIds = Array.Empty<Guid>() });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
        (await ScalarAsync<int>("SELECT count(*)::int FROM app_user WHERE email = 'invisible.name@example.test'", new { })).ShouldBe(0);
    }

    [Test]
    public async Task Add_WhenNameHasANoBreakSpaceAndAZeroWidthJoiner_ShouldBeAcceptedWithInitialsFromBothWords()
    {
        using var spaced = await Api.PostJsonAsync("api/admin/users", new { email = "nbsp.name@example.test", name = "Ada\u00A0Lovelace", projectIds = Array.Empty<Guid>() });
        using var emoji = await Api.PostJsonAsync("api/admin/users", new { email = "zwj.name@example.test", name = "Dev \U0001F469\u200D\U0001F4BB", projectIds = Array.Empty<Guid>() });

        (await ReadAsync<AddUserResponse>(spaced)).User.Initials.ShouldBe("AL");
        (await ReadAsync<AddUserResponse>(emoji)).User.Name.ShouldBe("Dev \U0001F469\u200D\U0001F4BB");
    }

    [Test]
    public async Task Add_WhenEmailTakenOrBad_ShouldBeConflictOrValidation()
    {
        using var taken = await Api.PostJsonAsync("api/admin/users", new { email = "JOEL@shiplean.local", name = "Twin", projectIds = Array.Empty<Guid>() });
        using var bad = await Api.PostJsonAsync("api/admin/users", new { email = "not-an-email", name = "Bad", projectIds = Array.Empty<Guid>() });

        (await ApiClient.ReadApiErrorAsync(taken, HttpStatusCode.Conflict)).Code.ShouldBe(ApiErrorCodes.Conflict);
        (await ApiClient.ReadApiErrorAsync(bad, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task OneTimePassword_WhenReset_ShouldLetTheUserSignInAndForceAChange()
    {
        var target = await ArrangeAsync(b => b.WithName("Reset Me"));

        var issued = await ReadAsync<OneTimePasswordDto>(await Api.PostJsonAsync($"api/admin/users/{target.Id}/one-time-password"));

        issued.UserId.ShouldBe(target.Id);
        (await RowAsync(target.Id)).Status.ShouldBe(UserStatus.OneTimePassword);
        using var client = AuthClient.Create(Host);
        (await client.LoginAsync(target.Email, AppUserBuilder.ArrangedPassword)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.LoginAsync(target.Email, issued.Password)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task OneTimePassword_WhenSetUpForImportedUser_ShouldLetThemSignIn()
    {
        var imported = await ArrangeAsync(b => b.WithPassword(null));
        (await RowAsync(imported.Id)).Status.ShouldBe(UserStatus.NoPassword);

        var issued = await ReadAsync<OneTimePasswordDto>(await Api.PostJsonAsync($"api/admin/users/{imported.Id}/one-time-password"));

        using var client = AuthClient.Create(Host);
        (await client.LoginAsync(imported.Email, issued.Password)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await RowAsync(imported.Id)).Status.ShouldBe(UserStatus.OneTimePassword);
    }

    [Test]
    public async Task Unlock_WhenLocked_ShouldBeActiveAgain()
    {
        var locked = await ArrangeAsync(b => b.LockedUntil(new DateTimeOffset(2099, 1, 1, 0, 0, 0, TimeSpan.Zero)));
        (await RowAsync(locked.Id)).Status.ShouldBe(UserStatus.Locked);

        using var response = await Api.PostJsonAsync($"api/admin/users/{locked.Id}/unlock");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await RowAsync(locked.Id)).Status.ShouldBe(UserStatus.Active);
    }

    [Test]
    public async Task Disabled_WhenToggled_ShouldDisableAndEnable()
    {
        var target = await ArrangeAsync(b => b);

        using var disable = await Api.PutJsonAsync($"api/admin/users/{target.Id}/disabled", new { disabled = true });
        var disabledStatus = (await RowAsync(target.Id)).Status;
        using var enable = await Api.PutJsonAsync($"api/admin/users/{target.Id}/disabled", new { disabled = false });

        disable.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        disabledStatus.ShouldBe(UserStatus.Disabled);
        enable.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await RowAsync(target.Id)).Status.ShouldBe(UserStatus.Active);
    }

    [Test]
    public async Task Disabled_WhenDisablingSelf_ShouldBeValidation()
    {
        using var response = await Api.PutJsonAsync($"api/admin/users/{SeedIds.Users.Joel}/disabled", new { disabled = true });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
        (await RowAsync(SeedIds.Users.Joel)).Status.ShouldBe(UserStatus.Active);
    }

    [Test]
    public async Task Disabled_WhenProjectLosesItsOnlyAdmin_ShouldFlagItInTheList()
    {
        var soleAdmin = await ArrangeAsync(b => b);
        await using (var connection = await OpenDatabaseAsync())
        {
            await connection.ExecuteAsync(
                "INSERT INTO project_member (id, project_id, user_id, role) VALUES (@id, @project, @user, 'admin')",
                new { id = Guid.NewGuid(), project = SeedIds.Projects.Part, user = soleAdmin.Id });
        }

        var before = (await RowAsync(soleAdmin.Id)).Projects.Single(p => p.Id == SeedIds.Projects.Part);
        using var disable = await Api.PutJsonAsync($"api/admin/users/{soleAdmin.Id}/disabled", new { disabled = true });
        var priya = (await RowAsync(SeedIds.Users.Priya)).Projects.Single(p => p.Id == SeedIds.Projects.Part);

        before.SoleActiveAdmin.ShouldBeFalse();
        disable.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        priya.SoleActiveAdmin.ShouldBeTrue();
    }

    [Test]
    public async Task InstanceAdmin_WhenToggled_ShouldPromoteAndDemote()
    {
        var target = await ArrangeAsync(b => b);

        using var promote = await Api.PutJsonAsync($"api/admin/users/{target.Id}/instance-admin", new { isInstanceAdmin = true });
        var promoted = (await RowAsync(target.Id)).IsInstanceAdmin;
        using var demote = await Api.PutJsonAsync($"api/admin/users/{target.Id}/instance-admin", new { isInstanceAdmin = false });

        promote.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        promoted.ShouldBeTrue();
        demote.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await RowAsync(target.Id)).IsInstanceAdmin.ShouldBeFalse();
    }

    [Test]
    public async Task InstanceAdmin_WhenDemotingSelf_ShouldBeValidation()
    {
        using var response = await Api.PutJsonAsync($"api/admin/users/{SeedIds.Users.Joel}/instance-admin", new { isInstanceAdmin = false });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
        (await RowAsync(SeedIds.Users.Joel)).IsInstanceAdmin.ShouldBeTrue();
    }

    [Test]
    public async Task Email_WhenChanged_ShouldSignInWithTheNewOneOnly()
    {
        var target = await ArrangeAsync(b => b);
        const string changed = "changed.email@example.test";

        using var response = await Api.PutJsonAsync($"api/admin/users/{target.Id}/email", new { email = changed });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var client = AuthClient.Create(Host);
        (await client.LoginAsync(target.Email, AppUserBuilder.ArrangedPassword)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.LoginAsync(changed, AppUserBuilder.ArrangedPassword)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task Email_WhenTakenOrBad_ShouldBeConflictOrValidation()
    {
        var target = await ArrangeAsync(b => b);

        using var taken = await Api.PutJsonAsync($"api/admin/users/{target.Id}/email", new { email = "joel@shiplean.local" });
        using var bad = await Api.PutJsonAsync($"api/admin/users/{target.Id}/email", new { email = "nope" });

        (await ApiClient.ReadApiErrorAsync(taken, HttpStatusCode.Conflict)).Code.ShouldBe(ApiErrorCodes.Conflict);
        (await ApiClient.ReadApiErrorAsync(bad, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Admin_WhenUserUnknown_ShouldBeNotFound()
    {
        var unknown = Guid.NewGuid();

        var responses = new[]
        {
            await Api.PostJsonAsync($"api/admin/users/{unknown}/one-time-password"),
            await Api.PostJsonAsync($"api/admin/users/{unknown}/unlock"),
            await Api.PutJsonAsync($"api/admin/users/{unknown}/disabled", new { disabled = true }),
            await Api.PutJsonAsync($"api/admin/users/{unknown}/instance-admin", new { isInstanceAdmin = true }),
            await Api.PutJsonAsync($"api/admin/users/{unknown}/email", new { email = "x@example.test" }),
        };

        foreach (var response in responses)
        {
            (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
            response.Dispose();
        }
    }

    [Test]
    public async Task OneTimePassword_WhenSelf_ShouldBeValidationAndKeepTheSession()
    {
        using var joel = await AuthClient.SignedInAsync(Host, TestUser.Joel.Email!, TestUser.Joel.Password!);

        using var response = await joel.PostAsync($"/api/admin/users/{SeedIds.Users.Joel}/one-time-password", null);

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
        using var after = await joel.GetAsync("/api/admin/users");
        after.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var fresh = AuthClient.Create(Host);
        (await fresh.LoginAsync(TestUser.Joel.Email!, TestUser.Joel.Password!)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task Email_WhenSelf_ShouldBeValidationAndChangeNothing()
    {
        using var response = await Api.PutJsonAsync($"api/admin/users/{SeedIds.Users.Joel}/email", new { email = "joel.moved@example.test" });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
        (await RowAsync(SeedIds.Users.Joel)).Email.ShouldBe(TestUser.Joel.Email);
    }

    [Test]
    public async Task Responses_WhenIssuingAPassword_ShouldNotBeCached()
    {
        var target = await ArrangeAsync(b => b);

        using var reset = await Api.PostJsonAsync($"api/admin/users/{target.Id}/one-time-password");
        using var add = await Api.PostJsonAsync("api/admin/users", new { email = "no.store@example.test", name = "No Store", projectIds = Array.Empty<Guid>() });

        reset.Headers.CacheControl!.NoStore.ShouldBeTrue();
        add.Headers.CacheControl!.NoStore.ShouldBeTrue();
    }

    [Test]
    public async Task List_WhenRowsAreArchived_ShouldIgnoreThem()
    {
        var left = await ArrangeAsync(b => b.WithName("Left Projects"));
        await using (var connection = await OpenDatabaseAsync())
        {
            await connection.ExecuteAsync(
                """
                INSERT INTO project_member (id, project_id, user_id, role, archived_at) VALUES
                    (@a, @part, @user, 'admin', now()), (@b, @ship, @user, 'member', now())
                """,
                new { a = Guid.NewGuid(), b = Guid.NewGuid(), part = SeedIds.Projects.Part, ship = SeedIds.Projects.Ship, user = left.Id });
            await connection.ExecuteAsync("UPDATE project_member SET archived_at = now() WHERE project_id = @mob AND user_id = @joel", new { mob = SeedIds.Projects.Mob, joel = SeedIds.Users.Joel });
        }

        try
        {
            var users = await ReadAsync<AdminUsersDto>(await Api.GetAsync("api/admin/users"));

            users.Users.Single(u => u.Id == left.Id).Projects.ShouldBeEmpty();
            users.Users.Single(u => u.Id == SeedIds.Users.Priya).Projects.Single(p => p.Id == SeedIds.Projects.Part).SoleActiveAdmin.ShouldBeTrue();
            users.AdministeredProjects.Select(p => p.Id).ShouldNotContain(SeedIds.Projects.Mob);
        }
        finally
        {
            await using var connection = await OpenDatabaseAsync();
            await connection.ExecuteAsync("UPDATE project_member SET archived_at = NULL WHERE project_id = @mob AND user_id = @joel", new { mob = SeedIds.Projects.Mob, joel = SeedIds.Users.Joel });
        }
    }

    [Test]
    public async Task Admin_WhenEmailOrStatusChanges_ShouldPublishProjectChangedForEachProjectOfTheUser()
    {
        var target = await ArrangeAsync(b => b);
        await using (var connection = await OpenDatabaseAsync())
        {
            await connection.ExecuteAsync(
                "INSERT INTO project_member (id, project_id, user_id, role) VALUES (@a, @ship, @user, 'member'), (@b, @mob, @user, 'member')",
                new { a = Guid.NewGuid(), b = Guid.NewGuid(), ship = SeedIds.Projects.Ship, mob = SeedIds.Projects.Mob, user = target.Id });
        }
        var events = Host.Services.GetRequiredService<IProjectEvents>();

        foreach (var change in new Func<Task<HttpResponseMessage>>[]
        {
            () => Api.PutJsonAsync($"api/admin/users/{target.Id}/email", new { email = "published.email@example.test" }),
            () => Api.PutJsonAsync($"api/admin/users/{target.Id}/disabled", new { disabled = true }),
            () => Api.PutJsonAsync($"api/admin/users/{target.Id}/disabled", new { disabled = false }),
        })
        {
            using var ship = new ProjectEventWatch(events, SeedIds.Projects.Ship);
            using var mob = new ProjectEventWatch(events, SeedIds.Projects.Mob);
            using var response = await change();
            response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
            await ship.NextAsync<ProjectChanged>(e => e.ProjectId == SeedIds.Projects.Ship);
            await mob.NextAsync<ProjectChanged>(e => e.ProjectId == SeedIds.Projects.Mob);
        }
    }
}
