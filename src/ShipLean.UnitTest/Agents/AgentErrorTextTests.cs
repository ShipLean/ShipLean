using ShipLean.Contracts.Agents;
using Shouldly;

namespace ShipLean.UnitTest.Agents;

[TestFixture]
public class AgentErrorTextTests
{
    [Test]
    public void Scrub_WhenNull_ShouldBeNull()
    {
        AgentErrorText.Scrub(null).ShouldBeNull();
    }

    [Test]
    public void Scrub_WhenPlainError_ShouldKeepIt()
    {
        AgentErrorText.Scrub("No answer within 5 seconds.").ShouldBe("No answer within 5 seconds.");
    }

    [Test]
    public void Scrub_WhenShipLeanToken_ShouldRedactIt()
    {
        AgentErrorText.Scrub("Bad callback shl_cb_Ab3dE-f9_x went nowhere").ShouldBe("Bad callback [redacted] went nowhere");
    }

    [TestCase("refused token=abc.def-1 here", "refused token=[redacted] here")]
    [TestCase("{\"uploadToken\": \"q1w2e3\"}", "{\"uploadToken\": \"[redacted]\"}")]
    [TestCase("access_token: zz9; retry", "access_token: [redacted]; retry")]
    public void Scrub_WhenTokenValue_ShouldRedactTheValue(string raw, string expected)
    {
        AgentErrorText.Scrub(raw).ShouldBe(expected);
    }

    [Test]
    public void Scrub_WhenUrlHasQueryString_ShouldStripIt()
    {
        AgentErrorText.Scrub("POST https://relay.example.com:8443/hook?sig=abc&x=1 failed: 502")
            .ShouldBe("POST https://relay.example.com:8443/hook failed: 502");
    }

    [Test]
    public void Scrub_WhenUploadLinkInEcho_ShouldLeaveNoneOfTheLink()
    {
        var scrubbed = AgentErrorText.Scrub("echo: curl 'https://planner.example.com/api/uploads/demo?token=Zm9vYmFyYmF6cXV4cXV1eGNvcmdlZ3JhdWx0'")!;

        scrubbed.ShouldBe("echo: curl 'https://planner.example.com/api/uploads/demo'");
    }

    [TestCase("hex 9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08 end")]
    [TestCase("b64 c2VjcmV0LXNpZ25pbmcta2V5LTEyMzQ1Njc4OTA= end")]
    [TestCase("b64url dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk end")]
    public void Scrub_WhenLongBase64OrHexRun_ShouldRedactIt(string raw)
    {
        var scrubbed = AgentErrorText.Scrub(raw)!;

        scrubbed.ShouldEndWith("[redacted] end");
        scrubbed.Split(' ').Length.ShouldBe(3);
    }

    [Test]
    public void Scrub_WhenLongWordWithoutDigits_ShouldKeepIt()
    {
        AgentErrorText.Scrub("linear-compatible-relay-connection-refused").ShouldBe("linear-compatible-relay-connection-refused");
    }

    [Test]
    public void Scrub_WhenShortRun_ShouldKeepIt()
    {
        AgentErrorText.Scrub("request 3f2a9c1d failed").ShouldBe("request 3f2a9c1d failed");
    }

    [Test]
    public void Scrub_WhenLongerThanTheCap_ShouldCutToOneThousandCharacters()
    {
        var scrubbed = AgentErrorText.Scrub(string.Concat(Enumerable.Repeat("error. ", 300)))!;

        scrubbed.Length.ShouldBe(AgentErrorText.MaxLength);
        AgentErrorText.MaxLength.ShouldBe(1000);
    }

    [Test]
    public void Scrub_WhenTheCutFallsInsideASurrogatePair_ShouldNotSplitIt()
    {
        var scrubbed = AgentErrorText.Scrub(new string('a', 999) + "\U0001F600 more")!;

        scrubbed.ShouldBe(new string('a', 999));
    }
}
