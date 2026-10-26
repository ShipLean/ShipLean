using ShipLean.Core;

namespace ShipLean.UnitTest.Core;

[TestFixture]
public class SlugsTests
{
    [TestCase("Checkout v2", "checkout-v2")]
    [TestCase("  Saved   cards -- & Apple Pay!  ", "saved-cards-apple-pay")]
    [TestCase("Café über résumé", "cafe-uber-resume")]
    [TestCase("---", "")]
    [TestCase("", "")]
    [TestCase("日本語", "")]
    public void FromTitle_ShouldBuildLowerCaseHyphenatedWords(string title, string expected)
    {
        Slugs.FromTitle(title).ShouldBe(expected);
    }

    [Test]
    public void FromTitle_WhenTooLong_ShouldCutAtTheLimitWithoutATrailingHyphen()
    {
        var title = string.Join(' ', Enumerable.Repeat("abcdefg", 12));

        var slug = Slugs.FromTitle(title);

        slug.Length.ShouldBe(Slugs.MaxLength - 1);
        slug.ShouldEndWith("g");
        Slugs.IsValid(slug).ShouldBeTrue();
    }

    [TestCase("checkout-v2", true)]
    [TestCase("a", true)]
    [TestCase("2fa", true)]
    [TestCase("", false)]
    [TestCase(null, false)]
    [TestCase("Checkout", false)]
    [TestCase("checkout--v2", false)]
    [TestCase("-checkout", false)]
    [TestCase("checkout-", false)]
    [TestCase("check out", false)]
    [TestCase("café", false)]
    public void IsValid_ShouldFollowThePattern(string? slug, bool expected)
    {
        Slugs.IsValid(slug).ShouldBe(expected);
    }

    [Test]
    public void IsValid_ShouldAllowExactly64Characters()
    {
        Slugs.IsValid(new string('a', 64)).ShouldBeTrue();
        Slugs.IsValid(new string('a', 65)).ShouldBeFalse();
    }

    [Test]
    public void FromTitle_ShouldAlwaysBeValidWhenNotEmpty()
    {
        foreach (var title in new[] { "Checkout v2", "A -- B", "Ünï cödé 12", new string('x', 200) })
        {
            Slugs.IsValid(Slugs.FromTitle(title)).ShouldBeTrue(title);
        }
    }
}
