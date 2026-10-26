using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ShipLean.Application.Features.Import.Jira;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Import;
using ShipLean.TestSupport.Jira;
using Shouldly;

namespace ShipLean.WebApi.Tests.Import.Jira;

[TestFixture]
public sealed class JiraClientTests
{
    private static readonly string[] NoNumbers = [];

    private static (JiraClient Client, AuthorizationRecorder Recorder) Arrange(bool allowLocal = false)
    {
        var recorder = new AuthorizationRecorder(new JiraStubHandler());
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [JiraEndpointPolicy.AllowLocalKey] = allowLocal ? "true" : "false" })
            .Build();
        return (new JiraClient(new JiraTransport(new StubHttpClientFactory(recorder), configuration), NullLogger<JiraClient>.Instance), recorder);
    }

    [Test]
    public async Task Cloud_WhenCalled_ShouldSendBasicEmailAndToken()
    {
        var (client, recorder) = Arrange();

        await client.GetMyselfAsync(JiraStubHandler.Credentials(JiraKind.Cloud));

        var expected = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{JiraStubHandler.CloudEmail}:{JiraStubHandler.CloudToken}"));
        recorder.Seen.ShouldBe([(JiraStubHandler.CloudHost, expected)]);
    }

    [Test]
    public async Task Server_WhenCalled_ShouldSendBearerToken()
    {
        var (client, recorder) = Arrange();

        await client.GetMyselfAsync(JiraStubHandler.Credentials(JiraKind.Server));

        recorder.Seen.ShouldBe([(JiraStubHandler.ServerHost, "Bearer " + JiraStubHandler.ServerToken)]);
    }

    [Test]
    public async Task Cloud_WhenAttachmentRedirectsToAnotherHost_ShouldSendTheTokenOnlyToTheSite()
    {
        var (client, recorder) = Arrange();
        var credentials = JiraStubHandler.Credentials(JiraKind.Cloud);
        var attachment = (await client.SearchIssuesAsync(credentials, JiraStubFiles.ProjectKey, null, 100, NoNumbers)).Issues.SelectMany(i => i.Attachments).First();

        await using var stream = await client.OpenAttachmentAsync(credentials, attachment);

        recorder.Seen.Where(r => r.Host == JiraStubHandler.MediaHost).ShouldHaveSingleItem().Authorization.ShouldBeNull();
        recorder.Seen.Where(r => r.Host == JiraStubHandler.CloudHost).ShouldAllBe(r => r.Authorization != null);
    }

    [TestCase("https://10.1.2.3")]
    [TestCase("https://192.168.0.10")]
    [TestCase("https://172.20.0.1")]
    [TestCase("https://127.0.0.1")]
    [TestCase("https://169.254.169.254")]
    [TestCase("https://localhost")]
    [TestCase("https://jira.localhost")]
    [TestCase("https://[::1]")]
    [TestCase("https://[fe80::1]")]
    [TestCase("https://[fd00::1]")]
    [TestCase("https://[::ffff:10.0.0.1]")]
    [TestCase("https://[64:ff9b::a00:1]")]
    [TestCase("https://[64:ff9b::7f00:1]")]
    [TestCase("https://[2002:a00:1::]")]
    [TestCase("https://[2002:7f00:1::1]")]
    [TestCase("https://[::7f00:1]")]
    [TestCase("https://[::a00:1]")]
    [TestCase("https://198.18.0.1")]
    [TestCase("https://[64:ff9b:1::808:808]")]
    [TestCase("https://[::ffff:0:808:808]")]
    [TestCase("https://[2001:0:4136:e378:8000:63bf:3fff:fdd2]")]
    [TestCase("https://198.19.255.254")]
    [TestCase("http://jira.example.com")]
    [TestCase("https://user:secret@jira.example.com")]
    [TestCase("ftp://jira.example.com")]
    [TestCase("not a url")]
    public async Task Client_WhenBaseUrlIsRefused_ShouldBeValidation(string baseUrl)
    {
        var (client, recorder) = Arrange();

        var error = await Should.ThrowAsync<ApiException>(() => client.GetMyselfAsync(new JiraCredentials(baseUrl, JiraKind.Server, string.Empty, "token")));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
        recorder.Seen.ShouldBeEmpty();
    }

    [TestCase("http://127.0.0.1:8080")]
    [TestCase("https://10.1.2.3")]
    public async Task Client_WhenLocalEndpointsAllowed_ShouldTryTheAddress(string baseUrl)
    {
        var (client, _) = Arrange(allowLocal: true);

        // The stub knows no such host, which reaches the client as an unreachable Jira: the address itself was accepted.
        var error = await Should.ThrowAsync<ApiException>(() => client.GetMyselfAsync(new JiraCredentials(baseUrl, JiraKind.Server, string.Empty, "token")));

        error.Code.ShouldBe(ApiErrorCodes.NotConfigured);
    }

    [TestCase("8.8.8.8", false)]
    [TestCase("100.63.0.1", false)]
    [TestCase("100.64.0.1", true)]
    [TestCase("172.15.0.1", false)]
    [TestCase("172.16.0.1", true)]
    [TestCase("0.0.0.0", true)]
    [TestCase("224.0.0.1", true)]
    [TestCase("2606:4700::1111", false)]
    [TestCase("198.17.255.255", false)]
    [TestCase("198.18.0.0", true)]
    [TestCase("198.19.255.255", true)]
    [TestCase("198.20.0.0", false)]
    [TestCase("64:ff9b::a00:1", true)]
    [TestCase("64:ff9b::808:808", false)]
    [TestCase("2002:c0a8:1::", true)]
    [TestCase("2002:808:808::", false)]
    [TestCase("::7f00:1", true)]
    [TestCase("::a9fe:a9fe", true)]
    [TestCase("64:ff9b:1::808:808", true)]
    [TestCase("64:ff9b:1:ffff::1", true)]
    [TestCase("64:ff9b:2::808:808", false)]
    [TestCase("::ffff:0:808:808", true)]
    [TestCase("2001:0:4136:e378::1", true)]
    [TestCase("2001:1::1", false)]
    public void IsLocal_ShouldCoverThePrivateRanges(string address, bool expected)
    {
        JiraEndpointPolicy.IsLocal(System.Net.IPAddress.Parse(address)).ShouldBe(expected);
    }

    [TestCase("PLATFORM\n")]
    [TestCase("PLATFORM\nORDER BY key")]
    public async Task Client_WhenProjectKeyHasATrailingNewline_ShouldBeNotFoundWithoutAskingJira(string projectKey)
    {
        var (client, recorder) = Arrange();

        var error = await Should.ThrowAsync<ApiException>(() => client.GetIssueTypesAsync(JiraStubHandler.Credentials(JiraKind.Cloud), projectKey));

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
        recorder.Seen.ShouldBeEmpty();
    }

    [Test]
    public async Task Client_WhenIssueKeyHasATrailingNewline_ShouldBeNotFoundWithoutAskingJira()
    {
        var (client, recorder) = Arrange();

        var error = await Should.ThrowAsync<ApiException>(() => client.GetCommentsAsync(JiraStubHandler.Credentials(JiraKind.Cloud), "PLATFORM-1\n"));

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
        recorder.Seen.ShouldBeEmpty();
    }
}
