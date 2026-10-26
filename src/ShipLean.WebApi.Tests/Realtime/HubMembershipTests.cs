using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Identity;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Features.Auth;
using ShipLean.WebApi.Realtime;

namespace ShipLean.WebApi.Tests.Realtime;

// The hub asks HubMembership because IProjectAccess cannot take a user; the two must never disagree.
[TestFixture]
public class HubMembershipTests : ApiFixture
{
    [Test]
    public async Task RequireMemberAsync_ShouldAgreeWithProjectAccessForEveryUserAndProject()
    {
        await using (var db = await OpenDatabaseAsync())
        {
            await db.ExecuteAsync(
                "UPDATE project_member SET archived_at = now() WHERE project_id = @projectId AND user_id = @userId",
                new { projectId = SeedIds.Projects.Ship, userId = SeedIds.Users.Priya });
        }
        var users = new[]
        {
            SeedIds.Users.Joel, SeedIds.Users.Priya, SeedIds.Users.Tom, SeedIds.Users.Mei, SeedIds.Users.Sam, SeedIds.Users.Ana,
            SeedIds.Users.Ravi, SeedIds.Users.Dana, SeedIds.Users.Lee, SeedIds.Users.Jo, SeedIds.Users.Kai, SeedIds.Users.JiraImport,
        };
        var projects = new[]
        {
            SeedIds.Projects.Ship, SeedIds.Projects.Pay, SeedIds.Projects.Mob, SeedIds.Projects.Data, SeedIds.Projects.Ops,
            SeedIds.Projects.Part, SeedIds.Projects.Arch, SeedIds.Projects.Empt, Guid.NewGuid(),
        };
        var access = Host.Services.GetRequiredService<IProjectAccess>();
        var hub = Host.Services.GetRequiredService<HubMembership>();
        var members = 0;

        foreach (var user in users)
        {
            Host.ActAs(user);
            foreach (var project in projects)
            {
                var viaAccess = await AnswerAsync(() => access.RequireAsync(project, ProjectRole.Member));
                var viaHub = await AnswerAsync(() => hub.RequireMemberAsync(project, user, CancellationToken.None));

                viaHub.ShouldBe(viaAccess, $"user {user}, project {project}");
                members += viaHub ? 1 : 0;
            }
        }

        members.ShouldBeGreaterThan(0);
        members.ShouldBeLessThan(users.Length * projects.Length);
    }

    private static async Task<bool> AnswerAsync(Func<Task> call)
    {
        try
        {
            await call();
            return true;
        }
        catch (ApiException e) when (e.Code == ApiErrorCodes.NotFound)
        {
            return false;
        }
    }
}

// The hub refuses a cookie identity without a stamp claim, and the test scheme's principals carry one so the check
// always runs.
[TestFixture]
public class HubSessionCheckTests : ApiFixture
{
    [Test]
    public async Task Refusal_WhenPrincipalHasNoStamp_ShouldBeSessionEnded()
    {
        var check = Host.Services.GetRequiredService<HubSessionCheck>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, SeedIds.Users.Joel.ToString())], "test"));

        var refusal = await check.RefusalAsync(principal, CancellationToken.None);

        refusal.ShouldNotBeNull().Code.ShouldBe(ApiErrorCodes.SessionEnded);
    }

    [Test]
    public async Task Refusal_WhenPrincipalHasTheCurrentStamp_ShouldBeNull()
    {
        var stamp = (await Host.Services.GetRequiredService<UserSessions>().GetAsync(SeedIds.Users.Joel))!.SecurityStamp;
        var principal = AuthPrincipal.For(SeedIds.Users.Joel, stamp);

        var refusal = await Host.Services.GetRequiredService<HubSessionCheck>().RefusalAsync(principal, CancellationToken.None);

        refusal.ShouldBeNull();
    }

    [Test]
    public async Task Hub_WhenTestSchemeStampIsStale_ShouldRefuseTheNextCall()
    {
        // Proves the test scheme's principal carries the stamp: rotate it after connect and the filter notices.
        await using var receiver = await HubReceiver.StartAsync(Host);
        await receiver.Connection.InvokeAsync("JoinProject", SeedIds.Projects.Ship);
        await using (var db = await OpenDatabaseAsync())
        {
            await db.ExecuteAsync("UPDATE app_user SET security_stamp = 'rotated' WHERE id = @id", new { id = SeedIds.Users.Joel });
        }
        Host.Services.GetRequiredService<UserSessions>().Forget(SeedIds.Users.Joel);

        var refused = await Should.ThrowAsync<Exception>(() => receiver.Connection.InvokeAsync("JoinProject", SeedIds.Projects.Ship));

        (refused is HubException ? refused.Message : "HubException: session_ended: ").ShouldContain("HubException: session_ended: ");
    }
}

// A member removed between JoinScreen's check and its presence entry must not stay listed.
[TestFixture]
[CancelAfter(30000)]
public class JoinScreenRaceTests : ApiFixture
{
    private readonly RemovedAfterFirstCheck _membership = new();

    protected override Action<IServiceCollection>? ConfigureTestServices => services => services.AddSingleton<HubMembership>(_ => _membership);

    [Test]
    public async Task JoinScreen_WhenRemovedAfterTheCheck_ShouldLeaveAgainAndFail()
    {
        await using var watcher = await HubReceiver.StartAsync(Host);
        await watcher.Connection.InvokeAsync("JoinProject", SeedIds.Projects.Ship);
        _membership.RemoveAfterChecks = _membership.Checks + 1;

        var refused = await Should.ThrowAsync<HubException>(() => watcher.Connection.InvokeAsync("JoinScreen", SeedIds.Projects.Ship, "raced"));

        refused.Message.ShouldEndWith("HubException: not_found: That project or item was not found.");
        (await watcher.NextPresenceAsync("raced")).GetProperty("userIds").GetArrayLength().ShouldBe(1);
        (await watcher.NextPresenceAsync("raced")).GetProperty("userIds").GetArrayLength().ShouldBe(0);
    }

    private sealed class RemovedAfterFirstCheck : HubMembership
    {
        private int _checks;

        public int Checks => Volatile.Read(ref _checks);

        public RemovedAfterFirstCheck() : base(null!)
        {
        }

        public int RemoveAfterChecks { get; set; } = int.MaxValue;

        public override Task RequireMemberAsync(Guid projectId, Guid userId, CancellationToken cancellationToken)
            => Interlocked.Increment(ref _checks) > RemoveAfterChecks
                ? Task.FromException(ApiException.NotFound("That project or item was not found."))
                : Task.CompletedTask;
    }
}
