using ShipLean.Application.Fakes.YouTube;
using ShipLean.Contracts.Demos;
using ShipLean.TestSupport.Contracts.YouTube;

namespace ShipLean.UnitTest.Demos;

[TestFixture]
public class FakeYouTubeClientContractTests : YouTubeClientContract
{
    protected override IYouTubeClient Create() => new FakeYouTubeClient();

    protected override void ForceVisibility(IYouTubeClient client, YouTubeVisibility visibility) =>
        ((FakeYouTubeClient)client).ForcedVisibility = visibility;

    protected override void ExhaustQuota(IYouTubeClient client) =>
        ((FakeYouTubeClient)client).FailWith(YouTubeFailureKind.Quota);

    protected override void RevokeAccess(IYouTubeClient client) =>
        ((FakeYouTubeClient)client).FailWith(YouTubeFailureKind.Revoked);

    protected override void NarrowGrantedScope(IYouTubeClient client) =>
        ((FakeYouTubeClient)client).NarrowGrantedScope = true;

    protected override void FailRevoke(IYouTubeClient client) =>
        ((FakeYouTubeClient)client).RevokeFails = true;
}
