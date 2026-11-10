using ShipLean.Application.Features.Inbox;

namespace ShipLean.UnitTest.Inbox;

// P8: who a comment body @mentions by full name. Tom and Tom Okafor share a prefix on purpose.
[TestFixture]
public class MentionTextTests
{
    private static readonly MentionCandidate TomOkafor = new(Guid.NewGuid(), "Tom Okafor");
    private static readonly MentionCandidate Tom = new(Guid.NewGuid(), "Tom");
    private static readonly MentionCandidate PriyaShah = new(Guid.NewGuid(), "Priya Shah");
    private static readonly MentionCandidate MeiLin = new(Guid.NewGuid(), "Mei Lin");

    private static readonly IReadOnlyList<MentionCandidate> Members = [Tom, TomOkafor, PriyaShah, MeiLin];

    private static IReadOnlyList<Guid> Find(string body, Guid? exclude = null) => MentionText.Find(body, Members, exclude);

    [TestCase("@Priya Shah can you check this?")]
    [TestCase("Thanks.\n@Priya Shah can you check this?")]
    [TestCase("Over to @Priya Shah")]
    [TestCase("Over to\t@Priya Shah ")]
    public void Find_WhenFullNameFollowsAt_ShouldReturnUser(string body)
    {
        Find(body).ShouldBe([PriyaShah.UserId]);
    }

    [Test]
    public void Find_WhenTwoNamesShareAPrefix_ShouldPickLongest()
    {
        Find("@Tom Okafor please look, and @Tom too").ShouldBe([TomOkafor.UserId, Tom.UserId]);
    }

    [TestCase("@priya shah can you check this?")]
    [TestCase("@PRIYA SHAH can you check this?")]
    public void Find_WhenCaseDiffers_ShouldStillMatch(string body)
    {
        Find(body).ShouldBe([PriyaShah.UserId]);
    }

    [TestCase("x@Priya Shah")]
    [TestCase("hi@Priya Shah")]
    [TestCase("Ask,@Priya Shah")]
    public void Find_WhenAtFollowsALetter_ShouldNotMatch(string body)
    {
        Find(body).ShouldBeEmpty();
    }

    [TestCase("Thanks @Priya Shah.", "Priya Shah")]
    [TestCase("@Priya Shah, can you?", "Priya Shah")]
    [TestCase("@Priya Shah; then ship", "Priya Shah")]
    [TestCase("@Priya Shah: over to you", "Priya Shah")]
    [TestCase("Ready @Priya Shah!", "Priya Shah")]
    [TestCase("Ready @Priya Shah?", "Priya Shah")]
    [TestCase("(ask @Priya Shah)", "Priya Shah")]
    [TestCase("@Tom Okafor, Lee and I agree", "Tom Okafor")]
    public void Find_WhenNameEndsWithPunctuation_ShouldMatch(string body, string name)
    {
        Find(body).ShouldBe([Members.Single(m => m.Name == name).UserId]);
    }

    [TestCase("@Priya Shahs")]
    [TestCase("@Priya Shah-Jones")]
    [TestCase("@Priya Sha")]
    public void Find_WhenNameRunsOnPastTheBoundary_ShouldNotMatch(string body)
    {
        Find(body).ShouldBeEmpty();
    }

    [TestCase("Run `@Priya Shah` here")]
    [TestCase("Run ``say @Priya Shah`` here")]
    [TestCase("```\n@Priya Shah\n```")]
    [TestCase("Before\n~~~ text\nlook @Priya Shah\n~~~\n")]
    [TestCase("````\n```\n@Priya Shah\n````")]
    [TestCase("```\nnever closed\n@Priya Shah")]
    [TestCase("  ```\n@Priya Shah\n  ```")]
    public void Find_WhenInInlineCodeOrFencedBlock_ShouldIgnore(string body)
    {
        Find(body).ShouldBeEmpty();
    }

    [TestCase("`code` then @Mei Lin")]
    [TestCase("```\n@Priya Shah\n```\n@Mei Lin after the block")]
    [TestCase("an unclosed ` then @Mei Lin")]
    public void Find_WhenOutsideCode_ShouldStillMatch(string body)
    {
        Find(body).ShouldBe([MeiLin.UserId]);
    }

    [TestCase("Write to tom@Tom Okafor.dev")]
    [TestCase("Write to priya.shah@Priya Shah.example")]
    [TestCase("<mei@Mei Lin>")]
    public void Find_WhenEmailAddress_ShouldIgnore(string body)
    {
        Find(body).ShouldBeEmpty();
    }

    [Test]
    public void Find_WhenNameRepeated_ShouldReturnOnce()
    {
        Find("@Mei Lin and @mei lin and @Mei Lin, again @Priya Shah").ShouldBe([MeiLin.UserId, PriyaShah.UserId]);
    }

    [Test]
    public void Find_WhenOverTwentyNames_ShouldCapAtTwenty()
    {
        var many = Enumerable.Range(1, 25).Select(i => new MentionCandidate(Guid.NewGuid(), $"Person {i:00}")).ToList();
        var body = string.Join(' ', many.Select(m => "@" + m.Name));

        var found = MentionText.Find(body, many, null);

        found.Count.ShouldBe(MentionText.MaxMentions);
        found.ShouldBe(many.Take(20).Select(m => m.UserId));
    }

    [Test]
    public void Find_WhenAuthorMentionsSelf_ShouldExcludeAuthor()
    {
        Find("Note to @Mei Lin and @Priya Shah", exclude: MeiLin.UserId).ShouldBe([PriyaShah.UserId]);
        Find("Note to @Mei Lin", exclude: MeiLin.UserId).ShouldBeEmpty();
    }
}
