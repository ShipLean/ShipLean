namespace ShipLean.WebApi.Tests.Search.Index.Service;

// The first pass comes from the timer, not an event: a full pass with nothing to sync first.
[TestFixture]
public class SearchIndexHostedServiceTimerTests : ServiceFixture
{
    protected override string FirstPassSeconds => "1";

    [Test]
    public async Task Service_WhenFirstPassDelayElapses_ShouldRunAFullPassWithoutSyncing()
    {
        (await Indexer.NextPassAsync()).ShouldBeTrue();

        Writer.SyncAllCalls.ShouldBe(0);
    }
}
