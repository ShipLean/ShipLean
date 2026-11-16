using ShipLean.Application.Features.Demos.Reads;

namespace ShipLean.UnitTest.Demos;

[TestFixture]
public class SprintDemoAgentMessageTests
{
    [TestCase(null)]
    [TestCase("")]
    [TestCase("   \n\t  ")]
    [TestCase("---")]
    public void Excerpt_WhenBlank_ShouldBeNull(string? markdown)
    {
        SprintDemoAgentMessage.Excerpt(markdown).ShouldBeNull();
    }

    [Test]
    public void Excerpt_WhenSeveralParagraphs_ShouldBeTheFirstOnly()
    {
        SprintDemoAgentMessage.Excerpt("I couldn't make the demo.\nMy first Bash call was denied.\n\nMore detail.\n\nEven more.")
            .ShouldBe("I couldn't make the demo. My first Bash call was denied.");
    }

    [Test]
    public void Excerpt_WhenMarkdown_ShouldBePlainText()
    {
        SprintDemoAgentMessage.Excerpt("## **Demo** failed: `ffmpeg` exited, see [the log](https://example.com/log) and _retry_")
            .ShouldBe("Demo failed: ffmpeg exited, see the log and retry");
    }

    [Test]
    public void Excerpt_WhenLeadingBlankLinesAndBreak_ShouldStartAtTheFirstText()
    {
        SprintDemoAgentMessage.Excerpt("\n\n***\n\n  The   render    stopped.  \n\nNext.").ShouldBe("The render stopped.");
    }

    [Test]
    public void Excerpt_WhenExactlyTheLimit_ShouldBeWhole()
    {
        var text = new string('a', 280);

        SprintDemoAgentMessage.Excerpt(text).ShouldBe(text);
    }

    [Test]
    public void Excerpt_WhenLongerThanTheLimit_ShouldCutAtAWordWithAnEllipsis()
    {
        // 6-character words, so a cut at 279 characters would fall inside a word.
        var words = string.Join(' ', Enumerable.Repeat("video", 60));

        var excerpt = SprintDemoAgentMessage.Excerpt(words)!;

        excerpt.Length.ShouldBeLessThanOrEqualTo(280);
        excerpt.ShouldEndWith("video…");
        excerpt[..^1].Split(' ').ShouldAllBe(w => w == "video");
    }

    [Test]
    public void Excerpt_WhenOneWordLongerThanTheLimit_ShouldCutItAtTheLimit()
    {
        var excerpt = SprintDemoAgentMessage.Excerpt(new string('x', 400))!;

        excerpt.ShouldBe(new string('x', 279) + "…");
    }

    [Test]
    public void Excerpt_WhenItRepeatsATokenLink_ShouldLeaveTheLinkOut()
    {
        SprintDemoAgentMessage.Excerpt("Upload to https://prod.shiplean.app/api/uploads/demo?token=abc123 failed.")
            .ShouldBe("Upload to [removed] failed.");
    }

    [Test]
    public void Excerpt_WhenItRepeatsAnUploadLinkWithoutATokenParameter_ShouldLeaveTheLinkOut()
    {
        SprintDemoAgentMessage.Excerpt("Upload to https://prod.shiplean.app/api/uploads/demo/abc123?expires=9 failed.")
            .ShouldBe("Upload to [removed] failed.");
    }

    [TestCase("Used ghp_abcdefghijklmnopqrstuvwxyz0123456789 to push.", "Used [removed] to push.")]
    [TestCase("Key sk-abcdefghijklmnopqrstuvwxyz0123456789ABCD leaked.", "Key [removed] leaked.")]
    [TestCase("Sent Authorization: Bearer abcDEF123456.xyz and failed.", "Sent Authorization: [removed] and failed.")]
    [TestCase("Sent Authorization: Basic dXNlcjpwYXNzd29yZA== and failed.", "Sent Authorization: [removed] and failed.")]
    public void Excerpt_WhenItHoldsACredential_ShouldReplaceItWithRemoved(string markdown, string expected)
    {
        SprintDemoAgentMessage.Excerpt(markdown).ShouldBe(expected);
    }

    [Test]
    public void Excerpt_WhenNestedPastMarkdigsDepthLimit_ShouldStillBePlainText()
    {
        var markdown = new string('[', 5000) + "<script>alert(1)</script> **bold** [x](http://example.com/a) done" + new string(']', 5000);

        var excerpt = SprintDemoAgentMessage.Excerpt(markdown)!;

        excerpt.ShouldNotContain("<");
        excerpt.ShouldNotContain("*");
        excerpt.ShouldNotContain("](");
        excerpt.ShouldContain("bold");
    }

    [Test]
    public void Excerpt_WhenLongerThanTheLimitWithoutSpaces_ShouldNeverSplitASurrogatePair()
    {
        var excerpt = SprintDemoAgentMessage.Excerpt(string.Concat(Enumerable.Repeat("😀", 400)))!;

        excerpt.Length.ShouldBeLessThanOrEqualTo(SprintDemoAgentMessage.MaxLength);
        excerpt.ShouldEndWith("…");
        excerpt[..^1].ShouldBe(string.Concat(Enumerable.Repeat("😀", (excerpt.Length - 1) / 2)));
    }
}
