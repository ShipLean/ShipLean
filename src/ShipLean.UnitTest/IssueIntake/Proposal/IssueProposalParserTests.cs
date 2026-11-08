using ShipLean.Application.Features.IssueIntake.Proposal;
using ShipLean.Contracts.IssueIntake;
using ShipLean.TestSupport;
using Shouldly;

namespace ShipLean.UnitTest.IssueIntake.Proposal;

// C2: the proposal block's parser. The goldens are shared with the relay's tests, so both sides read the same responses.
public sealed class IssueProposalParserTests
{
    private static readonly string Goldens = Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "IssueIntake", "Proposals");

    [Test]
    public void Parse_WhenGoldenNew_ShouldReturnProposalAndPublicText()
    {
        var ok = IssueProposalParser.TryParse(Golden("new"), out var proposal, out var publicMarkdown);

        ok.ShouldBeTrue();
        proposal.ShouldNotBeNull();
        proposal.Verdict.ShouldBe(ProposalVerdict.New);
        proposal.Type.ShouldBe(ProposalType.Story);
        proposal.Title.ShouldBe("Filter the backlog by tag");
        proposal.Description.ShouldNotBeNull().ShouldStartWith("Add a **Tag** field");
        proposal.AcceptanceCriteria.ShouldBe(["The backlog filter offers a Tag field", "Tag = x shows only items tagged x"]);
        proposal.ParentKey.ShouldBe("SHIP-85");
        proposal.Existing.ShouldBeEmpty();
        publicMarkdown.ShouldStartWith("Thanks for the report.");
        publicMarkdown.ShouldEndWith("or tell me what to change.");
    }

    [Test]
    public void Parse_WhenGoldenAlreadyShipped_ShouldReadExistingWork()
    {
        IssueProposalParser.TryParse(Golden("alreadyShipped"), out var proposal, out var publicMarkdown).ShouldBeTrue();

        proposal.Verdict.ShouldBe(ProposalVerdict.AlreadyShipped);
        proposal.Description.ShouldBeNull();
        var existing = proposal.Existing.ShouldHaveSingleItem();
        existing.ShouldBe(new ProposalExisting(
            "SHIP-412", "https://github.com/ShipLean/ShipLean/pull/47", "54dc0d4",
            new DateTimeOffset(2026, 11, 3, 21, 58, 0, TimeSpan.Zero), new DateTimeOffset(2026, 11, 3, 22, 19, 0, TimeSpan.Zero),
            "https://github.com/ShipLean/ShipLean/actions/runs/123456789"));
        proposal.Requires.ShouldBe(["Search indexing turned on"]);
        publicMarkdown.ShouldContain("git merge-base --is-ancestor 54dc0d4 <sha>");
    }

    [TestCase("duplicate", ProposalVerdict.Duplicate, ProposalType.Story)]
    [TestCase("needsInfo", ProposalVerdict.NeedsInfo, ProposalType.Bug)]
    public void Parse_WhenGoldenWithoutDescription_ShouldReturnVerdict(string golden, ProposalVerdict verdict, ProposalType type)
    {
        IssueProposalParser.TryParse(Golden(golden), out var proposal, out _).ShouldBeTrue();

        proposal.Verdict.ShouldBe(verdict);
        proposal.Type.ShouldBe(type);
    }

    [TestCase("noBlock")]
    [TestCase("twoBlocks")]
    public void Parse_WhenNoBlockOrTwoBlocks_ShouldFail(string golden)
    {
        var ok = IssueProposalParser.TryParse(Golden(golden), out var proposal, out _, out var failure);

        ok.ShouldBeFalse();
        proposal.ShouldBeNull();
        failure.ShouldNotBeNull().ShouldContain("shiplean-proposal block");
    }

    [Test]
    public void Parse_WhenBadJson_ShouldFail()
    {
        var ok = IssueProposalParser.TryParse(Golden("badJson"), out var proposal, out _, out var failure);

        ok.ShouldBeFalse();
        proposal.ShouldBeNull();
        failure.ShouldNotBeNull().ShouldContain("not valid JSON");
    }

    [TestCase("""{ "verdict": "maybe", "type": "story", "title": "T", "description": "D" }""")]
    [TestCase("""{ "verdict": "new", "type": "epic", "title": "T", "description": "D" }""")]
    [TestCase("""{ "verdict": 0, "type": "story", "title": "T", "description": "D" }""")]
    [TestCase("""{ "type": "story", "title": "T", "description": "D" }""")]
    [TestCase("""{ "verdict": "new", "title": "T", "description": "D" }""")]
    [TestCase("""{ "verdict": "new", "type": "story", "title": " ", "description": "D" }""")]
    [TestCase("""{ "verdict": "new", "type": "story", "title": "T" }""")]
    [TestCase("""{ "verdict": "new", "type": "story", "title": "T", "description": "D", "parentKey": "ship-85" }""")]
    [TestCase("""{ "verdict": "duplicate", "type": "story", "title": "T", "existing": [{ "key": "SHIPLE-1" }] }""")]
    [TestCase("""{ "verdict": "duplicate", "type": "story", "title": "T", "existing": [{ "key": "SHIP-1 " }] }""")]
    [TestCase("""{ "verdict": "alreadyShipped", "type": "story", "title": "T", "existing": [{ "commit": "54dc0d" }] }""")]
    [TestCase("""{ "verdict": "alreadyShipped", "type": "story", "title": "T", "existing": [{ "commit": "54dc0dz" }] }""")]
    [TestCase("""{ "verdict": "alreadyShipped", "type": "story", "title": "T", "existing": [{ "mergedAt": "yesterday" }] }""")]
    [TestCase("""{ "verdict": "alreadyShipped", "type": "story", "title": "T", "existing": [null] }""")]
    [TestCase("""[]""")]
    [TestCase("""null""")]
    public void Parse_WhenFieldBreaksSchema_ShouldFail(string json)
    {
        var ok = IssueProposalParser.TryParse(Response(json), out var proposal, out _, out var failure);

        ok.ShouldBeFalse();
        proposal.ShouldBeNull();
        failure.ShouldNotBeNullOrWhiteSpace();
    }

    [Test]
    public void Parse_WhenOversized_ShouldCapEveryField()
    {
        IssueProposalParser.TryParse(Golden("oversized"), out var proposal, out _).ShouldBeTrue();

        proposal.Title.Length.ShouldBe(IssueProposal.MaxTitle);
        proposal.Description.ShouldNotBeNull().Length.ShouldBe(IssueProposal.MaxDescription);
        proposal.AcceptanceCriteria.Count.ShouldBe(IssueProposal.MaxAcceptanceCriteria);
        proposal.Existing.Count.ShouldBe(IssueProposal.MaxExisting);
        proposal.Requires.Count.ShouldBe(IssueProposal.MaxRequires);
        proposal.Questions.Count.ShouldBe(IssueProposal.MaxQuestions);
    }

    [Test]
    public void Parse_WhenBlockShownInsideLongerFence_ShouldCountOnlyTheRealBlock()
    {
        var response = "An example of the format:\n\n````markdown\n```shiplean-proposal\n{}\n```\n````\n\n"
            + Response("""{ "verdict": "needsInfo", "type": "bug", "title": "T" }""");

        IssueProposalParser.TryParse(response, out var proposal, out var publicMarkdown).ShouldBeTrue();

        proposal.Verdict.ShouldBe(ProposalVerdict.NeedsInfo);
        publicMarkdown.ShouldEndWith("````");
    }

    [Test]
    public void Parse_WhenTextFollowsBlock_ShouldLeaveItOutOfPublicText()
    {
        var response = Response("""{ "verdict": "needsInfo", "type": "bug", "title": "T", "extra": { "ignored": true } }""") + "\nSigned, the agent";

        IssueProposalParser.TryParse(response, out _, out var publicMarkdown).ShouldBeTrue();

        publicMarkdown.ShouldBe("Public text.");
    }

    [Test]
    public void Parse_WhenNestedTooDeep_ShouldFailWithoutThrowing()
    {
        var response = new string('>', 20_000) + " deep\n\n" + Response("""{ "verdict": "needsInfo", "type": "bug", "title": "T" }""");

        var ok = IssueProposalParser.TryParse(response, out var proposal, out _, out var failure);

        ok.ShouldBeFalse();
        proposal.ShouldBeNull();
        failure.ShouldNotBeNull().ShouldContain("nested too deeply");
    }

    private static string Response(string json)
    {
        return $"Public text.\n\n```shiplean-proposal\n{json}\n```\n";
    }

    private static string Golden(string name)
    {
        return File.ReadAllText(Path.Combine(Goldens, name + ".md"));
    }
}
