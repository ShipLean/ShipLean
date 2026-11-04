using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShipLean.Contracts.Agents;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Attach;

// K6: the transfer token's demo kinds, its 24 hour lifetime, terminal revocation, and the overloads that run in a caller's transaction.
[TestFixture]
public class DemoTransferTokenTests : McpFixture
{
    private static readonly TimeSpan Day = TimeSpan.FromHours(24);

    protected override bool WithProbeTools => false;

    [Test]
    public async Task Issue_WhenDemo_ShouldLastTwentyFourHours()
    {
        var item = await NewItemAsync();

        var issued = await WithTokens(t => t.IssueAsync(Transfer(TransferKind.Demo, item, Day)));

        var now = Host.Services.GetRequiredService<TimeProvider>().GetUtcNow();
        issued.ExpiresAt.ShouldBeInRange(now.AddHours(23.9), now.AddHours(24.1));
        (await WithTokens(t => t.FindAsync(TransferKind.Demo, issued.Token)))!.WorkItemId.ShouldBe(item);
    }

    [Test]
    public async Task Issue_WhenUpload_ShouldStillLastTenMinutes()
    {
        var item = await NewItemAsync();

        var issued = await WithTokens(t => t.IssueAsync(Transfer(TransferKind.Upload, item)));

        var now = Host.Services.GetRequiredService<TimeProvider>().GetUtcNow();
        issued.ExpiresAt.ShouldBeInRange(now.AddMinutes(9), now.AddMinutes(11));
    }

    [Test]
    public async Task Claim_WhenDemoTwice_ShouldSucceedOnce()
    {
        var tokenItem = await NewItemAsync();
        var token = (await WithTokens(t => t.IssueAsync(Transfer(TransferKind.Demo, tokenItem, Day)))).Token;

        var first = await WithTokens(t => t.ClaimAsync(token));
        var second = await WithTokens(t => t.ClaimAsync(token));

        (first, second).ShouldBe((true, false));
    }

    [Test]
    public async Task Release_WhenDemo_ShouldAllowAgain()
    {
        var tokenItem = await NewItemAsync();
        var token = (await WithTokens(t => t.IssueAsync(Transfer(TransferKind.Demo, tokenItem, Day)))).Token;
        (await WithTokens(t => t.ClaimAsync(token))).ShouldBeTrue();

        await WithTokens(t => t.ReleaseAsync(token));

        (await WithTokens(t => t.ClaimAsync(token))).ShouldBeTrue();
    }

    [Test]
    public async Task Release_WhenRevoked_ShouldStayDead()
    {
        var item = await NewItemAsync();
        var token = (await WithTokens(t => t.IssueAsync(Transfer(TransferKind.Demo, item, Day)))).Token;
        (await WithTokens(t => t.ClaimAsync(token))).ShouldBeTrue();
        await WithTokens(t => t.RevokeAsync(TransferKind.Demo, item));

        await WithTokens(t => t.ReleaseAsync(token));

        (await WithTokens(t => t.ClaimAsync(token))).ShouldBeFalse();
        (await WithTokens(t => t.FindAsync(TransferKind.Demo, token))).ShouldBeNull();
    }

    [Test]
    public async Task Revoke_ShouldKillEveryTokenOfThatKindOnly_EvenAClaimedOne()
    {
        var item = await NewItemAsync();
        var other = await NewItemAsync();
        var unused = (await WithTokens(t => t.IssueAsync(Transfer(TransferKind.Demo, item, Day)))).Token;
        var claimed = (await WithTokens(t => t.IssueAsync(Transfer(TransferKind.Demo, item, Day)))).Token;
        (await WithTokens(t => t.ClaimAsync(claimed))).ShouldBeTrue();
        var otherKind = (await WithTokens(t => t.IssueAsync(Transfer(TransferKind.DemoLogo, item, Day)))).Token;
        var otherItem = (await WithTokens(t => t.IssueAsync(Transfer(TransferKind.Demo, other, Day)))).Token;

        await WithTokens(t => t.RevokeAsync(TransferKind.Demo, item));

        (await WithTokens(t => t.FindAsync(TransferKind.Demo, unused))).ShouldBeNull();
        (await WithTokens(t => t.ClaimAsync(unused))).ShouldBeFalse();
        await using var connection = await OpenDatabaseAsync();
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM transfer_token WHERE work_item_id = @item AND kind = 'demo' AND revoked_at IS NOT NULL", new { item })).ShouldBe(2);
        (await WithTokens(t => t.FindAsync(TransferKind.DemoLogo, otherKind))).ShouldNotBeNull();
        (await WithTokens(t => t.FindAsync(TransferKind.Demo, otherItem))).ShouldNotBeNull();
    }

    [Test]
    public async Task Find_WhenKindDiffers_ShouldBeNull()
    {
        var tokenItem = await NewItemAsync();
        var token = (await WithTokens(t => t.IssueAsync(Transfer(TransferKind.Demo, tokenItem, Day)))).Token;

        (await WithTokens(t => t.FindAsync(TransferKind.Upload, token))).ShouldBeNull();
        (await WithTokens(t => t.FindAsync(TransferKind.DemoLogo, token))).ShouldBeNull();
        (await WithTokens(t => t.FindAsync(TransferKind.Demo, token))).ShouldNotBeNull();
    }

    [Test]
    public async Task DemoLogo_ShouldBeReusableUntilItExpires()
    {
        var tokenItem = await NewItemAsync();
        var token = (await WithTokens(t => t.IssueAsync(Transfer(TransferKind.DemoLogo, tokenItem, Day)))).Token;

        (await WithTokens(t => t.ClaimAsync(token))).ShouldBeFalse();
        (await WithTokens(t => t.FindAsync(TransferKind.DemoLogo, token))).ShouldNotBeNull();
        (await WithTokens(t => t.FindAsync(TransferKind.DemoLogo, token))).ShouldNotBeNull();

        var expiredItem = await NewItemAsync();

        var expired = (await WithTokens(t => t.IssueAsync(Transfer(TransferKind.DemoLogo, expiredItem, TimeSpan.FromSeconds(-1))))).Token;
        (await WithTokens(t => t.FindAsync(TransferKind.DemoLogo, expired))).ShouldBeNull();
    }

    [Test]
    public async Task Find_WhenAuthorizationRevoked_ShouldBeNullForDemo()
    {
        await TokenAsync(TestUser.Joel, ReadWrite);
        await using var connection = await OpenDatabaseAsync();
        var authorization = await connection.QueryFirstAsync<Guid>("SELECT id FROM oauth_authorization WHERE revoked_at IS NULL ORDER BY created_at DESC LIMIT 1");
        var authorizedItem = await NewItemAsync();
        var token = (await WithTokens(t => t.IssueAsync(Transfer(TransferKind.Demo, authorizedItem, Day) with { AuthorizationId = authorization }))).Token;
        (await WithTokens(t => t.FindAsync(TransferKind.Demo, token))).ShouldNotBeNull();

        await connection.ExecuteAsync("UPDATE oauth_authorization SET revoked_at = now() WHERE id = @authorization", new { authorization });

        (await WithTokens(t => t.FindAsync(TransferKind.Demo, token))).ShouldBeNull();
    }

    [Test]
    public async Task Issue_WhenTransactionRollsBack_ShouldLeaveNoToken()
    {
        var item = await NewItemAsync();
        await using var connection = await OpenDatabaseAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var issued = await WithTokens(t => t.IssueAsync(Transfer(TransferKind.Demo, item, Day), transaction));
        (await WithTokens(t => t.FindAsync(TransferKind.Demo, issued.Token))).ShouldBeNull();
        await transaction.RollbackAsync();

        (await WithTokens(t => t.FindAsync(TransferKind.Demo, issued.Token))).ShouldBeNull();
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM transfer_token WHERE work_item_id = @item", new { item })).ShouldBe(0);
    }

    [Test]
    public async Task Issue_WhenTransactionCommits_ShouldLeaveALiveToken()
    {
        var item = await NewItemAsync();
        await using var connection = await OpenDatabaseAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var issued = await WithTokens(t => t.IssueAsync(Transfer(TransferKind.Demo, item, Day), transaction));

        await transaction.CommitAsync();

        (await WithTokens(t => t.FindAsync(TransferKind.Demo, issued.Token))).ShouldNotBeNull();
    }

    [Test]
    public async Task Revoke_WhenTransactionRollsBack_ShouldLeaveTheTokenLive()
    {
        var item = await NewItemAsync();
        var token = (await WithTokens(t => t.IssueAsync(Transfer(TransferKind.Demo, item, Day)))).Token;
        await using var connection = await OpenDatabaseAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await WithTokens(t => t.RevokeAsync(TransferKind.Demo, item, transaction));
        await transaction.RollbackAsync();

        (await WithTokens(t => t.FindAsync(TransferKind.Demo, token))).ShouldNotBeNull();
    }

    [Test]
    public async Task IsRevoked_ShouldBlockOnAnUncommittedRevoke()
    {
        var item = await NewItemAsync();
        var token = (await WithTokens(t => t.IssueAsync(Transfer(TransferKind.Demo, item, Day)))).Token;
        await using var writer = await OpenDatabaseAsync();
        await using var revoking = await writer.BeginTransactionAsync();
        await WithTokens(t => t.RevokeAsync(TransferKind.Demo, item, revoking));
        await using var reader = await OpenDatabaseAsync();
        await using var reading = await reader.BeginTransactionAsync();
        await reader.ExecuteAsync("SET LOCAL lock_timeout = '500ms'", transaction: reading);

        var blocked = await Should.ThrowAsync<PostgresException>(async () => await WithTokens(t => t.IsRevokedAsync(token, reading)));
        await reading.RollbackAsync();
        await revoking.CommitAsync();
        await using var after = await reader.BeginTransactionAsync();

        blocked.SqlState.ShouldBe(PostgresErrorCodes.LockNotAvailable);
        (await WithTokens(t => t.IsRevokedAsync(token, after))).ShouldBeTrue();
    }

    [Test]
    public async Task IsRevoked_WhenNotRevoked_ShouldBeFalse()
    {
        var tokenItem = await NewItemAsync();
        var token = (await WithTokens(t => t.IssueAsync(Transfer(TransferKind.Demo, tokenItem, Day)))).Token;
        await using var connection = await OpenDatabaseAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        (await WithTokens(t => t.IsRevokedAsync(token, transaction))).ShouldBeFalse();
    }

    private async Task<Guid> NewItemAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        return await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("task").WithTitle("Demo token").InsertAsync(connection);
    }

    private async Task<T> WithTokens<T>(Func<ITransferTokens, Task<T>> action)
    {
        using var scope = Host.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<ITransferTokens>());
    }

    private async Task WithTokens(Func<ITransferTokens, Task> action)
        => await WithTokens(async t =>
        {
            await action(t);
            return 0;
        });

    private static NewTransfer Transfer(TransferKind kind, Guid item, TimeSpan? lifetime = null)
        => new(kind, item, null, "n", "video/mp4", SeedIds.Users.Joel, null, "ShipLean", lifetime);
}
