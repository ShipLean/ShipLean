using System.Globalization;
using System.Text;
using System.Text.Json;
using ShipLean.Application.Features.IssueIntake.Inbox;
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
        existing.Key.ShouldBe("SHIP-412");
        existing.PrUrl.ShouldBe("https://github.com/ShipLean/ShipLean/pull/47");
        existing.Commit.ShouldBe("54dc0d4");
        Utc(existing.MergedAt).ShouldBe("2026-11-03T21:58:00Z");
        Utc(existing.PublishedAt).ShouldBe("2026-11-03T22:19:00Z");
        existing.RunUrl.ShouldBe("https://github.com/ShipLean/ShipLean/actions/runs/123456789");
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

    // P2: a field past its limit is refused with a reason, never cut.
    [Test]
    public void Parse_WhenOversized_ShouldRejectWithReason()
    {
        var ok = IssueProposalParser.TryParse(Golden("oversized"), out var proposal, out _, out var failure);

        ok.ShouldBeFalse();
        proposal.ShouldBeNull();
        failure.ShouldNotBeNull().ShouldContain("past the 255 limit");
    }

    [TestCase("title", "title is 256 characters")]
    [TestCase("description", "description is 65537 characters")]
    [TestCase("acceptanceCriteria", "acceptanceCriteria has 51 entries")]
    [TestCase("criterion", "acceptanceCriteria[1] is 1001 characters")]
    [TestCase("requires", "requires has 11 entries")]
    [TestCase("requirement", "requires[0] is 1001 characters")]
    [TestCase("questions", "questions has 4 entries")]
    [TestCase("question", "questions[2] is 1001 characters")]
    [TestCase("existing", "existing has 11 entries")]
    [TestCase("prUrl", "existing[0] has a URL past the 2048 character limit")]
    [TestCase("runUrl", "existing[0] has a URL past the 2048 character limit")]
    public void Parse_WhenFieldPastLimit_ShouldRejectWithReason(string field, string reason)
    {
        var ok = IssueProposalParser.TryParse(Response(AtLimits(field)), out var proposal, out _, out var failure);

        ok.ShouldBeFalse();
        proposal.ShouldBeNull();
        failure.ShouldNotBeNull().ShouldStartWith(reason);
    }

    [Test]
    public void Parse_WhenEveryFieldAtLimit_ShouldKeepEveryField()
    {
        IssueProposalParser.TryParse(Response(AtLimits(string.Empty)), out var proposal, out _, out var failure).ShouldBeTrue(failure);

        proposal.Title.Length.ShouldBe(IssueProposal.MaxTitle);
        proposal.Description.ShouldNotBeNull().Length.ShouldBe(IssueProposal.MaxDescription);
        proposal.AcceptanceCriteria.Count.ShouldBe(IssueProposal.MaxAcceptanceCriteria);
        proposal.AcceptanceCriteria.ShouldAllBe(c => c.Length == IssueProposalParser.MaxEntry);
        proposal.Existing.Count.ShouldBe(IssueProposal.MaxExisting);
        proposal.Existing[0].PrUrl.ShouldNotBeNull().Length.ShouldBe(IssueProposalParser.MaxUrl);
        proposal.Requires.Count.ShouldBe(IssueProposal.MaxRequires);
        proposal.Questions.Count.ShouldBe(IssueProposal.MaxQuestions);
    }

    // P1: the whole response is bounded before Markdig or the JSON reader sees it.
    [Test]
    public void Parse_WhenResponsePast256Kb_ShouldRejectBeforeParsing()
    {
        var response = new string('x', 300 * 1024) + "\n\n" + Response("""{ "verdict": "needsInfo", "type": "bug", "title": "T" }""");

        var ok = IssueProposalParser.TryParse(response, out var proposal, out var publicMarkdown, out var failure);

        ok.ShouldBeFalse();
        proposal.ShouldBeNull();
        publicMarkdown.ShouldBeEmpty();
        failure.ShouldNotBeNull().ShouldContain("past the 262144 limit");
    }

    [Test]
    public void Parse_WhenQuestionIsTwoMegabytes_ShouldReject()
    {
        var json = JsonSerializer.Serialize(new { verdict = "needsInfo", type = "bug", title = "T", questions = new[] { new string('q', 2 * 1024 * 1024) } });

        var ok = IssueProposalParser.TryParse(Response(json), out var proposal, out _, out var failure);

        ok.ShouldBeFalse();
        proposal.ShouldBeNull();
        failure.ShouldNotBeNull().ShouldContain("bytes, past the");
    }

    // Every field at its limit, with the one named pushed one past it.
    private static string AtLimits(string over)
    {
        var url = "https://github.com/ShipLean/ShipLean/pull/47?" + new string('a', IssueProposalParser.MaxUrl - 45);
        var existing = Enumerable.Range(0, over == "existing" ? 11 : IssueProposal.MaxExisting)
            .Select(i => new { key = $"SHIP-{i + 1}", prUrl = over == "prUrl" ? url + "a" : url, runUrl = over == "runUrl" ? url + "a" : url })
            .ToList();
        return JsonSerializer.Serialize(new
        {
            verdict = "new",
            type = "story",
            title = new string('t', over == "title" ? 256 : IssueProposal.MaxTitle),
            description = new string('d', over == "description" ? IssueProposal.MaxDescription + 1 : IssueProposal.MaxDescription),
            acceptanceCriteria = Entries(over == "acceptanceCriteria" ? 51 : IssueProposal.MaxAcceptanceCriteria, over == "criterion" ? 1 : -1),
            existing,
            requires = Entries(over == "requires" ? 11 : IssueProposal.MaxRequires, over == "requirement" ? 0 : -1),
            questions = Entries(over == "questions" ? 4 : IssueProposal.MaxQuestions, over == "question" ? 2 : -1),
        });
    }

    private static List<string> Entries(int count, int longIndex)
    {
        return Enumerable.Range(0, count).Select(i => new string('e', i == longIndex ? IssueProposalParser.MaxEntry + 1 : IssueProposalParser.MaxEntry)).ToList();
    }

    [Test]
    public void Parse_WhenBlockShownInsideLongerFence_ShouldCountOnlyTheRealBlock()
    {
        var response = "An example of the format:\n\n````markdown\n```shiplean-proposal\n{}\n```\n````\n\n"
            + Response("""{ "verdict": "needsInfo", "type": "bug", "title": "T" }""");

        IssueProposalParser.TryParse(response, out var proposal, out var publicMarkdown).ShouldBeTrue();

        proposal.Verdict.ShouldBe(ProposalVerdict.NeedsInfo);
        publicMarkdown.ShouldEndWith("````\n\nPublic text.");
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

    // A6: the stored proposal (github_issue.proposal) reads with the same rules but no fence, so a fence in a field can't end it.
    [Test]
    public void ParseStored_WhenTitleHoldsTenBackticks_ShouldRead()
    {
        var stored = Stored("Show ``````````shiplean-proposal fences", "Ten ``````````\n``````````", 0);

        IssueProposalParser.TryParseStored(stored, out var proposal, out var failure).ShouldBeTrue(failure);

        proposal.Title.ShouldBe("Show ``````````shiplean-proposal fences");
        proposal.Description.ShouldBe("Ten ``````````\n``````````");
    }

    // A6: escaped, a description within its limit is nearly 256 KB of JSON; the stored read has no response limit to trip on.
    [Test]
    public void ParseStored_WhenEscapedJsonNearly256Kb_ShouldRead()
    {
        var empty = Stored("Escaped", string.Empty, 0).Length;
        var target = IssueProposalParser.MaxResponseBytes - 10;
        var stored = Stored("Escaped", string.Empty, (target - empty) / 6);
        stored += new string(' ', target - stored.Length);
        Encoding.UTF8.GetByteCount(stored).ShouldBe(target);

        IssueProposalParser.TryParseStored(stored, out var proposal, out var failure).ShouldBeTrue(failure);

        proposal.Description.ShouldBe(new string('<', (target - empty) / 6));
    }

    [Test]
    public void ParseStored_WhenFieldBreaksRule_ShouldFailWithReason()
    {
        IssueProposalParser.TryParseStored("""{"verdict":"new","type":"story","title":" "}""", out var proposal, out var failure).ShouldBeFalse();

        proposal.ShouldBeNull();
        failure.ShouldBe("The proposal needs a title.");
    }

    // The JSON as ProposalJobWorker stores it, with `escaped` characters that JSON writes as six bytes each appended to the description.
    private static string Stored(string title, string description, int escaped)
    {
        var proposal = new IssueProposal(
            ProposalVerdict.New, ProposalType.Story, title, description + new string('<', escaped), ["It works"], null, [], [], []);
        return ProposalJobWorker.ProposalJson(proposal);
    }

    private static string? Utc(DateTimeOffset? at)
    {
        return at?.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
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
