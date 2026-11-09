using ShipLean.Contracts.Backups.Dtos;

namespace ShipLean.WebApi.Tests.Backups.EndToEnd;

// SHIP-801 J1: Keep the last N, through the real job and a real bucket.
[TestFixture]
public class BackupPruneTests : BackupRoundTripFixture
{
    [Test]
    public async Task BackupRun_WhenKeepCountExceeded_ShouldPruneOldestDump()
    {
        await SaveAsync("correct horse battery staple", keepCount: 1, includeAttachments: false);

        var first = await RunNowAsync();
        var second = await RunNowAsync();

        first.State.ShouldBe(BackupRunState.Succeeded, first.Error);
        second.State.ShouldBe(BackupRunState.Succeeded, second.Error);
        first.DatabaseObjectKey.ShouldNotBe(second.DatabaseObjectKey);
        // The run turns succeeded before the prune, so the history settles a moment later.
        var settings = await SettingsWhereAsync(s => s.Runs.Single(r => r.Id == first.Id).Pruned);
        settings.Runs.Single(r => r.Id == second.Id).Pruned.ShouldBeFalse();
        (await KeysWhenAsync($"{BackupPrefix}database/", [second.DatabaseObjectKey!])).ShouldBe([second.DatabaseObjectKey!]);
    }
}
