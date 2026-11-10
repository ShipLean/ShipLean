using ShipLean.Application.Features.Inbox;

namespace ShipLean.UnitTest.Inbox;

// D2 and D3: the Inbox row order. The offsets are hours before a shared "now", so a larger number has waited longer.
[TestFixture]
public class InboxOrderingTests
{
    private static readonly DateTimeOffset Now = new DateTimeOffset(TimeProvider.System.GetUtcNow().UtcDateTime.Date, TimeSpan.Zero);

    private static InboxOrderEntry Entry(Guid id, int hoursAgo, Guid? featureId = null) => new(id, featureId, Now.AddHours(-hoursAgo));

    private static IEnumerable<(Guid Id, Guid? UnderId)> Placed(params InboxOrderEntry[] entries) =>
        InboxOrdering.Order(entries).Select(p => (p.Id, p.UnderId));

    [Test]
    public void Order_WhenItemsWaitedDifferently_ShouldPutLongestWaitingFirst()
    {
        var (short1, long1, middle) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        Placed(Entry(short1, 2), Entry(long1, 50), Entry(middle, 10)).Select(p => p.Id).ShouldBe([long1, middle, short1]);
    }

    [Test]
    public void Order_WhenStoryAndItsFeatureBothHaveRows_ShouldPutStoryStraightAfterFeature()
    {
        var (feature, story, other) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // The other item (35h) waited longer than the story (30h), but the story follows its Feature (40h).
        Placed(Entry(story, 30, feature), Entry(other, 35), Entry(feature, 40)).ToList()
            .ShouldBe([(feature, (Guid?)null), (story, feature), (other, null)]);
    }

    [Test]
    public void Order_WhenStoryAskIsOlderThanFeatureAsk_ShouldPlacePairByStoryAsk()
    {
        var (feature, story, other) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // The pair sits at the story's 60h, ahead of the other item's 50h, yet the Feature row still leads the pair.
        Placed(Entry(other, 50), Entry(feature, 10), Entry(story, 60, feature)).ToList()
            .ShouldBe([(feature, (Guid?)null), (story, feature), (other, null)]);
    }

    [Test]
    public void Order_WhenSeveralStoriesUnderOneFeature_ShouldOrderThemByOldestAt()
    {
        var feature = Guid.NewGuid();
        var (newest, oldest, middle) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // The Feature (10h) is newer than its Stories, yet leads them: they are siblings after it, oldest first.
        Placed(Entry(newest, 5, feature), Entry(feature, 10), Entry(oldest, 90, feature), Entry(middle, 50, feature))
            .Select(p => p.Id).ShouldBe([feature, oldest, middle, newest]);
    }

    [Test]
    public void Order_WhenStoryFeatureHasNoRow_ShouldStandAloneWithNoUnderKey()
    {
        var (story, other) = (Guid.NewGuid(), Guid.NewGuid());

        Placed(Entry(other, 5), Entry(story, 20, Guid.NewGuid())).ToList().ShouldBe([(story, (Guid?)null), (other, null)]);
    }
}
