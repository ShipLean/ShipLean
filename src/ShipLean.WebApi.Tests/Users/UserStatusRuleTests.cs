using Dapper;
using ShipLean.Application.Identity;
using ShipLean.Contracts.Users;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Database;

namespace ShipLean.WebApi.Tests.Users;

[TestFixture]
public class UserStatusRuleTests
{
    private ClonedDatabase _database = null!;

    [OneTimeSetUp]
    public async Task CloneDatabase() => _database = await ShipLeanDatabase.CloneEmptyAsync();

    [OneTimeTearDown]
    public async Task DropDatabase() => await _database.DisposeAsync();

    [Test]
    public async Task Status_WhenRowsDerived_ShouldMatchInCSharpAndSql()
    {
        var now = TimeProvider.System.GetUtcNow();
        var cases = new (string Name, AppUserBuilder Builder, UserStatus Expected)[]
        {
            ("active", new AppUserBuilder(), UserStatus.Active),
            ("oneTimePassword", new AppUserBuilder().MustChangePassword(), UserStatus.OneTimePassword),
            ("locked", new AppUserBuilder().LockedUntil(now.AddHours(1)), UserStatus.Locked),
            ("lockedInThePast", new AppUserBuilder().LockedUntil(now.AddHours(-1)), UserStatus.Active),
            ("lockedAndOneTimePassword", new AppUserBuilder().LockedUntil(now.AddHours(1)).MustChangePassword(), UserStatus.Locked),
            ("noPassword", new AppUserBuilder().WithPassword(null), UserStatus.NoPassword),
            ("noPasswordAndLocked", new AppUserBuilder().WithPassword(null).LockedUntil(now.AddHours(1)), UserStatus.NoPassword),
            ("disabled", new AppUserBuilder().DisabledAt(now.AddDays(-1)), UserStatus.Disabled),
            ("disabledAndLocked", new AppUserBuilder().DisabledAt(now.AddDays(-1)).LockedUntil(now.AddHours(1)), UserStatus.Disabled),
            ("disabledAndNoPassword", new AppUserBuilder().DisabledAt(now.AddDays(-1)).WithPassword(null), UserStatus.Disabled),
        };
        var ids = new Dictionary<Guid, (string Name, UserStatus Expected)>();
        await using var connection = await _database.OpenAsync();
        foreach (var (name, builder, expected) in cases)
        {
            ids[await builder.InsertAsync(connection)] = (name, expected);
        }

        var rows = (await connection.QueryAsync<Row>(
            $"""
            SELECT id, disabled_at AS disabledAt, password_hash IS NOT NULL AS hasPassword, locked_until AS lockedUntil,
                   must_change_password AS mustChangePassword, {UserStatusRule.Sql} AS sqlStatus
            FROM app_user
            """, new { now })).ToList();

        rows.Count.ShouldBe(cases.Length);
        foreach (var row in rows)
        {
            var (name, expected) = ids[row.Id];
            UserStatusRule.Parse(row.SqlStatus).ShouldBe(expected, $"SQL for {name}");
            UserStatusRule.Derive(row.DisabledAt, row.HasPassword, row.LockedUntil, row.MustChangePassword, now).ShouldBe(expected, $"C# for {name}");
        }
    }

    private sealed record Row(Guid Id, DateTimeOffset? DisabledAt, bool HasPassword, DateTimeOffset? LockedUntil, bool MustChangePassword, string SqlStatus);
}
