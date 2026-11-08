using System.Text.RegularExpressions;
using ShipLean.TestSupport;

namespace ShipLean.UnitTest.Backups;

// SHIP-791: the attachment mirror skips a key it already holds in this generation (D5), so a key must never name
// different content later. Each writer below makes a fresh key per write (a new GUID) or a content hash
// (OpenGraphSource: same key, same bytes). A new production type that calls IBlobStore.PutAsync fails this test and has
// to be checked for the same property before it is added here.
[TestFixture]
public class BlobWritersTests
{
    // Restore (Features/Backups/Cli) writes keys back exactly as they were, so it isn't a writer of new keys.
    private static readonly string[] KnownFreshKeyWriters =
    [
        "AttachmentStore",
        "DemoLogoUpload",
        "OpenGraphSource",
        "ProfileEndpoints",
    ];

    private static readonly Regex BlobPut = new(@"\b[A-Za-z_]*[bB]lobs?\s*\.PutAsync\s*\(", RegexOptions.Compiled);

    [Test]
    public void Writers_WhenListed_ShouldMatchKnownFreshKeyWriters()
    {
        var application = Path.Combine(RepoPaths.Root, "src", "ShipLean.Application");
        var writers = Directory.EnumerateFiles(application, "*.cs", SearchOption.AllDirectories)
            .Select(f => (Path: f, Parts: Path.GetRelativePath(application, f).Split(Path.DirectorySeparatorChar)))
            .Where(f => !f.Parts.Any(p => p is "bin" or "obj" or "Fakes" or "Storage"))
            .Where(f => !IsRestore(f.Parts))
            .Where(f => BlobPut.IsMatch(File.ReadAllText(f.Path)))
            .Select(f => Path.GetFileNameWithoutExtension(f.Path))
            .Order(StringComparer.Ordinal)
            .ToList();

        writers.ShouldBe(KnownFreshKeyWriters, "A new IBlobStore writer: check that its keys never change meaning, then list it here.");
    }

    private static bool IsRestore(string[] parts)
        => parts.Length > 3 && parts[0] == "Features" && parts[1] == "Backups" && parts[2] == "Cli";
}
