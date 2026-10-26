using ShipLean.Application.Features.Agents.Clients;

namespace ShipLean.UnitTest.Agents.Clients;

[TestFixture]
public class RedirectUriRulesTests
{
    [TestCase("https://agent.example.test/cb")]
    [TestCase("http://127.0.0.1:8123/cb")]
    [TestCase("http://[::1]:9/cb")]
    [TestCase("http://localhost/cb")]
    [TestCase("cursor://anysphere.cursor-retrieval/oauth/callback")]
    [TestCase("com.example.app:/oauth2redirect")]
    [TestCase("MyApp://callback")]
    [TestCase("a+b.c-d1://x/y")]
    public void IsAcceptable_WhenAllowedShape_ShouldAccept(string redirect)
    {
        RedirectUriRules.IsAcceptable(redirect).ShouldBeTrue();
    }

    [TestCase("http")]
    [TestCase("https")]
    [TestCase("javascript")]
    [TestCase("data")]
    [TestCase("file")]
    [TestCase("vbscript")]
    [TestCase("blob")]
    [TestCase("about")]
    [TestCase("ws")]
    [TestCase("wss")]
    [TestCase("ftp")]
    [TestCase("filesystem")]
    [TestCase("view-source")]
    [TestCase("mailto")]
    [TestCase("tel")]
    [TestCase("ms-msdt")]
    [TestCase("search-ms")]
    [TestCase("search")]
    [TestCase("ms-officecmd")]
    [TestCase("intent")]
    [TestCase("chrome")]
    [TestCase("chrome-extension")]
    [TestCase("jar")]
    [TestCase("JavaScript")]
    public void IsPrivateUseScheme_WhenBlocked_ShouldRefuse(string scheme)
    {
        RedirectUriRules.IsPrivateUseScheme(scheme).ShouldBeFalse();
    }

    [TestCase("javascript:alert(1)")]
    [TestCase("JAVASCRIPT:alert(1)")]
    [TestCase("data:text/html,x")]
    [TestCase("file:///etc/passwd")]
    [TestCase("vbscript:x")]
    [TestCase("blob:https://a.example/x")]
    [TestCase("about:blank")]
    [TestCase("ws://a.example/x")]
    [TestCase("wss://a.example/x")]
    [TestCase("ftp://a.example/x")]
    [TestCase("filesystem:https://a.example/x")]
    [TestCase("view-source:https://a.example/")]
    [TestCase("mailto:a@b.example")]
    [TestCase("tel:+15555550100")]
    [TestCase("http://evil.example.test/cb")]
    [TestCase("https://user@agent.example.test/cb")]
    [TestCase("https://agent.example.test/cb#f")]
    [TestCase("cursor://user@host/cb")]
    [TestCase("cursor://host/cb#frag")]
    [TestCase("cursor://host/cb#")]
    [TestCase("cursor://h/cb\n")]
    [TestCase(" cursor://h/cb")]
    [TestCase("cursor://h/cb\t")]
    [TestCase("cursor://h/\u0001cb")]
    [TestCase("/relative/cb")]
    [TestCase("not a uri")]
    [TestCase("")]
    [TestCase(null)]
    public void IsAcceptable_WhenRefusedShape_ShouldRefuse(string? redirect)
    {
        RedirectUriRules.IsAcceptable(redirect).ShouldBeFalse();
    }

    [TestCase("https://agent.example.test/cb", "agent.example.test")]
    [TestCase("http://127.0.0.1:8123/cb", "127.0.0.1")]
    [TestCase("cursor://anysphere.cursor-retrieval/oauth/callback", "cursor://anysphere.cursor-retrieval")]
    public void Describe_WhenRedirect_ShouldNameWhereItGoes(string redirect, string expected)
    {
        RedirectUriRules.Describe(new Uri(redirect)).ShouldBe(expected);
    }
}
