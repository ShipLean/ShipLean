using ShipLean.Application.Features.Demos.Data;

namespace ShipLean.UnitTest.Demos;

[TestFixture]
public class DemoWhyTests
{
    [Test]
    public void Extract_WhenWhySectionBetweenHeadings_ShouldReturnItsText()
    {
        var parts = DemoWhy.Extract("Intro.\n\n## Why\n\nShoppers drop out.\nThree steps is too many.\n\n## Plan\n\nBuild it.");

        parts.Why.ShouldBe("Shoppers drop out.\nThree steps is too many.");
    }

    [Test]
    public void Extract_WhenNoWhySection_ShouldBeNull()
    {
        DemoWhy.Extract("## Plan\n\nBuild it.\n\n## Risks\n\nNone.").ShouldBe(new DemoWhyParts(null, null));
    }

    [Test]
    public void Extract_WhenDescriptionEmpty_ShouldBeNull()
    {
        DemoWhy.Extract(string.Empty).ShouldBe(new DemoWhyParts(null, null));
    }

    [Test]
    public void Extract_WhenWhyIsTheLastSection_ShouldRunToTheEnd()
    {
        DemoWhy.Extract("## Plan\n\nBuild it.\n\n## Why\n\nBecause.\r\nAnd more.\n").Why.ShouldBe("Because.\nAnd more.");
    }

    [Test]
    public void Extract_WhenWhyHeadingEmpty_ShouldBeNull()
    {
        DemoWhy.Extract("## Why\n\n## Plan\n\nBuild it.").Why.ShouldBeNull();
    }

    [Test]
    public void Extract_WhenWhyHeadingIsInsideACodeFence_ShouldNotCountAsASection()
    {
        var parts = DemoWhy.Extract("## Plan\n\n```md\n## Why\n\nNot a section.\n```\n");

        parts.Why.ShouldBeNull();
    }

    [Test]
    public void Extract_WhenNextHeadingIsInsideACodeFence_ShouldKeepReadingTheSection()
    {
        var parts = DemoWhy.Extract("## Why\n\nBefore.\n\n```md\n## Not a heading\n```\n\nAfter.\n\n## Plan\n\nBuild it.");

        parts.Why.ShouldBe("Before.\n\n```md\n## Not a heading\n```\n\nAfter.");
    }

    [Test]
    public void Extract_WhenMeasureWithToConfirm_ShouldKeepTheMarker()
    {
        var parts = DemoWhy.Extract("## Why\n\nBecause.\n\n- **Measure (to confirm):** Checkout completion rate.\n\n## Plan\n");

        parts.Measure.ShouldBe("Checkout completion rate. (to confirm)");
    }

    [Test]
    public void Extract_WhenMeasureWithoutToConfirm_ShouldReturnTheText()
    {
        var parts = DemoWhy.Extract("## Why\n\nBecause.\n\n- **Measure:** Checkout completion rate.\n  Weekly, from the funnel.\n- **Other:** x");

        parts.Measure.ShouldBe("Checkout completion rate. Weekly, from the funnel.");
    }

    [Test]
    public void Extract_WhenMeasureIsOutsideTheWhySection_ShouldBeNull()
    {
        var parts = DemoWhy.Extract("## Why\n\nBecause.\n\n## Plan\n\n- **Measure:** Elsewhere.");

        parts.Measure.ShouldBeNull();
    }
}
