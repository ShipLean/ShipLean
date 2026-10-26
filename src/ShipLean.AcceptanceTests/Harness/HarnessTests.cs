namespace ShipLean.AcceptanceTests.Harness;

[TestFixture]
public class HarnessTests : SeededAcceptanceTest
{
    [Test]
    public async Task Harness_WhenSeededPageOpened_ShouldServeTheSpa()
    {
        var response = await Page.GotoAsync("/");

        response.ShouldNotBeNull();
        response.Status.ShouldBe(200);
        await Expect(Page).ToHaveTitleAsync("ShipLean");
    }
}
