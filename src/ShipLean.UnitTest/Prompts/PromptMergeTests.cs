using CsCheck;
using ShipLean.Application.Features.Prompts.Updates;

namespace ShipLean.UnitTest.Prompts;

[TestFixture]
public class PromptMergeTests
{
    private const string Base = "one\ntwo\nthree\nfour\nfive\nsix\nseven\n";

    private static readonly Gen<string> Texts =
        Gen.Select(Gen.String[Gen.Char["abc"], 0, 3].Array[0, 12], Gen.Bool)
            .Select(t => string.Join('\n', t.Item1) + (t.Item2 ? "\n" : ""));

    [Test]
    public void Merge_WhenTheirsOnlyChanged_ShouldTakeTheirs()
    {
        var result = PromptMerge.Merge(Base, Base, "one\ntwo\nTHREE\nfour\nfive\nsix\nseven\n");

        result.ShouldBe(new PromptMergeResult("one\ntwo\nTHREE\nfour\nfive\nsix\nseven\n", true));
    }

    [Test]
    public void Merge_WhenMineOnlyChanged_ShouldKeepMine()
    {
        var mine = "one\ntwo\nthree\nfour\nfive\nsix\nseven\nextra rule\n";

        PromptMerge.Merge(Base, mine, Base).ShouldBe(new PromptMergeResult(mine, true));
    }

    [Test]
    public void Merge_WhenChangeMissesProjectLines_ShouldApplyCleanly()
    {
        var mine = Base + "project rule a\nproject rule b\n";
        var theirs = "one\ntwo\nthree reworded\nfour\nfive\nsix\nseven\n";

        PromptMerge.Merge(Base, mine, theirs).ShouldBe(new PromptMergeResult("one\ntwo\nthree reworded\nfour\nfive\nsix\nseven\nproject rule a\nproject rule b\n", true));
    }

    [Test]
    public void Merge_WhenBothChangedTheSameLineDifferently_ShouldConflictWithMarkers()
    {
        var result = PromptMerge.Merge(Base, "one\ntwo\nmine\nfour\nfive\nsix\nseven\n", "one\ntwo\ntheirs\nfour\nfive\nsix\nseven\n");

        result.ShouldBe(new PromptMergeResult("one\ntwo\n<<<<<<< yours\nmine\n=======\ntheirs\n>>>>>>> built-in\nfour\nfive\nsix\nseven\n", false));
    }

    [Test]
    public void Merge_WhenBothMadeTheSameChange_ShouldBeCleanOnce()
    {
        var changed = "one\ntwo\nsame\nfour\nfive\nsix\nseven\n";

        PromptMerge.Merge(Base, changed, changed).ShouldBe(new PromptMergeResult(changed, true));
    }

    [Test]
    public void Merge_WhenAdjacentEdits_ShouldConflict()
    {
        var result = PromptMerge.Merge(Base, "one\ntwo\nmine\nfour\nfive\nsix\nseven\n", "one\ntwo\nthree\nTHEIRS\nfive\nsix\nseven\n");

        result.Clean.ShouldBeFalse();
        result.Text.ShouldContain("<<<<<<< yours\n");
    }

    [Test]
    public void Merge_WhenOneSideLacksTheFinalNewline_ShouldStillStartMarkersOnTheirOwnLine()
    {
        var result = PromptMerge.Merge("a\nb", "a\nmine", "a\ntheirs");

        result.ShouldBe(new PromptMergeResult("a\n<<<<<<< yours\nmine\n=======\ntheirs\n>>>>>>> built-in\n", false));
    }

    [Test]
    public void Merge_WhenOnlyTheBuiltInChanged_ShouldTakeItAndBeClean()
    {
        Gen.Select(Texts, Texts).Sample(t => PromptMerge.Merge(t.Item1, t.Item1, t.Item2).ShouldBe(new PromptMergeResult(t.Item2, true)));
    }

    [Test]
    public void Merge_WhenOnlyTheCustomChanged_ShouldKeepItAndBeClean()
    {
        Gen.Select(Texts, Texts).Sample(t => PromptMerge.Merge(t.Item1, t.Item2, t.Item1).ShouldBe(new PromptMergeResult(t.Item2, true)));
    }

    [Test]
    public void Merge_WhenBothSidesAreTheSame_ShouldTakeItOnceAndBeClean()
    {
        Gen.Select(Texts, Texts).Sample(t => PromptMerge.Merge(t.Item1, t.Item2, t.Item2).ShouldBe(new PromptMergeResult(t.Item2, true)));
    }

    [Test]
    public void Merge_When32000ShortLinesAllDiffer_ShouldFinish()
    {
        var baseText = string.Concat(Enumerable.Repeat("a\n", 32000));
        var mine = string.Concat(Enumerable.Repeat("b\n", 32000));
        var theirs = string.Concat(Enumerable.Repeat("c\n", 32000));

        var result = PromptMerge.Merge(baseText, mine, theirs);

        result.Clean.ShouldBeFalse();
        result.Text.ShouldStartWith("<<<<<<< yours\n");
    }
}
