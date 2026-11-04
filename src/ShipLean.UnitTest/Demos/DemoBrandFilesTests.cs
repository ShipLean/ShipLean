using ShipLean.TestSupport;

namespace ShipLean.UnitTest.Demos;

// K11: a default-branded run needs nothing from the instance, so the copies the prompt embeds must be the design folder's bytes.
[TestFixture]
public class DemoBrandFilesTests
{
    private static readonly string Embedded = Path.Combine(RepoPaths.Root, "src", "ShipLean.Application", "Features", "Demos", "Prompt");

    [Test]
    public void EmbeddedTokens_ShouldEqualTheDesignFolders()
    {
        File.ReadAllBytes(Path.Combine(Embedded, "tokens.css")).ShouldBe(File.ReadAllBytes(Path.Combine(RepoPaths.Root, "design", "tokens.css")));
    }

    [Test]
    public void EmbeddedLogo_ShouldEqualTheDesignFolders()
    {
        File.ReadAllBytes(Path.Combine(Embedded, "shiplean-logo-on-light.svg"))
            .ShouldBe(File.ReadAllBytes(Path.Combine(RepoPaths.Root, "design", "logo", "shiplean-logo-on-light.svg")));
    }
}
