using ShipLean.Application.Identity;

namespace ShipLean.UnitTest.Application.Identity;

// UserInput's rules for values an identity provider sent, with null in place of a validation error.
[TestFixture]
public class IdentityInputTests
{
    [Test]
    public void Email_WhenValid_ShouldReturnItTrimmed()
    {
        IdentityInput.Email("  mei@example.test ").ShouldBe("mei@example.test");
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("mei")]
    [TestCase("@example.test")]
    [TestCase("mei@")]
    [TestCase("mei@exa mple.test")]
    [TestCase("mei@one@two.test")]
    public void Email_WhenInvalid_ShouldReturnNull(string? raw)
    {
        IdentityInput.Email(raw).ShouldBeNull();
    }

    [Test]
    public void Email_WhenTooLong_ShouldReturnNull()
    {
        IdentityInput.Email(new string('a', 250) + "@example.test").ShouldBeNull();
    }

    [Test]
    public void Name_WhenValid_ShouldReturnItTrimmed()
    {
        IdentityInput.Name("  Zoë Müller ").ShouldBe("Zoë Müller");
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Name_WhenBlank_ShouldReturnNull(string? raw)
    {
        IdentityInput.Name(raw).ShouldBeNull();
    }

    [TestCase("Mei\u0000")]
    [TestCase("Mei​ Tanaka")]
    public void Name_WhenRefused_ShouldReturnNull(string raw)
    {
        IdentityInput.Name(raw).ShouldBeNull();
    }

    [Test]
    public void Name_WhenTooLong_ShouldReturnNull()
    {
        IdentityInput.Name(new string('a', 101)).ShouldBeNull();
    }
}
