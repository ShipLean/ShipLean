using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ShipLean.Application.Features.Import.Jira;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Import;
using ShipLean.Contracts.Import.Jira;
using ShipLean.TestSupport.Jira;
using Shouldly;

namespace ShipLean.WebApi.Tests.Import.Jira;

// The real client over handlers that misbehave the way a hostile or broken Jira does: slow bodies, huge bodies, redirects to the inside, answers that are not Jira's.
[TestFixture]
public sealed class JiraTransportTests
{
    private static readonly TimeSpan ShortDeadline = TimeSpan.FromMilliseconds(300);
    private static readonly byte[] UserHead = Encoding.UTF8.GetBytes("""{"accountId":"a","displayName":"b","pad":"a""");
    private static readonly byte[] UserTail = Encoding.UTF8.GetBytes("\"}");
    private static readonly JiraCredentials Cloud = JiraStubHandler.Credentials(JiraKind.Cloud);

    private static JiraClient ClientOver(HttpMessageHandler handler, TimeSpan? timeout = null, ILogger<JiraClient>? logger = null)
        => new(new JiraTransport(new StubHttpClientFactory(handler, timeout), new ConfigurationBuilder().Build()), logger ?? NullLogger<JiraClient>.Instance);

    private static HttpResponseMessage Ok(Stream body, string mediaType = "application/json")
        => new(HttpStatusCode.OK) { Content = new StreamContent(body) { Headers = { { "Content-Type", mediaType } } } };

    private static HttpResponseMessage Json(string text) => Ok(new MemoryStream(Encoding.UTF8.GetBytes(text)));

    private static HttpResponseMessage ProjectsWhereOnlyAlphaCounts(HttpRequestMessage request, string body)
    {
        if (request.RequestUri!.AbsolutePath.EndsWith("/project/search", StringComparison.Ordinal))
        {
            return Json("""{"values":[{"key":"AAA","name":"Alpha"},{"key":"BBB","name":"Beta"}],"isLast":true}""");
        }
        return body.Contains("AAA", StringComparison.Ordinal) ? Json("""{"count":5}""") : new HttpResponseMessage(HttpStatusCode.InternalServerError);
    }

    [Test]
    [CancelAfter(30000)]
    public async Task Client_WhenTheBodyDripsForever_ShouldGiveUpAtTheDeadline(CancellationToken cancellationToken)
    {
        var handler = new FuncHandler((_, _) => Ok(new GeneratedStream(UserHead, long.MaxValue, (byte)'a', UserTail, TimeSpan.FromMilliseconds(20))));

        var error = await Should.ThrowAsync<ApiException>(() => ClientOver(handler, ShortDeadline).GetMyselfAsync(Cloud, cancellationToken));

        error.Code.ShouldBe(ApiErrorCodes.NotConfigured);
        error.Message.ShouldContain("took too long");
    }

    [Test]
    [CancelAfter(30000)]
    public async Task Client_WhenAnAttachmentStalls_ShouldFailTheReadAtTheDeadline(CancellationToken cancellationToken)
    {
        var handler = new FuncHandler((_, _) => Ok(new GeneratedStream(UserHead, long.MaxValue, (byte)'a', UserTail, TimeSpan.FromMinutes(10)), "image/png"));
        var attachment = new JiraAttachment("1", "a.png", "image/png", 10, JiraStubHandler.CloudUrl + "/secure/attachment/1/a.png");
        await using var stream = await ClientOver(handler, ShortDeadline).OpenAttachmentAsync(Cloud, attachment, cancellationToken);
        var buffer = new byte[UserHead.Length];

        await stream.ReadExactlyAsync(buffer, cancellationToken);
        var error = await Should.ThrowAsync<ApiException>(async () => await stream.ReadExactlyAsync(buffer, cancellationToken));

        error.Code.ShouldBe(ApiErrorCodes.NotConfigured);
    }

    [Test]
    public async Task Client_WhenTheBodyIsLargerThanTheCap_ShouldBeValidation()
    {
        var handler = new FuncHandler((_, _) => Ok(new GeneratedStream(UserHead, JiraTransport.MaxBodyBytes, (byte)'a', UserTail, TimeSpan.Zero)));

        var error = await Should.ThrowAsync<ApiException>(() => ClientOver(handler).GetMyselfAsync(Cloud));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Message.ShouldContain("larger answer");
    }

    [Test]
    public async Task Client_WhenAnAttachmentRedirectsToAPrivateAddress_ShouldRefuseTheHopWithoutCallingIt()
    {
        var handler = new FuncHandler((_, _) => new HttpResponseMessage(HttpStatusCode.Found) { Headers = { Location = new Uri("https://10.0.0.5/secret") } });
        var attachment = new JiraAttachment("1", "a.png", "image/png", 10, JiraStubHandler.CloudUrl + "/secure/attachment/1/a.png");

        var error = await Should.ThrowAsync<ApiException>(() => ClientOver(handler).OpenAttachmentAsync(Cloud, attachment));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
        handler.Requests.ShouldHaveSingleItem();
    }

    [TestCase("""{"accountId":{"nested":1},"displayName":"Priya"}""")]
    [TestCase("""{"accountId":["a"],"displayName":5}""")]
    [TestCase("""{"accountId":"a","accountId":"b","displayName":"x"}""")]
    [TestCase("""{"accountId":"a","displayName":"x","displayName":"y"}""")]
    public async Task Client_WhenJiraSendsFieldsOfTheWrongKind_ShouldBeNotConfiguredWithAMessage(string json)
    {
        var handler = new FuncHandler((_, _) => Json(json));

        var error = await Should.ThrowAsync<ApiException>(() => ClientOver(handler).GetMyselfAsync(Cloud));

        error.Code.ShouldBe(ApiErrorCodes.NotConfigured);
        error.Message.ShouldContain("does not understand");
    }

    [Test]
    public async Task Client_WhenAProjectIsMissingItsKey_ShouldBeNotConfiguredWithAMessage()
    {
        var handler = new FuncHandler((_, _) => Json("""{"values":[{"name":"No key"}],"isLast":true}"""));

        var error = await Should.ThrowAsync<ApiException>(() => ClientOver(handler).ListProjectsAsync(Cloud));

        error.Code.ShouldBe(ApiErrorCodes.NotConfigured);
    }

    [Test]
    public async Task Client_WhenOneProjectsCountFails_ShouldStillListItAndLogTheFailure()
    {
        var logger = new RecordingLogger<JiraClient>();
        var handler = new FuncHandler(ProjectsWhereOnlyAlphaCounts);

        var projects = await ClientOver(handler, logger: logger).ListProjectsAsync(Cloud);

        projects.Select(p => (p.Key, p.IssueCount)).ShouldBe([("AAA", 5), ("BBB", 0)]);
        logger.Entries.ShouldHaveSingleItem().Message.ShouldContain("BBB");
    }

    [Test]
    [CancelAfter(30000)]
    public async Task Client_WhenProjectsNeverEndOnePagePerRequest_ShouldStopAtThePageCap(CancellationToken cancellationToken)
    {
        var handler = new FuncHandler((_, _) => Json("""{"values":[{"key":"AAA","name":"Alpha"}],"isLast":false}"""));

        var error = await Should.ThrowAsync<ApiException>(() => ClientOver(handler).ListProjectsAsync(Cloud, cancellationToken));

        error.Code.ShouldBe(ApiErrorCodes.NotConfigured);
        error.Message.ShouldContain("more projects than ShipLean reads");
        handler.Requests.Count.ShouldBe(JiraClient.MaxProjectPages);
    }

    [Test]
    [CancelAfter(30000)]
    public async Task Client_WhenProjectsNeverEndAndPagesAreFat_ShouldStopAtTheItemCap(CancellationToken cancellationToken)
    {
        var page = "{\"values\":[" + string.Join(',', Enumerable.Repeat("""{"key":"AAA","name":"Alpha"}""", JiraClient.MaxProjects / 2 + 1)) + "],\"isLast\":false}";
        var handler = new FuncHandler((_, _) => Json(page));

        var error = await Should.ThrowAsync<ApiException>(() => ClientOver(handler).ListProjectsAsync(Cloud, cancellationToken));

        error.Code.ShouldBe(ApiErrorCodes.NotConfigured);
        handler.Requests.Count.ShouldBe(2);
    }

    [Test]
    [CancelAfter(30000)]
    public async Task Client_WhenCommentsNeverEndOnePagePerRequest_ShouldStopAtThePageCap(CancellationToken cancellationToken)
    {
        var handler = new FuncHandler((_, _) => Json("""{"comments":[{"id":"1","body":"x","created":"2026-01-01T00:00:00.000+0000"}],"total":2147483647}"""));

        var error = await Should.ThrowAsync<ApiException>(() => ClientOver(handler).GetCommentsAsync(Cloud, "PLATFORM-1", cancellationToken));

        error.Code.ShouldBe(ApiErrorCodes.NotConfigured);
        error.Message.ShouldContain("than ShipLean reads");
        handler.Requests.Count.ShouldBe(JiraClient.MaxCommentPages);
    }

    [Test]
    [CancelAfter(30000)]
    public async Task Client_WhenCommentsNeverEndAndPagesAreFat_ShouldStopAtTheItemCap(CancellationToken cancellationToken)
    {
        var page = "{\"comments\":[" + string.Join(',', Enumerable.Repeat("""{"id":"1","body":"x","created":"2026-01-01T00:00:00.000+0000"}""", JiraClient.MaxComments / 2 + 1)) + "],\"total\":2147483647}";
        var handler = new FuncHandler((_, _) => Json(page));

        var error = await Should.ThrowAsync<ApiException>(() => ClientOver(handler).GetCommentsAsync(Cloud, "PLATFORM-1", cancellationToken));

        error.Code.ShouldBe(ApiErrorCodes.NotConfigured);
        handler.Requests.Count.ShouldBe(2);
    }

    [Test]
    public async Task Client_WhenTheConnectionDropsMidBody_ShouldBeNotConfigured()
    {
        var handler = new FuncHandler((_, _) => Ok(new DroppedStream(UserHead)));

        var error = await Should.ThrowAsync<ApiException>(() => ClientOver(handler).GetMyselfAsync(Cloud));

        error.Code.ShouldBe(ApiErrorCodes.NotConfigured);
        error.Message.ShouldContain("did not finish");
    }

    [Test]
    [CancelAfter(30000)]
    public async Task Client_WhenTheConnectionDropsMidAttachment_ShouldBeNotConfigured(CancellationToken cancellationToken)
    {
        var handler = new FuncHandler((_, _) => Ok(new DroppedStream(UserHead), "image/png"));
        var attachment = new JiraAttachment("1", "a.png", "image/png", 10, JiraStubHandler.CloudUrl + "/secure/attachment/1/a.png");
        await using var stream = await ClientOver(handler).OpenAttachmentAsync(Cloud, attachment, cancellationToken);
        var buffer = new byte[UserHead.Length];

        await stream.ReadExactlyAsync(buffer, cancellationToken);
        var error = await Should.ThrowAsync<ApiException>(async () => await stream.ReadExactlyAsync(buffer, cancellationToken));

        error.Code.ShouldBe(ApiErrorCodes.NotConfigured);
    }

    [Test]
    [CancelAfter(30000)]
    public async Task Client_WhenAnAttachmentIsReadThroughTheArrayOverload_ShouldReturnItsBytes(CancellationToken cancellationToken)
    {
        var handler = new FuncHandler((_, _) => Ok(new MemoryStream(UserHead), "image/png"));
        var attachment = new JiraAttachment("1", "a.png", "image/png", 10, JiraStubHandler.CloudUrl + "/secure/attachment/1/a.png");
        await using var stream = await ClientOver(handler).OpenAttachmentAsync(Cloud, attachment, cancellationToken);
        var buffer = new byte[UserHead.Length];

        var read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);

        buffer.Take(read).ToArray().ShouldBe(UserHead.Take(read).ToArray());
        read.ShouldBeGreaterThan(0);
    }
}
