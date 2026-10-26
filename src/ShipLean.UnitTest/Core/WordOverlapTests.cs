using ShipLean.Core;

namespace ShipLean.UnitTest.Core;

[TestFixture]
public class WordOverlapTests
{
    [Test]
    public void WordOverlap_WhenStopWordsOnly_ShouldScoreZero()
    {
        WordOverlap.Score("The end of the road", ["the", "of", "a", "and"]).ShouldBe(0);
        WordOverlap.Score("", ["pay"]).ShouldBe(0);
        WordOverlap.Score("Pay now", []).ShouldBe(0);
    }

    [Test]
    public void WordOverlap_ShouldRankMoreSharedWordsFirst()
    {
        string[] words = ["pay", "saved", "card"];
        string[] titles = ["Pay with saved card", "Delete saved card", "Gift card balance", "Unrelated"];

        titles.OrderByDescending(t => WordOverlap.Score(t, words)).ToArray().ShouldBe(["Pay with saved card", "Delete saved card", "Gift card balance", "Unrelated"]);
        WordOverlap.Score("Pay with saved card", words).ShouldBe(3);
    }

    [Test]
    public void WordOverlap_ShouldIgnoreCasePunctuationAndRepeats()
    {
        WordOverlap.Score("Card, CARD; card-vault v2!", ["Card", "vault", "v2", "card"]).ShouldBe(3);
        WordOverlap.Score("Sign-up page", ["sign up"]).ShouldBe(2);
    }

    [Test]
    public void WordOverlap_WhenPossessives_ShouldNotShareSingleLetters()
    {
        WordOverlap.Score("User's card", ["don't", "x"]).ShouldBe(0);
        WordOverlap.Score("Plan 9", ["9"]).ShouldBe(1);
        WordOverlap.Score("Caf\u00e9 menu", ["cafe\u0301"]).ShouldBe(1);
    }
}
