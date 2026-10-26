using ShipLean.Application.Features.Search.Ranking;

namespace ShipLean.UnitTest.Search.Lexical;

[TestFixture]
public class SearchTsQueryTests
{
    [TestCase("login broken", null, "login or broken", null)]
    [TestCase("login ", "'broken':*", "login", "'broken':*")]
    [TestCase("login ", "'bro':*", "login", "'bro':*")]
    [TestCase("a-b c_d", null, "a-b or c_d", null)]
    public void AnyWords_WhenPlainWords_ShouldJoinWithOr(string head, string? last, string expectedHead, string? expectedLast)
    {
        SearchTsQuery.AnyWords(head, last).ShouldBe((expectedHead, expectedLast));
    }

    [Test]
    public void AnyWords_WhenPrefixUnderThreeCharacters_ShouldLeaveItOut()
    {
        SearchTsQuery.AnyWords("login broken ", "'r':*").ShouldBe(("login or broken", null));
    }

    [TestCase("login", null)]
    [TestCase("", null)]
    [TestCase("\"login broken\" crash", null)]
    [TestCase("login -broken", null)]
    [TestCase("login or broken", null)]
    [TestCase("login ", "'br':*")]
    public void AnyWords_WhenNothingToLoosen_ShouldBeNull(string head, string? last)
    {
        SearchTsQuery.AnyWords(head, last).ShouldBeNull();
    }
}
