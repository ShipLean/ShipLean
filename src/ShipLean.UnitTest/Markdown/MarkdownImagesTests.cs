using ShipLean.Application.Markdown;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Markdown;
using Shouldly;

namespace ShipLean.UnitTest.Markdown;

[TestFixture]
public class MarkdownImagesTests
{
    private static readonly Guid PickerId = Guid.NewGuid();

    private static AttachmentDto Attachment(Guid id, string name, AttachmentKind kind)
        => new(id, Guid.NewGuid(), name, "application/octet-stream", 1, null, DateTimeOffset.UnixEpoch, "", "", kind, 0, null, null);

    private static readonly AttachmentDto[] Item =
    [
        Attachment(PickerId, "picker.png", AttachmentKind.Image),
        Attachment(Guid.NewGuid(), "notes.md", AttachmentKind.Markdown),
    ];

    [Test]
    public void Classify_WhenNameMatchesImage_ShouldBeAttached()
    {
        var images = MarkdownImages.Classify("![alt](picker.png)", Item);

        images.ShouldBe([new MarkdownImageDto("picker.png", MarkdownImageStatus.Attached, PickerId)]);
    }

    [Test]
    public void Classify_WhenNameDiffersOnlyByCase_ShouldBeAttached()
    {
        MarkdownImages.Classify("![alt](Picker.PNG)", Item).Single().Status.ShouldBe(MarkdownImageStatus.Attached);
    }

    [Test]
    public void Classify_WhenAbsoluteUrl_ShouldBeExternal()
    {
        var images = MarkdownImages.Classify("![alt](https://example.com/picker.png)", Item);

        images.ShouldBe([new MarkdownImageDto("https://example.com/picker.png", MarkdownImageStatus.External, null)]);
    }

    [TestCase("![alt](nothing.png)")]
    [TestCase("![alt](folder/picker.png)")]
    [TestCase("![alt](notes.md)")]
    public void Classify_WhenNoImageAttachmentMatches_ShouldBeMissing(string markdown)
    {
        MarkdownImages.Classify(markdown, Item).Single().Status.ShouldBe(MarkdownImageStatus.Missing);
    }

    [Test]
    public void Classify_WhenSameImageTwice_ShouldListItOnce()
    {
        MarkdownImages.Classify("![a](picker.png) and ![b](picker.png) and a [link](picker.png)", Item).Count.ShouldBe(1);
    }

    [Test]
    public void Classify_WhenNoImages_ShouldBeEmpty()
    {
        MarkdownImages.Classify("# Just text", Item).ShouldBeEmpty();
    }
}
