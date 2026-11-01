using ShipLean.Application.Features.Retros.Theming;

namespace ShipLean.UnitTest.Retros;

[TestFixture]
public class ThemeNamerTests
{
    [Test]
    public void Name_WhenSeveralSentences_ShouldUseTheFirstWithoutItsPunctuation()
    {
        ThemeNamer.Name("Builds waited on the lock. Nobody noticed for an hour.", []).ShouldBe("Builds waited on the lock");
    }

    [Test]
    public void Name_WhenTrailingPunctuation_ShouldDropIt()
    {
        ThemeNamer.Name("  The proxy broke sign-in?! ", []).ShouldBe("The proxy broke sign-in");
    }

    [Test]
    public void Name_WhenClosingBracketIsMatched_ShouldKeepIt()
    {
        ThemeNamer.Name("Tests flaky (again).", []).ShouldBe("Tests flaky (again)");
    }

    [Test]
    public void Name_WhenClosingBracketOrQuoteIsUnmatched_ShouldDropIt()
    {
        ThemeNamer.Name("Deploys slow) today\"", []).ShouldBe("Deploys slow) today");
    }

    [Test]
    public void Name_WhenMoreThanSixWords_ShouldKeepSix()
    {
        ThemeNamer.Name("one two three four five six seven eight", []).ShouldBe("one two three four five six");
    }

    [Test]
    public void Name_WhenOver48Characters_ShouldCutAtAWordBoundary()
    {
        var name = ThemeNamer.Name("Infrastructure provisioning orchestration reconfiguration problems", []);

        name.Length.ShouldBeLessThanOrEqualTo(48);
        name.ShouldBe("Infrastructure provisioning orchestration");
    }

    [Test]
    public void Name_WhenOneWordOver48Characters_ShouldCutHard()
    {
        ThemeNamer.Name(new string('a', 60), []).ShouldBe(new string('a', 48));
    }

    [Test]
    public void Name_WhenNameTaken_ShouldNumberIt()
    {
        ThemeNamer.Name("Flaky waits", ["flaky waits"]).ShouldBe("Flaky waits (2)");
    }

    [Test]
    public void Name_WhenNumberedNameTaken_ShouldTakeTheNextNumber()
    {
        ThemeNamer.Name("Flaky waits", ["Flaky waits", "Flaky waits (2)"]).ShouldBe("Flaky waits (3)");
    }

    [Test]
    public void Name_WhenCollisionOnAMaxLengthName_ShouldStayWithin48Characters()
    {
        var taken = ThemeNamer.Name(new string('a', 60), []);

        var name = ThemeNamer.Name(new string('a', 60), [taken]);

        name.Length.ShouldBeLessThanOrEqualTo(48);
        name.ShouldEndWith(" (2)");
    }

    [Test]
    public void Name_WhenNoWords_ShouldFallBack()
    {
        ThemeNamer.Name("  ... ", []).ShouldBe("Untitled theme");
    }
}
