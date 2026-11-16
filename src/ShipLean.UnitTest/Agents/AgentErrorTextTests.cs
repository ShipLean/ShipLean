using ShipLean.Contracts.Agents;
using Shouldly;

namespace ShipLean.UnitTest.Agents;

[TestFixture]
public class AgentErrorTextTests
{
    // "a.a.a…" with no "://": the URL pattern tries a scheme at every "a" and scans the whole run each time, so it passes its
    // 1 second timeout. The cost is quadratic: about 1 s for 300,000 pairs on this host, roughly 45 s unbounded (estimated) for the 2,000,000 here, so even
    // much faster CI hardware times out with a wide margin, and the timeout still caps the test at about 1 s.
    private static readonly string PatternTimeoutInput = "a" + string.Concat(Enumerable.Repeat(".a", 2_000_000));

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
        AgentErrorText.Scrub("compatible-relay-connection-refused").ShouldBe("compatible-relay-connection-refused");
    }

    [Test]
    public void Scrub_WhenFortyCharacterBase64UrlRunHasNoDigit_ShouldRedactIt()
    {
        AgentErrorText.Scrub("sent dBjftJeZqCVPxmBzaKwiuhbUJUapqrHwWsgFWFOEjXk to relay").ShouldBe("sent [redacted] to relay");
    }

    [Test]
    public void Scrub_WhenBearerHeader_ShouldRedactTheValue()
    {
        AgentErrorText.Scrub("401 for Authorization: Bearer abc.def").ShouldBe("401 for Authorization: Bearer [redacted]");
    }

    [Test]
    public void Scrub_WhenTokenKeyInsideEscapedJson_ShouldRedactTheValue()
    {
        AgentErrorText.Scrub("""{"error":"bad body {\"uploadToken\":\"q1w2e3\"}"}""")
            .ShouldBe("""{"error":"bad body {\"uploadToken\":\"[redacted]\"}"}""");
    }

    [Test]
    public void Scrub_WhenPathWithoutSchemeHasQueryString_ShouldStripIt()
    {
        AgentErrorText.Scrub("POST /hook?sig=abc failed: 404").ShouldBe("POST /hook failed: 404");
    }

    [Test]
    public void Scrub_WhenUrlHasUserInfoAndFragment_ShouldRemoveThem()
    {
        AgentErrorText.Scrub("POST https://user:pass@relay.example.com/hook#sig=abc failed")
            .ShouldBe("POST https://relay.example.com/hook failed");
    }

    [Test]
    public void Scrub_WhenAPatternTimesOut_ShouldWithholdTheText()
    {
        var scrubbed = AgentErrorText.Scrub(PatternTimeoutInput);

        scrubbed.ShouldBe("The error text could not be checked and was withheld.");
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
        var scrubbed = AgentErrorText.Scrub(new string('.', 999) + "\U0001F600 more")!;

        scrubbed.ShouldBe(new string('.', 999));
    }
}
