using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Agents;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.AgentDispatch;

// C10: the agentLog transfer token is tied to its session, issues for 24 hours, is claimed once and can be given back.
[TestFixture]
public class AgentLogTransferTokenTests : ApiFixture
{
    private static readonly TimeSpan Day = TimeSpan.FromHours(24);

    private ITransferTokens Tokens => Host.Services.GetRequiredService<ITransferTokens>();

    private async Task<(Guid Item, Guid Session)> NewSessionAsync()
    {
        await using var database = await OpenDatabaseAsync();
        var connection = await AgentSessionSeeds.InsertConnectionAsync(database);
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("task").WithTitle("Agent log token").InsertAsync(database);
        return (item, await new AgentSessionSeeds(item, connection).WithState("working").InsertAsync(database));
    }

    private static NewTransfer Transfer(Guid item, Guid session)
        => new(TransferKind.AgentLog, item, null, $"agent-run-{session}.log", "text/plain", SeedIds.Users.Joel, null, "ShipLean", Day, session);

    [Test]
    public async Task Issue_WhenAgentLogWithSession_ShouldBeFoundWithItsSession()
    {
        var (item, session) = await NewSessionAsync();

        var issued = await Tokens.IssueAsync(Transfer(item, session));

        var grant = await Tokens.FindAsync(TransferKind.AgentLog, issued.Token);
        grant.ShouldNotBeNull();
        (grant.Kind, grant.WorkItemId, grant.SessionId, grant.ContentType).ShouldBe((TransferKind.AgentLog, item, (Guid?)session, "text/plain"));
        (await Tokens.FindAsync(TransferKind.Upload, issued.Token)).ShouldBeNull();
    }

    [Test]
    public async Task Claim_WhenAgentLogTwice_ShouldSucceedOnce()
    {
        var (item, session) = await NewSessionAsync();
        var token = (await Tokens.IssueAsync(Transfer(item, session))).Token;

        var first = await Tokens.ClaimAsync(token);
        var second = await Tokens.ClaimAsync(token);

        (first, second).ShouldBe((true, false));
        (await Tokens.FindAsync(TransferKind.AgentLog, token)).ShouldBeNull();
    }

    [Test]
    public async Task Release_WhenAgentLogClaimed_ShouldAllowItAgain()
    {
        var (item, session) = await NewSessionAsync();
        var token = (await Tokens.IssueAsync(Transfer(item, session))).Token;
        (await Tokens.ClaimAsync(token)).ShouldBeTrue();

        await Tokens.ReleaseAsync(token);

        (await Tokens.FindAsync(TransferKind.AgentLog, token)).ShouldNotBeNull();
        (await Tokens.ClaimAsync(token)).ShouldBeTrue();
    }

    [Test]
    public async Task Issue_WhenNotAgentLog_ShouldHaveNoSession()
    {
        var (item, _) = await NewSessionAsync();

        var issued = await Tokens.IssueAsync(new NewTransfer(TransferKind.Demo, item, null, "sprint-demo.mp4", "video/mp4", SeedIds.Users.Joel, null, "ShipLean", Day));

        (await Tokens.FindAsync(TransferKind.Demo, issued.Token))!.SessionId.ShouldBeNull();
    }
}
