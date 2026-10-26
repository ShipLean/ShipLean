using System.Net;
using System.Text.Json.Nodes;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Jira;
using Shouldly;

namespace ShipLean.WebApi.Tests.Import.Jira;

// Reading a connection shows the last four characters stored beside the token, so it never decrypts: here the token protector throws if it is touched.
[TestFixture]
public sealed class JiraReadsWithoutDecryptingTests : ApiFixture
{
    private static readonly string Connection = $"api/projects/{SeedIds.Projects.Ship}/jira/connection";

    protected override Action<IServiceCollection>? ConfigureTestServices => ThrowingTokenProtection.Pin;

    [Test]
    public async Task Jira_WhenTheProtectorIsPinnedToThrow_ShouldFailASave()
    {
        using var saved = await Api.PutJsonAsync(Connection, new { baseUrl = JiraStubHandler.CloudUrl, kind = "cloud", email = JiraStubHandler.CloudEmail, token = "a-token-with-tail-9876" });

        saved.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }

    [Test]
    public async Task Jira_WhenAConnectionIsRead_ShouldNotCallTheTokenProtector()
    {
        await using var database = await OpenDatabaseAsync();
        await database.ExecuteAsync(
            """
            INSERT INTO jira_connection (id, project_id, base_url, kind, email, api_token_encrypted, token_last_four)
            VALUES (@id, @projectId, @baseUrl, 'cloud', 'priya@acme.io', 'not-a-ciphertext', 'WXYZ')
            """,
            new { id = Guid.NewGuid(), projectId = SeedIds.Projects.Ship, baseUrl = JiraStubHandler.CloudUrl });

        using var read = await Api.GetAsync(Connection);

        var text = await read.Content.ReadAsStringAsync();
        read.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        JsonNode.Parse(text)!["tokenLastFour"]!.GetValue<string>().ShouldBe("WXYZ");
    }
}
