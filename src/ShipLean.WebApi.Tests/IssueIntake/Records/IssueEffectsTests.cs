using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shouldly;
using ShipLean.Contracts.IssueIntake;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.IssueIntake.Records;

// The real effect ledger on a seeded clone. Each test claims its own keys.
[TestFixture]
public class IssueEffectsTests : ApiFixture
{
    private IIssueEffects Effects => Host.Services.GetRequiredService<IIssueEffects>();

    private NpgsqlDataSource DataSource => Host.Services.GetRequiredService<NpgsqlDataSource>();

    private static string NewKey(string what) => $"test:{what}:{Guid.NewGuid():N}";

    [Test]
    public async Task TryClaim_WhenClaimedTwice_ShouldGrantOnceAndKeepTheFirstResult()
    {
        var key = NewKey("twice");
        var first = Guid.NewGuid();

        (await Effects.TryClaimAsync(key, null, first)).ShouldBeTrue();
        (await Effects.TryClaimAsync(key, null, Guid.NewGuid())).ShouldBeFalse();

        (await Effects.FindAsync(key)).ShouldBe(first);
    }

    [Test]
    public async Task TryClaim_WhenConcurrent_ShouldGrantOnce()
    {
        var key = NewKey("concurrent");
        await using var firstConnection = await DataSource.OpenConnectionAsync();
        await using var secondConnection = await DataSource.OpenConnectionAsync();
        await using var firstTransaction = await firstConnection.BeginTransactionAsync();
        await using var secondTransaction = await secondConnection.BeginTransactionAsync();

        var first = await Effects.TryClaimAsync(key, null, Guid.NewGuid(), firstTransaction);
        // The second claim waits on the first transaction's uncommitted row until it commits.
        var second = Effects.TryClaimAsync(key, null, Guid.NewGuid(), secondTransaction);
        await firstTransaction.CommitAsync();

        first.ShouldBeTrue();
        (await second).ShouldBeFalse();
        await secondTransaction.CommitAsync();
    }

    [Test]
    public async Task TryClaim_WhenTransactionRollsBack_ShouldNotKeepClaim()
    {
        var key = NewKey("rollback");
        await using (var connection = await DataSource.OpenConnectionAsync())
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            (await Effects.TryClaimAsync(key, null, Guid.NewGuid(), transaction)).ShouldBeTrue();
            await transaction.RollbackAsync();
        }

        (await Effects.FindAsync(key)).ShouldBeNull();
        var retried = Guid.NewGuid();
        (await Effects.TryClaimAsync(key, null, retried)).ShouldBeTrue();
        (await Effects.FindAsync(key)).ShouldBe(retried);
    }
}
