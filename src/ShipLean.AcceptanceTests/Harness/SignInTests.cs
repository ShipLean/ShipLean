namespace ShipLean.AcceptanceTests.Harness;

// The fixture's sign-in is the real one: POST /api/auth/login on the page's browser context, as Joel by default.
[TestFixture]
public class SignInTests : SeededAcceptanceTest
{
    [Test]
    [Order(1)]
    public async Task SignIn_WhenFixtureSignsIn_ShouldServeTheShell()
    {
        var response = await Page.GotoAsync("/");

        response.ShouldNotBeNull();
        response.Status.ShouldBe(200);
        await Expect(Page).ToHaveTitleAsync("ShipLean");
        var me = await Page.Context.APIRequest.GetAsync("/api/me");
        me.Status.ShouldBe(200);
        (await me.TextAsync()).ShouldContain("joel@shiplean.local");
    }

    [Test]
    [Order(2)]
    public async Task SignIn_WhenSignedOut_ShouldSendThePageToLogin()
    {
        await SignOutAsync();

        await Page.GotoAsync("/p/SHIP/backlog");

        Page.Url.ShouldContain("/login");
        var me = await Page.Context.APIRequest.GetAsync("/api/me");
        me.Status.ShouldBe(401);
    }
}
