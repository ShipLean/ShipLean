using ShipLean.Core.People;

namespace ShipLean.UnitTest.Core.People;

[TestFixture]
public class InitialsTests
{
    [TestCase("Joel Dickson", "JD")]
    [TestCase("Émile Ray", "ER")]
    [TestCase("Øyvind Ek", "E")]
    [TestCase("ana", "A")]
    [TestCase("42", "U")]
    [TestCase("Ada Lovelace", "AL")]
    [TestCase("Ada Lovelace", "AL")]
    public void From_WhenNameGiven_ShouldTakeFirstAndLastWordsLetters(string name, string expected)
    {
        Initials.From(name).ShouldBe(expected);
    }
}
