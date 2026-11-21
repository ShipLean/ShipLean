using ShipLean.Contracts.Prompts;

namespace ShipLean.UnitTest.Prompts;

[TestFixture]
public class PromptHashTests
{
    [Test]
    public void Of_WhenCrLf_ShouldEqualLf()
    {
        PromptHash.Of("one\r\ntwo\r\n").ShouldBe(PromptHash.Of("one\ntwo\n"));
    }

    [Test]
    public void Of_ShouldBeLowercaseHex64()
    {
        PromptHash.Of("anything").ShouldMatch("^[0-9a-f]{64}$");
    }
}
