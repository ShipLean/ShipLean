using ShipLean.TestSupport.Http;

namespace ShipLean.WebApi.Tests.Links;

[TestFixture]
public class StubHttpServerTests
{
    [Test]
    public async Task Server_WhenPathIsRegistered_ShouldAnswerItAndRecordTheRequest()
    {
        await using var server = await StubHttpServer.StartAsync();
        server.Respond("/page", "<title>Hello</title>", headers: new Dictionary<string, string> { ["X-Stub"] = "1" });
        using var client = new HttpClient();

        using var response = await client.GetAsync(server.UrlOf("/page?a=1"));

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("<title>Hello</title>");
        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/html");
        response.Headers.GetValues("X-Stub").ShouldBe(["1"]);
        server.Requests.ShouldBe(["GET /page?a=1"]);
    }

    [Test]
    public async Task Server_WhenPathIsNotRegistered_ShouldAnswerNotFound()
    {
        await using var server = await StubHttpServer.StartAsync();
        using var client = new HttpClient();

        using var response = await client.GetAsync(server.UrlOf("/missing"));

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.NotFound);
    }
}
