using CsCheck;
using ShipLean.Core;

namespace ShipLean.UnitTest.Core;

[TestFixture]
public class RankTests
{
    // Any digit string, including keys that end in 0 (the seed's do) and keys that are all zeros.
    private static readonly Gen<string> Keys = Gen.Char['0', '9'].Array[1, 12]
        .Select(chars => new string(chars))
        .Where(key => key.TrimEnd('0').Length > 0);

    private static bool HasRoomBetween(string low, string high)
        => string.CompareOrdinal(low, high) < 0 && low.TrimEnd('0') != high.TrimEnd('0');

    [Test]
    public void Rank_Between_ShouldAlwaysSortBetween()
    {
        Gen.Select(Keys, Keys)
            .Where(pair => HasRoomBetween(pair.Item1, pair.Item2))
            .Sample(pair =>
            {
                var key = Rank.Between(pair.Item1, pair.Item2);
                string.CompareOrdinal(pair.Item1, key).ShouldBeLessThan(0);
                string.CompareOrdinal(key, pair.Item2).ShouldBeLessThan(0);
                key.EndsWith('0').ShouldBeFalse();
                Rank.Between(pair.Item1, key).ShouldNotBeNull();
                Rank.Between(key, pair.Item2).ShouldNotBeNull();
            });
    }

    [TestCase(null, "00001000")]
    [TestCase("00001000", "00002000")]
    [TestCase("00001000", "00001001")]
    [TestCase("00000010", "00000020")]
    [TestCase("00009000", null)]
    [TestCase(null, "00000001")]
    public void Rank_WhenNeighboursEndInZeros_ShouldLeaveRoomForTheNextInsert(string? before, string? after)
    {
        var key = Rank.Between(before, after);

        key.EndsWith('0').ShouldBeFalse();
        if (before is not null)
        {
            string.CompareOrdinal(before, key).ShouldBeLessThan(0);
        }

        if (after is not null)
        {
            string.CompareOrdinal(key, after).ShouldBeLessThan(0);
        }

        var next = Rank.Between(before, key);
        string.CompareOrdinal(next, key).ShouldBeLessThan(0);
        Rank.Between(key, after).ShouldNotBe(key);
    }

    [Test]
    public void Rank_WhenInsertingRepeatedlyAtTheFrontOfSeedKeys_ShouldNeverRunOutOfRoom()
    {
        var first = "00001000";
        for (var i = 0; i < 50; i++)
        {
            var key = Rank.Between(null, first);
            string.CompareOrdinal(key, first).ShouldBeLessThan(0);
            first = key;
        }
    }

    [Test]
    public void Rank_BetweenWhenOpenEnded_ShouldSortAfterOrBeforeTheNeighbour()
    {
        Keys.Sample(key =>
        {
            var after = Rank.Between(key, null);
            var before = Rank.Between(null, key);
            string.CompareOrdinal(key, after).ShouldBeLessThan(0);
            string.CompareOrdinal(before, key).ShouldBeLessThan(0);
            before.EndsWith('0').ShouldBeFalse();
        });
    }

    [Test]
    public void Rank_WhenNoNeighbours_ShouldGiveAMiddleKey()
    {
        Rank.Between(null, null).ShouldBe("5");
    }

    [Test]
    public void Rank_WhenNeighboursAreSeedNumbers_ShouldSortBetweenThem()
    {
        var key = Rank.Between("00000010", "00000020");

        string.CompareOrdinal("00000010", key).ShouldBeLessThan(0);
        string.CompareOrdinal(key, "00000020").ShouldBeLessThan(0);
        string.CompareOrdinal(Rank.Between(null, "00000001"), "00000001").ShouldBeLessThan(0);
    }

    [Test]
    public void Rank_WhenKeysAreInsertedAtTheFrontRepeatedly_ShouldStayOrdered()
    {
        var keys = new List<string> { Rank.Between(null, null) };
        for (var i = 0; i < 200; i++)
        {
            keys.Insert(0, Rank.Between(null, keys[0]));
        }

        keys.ShouldBe(keys.Order(StringComparer.Ordinal).ToList());
        keys.Distinct().Count().ShouldBe(keys.Count);
    }

    [Test]
    public void Rank_WhenBeforeIsNotAheadOfAfter_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() => Rank.Between("5", "5"));
        Should.Throw<ArgumentException>(() => Rank.Between("6", "5"));
        Should.Throw<ArgumentException>(() => Rank.Between("a", null));
        Should.Throw<ArgumentException>(() => Rank.Between(null, "0000"));
        Should.Throw<InvalidOperationException>(() => Rank.Between("5", "50"));
    }
}
