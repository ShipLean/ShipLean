using System.Text.RegularExpressions;
using ShipLean.TestSupport;

namespace ShipLean.UnitTest.Backups;

// SHIP-791: the attachment mirror skips a key it already holds in this generation (D5), so a key must never name
// different content later. Every production file that calls .PutAsync( must be named in one of the two lists below,
// so a new caller fails this test until someone has checked it. A new IBlobStore writer has to make a fresh key per
// write (a new GUID) or a content hash (same key, same bytes) before it goes in the first list.
[TestFixture]
public class BlobWritersTests
{
    private static readonly string[] FreshKeyWriters =
    [
        "AttachmentStore",
        "DemoLogoUpload",
        "OpenGraphSource",
        "ProfileEndpoints",
    ];

    private static readonly Dictionary<string, string> NotNewKeyBlobWriters = new()
    {
        ["AttachmentMirror"] = "puts to the backup destination, not the blob store",
        ["BackupJob"] = "puts to the backup destination, not the blob store",
        ["RestoreBlobsVerb"] = "writes keys back exactly as they were",
        ["StateProvisioner"] = "calls a non-blob PutAsync",
    };

    private static readonly Regex Put = new(@"\.PutAsync\s*\(", RegexOptions.Compiled);

    [Test]
    public void Writers_WhenListed_ShouldMatchKnownCallers()
    {
        // Only the store implementations (top-level Fakes/ and Storage/) are exempt.
        var application = Path.Combine(RepoPaths.Root, "src", "ShipLean.Application");
        var callers = Directory.EnumerateFiles(application, "*.cs", SearchOption.AllDirectories)
            .Select(f => (Path: f, Parts: Path.GetRelativePath(application, f).Split(Path.DirectorySeparatorChar)))
            .Where(f => f.Parts[0] is not ("bin" or "obj" or "Fakes" or "Storage"))
            .Where(f => Put.IsMatch(File.ReadAllText(f.Path)))
            .Select(f => Path.GetFileNameWithoutExtension(f.Path))
            .Order(StringComparer.Ordinal)
            .ToList();

        var expected = FreshKeyWriters.Concat(NotNewKeyBlobWriters.Keys).Order(StringComparer.Ordinal).ToList();

        callers.ShouldBe(
            expected,
            ignoreOrder: false,
            customMessage: "A production file calls .PutAsync( and is not listed. If it writes to IBlobStore, check its keys never change meaning and add it to FreshKeyWriters; otherwise add it to NotNewKeyBlobWriters with a reason.");
    }
}
