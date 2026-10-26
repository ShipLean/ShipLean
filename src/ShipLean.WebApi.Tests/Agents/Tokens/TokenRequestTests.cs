using System.Net;
using ModelContextProtocol.Client;
using ShipLean.TestSupport.Contracts.Agents;
using ShipLean.TestSupport.Mcp;

namespace ShipLean.WebApi.Tests.Agents.Tokens;

[TestFixture]
public class TokenRequestTests : TokenFixture
{
    [Test]
    public async Task Token_WhenClientSendsSecretOrBasic_ShouldRefuseAsInvalidClient()
    {
        var arranged = await ArrangeAuthorizationAsync();
        var withSecret = CodeGrant(arranged.ClientId, await IssueCodeAsync(arranged.AuthorizationId));
        withSecret["client_secret"] = "s3cret";
        var secret = await TokenAsync(withSecret);
        RefusalOf(secret.Response, secret.Body, HttpStatusCode.Unauthorized).ShouldBe("invalid_client");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/oauth/token")
        {
            Content = new FormUrlEncodedContent(CodeGrant(arranged.ClientId, await IssueCodeAsync(arranged.AuthorizationId))),
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String("id:secret"u8));
        using var basic = await Client.SendAsync(request);
        basic.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Token_WhenClientUnknownOrGrantUnsupportedOrNotAForm_ShouldUseOAuthErrors()
    {
        var arranged = await ArrangeAuthorizationAsync();

        var unknown = await TokenAsync(CodeGrant("nobody", "c"));
        RefusalOf(unknown.Response, unknown.Body).ShouldBe("invalid_grant");

        var password = CodeGrant(arranged.ClientId, "c");
        password["grant_type"] = "password";
        var unsupported = await TokenAsync(password);
        RefusalOf(unsupported.Response, unsupported.Body).ShouldBe("unsupported_grant_type");

        var noResource = CodeGrant(arranged.ClientId, "c");
        noResource.Remove("resource");
        var missing = await TokenAsync(noResource);
        RefusalOf(missing.Response, missing.Body).ShouldBe("invalid_request");

        using var json = await Client.PostAsync("/oauth/token", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        json.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        System.Text.Json.JsonDocument.Parse(await json.Content.ReadAsStringAsync()).RootElement.GetProperty("error").GetString().ShouldBe("invalid_request");
    }

    [Test]
    public async Task Token_WhenMultipartOrOversize_ShouldRefuse()
    {
        var arranged = await ArrangeAuthorizationAsync();
        using var multipart = new MultipartFormDataContent();
        foreach (var (name, value) in CodeGrant(arranged.ClientId, await IssueCodeAsync(arranged.AuthorizationId)))
        {
            multipart.Add(new StringContent(value), name);
        }
        using var asMultipart = await Client.PostAsync("/oauth/token", multipart);

        asMultipart.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [TestCase("/oauth/token")]
    [TestCase("/oauth/revoke")]
    public async Task Post_WhenBodyOverSize_ShouldRefuseAsOAuthInvalidRequest(string path)
    {
        var (response, body) = await PostAsync(path, new() { ["token"] = new string('a', 20 * 1024), ["client_id"] = "c" });

        RefusalOf(response, body).ShouldBe("invalid_request");
    }
}
