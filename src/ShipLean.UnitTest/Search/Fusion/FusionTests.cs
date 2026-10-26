using ShipLean.Application.Features.Search.Ranking;
using ShipLean.Application.Features.Search.Ranking.Fusion;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Search;

namespace ShipLean.UnitTest.Search.Fusion;

public sealed class FusionTests
{
    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();

    private static CandidateHit Hit(Guid id, SearchField field = SearchField.Title) => new(id, field, 1, "snippet");

    [Test]
    public void Fusion_WhenItemInSeveralLists_ShouldOutrankSingleList()
    {
        var lists = new[]
        {
            new RankedList(RankedListName.TitleLexical, [Hit(B), Hit(A)]),
            new RankedList(RankedListName.TitleVector, [Hit(A)]),
        };

        var fused = ReciprocalRankFusion.Fuse(lists, 60);

        fused[A].Score.ShouldBeGreaterThan(fused[B].Score);
        fused[A].Score.ShouldBe((1.0 / 62) + (1.0 / 61), 1e-12);
    }

    [Test]
    public void Fusion_WhenRanksTie_ShouldTakeTheLexicalTitleHitWhateverTheOrder()
    {
        var lists = new[]
        {
            new RankedList(RankedListName.BodyVector, [new CandidateHit(A, SearchField.Body, 1, "vector body")]),
            new RankedList(RankedListName.BodyLexical, [new CandidateHit(A, SearchField.Body, 1, "lexical body")]),
            new RankedList(RankedListName.TitleLexical, [new CandidateHit(A, SearchField.Title, 1, "lexical title")]),
        };

        ReciprocalRankFusion.Fuse(lists, 60)[A].Best.Snippet.ShouldBe("lexical title");
        ReciprocalRankFusion.Fuse([.. lists.Reverse()], 60)[A].Best.Snippet.ShouldBe("lexical title");
    }

    [Test]
    public void Fusion_WhenAHitRanksHigherInALaterList_ShouldTakeIt()
    {
        var lists = new[]
        {
            new RankedList(RankedListName.TitleLexical, [Hit(B), new CandidateHit(A, SearchField.Title, 1, "lexical")]),
            new RankedList(RankedListName.BodyVector, [new CandidateHit(A, SearchField.Body, 1, "vector")]),
        };

        ReciprocalRankFusion.Fuse(lists, 60)[A].Best.Snippet.ShouldBe("vector");
    }

    [TestCase("SHIP-12", "SHIP-12")]
    [TestCase("ship-12", "SHIP-12")]
    [TestCase("ship12", "SHIP-12")]
    [TestCase("  Ship12 ", "SHIP-12")]
    [TestCase("ab-007", "AB-7")]
    // Words that look like keys parse too; the ranker still needs a member project with that key and an item.
    [TestCase("mp3", "MP-3")]
    [TestCase("utf8", "UTF-8")]
    [TestCase("SHIP-999999999", "SHIP-999999999")]
    public void ExactKey_WhenKeyShaped_ShouldNormalise(string text, string expected)
    {
        ExactKey.TryParse(text, out var key).ShouldBeTrue();
        key.ShouldBe(expected);
    }

    [TestCase("login")]
    [TestCase("SHIP-12 login")]
    [TestCase("SHIPS-12")]
    [TestCase("12")]
    [TestCase("")]
    [TestCase("v2")]
    [TestCase("SHIP-1234567890")]
    [TestCase("1234567890")]
    [TestCase("SHIP-")]
    public void ExactKey_WhenNotAKey_ShouldNotParse(string text)
    {
        ExactKey.TryParse(text, out _).ShouldBeFalse();
    }

    [Test]
    public void Boosts_WhenEverythingBoosts_ShouldStillLoseToAStrongTextMatch()
    {
        DateTimeOffset now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
        var me = Guid.NewGuid();
        var project = Guid.NewGuid();
        var strong = ReciprocalRankFusion.Fuse([new RankedList(RankedListName.TitleLexical, [Hit(A)])], 60)[A].Score;
        var weak = ReciprocalRankFusion.Fuse([new RankedList(RankedListName.TitleLexical, [.. Enumerable.Range(0, 99).Select(_ => Hit(Guid.NewGuid())), Hit(B)])], 60)[B].Score;
        var boosted = SearchBoosts.Factor(new ItemFacts(B, project, StateCategory.InProgress, me, now), me, project, now);
        var unboosted = SearchBoosts.Factor(new ItemFacts(A, Guid.NewGuid(), StateCategory.Completed, null, now.AddDays(-90)), me, project, now);

        unboosted.ShouldBe(1.0);
        (strong * unboosted).ShouldBeGreaterThan(weak * boosted);
    }

    [Test]
    public void Boosts_WhenOpenAndClosedTieOnText_ShouldFavourOpen()
    {
        DateTimeOffset now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
        var open = SearchBoosts.Factor(new ItemFacts(A, Guid.NewGuid(), StateCategory.InProgress, null, now.AddDays(-90)), Guid.NewGuid(), null, now);
        var closed = SearchBoosts.Factor(new ItemFacts(B, Guid.NewGuid(), StateCategory.Completed, null, now.AddDays(-90)), Guid.NewGuid(), null, now);

        open.ShouldBeGreaterThan(closed);
    }
}
