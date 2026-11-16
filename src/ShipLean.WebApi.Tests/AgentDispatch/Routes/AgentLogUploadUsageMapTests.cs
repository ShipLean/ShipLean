using ShipLean.WebApi.Features.Usage;

namespace ShipLean.WebApi.Tests.AgentDispatch.Routes;

// The upload is anonymous and UsageActionFilter records nothing without a signed-in actor, so a Track entry would never fire from the agent's curl.
// The route is SelfEmitted: the upload code fires agent_session.run_log_uploaded itself after a successful upload (C12: mapped by 0e, fired by E1).
[TestFixture]
public class AgentLogUploadUsageMapTests
{
    [Test]
    public void Upload_ShouldBeMappedSelfEmittedBecauseTheCallerIsAnonymous()
    {
        ActionUsageMap.All["AgentLogUpload.Upload"].Kind.ShouldBe(ActionUsageKind.SelfEmitted);
    }
}
