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
        var words = string.Join(' ', Enumerable.Repeat("render", 60));

        var excerpt = SprintDemoAgentMessage.Excerpt(words)!;

        excerpt.Length.ShouldBeLessThanOrEqualTo(280);
        excerpt.ShouldEndWith("render…");
        excerpt[..^1].Split(' ').ShouldAllBe(w => w == "render");
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
            .ShouldBe("Upload to [link removed] failed.");
    }
}
