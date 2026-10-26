using System.Reflection;
using Dapper;
using Microsoft.AspNetCore.Identity;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Database;

namespace ShipLean.WebApi.Tests.Schema;

[TestFixture]
public class SeedAccountsTests
{
    private const string StatusSql =
        """
        CASE WHEN disabled_at IS NOT NULL THEN 'disabled'
             WHEN password_hash IS NULL THEN 'noPassword'
             WHEN locked_until > now() THEN 'locked'
             WHEN must_change_password THEN 'oneTimePassword'
             ELSE 'active' END
        """;

    private ClonedDatabase _database = null!;

    [OneTimeSetUp]
    public async Task SetUp() => _database = await ShipLeanDatabase.CloneSeedAsync();

    [OneTimeTearDown]
    public async Task TearDown() => await _database.DisposeAsync();

    [Test]
    public async Task Seed_WhenLoaded_ShouldHaveOneUserInEachStatus()
    {
        var pinned = PinnedFiles.Read("me.json")["users"]!.AsArray()
            .ToDictionary(u => Guid.Parse(u!["id"]!.GetValue<string>()), u => u!);
        await using var connection = await _database.OpenAsync();

        var rows = (await connection.QueryAsync<UserStatusRow>(
            $"SELECT id, name, email, initials, avatar_colour AS avatarColour, {StatusSql} AS status FROM app_user")).ToDictionary(r => r.Id);

        foreach (var (id, user) in pinned)
        {
            var row = rows[id];
            row.Name.ShouldBe(user["name"]!.GetValue<string>());
            row.Email.ShouldBe(user["email"]!.GetValue<string>());
            row.Initials.ShouldBe(user["initials"]!.GetValue<string>());
            row.AvatarColour.ShouldBe(user["avatarColour"]!.GetValue<string>());
            row.Status.ShouldBe(user["status"]!.GetValue<string>(), row.Name);
        }
        rows[SeedIds.Users.Kai].ShouldBe(new UserStatusRow(SeedIds.Users.Kai, "Kai Moreno", "kai@shiplean.local", "KM", "#52627c", "active"));
        rows[SeedIds.Users.JiraImport].ShouldBe(
            new UserStatusRow(SeedIds.Users.JiraImport, "Jira import", "jira-import@shiplean.local", "JI", "#52627c", "disabled"));
        rows.Values.Select(r => r.Status).Distinct().Order().ShouldBe(["active", "disabled", "locked", "noPassword", "oneTimePassword"]);
        var dana = await connection.ExecuteScalarAsync<bool>(
            "SELECT app_user.created_at = import_job.started_at FROM app_user, import_job WHERE app_user.id = @user AND import_job.id = @job",
            new { user = SeedIds.Users.Dana, job = SeedIds.ImportJobs.MobImport });
        dana.ShouldBeTrue();
        var lee = await connection.QuerySingleAsync<(bool Disabled55DaysAgo, bool IsAdmin)>(
            "SELECT disabled_at = ((now() AT TIME ZONE 'utc')::date - 55)::timestamp AT TIME ZONE 'utc', is_admin FROM app_user WHERE id = @id",
            new { id = SeedIds.Users.Lee });
        lee.Disabled55DaysAgo.ShouldBeTrue();
        lee.IsAdmin.ShouldBeFalse();
    }

    [Test]
    public async Task Seed_WhenLoaded_ShouldHaveAnActiveUserInNoProject()
    {
        await using var connection = await _database.OpenAsync();

        var active = await connection.QueryAsync<string>(
            $"SELECT name FROM app_user WHERE {StatusSql} = 'active' AND NOT EXISTS (SELECT 1 FROM project_member WHERE user_id = app_user.id)");

        active.ShouldBe(["Kai Moreno"]);
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM project_member WHERE user_id = @id", new { id = SeedIds.Users.JiraImport })).ShouldBe(0);
    }

    [Test]
    public async Task Seed_WhenLoaded_ShouldHaveArchWithNoActiveAdmin()
    {
        await using var connection = await _database.OpenAsync();

        var admins = await connection.QueryAsync<(string Name, bool Disabled)>(
            "SELECT u.name, u.disabled_at IS NOT NULL FROM project_member m JOIN app_user u ON u.id = m.user_id WHERE m.project_id = @id AND m.role = 'admin'",
            new { id = SeedIds.Projects.Arch });
        var members = await connection.QueryAsync<string>(
            "SELECT u.name FROM project_member m JOIN app_user u ON u.id = m.user_id WHERE m.project_id = @id ORDER BY 1", new { id = SeedIds.Projects.Arch });

        admins.ShouldBe([("Lee Park", true)]);
        members.ShouldBe(["Lee Park", "Mei Lin"]);
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM work_item WHERE project_id = @id AND type = 'story'", new { id = SeedIds.Projects.Arch })).ShouldBeInRange(3, 5);
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM work_item WHERE project_id = @id AND type = 'feature'", new { id = SeedIds.Projects.Arch })).ShouldBe(1);
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM sprint WHERE project_id = @id", new { id = SeedIds.Projects.Arch })).ShouldBe(0);
        var next = await connection.ExecuteScalarAsync<int>("SELECT next_item_number FROM project WHERE id = @id", new { id = SeedIds.Projects.Arch });
        next.ShouldBe(1 + await connection.ExecuteScalarAsync<int>("SELECT max(number) FROM work_item WHERE project_id = @id", new { id = SeedIds.Projects.Arch }));
    }

    [Test]
    public async Task Seed_WhenLoaded_ShouldHaveEmptyProjectAdministeredByJoel()
    {
        await using var connection = await _database.OpenAsync();
        var id = SeedIds.Projects.Empt;

        var members = await connection.QueryAsync<(Guid UserId, string Role)>("SELECT user_id, role FROM project_member WHERE project_id = @id ORDER BY role", new { id });

        members.ShouldBe([(SeedIds.Users.Joel, "admin"), (SeedIds.Users.Priya, "member")]);
        (await connection.ExecuteScalarAsync<string>("SELECT key FROM project WHERE id = @id", new { id })).ShouldBe("EMPT");
        (await connection.ExecuteScalarAsync<int>("SELECT next_item_number FROM project WHERE id = @id", new { id })).ShouldBe(1);
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM work_item WHERE project_id = @id", new { id })).ShouldBe(0);
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM sprint WHERE project_id = @id", new { id })).ShouldBe(0);
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM board WHERE project_id = @id", new { id })).ShouldBe(4);
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM state WHERE project_id = @id", new { id })).ShouldBe(25);
    }

    [Test]
    public async Task Seed_WhenLoaded_ShouldLockJoForTheWholeDay()
    {
        await using var connection = await _database.OpenAsync();

        // Locked at least until the end of the current UTC day, whichever day the per-day template was built on.
        var row = await connection.QuerySingleAsync<(int Failed, bool LockedAllDay, bool HasPassword)>(
            "SELECT failed_sign_in_count, locked_until >= ((now() AT TIME ZONE 'utc')::date + 1)::timestamp AT TIME ZONE 'utc', password_hash IS NOT NULL FROM app_user WHERE id = @id",
            new { id = SeedIds.Users.Jo });

        row.Failed.ShouldBe(10);
        row.HasPassword.ShouldBeTrue();
        row.LockedAllDay.ShouldBeTrue();
    }

    [Test]
    public async Task SeedPasswords_WhenNewUsersVerified_ShouldMatch()
    {
        await using var connection = await _database.OpenAsync();
        var hasher = new PasswordHasher<object>();

        foreach (var name in new[] { "Ravi", "Lee", "Jo", "Kai" })
        {
            var hash = await connection.ExecuteScalarAsync<string>("SELECT password_hash FROM app_user WHERE name LIKE @name", new { name = name + " %" });
            var password = (string)typeof(SeedIds.Passwords).GetField(name, BindingFlags.Public | BindingFlags.Static)!.GetRawConstantValue()!;
            hasher.VerifyHashedPassword(new object(), hash!, password).ShouldBe(PasswordVerificationResult.Success, name);
        }
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM app_user WHERE password_hash IS NULL")).ShouldBe(2);
        (await connection.ExecuteScalarAsync<bool>("SELECT must_change_password FROM app_user WHERE id = @id", new { id = SeedIds.Users.Ravi })).ShouldBeTrue();
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM app_user WHERE is_admin AND disabled_at IS NULL")).ShouldBe(2);
    }

    private sealed record UserStatusRow(Guid Id, string Name, string Email, string Initials, string AvatarColour, string Status);
}
