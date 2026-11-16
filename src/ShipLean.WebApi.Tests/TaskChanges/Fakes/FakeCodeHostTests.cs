using ShipLean.Contracts.TaskChanges;
using ShipLean.TestSupport.TaskChanges;

namespace ShipLean.WebApi.Tests.TaskChanges.Fakes;

// The fake code host over the real git patches in TestSupport/TaskChanges/repos.
[TestFixture]
public class FakeCodeHostTests
{
    private readonly FakeCodeHost _host = new();

    [Test]
    public async Task FakeCodeHost_WhenShortSha_ShouldResolveFullSha()
    {
        var read = await _host.ReadCommitAsync(FakeCodeHost.StorefrontRepo, FakeCodeHost.Shas.Second[..7]);

        read.Status.ShouldBe(CommitReadStatus.Found);
        read.Meta!.Sha.ShouldBe(FakeCodeHost.Shas.Second);
        read.Meta.ParentShas.ShouldBe([FakeCodeHost.Shas.First]);
        read.Meta.Subject.ShouldBe("Rename cart to basket");
        read.Meta.Paths.ShouldBe(["src/basket.ts", "src/checkout.ts"]);
        (await _host.ReadPatchAsync(FakeCodeHost.StorefrontRepo, FakeCodeHost.Shas.Second[..7])).UnifiedDiff!.ShouldContain("rename from src/cart.ts");
    }

    [Test]
    public async Task FakeCodeHost_WhenRepoUnreadable_ShouldReturnUnreadable()
    {
        var read = await _host.ReadCommitAsync(FakeCodeHost.SecretRepo, FakeCodeHost.Shas.First);
        var patch = await _host.ReadPatchAsync(FakeCodeHost.SecretRepo, FakeCodeHost.Shas.First);

        read.Status.ShouldBe(CommitReadStatus.Unreadable);
        read.HttpStatus.ShouldBe(403);
        patch.Status.ShouldBe(CommitReadStatus.Unreadable);
        patch.UnifiedDiff.ShouldBeNull();
    }

    [Test]
    public async Task FakeCodeHost_WhenCommitMissing_ShouldReturnMissing()
    {
        var read = await _host.ReadCommitAsync(FakeCodeHost.StorefrontRepo, FakeCodeHost.Shas.Missing);
        var patch = await _host.ReadPatchAsync(FakeCodeHost.StorefrontRepo, FakeCodeHost.Shas.Missing);

        read.Status.ShouldBe(CommitReadStatus.Missing);
        read.Meta.ShouldBeNull();
        patch.Status.ShouldBe(CommitReadStatus.Missing);
    }

    [Test]
    public async Task FakeCodeHost_WhenRange_ShouldReturnCombinedPatch()
    {
        var range = await _host.ReadRangePatchAsync(FakeCodeHost.StorefrontRepo, FakeCodeHost.Shas.First, FakeCodeHost.Shas.Third);

        range.Status.ShouldBe(CommitReadStatus.Found);
        range.UnifiedDiff!.ShouldContain("rename from src/cart.ts");
        range.UnifiedDiff!.ShouldContain("+export const discount = 0;");
        range.UnifiedDiff!.ShouldNotContain("Docs for contributors");
    }

    [Test]
    public async Task FakeCodeHost_WhenRootCommit_ShouldHaveNoParents()
    {
        var read = await _host.ReadCommitAsync(FakeCodeHost.StorefrontRepo, FakeCodeHost.Shas.Root);
        var other = await _host.ReadCommitAsync(FakeCodeHost.StorefrontRepo, FakeCodeHost.Shas.OtherBranch);
        var payments = await _host.ReadCommitAsync(FakeCodeHost.Payments, FakeCodeHost.Shas.PaymentsOnly);

        read.Meta!.ParentShas.ShouldBeEmpty();
        other.Meta!.ParentShas.ShouldBe([FakeCodeHost.Shas.First]);
        payments.Status.ShouldBe(CommitReadStatus.Found);
    }
}
