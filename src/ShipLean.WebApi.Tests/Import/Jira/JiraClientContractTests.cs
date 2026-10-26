using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ShipLean.Application.Features.Import.Jira;
using ShipLean.Contracts.Import;
using ShipLean.TestSupport.Contracts.Import;
using ShipLean.TestSupport.Jira;

namespace ShipLean.WebApi.Tests.Import.Jira;

// The Jira client contract against the real client, over the in-process Jira stub: no network, the stub plays Cloud and Server.
public sealed class JiraClientContractTests : JiraClientContract
{
    private const string BotAccountId = "557058:f58131cb-b67d-43c7-b30d-6b58d40bd077";
    private const int CommentCount = 2;

    protected override Task<JiraWorld> ArrangeAsync(JiraKind kind, JiraFault fault)
    {
        var stub = new JiraStubHandler
        {
            IsDown = fault == JiraFault.Down,
            ServerRedirectsAttachmentsOffSite = fault == JiraFault.RedirectOffSite,
        };
        var credentials = JiraStubHandler.Credentials(kind, fault == JiraFault.WrongToken ? "not-the-token" : null);
        var client = new JiraClient(new JiraTransport(new StubHttpClientFactory(stub), new ConfigurationBuilder().Build()), NullLogger<JiraClient>.Instance);
        return Task.FromResult(new JiraWorld(
            credentials, client, JiraStubFiles.ProjectKey, JiraStubFiles.IssueKeys, JiraStubFiles.CommentedIssueKey, CommentCount,
            JiraStubFiles.AttachmentBytes, "PLATFORM-403", "PLATFORM-404", kind == JiraKind.Cloud ? BotAccountId : null));
    }
}
