using ShipLean.Application.Features.Backups.Engine;

namespace ShipLean.UnitTest.Backups.Engine;

[TestFixture]
public class BackupObjectKeysTests
{
    [Test]
    public void ObjectKeys_WhenDatabase_ShouldFollowLayout()
    {
        var startedAt = new DateTimeOffset(2026, 10, 24, 16, 0, 5, TimeSpan.FromHours(2));

        var key = BackupObjectKeys.Database("shiplean/", startedAt, Guid.Parse("0123abcd-4567-89ef-0123-456789abcdef"), 3);

        key.ShouldBe("shiplean/database/shiplean-20261024T140005Z-89abcdef-g3.dump.slbk");
    }

    [Test]
    public void ObjectKeys_WhenTwoRunsStartInSameSecond_ShouldDiffer()
    {
        var startedAt = new DateTimeOffset(2026, 10, 24, 14, 0, 5, TimeSpan.Zero);

        var first = BackupObjectKeys.Database("shiplean/", startedAt, Guid.CreateVersion7(startedAt), 1);
        var second = BackupObjectKeys.Database("shiplean/", startedAt, Guid.CreateVersion7(startedAt), 1);

        first.ShouldNotBe(second);
    }

    [Test]
    public void ObjectKeys_WhenAttachment_ShouldIncludeGeneration()
    {
        var key = BackupObjectKeys.Attachment("shiplean/", 2, "attachments/9f1c2d3e");

        key.ShouldBe("shiplean/attachments/g2/attachments/9f1c2d3e.slbk");
    }

    [Test]
    public void ObjectKeys_WhenAttachmentPrefix_ShouldBeFolderOfAttachmentKeys()
    {
        var prefix = BackupObjectKeys.AttachmentPrefix("shiplean/", 2);

        prefix.ShouldBe("shiplean/attachments/g2/");
        BackupObjectKeys.Attachment("shiplean/", 2, "attachments/9f1c2d3e").ShouldStartWith(prefix);
    }
}
