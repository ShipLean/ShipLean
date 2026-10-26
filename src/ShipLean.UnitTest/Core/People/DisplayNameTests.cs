using ShipLean.Core.People;

namespace ShipLean.UnitTest.Core.People;

[TestFixture]
public class DisplayNameTests
{
    [TestCase("Joel Dickson")]
    [TestCase("Ada Lovelace")]
    [TestCase("Dev \U0001F469‍\U0001F4BB")]
    [TestCase("Zoë Ångström-Ng")]
    public void Problem_WhenNameIsFine_ShouldBeNull(string name)
    {
        DisplayName.Problem(name).ShouldBeNull();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Problem_WhenBlank_ShouldSayToEnterAName(string? name)
    {
        DisplayName.Problem(name).ShouldBe("Enter a name.");
    }

    [Test]
    public void Problem_WhenExactlyTheLimit_ShouldBeNullAndOneMoreShouldBeRefused()
    {
        DisplayName.Problem(new string('a', DisplayName.MaxLength)).ShouldBeNull();
        DisplayName.Problem(new string('a', DisplayName.MaxLength + 1)).ShouldNotBeNull();
    }

    [TestCase("a\u0007b")]
    [TestCase("a\nb")]
    [TestCase("‮evil")]
    [TestCase("‪evil")]
    [TestCase("‬evil")]
    [TestCase("a⁦b")]
    [TestCase("a⁩b")]
    [TestCase("Joel​Dickson")]
    [TestCase("Joel‌Dickson")]
    [TestCase("Joel‎Dickson")]
    [TestCase("Joel‏Dickson")]
    [TestCase("Joel﻿Dickson")]
    public void Problem_WhenNameHasControlOrInvisibleFormatting_ShouldBeRefused(string name)
    {
        DisplayName.Problem(name).ShouldNotBeNull();
    }
}
